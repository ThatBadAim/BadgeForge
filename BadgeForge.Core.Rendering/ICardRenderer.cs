using BadgeForge.Core.Printing.Interfaces;
using BadgeForge.Core.Printing.Models;
using BadgeForge.Core.Templates.Models;
using SkiaSharp;

namespace BadgeForge.Core.Rendering;

/// <summary>
/// Interface for rendering badge templates onto physical/driver canvas pixel buffers.
/// </summary>
public interface ICardRenderer
{
    /// <summary>
    /// Renders a badge template with bound field data against the real, driver-reported canvas size.
    /// Never uses hardcoded dimensions.
    /// </summary>
    /// <param name="template">Template definition defining layout and layers.</param>
    /// <param name="fieldData">Dictionary of record values keyed by field/token name.</param>
    /// <param name="driver">Hardware driver abstraction used to probe real canvas size and validate capabilities.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Rendered 300 DPI badge canvas as an SKBitmap.</returns>
    Task<SKBitmap> RenderCardAsync(
        TemplateDefinition template,
        IReadOnlyDictionary<string, string> fieldData,
        ICardPrinterDriver driver,
        CancellationToken ct = default);

    /// <summary>
    /// Synchronous convenience overload for rendering a badge card against a driver.
    /// </summary>
    SKBitmap RenderCard(
        TemplateDefinition template,
        IReadOnlyDictionary<string, string> fieldData,
        ICardPrinterDriver driver);

    /// <summary>
    /// Renders a badge for on-screen preview at the template format's own pixel size, without consulting a printer,
    /// so the design can be seen even when the selected printer can't print it.
    /// </summary>
    SKBitmap RenderPreview(
        TemplateDefinition template,
        IReadOnlyDictionary<string, string> fieldData);

    /// <summary>
    /// Lists reasons this record's badge couldn't be printed faithfully on a printer with these capabilities
    /// (for example a barcode that can't be encoded or doesn't fit its frame). Empty when there are none.
    /// </summary>
    IReadOnlyList<string> FindPrintProblems(
        TemplateDefinition template,
        IReadOnlyDictionary<string, string> fieldData,
        PrinterCapabilities capabilities);
}
