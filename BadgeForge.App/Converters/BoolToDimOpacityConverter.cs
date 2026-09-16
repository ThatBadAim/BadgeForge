using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace BadgeForge.App.Converters;

/// <summary>
/// Full opacity for true, dimmed for false — used to show a hidden layer's row as faded out.
/// </summary>
public class BoolToDimOpacityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? 1.0 : 0.45;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
