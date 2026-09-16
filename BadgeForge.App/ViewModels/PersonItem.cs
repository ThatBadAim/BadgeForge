using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BadgeForge.Core.Data.Models;
using BadgeForge.Core.Printing.Batch;
using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;

namespace BadgeForge.App.ViewModels;

/// <summary>
/// A template field shown as a column on the People page.
/// </summary>
public record PeopleFieldColumn(string Token)
{
    /// <summary>
    /// Readable column title: "EmployeeId" reads as "Employee Id".
    /// </summary>
    public string Label { get; } = Regex.Replace(Token, "(?<=[a-z0-9])(?=[A-Z])", " ");
}

/// <summary>
/// One editable field value (e.g. FirstName) for a person on the People page.
/// </summary>
public class PersonFieldCell : ViewModelBase
{
    public PersonFieldCell(PersonItem owner, string token, string key)
    {
        Owner = owner;
        Token = token;
        Key = key;
        Label = new PeopleFieldColumn(token).Label;
    }

    public PersonItem Owner { get; }

    public string Token { get; }

    /// <summary>
    /// The record field the value is stored under: the mapped CSV column, or the token itself.
    /// </summary>
    public string Key { get; }

    public string Label { get; }

    public string Value
    {
        get => Owner.Record.GetValue(Key) ?? string.Empty;
        set
        {
            string next = value ?? string.Empty;
            if (next == Value)
            {
                return;
            }

            Owner.Record.Fields[Key] = next;
            OnPropertyChanged();
            Owner.NotifyFieldEdited();
        }
    }
}

/// <summary>
/// One person on the print list: their field values, photo, place in the print order and print status.
/// </summary>
public class PersonItem : ViewModelBase
{
    private int _position;
    private bool _isIncluded = true;
    private string? _thumbnailPath;
    private AvaloniaBitmap? _thumbnail;

    public PersonItem(BadgeRecord record)
    {
        Record = record ?? throw new ArgumentNullException(nameof(record));
    }

    /// <summary>
    /// Raised after the user edits one of this person's field values.
    /// </summary>
    public event EventHandler? FieldsEdited;

    public BadgeRecord Record { get; }

    public ObservableCollection<PersonFieldCell> Cells { get; } = new();

    /// <summary>
    /// 1-based place in the print order.
    /// </summary>
    public int Position
    {
        get => _position;
        set
        {
            if (SetProperty(ref _position, value))
            {
                OnPropertyChanged(nameof(Name));
            }
        }
    }

    /// <summary>
    /// Whether this person prints when the whole list is printed.
    /// </summary>
    public bool IsIncluded
    {
        get => _isIncluded;
        set => SetProperty(ref _isIncluded, value);
    }

    public string Name
    {
        get
        {
            string fullName = $"{FieldValue("FirstName")} {FieldValue("LastName")}".Trim();
            if (fullName.Length > 0)
            {
                return fullName;
            }

            string? named = FirstNonEmpty(FieldValue("FullName"), FieldValue("Name"));
            if (named != null)
            {
                return named;
            }

            return FirstNonEmpty(Cells.Select(c => c.Value.Trim()).ToArray()) ?? $"Person {Position}";
        }
    }

    /// <summary>
    /// Last name used for sorting, falling back to the display name.
    /// </summary>
    public string SortLastName => FirstNonEmpty(FieldValue("LastName")) ?? Name;

    public string RecordId => Record.GetPrimaryIdentifier();

    /// <summary>
    /// The person's ID, or empty when they don't have one.
    /// </summary>
    public string IdentifierText => RecordId == $"Row-{Record.RowNumber}" ? string.Empty : RecordId;

    public string? PhotoPath => Record.ResolvedPhotoPath;

    public bool HasPhoto => !string.IsNullOrEmpty(PhotoPath);

    public string PhotoFileName => HasPhoto ? Path.GetFileName(PhotoPath!) : "No photo yet";

    /// <summary>
    /// True while nothing has been typed for this person and they have no photo.
    /// </summary>
    public bool IsBlank => !HasPhoto && Cells.All(c => string.IsNullOrWhiteSpace(c.Value));

    /// <summary>
    /// Small preview of the photo, decoded in the background the first time it is shown.
    /// </summary>
    public AvaloniaBitmap? Thumbnail
    {
        get
        {
            string? path = PhotoPath;
            if (path != _thumbnailPath)
            {
                _thumbnailPath = path;
                _thumbnail = null;
                if (path != null)
                {
                    _ = LoadThumbnailAsync(path);
                }
            }

            return _thumbnail;
        }
    }

    public string PrintStatus { get; private set; } = nameof(BatchRecordStatus.Pending);
    public string StatusBadgeColor { get; private set; } = "#E2E8F0";
    public string? ErrorMessage { get; private set; }
    public bool HasPrintStatus => PrintStatus != nameof(BatchRecordStatus.Pending);

    public void UpdateStatus(BatchRecordStatus status, string? error = null)
    {
        ErrorMessage = error;
        PrintStatus = status.ToString();
        StatusBadgeColor = status switch
        {
            BatchRecordStatus.Success => "#C5EDAC",  // Tea Green
            BatchRecordStatus.Printing => "#DBFEB8", // Light Tea Green
            BatchRecordStatus.Failed => "#FCA5A5",   // Light Red
            _ => "#E2E8F0"                           // Neutral Gray
        };
        OnPropertyChanged(nameof(PrintStatus));
        OnPropertyChanged(nameof(StatusBadgeColor));
        OnPropertyChanged(nameof(ErrorMessage));
        OnPropertyChanged(nameof(HasPrintStatus));
    }

    public void SetPhoto(string? path)
    {
        Record.ResolvedPhotoPath = path;
        RefreshPhoto();
    }

    /// <summary>
    /// Re-reads the photo after the record's photo path changed outside this item (e.g. folder matching).
    /// </summary>
    public void RefreshPhoto()
    {
        OnPropertyChanged(nameof(PhotoPath));
        OnPropertyChanged(nameof(HasPhoto));
        OnPropertyChanged(nameof(PhotoFileName));
        OnPropertyChanged(nameof(Thumbnail));
    }

    internal void NotifyFieldEdited()
    {
        RaiseNameChanged();
        FieldsEdited?.Invoke(this, EventArgs.Empty);
    }

    internal void RaiseNameChanged()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(RecordId));
        OnPropertyChanged(nameof(IdentifierText));
    }

    private string? FieldValue(string token)
    {
        var cell = Cells.FirstOrDefault(c => string.Equals(c.Token, token, StringComparison.OrdinalIgnoreCase));
        return (cell?.Value ?? Record.GetValue(token))?.Trim();
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private async Task LoadThumbnailAsync(string path)
    {
        var bitmap = await Task.Run(() => MainWindowViewModel.CreateThumbnail(path, 120));
        if (path != _thumbnailPath)
        {
            // The photo changed while this one was decoding
            return;
        }

        _thumbnail = bitmap;
        OnPropertyChanged(nameof(Thumbnail));
    }
}
