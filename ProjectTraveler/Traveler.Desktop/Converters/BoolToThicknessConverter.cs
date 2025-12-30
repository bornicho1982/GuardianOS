using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace Traveler.Desktop.Converters;

/// <summary>
/// Converts a boolean (IsMasterwork) to thickness: 2 for masterwork, 0 otherwise.
/// </summary>
public class BoolToThicknessConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isMasterwork && isMasterwork)
        {
            return new Thickness(2); // Masterwork border
        }
        return new Thickness(0);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
