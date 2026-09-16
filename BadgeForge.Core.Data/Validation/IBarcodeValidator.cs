using BadgeForge.Core.Templates.Enums;

namespace BadgeForge.Core.Data.Validation;

/// <summary>
/// Result of a barcode payload validation check.
/// </summary>
public record BarcodeValidationResult(bool IsValid, string? ErrorMessage = null)
{
    public static BarcodeValidationResult Success() => new(true);
    public static BarcodeValidationResult Failure(string errorMessage) => new(false, errorMessage);
}

/// <summary>
/// Validates string payloads against standard barcode symbology constraints.
/// </summary>
public interface IBarcodeValidator
{
    /// <summary>
    /// Validates whether content can be encoded using the specified barcode symbology.
    /// </summary>
    BarcodeValidationResult Validate(string? content, BarcodeSymbology symbology);
}
