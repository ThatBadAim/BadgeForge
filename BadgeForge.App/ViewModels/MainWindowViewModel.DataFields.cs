using System;
using System.Linq;
using BadgeForge.Core.Templates.Enums;
using BadgeForge.Core.Templates.Layers;
using BadgeForge.Core.Templates.Tokens;

namespace BadgeForge.App.ViewModels;

/// <summary>
/// The kind of layer a data field is inserted as.
/// </summary>
public enum DataFieldKind
{
    Text,
    Photo,
    Barcode,
    QrCode
}

/// <summary>
/// Template designer support for dynamic data: inserting token-bound layers, showing what a layer is bound to, and
/// how bound text that turns out too long for its frame is handled.
/// </summary>
public partial class MainWindowViewModel
{
    /// <summary>
    /// Adds a layer bound to a record field: a text layer showing <c>{{Token}}</c>, a photo frame, or a barcode / QR
    /// code encoding the field. The new layer is selected.
    /// </summary>
    public void AddDataFieldLayer(DataFieldKind kind, string token)
    {
        string name = TokenSyntax.NormalizeName(token);
        if (name.Length == 0)
        {
            return;
        }

        string formatted = TokenSyntax.Format(name);
        switch (kind)
        {
            case DataFieldKind.Photo:
                AddPhotoLayer();
                if (SelectedLayer is PhotoLayer photo)
                {
                    ReplaceSelectedLayer(photo with { SourceToken = formatted });
                }
                break;

            case DataFieldKind.Barcode:
            case DataFieldKind.QrCode:
                AddBarcodeLayer(symbology: kind == DataFieldKind.QrCode ? BarcodeSymbology.QrCode : BarcodeSymbology.Code128);
                if (SelectedLayer is BarcodeLayer barcode)
                {
                    ReplaceSelectedLayer(barcode with { ContentToken = formatted });
                }
                break;

            default:
                AddTextLayer(text: formatted, name: new PeopleFieldColumn(name).Label);
                break;
        }

        StatusText = $"Added {formatted}. Each badge fills it in from that person's data.";
    }

    /// <summary>
    /// The selected text layer's overflow handling as a combo box index, in <see cref="TextOverflowMode"/> order.
    /// </summary>
    public int TextOverflowIndex
    {
        get => (int)(SelectedTextLayer?.Overflow ?? TextOverflowMode.ShrinkToFit);
        set
        {
            // A combo box reports -1 while its selection is being rebuilt; that isn't a choice
            if (SelectedLayer is TextLayer text && Enum.IsDefined(typeof(TextOverflowMode), value) && (TextOverflowMode)value != text.Overflow)
            {
                ReplaceSelectedLayer(text with { Overflow = (TextOverflowMode)value });
            }
        }
    }

    /// <summary>
    /// The tokens the selected layer is bound to, e.g. "{{FirstName}}  {{LastName}}".
    /// </summary>
    public string SelectedLayerTokensText => SelectedLayer == null
        ? string.Empty
        : string.Join("  ", SelectedLayer.GetReferencedTokens().Distinct(StringComparer.OrdinalIgnoreCase).Select(TokenSyntax.Format));

    public bool HasSelectedLayerTokens => SelectedLayer?.HasDynamicTokens == true;
}
