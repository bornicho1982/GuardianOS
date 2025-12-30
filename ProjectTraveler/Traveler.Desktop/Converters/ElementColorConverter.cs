using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace Traveler.Desktop.Converters;

/// <summary>
/// Converts DamageTypeHash to element color brush (DIM-style).
/// </summary>
public class ElementColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not uint damageTypeHash)
            return new SolidColorBrush(Color.Parse("#d0d0d0")); // Kinetic default
        
        return damageTypeHash switch
        {
            // Based on DIM _variables.scss element colors
            2303181850 => new SolidColorBrush(Color.Parse("#79bbe8")), // Arc
            1847026933 => new SolidColorBrush(Color.Parse("#f0631e")), // Solar
            3454344768 => new SolidColorBrush(Color.Parse("#8e749e")), // Void
            1513470552 => new SolidColorBrush(Color.Parse("#4d88ff")), // Stasis
            3949783978 => new SolidColorBrush(Color.Parse("#35e366")), // Strand
            3373582085 => new SolidColorBrush(Color.Parse("#d0d0d0")), // Kinetic
            _ => new SolidColorBrush(Color.Parse("#d0d0d0"))           // Default
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
