using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using BadgeForge.Core.Templates.Enums;
using BadgeForge.Core.Templates.Layers;

namespace BadgeForge.App.Converters;

/// <summary>
/// Maps a template layer to a 16×16 icon geometry for the layer list.
/// </summary>
public class LayerIconConverter : IValueConverter
{
    private static readonly Geometry TextIcon =
        Geometry.Parse("M2 2h12v3h-1.5V3.5h-3.75v9H11V14H5v-1.5h2.25v-9H3.5V5H2z");
    private static readonly Geometry PhotoIcon =
        Geometry.Parse("M8 2a3 3 0 1 1 0 6 3 3 0 0 1 0-6zm0 7c3.3 0 6 1.6 6 3.5V14H2v-1.5C2 10.6 4.7 9 8 9z");
    private static readonly Geometry BarcodeIcon =
        Geometry.Parse("M1 3h1.5v10H1zM3.5 3h1v10h-1zM5.5 3h2v10h-2zM8.5 3h1v10h-1zM10.5 3h2v10h-2zM13.5 3H15v10h-1.5z");
    private static readonly Geometry QrIcon =
        Geometry.Parse("M2 2h5v5H2zm1.5 1.5v2h2v-2zM9 2h5v5H9zm1.5 1.5v2h2v-2zM2 9h5v5H2zm1.5 1.5v2h2v-2zM9 9h2v2H9zm3 0h2v2h-2zm-3 3h2v2H9zm3 0h2v2h-2z");
    private static readonly Geometry ImageIcon =
        Geometry.Parse("M2 3h12v10H2zm1.5 1.5v5.4l2.8-2.8 2.2 2.2 1.5-1.5 2.5 2.5V4.5zM10.5 5.5a1 1 0 1 1 0 2 1 1 0 0 1 0-2z");

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        TextLayer => TextIcon,
        PhotoLayer => PhotoIcon,
        BarcodeLayer { Symbology: BarcodeSymbology.QrCode } => QrIcon,
        BarcodeLayer => BarcodeIcon,
        StaticImageLayer => ImageIcon,
        _ => TextIcon
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
