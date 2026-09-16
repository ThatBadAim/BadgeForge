using BadgeForge.Core.Templates.Enums;

namespace BadgeForge.Core.Templates.Layers;

/// <summary>
/// Represents a dynamic 1D or 2D barcode element bound to record data.
/// </summary>
public record BarcodeLayer : TemplateLayer
{
    public const string TypeDiscriminator = "Barcode";

    public override string LayerType => TypeDiscriminator;

    /// <summary>
    /// Content token or pattern to encode into the barcode (e.g. "{EmployeeId}" or "EMP-{Id}").
    /// </summary>
    public string ContentToken { get; init; } = "{EmployeeId}";

    /// <summary>
    /// Barcode symbology to render (Code128, Code39, QrCode, Ean13, UpcA, Pdf417).
    /// </summary>
    public BarcodeSymbology Symbology { get; init; } = BarcodeSymbology.Code128;

    /// <summary>
    /// For 1D barcodes, whether human-readable text is displayed below the bars.
    /// </summary>
    public bool IncludeText { get; init; } = true;

    /// <summary>
    /// If true, renders pure 1-bit black with anti-aliasing disabled for K-resin ribbon transfer.
    /// </summary>
    public bool IsPureBlackKResin { get; init; } = true;

    /// <summary>
    /// If true, the barcode value cannot be empty, and pre-flight validation verifies payload validity
    /// against the chosen symbology. Defaults to false.
    /// </summary>
    public bool IsRequired { get; init; } = false;

    public override IEnumerable<string> GetReferencedTokens()
    {
        return ExtractTokensFromString(ContentToken);
    }
}
