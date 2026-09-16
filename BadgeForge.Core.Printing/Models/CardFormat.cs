namespace BadgeForge.Core.Printing.Models;

/// <summary>
/// Defines a card format with physical dimensions, target DPI, and safe margins.
/// Card formats are configurable runtime models, never hardcoded constants,
/// allowing card sizes (CR80, CR79, oversized credentials, custom) to change
/// without code changes.
/// </summary>
public record CardFormat
{
    /// <summary>
    /// Descriptive name for the card profile (e.g., "CR80 Standard", "CR79 Adhesive", "Custom").
    /// </summary>
    public string Name { get; init; } = "CR80 Standard";

    /// <summary>
    /// Physical width in millimeters (long edge in landscape).
    /// </summary>
    public double WidthMm { get; init; } = 85.60;

    /// <summary>
    /// Physical height in millimeters (short edge in landscape).
    /// </summary>
    public double HeightMm { get; init; } = 53.98;

    /// <summary>
    /// Target printing resolution in dots per inch (DPI). Default is 300 DPI for direct-to-card printers.
    /// </summary>
    public int Dpi { get; init; } = 300;

    /// <summary>
    /// Recommended safe margin from the physical edge in millimeters (~0.51mm ≈ 6 pixels at 300 DPI).
    /// Prevents content truncation near card edges.
    /// </summary>
    public double SafeMarginMm { get; init; } = 0.508;

    /// <summary>
    /// True if portrait orientation (width < height), false if landscape.
    /// </summary>
    public bool IsPortrait { get; init; } = false;

    /// <summary>
    /// Computed canvas pixel width based on physical width and DPI.
    /// For standard CR80 at 300 DPI: (85.60 / 25.4) * 300 ≈ 1011 to 1013 pixels.
    /// </summary>
    public int WidthPixels => (int)Math.Round((WidthMm / 25.4) * Dpi);

    /// <summary>
    /// Computed canvas pixel height based on physical height and DPI.
    /// For standard CR80 at 300 DPI: (53.98 / 25.4) * 300 ≈ 637 to 638 pixels.
    /// </summary>
    public int HeightPixels => (int)Math.Round((HeightMm / 25.4) * Dpi);

    /// <summary>
    /// Computed safe margin in pixels based on SafeMarginMm and DPI (~6 pixels at 300 DPI).
    /// </summary>
    public int SafeMarginPixels => (int)Math.Round((SafeMarginMm / 25.4) * Dpi);

    /// <summary>
    /// Aspect ratio of the printable card canvas (width / height).
    /// </summary>
    public double AspectRatio => HeightMm > 0 ? WidthMm / HeightMm : 1.0;

    /// <summary>
    /// Returns an orientation-swapped copy of this format.
    /// </summary>
    public CardFormat WithOrientation(bool portrait)
    {
        if (portrait == IsPortrait)
            return this;

        return this with
        {
            WidthMm = HeightMm,
            HeightMm = WidthMm,
            IsPortrait = portrait
        };
    }

    /// <summary>
    /// Standard ISO/IEC 7810 ID-1 (CR80) format: 85.60mm × 53.98mm (3.370" × 2.125") @ 300 DPI.
    /// Yields ~1011–1013 × 637–638 pixels with ~6px safe margin.
    /// </summary>
    public static CardFormat CR80 => new()
    {
        Name = "CR80 Standard (ID-1)",
        WidthMm = 85.60,
        HeightMm = 53.98,
        Dpi = 300,
        SafeMarginMm = 0.508,
        IsPortrait = false
    };

    /// <summary>
    /// CR79 adhesive-backed format: 83.90mm × 51.00mm (3.303" × 2.008") @ 300 DPI.
    /// Used frequently for proximity card overlays.
    /// </summary>
    public static CardFormat CR79 => new()
    {
        Name = "CR79 Adhesive Overlay",
        WidthMm = 83.90,
        HeightMm = 51.00,
        Dpi = 300,
        SafeMarginMm = 0.508,
        IsPortrait = false
    };
}
