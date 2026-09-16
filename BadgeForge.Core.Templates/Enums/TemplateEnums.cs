namespace BadgeForge.Core.Templates.Enums;

/// <summary>
/// Horizontal alignment for text rendering.
/// </summary>
public enum TextAlignment
{
    Left,
    Center,
    Right
}

/// <summary>
/// Sizing mode for photo/image layer content.
/// </summary>
public enum PhotoCropMode
{
    AspectFill,
    AspectFit,
    Stretch
}

/// <summary>
/// Supported barcode symbologies.
/// </summary>
public enum BarcodeSymbology
{
    Code128,
    Code39,
    QrCode,
    Ean13,
    UpcA,
    Pdf417
}

/// <summary>
/// What a text layer does when its (resolved) text doesn't fit the layer frame.
/// </summary>
public enum TextOverflowMode
{
    /// <summary>
    /// Draw at the chosen size even if the text runs past the frame (legacy behaviour).
    /// </summary>
    Overflow,

    /// <summary>
    /// Shrink the font until every line fits the frame width, down to the layer's minimum size; text still too long
    /// at the minimum size is cut with an ellipsis.
    /// </summary>
    ShrinkToFit,

    /// <summary>
    /// Keep the chosen size and cut lines that are too long with an ellipsis ("…").
    /// </summary>
    Ellipsis,

    /// <summary>
    /// Break lines at word boundaries to fit the frame width; if the lines are taller than the frame the font
    /// shrinks, and anything still left over is cut with an ellipsis on the last visible line.
    /// </summary>
    Wrap
}
