using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace BadgeForge.App.Converters;

/// <summary>
/// Maps a layer's IsVisible flag to an open-eye or crossed-out-eye icon geometry.
/// </summary>
public class LayerVisibilityIconConverter : IValueConverter
{
    private static readonly Geometry EyeIcon =
        Geometry.Parse("M8 3.5C4.2 3.5 1.1 6 .3 8c.8 2 3.9 4.5 7.7 4.5s6.9-2.5 7.7-4.5c-.8-2-3.9-4.5-7.7-4.5zm0 7.3A2.8 2.8 0 1 1 8 5.2a2.8 2.8 0 0 1 0 5.6z");
    private static readonly Geometry EyeOffIcon =
        Geometry.Parse("M8 3.5C4.2 3.5 1.1 6 .3 8c.8 2 3.9 4.5 7.7 4.5s6.9-2.5 7.7-4.5c-.8-2-3.9-4.5-7.7-4.5zm0 7.3A2.8 2.8 0 1 1 8 5.2a2.8 2.8 0 0 1 0 5.6zM2.1 1.4 14.6 13.9l-1 1L1.1 2.4z");

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? EyeIcon : EyeOffIcon;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
