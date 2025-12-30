using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Traveler.Desktop.Converters;

/// <summary>
/// Converts a boolean to a Brush. True = Gold (masterwork), False = Transparent.
/// </summary>
public class BoolToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isMasterwork && isMasterwork)
        {
            return new SolidColorBrush(Color.FromRgb(255, 215, 0)); // Gold
        }
        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
