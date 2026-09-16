namespace BadgeForge.Core.Data.Validation;

/// <summary>
/// Configurable tolerances and parameters for the pre-flight validation engine.
/// </summary>
public class PreFlightValidationOptions
{
    /// <summary>
    /// If true, missing photo files are treated as warnings instead of critical errors
    /// when a fallback placeholder is present or layer IsRequired is false.
    /// </summary>
    public bool AllowMissingPhotoAsWarning { get; set; } = false;

    /// <summary>
    /// Explicit primary key token name to check for duplicates across records (e.g. "EmployeeId", "Id").
    /// If null, primary key is inferred automatically from barcode layers or common ID field names.
    /// </summary>
    public string? PrimaryKeyToken { get; set; }

    /// <summary>
    /// Whether to check for duplicate IDs/barcodes across the batch. Defaults to true.
    /// </summary>
    public bool CheckDuplicates { get; set; } = true;

    /// <summary>
    /// Whether layer requirements (e.g. required tokens, photos, non-empty barcodes) are enforced
    /// as fatal errors. When false, no badge data requirements are enforced and printing is never blocked
    /// by missing tokens or unmapped data. Defaults to true for backward-compatible standalone validation.
    /// </summary>
    public bool EnforceRequirements { get; set; } = true;
}
