using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace Traveler.Desktop.Converters;

/// <summary>
/// Converts boolean to color brush for active/inactive states.
/// </summary>
public class BoolToColorConverter : IValueConverter
{
    public Color TrueColor { get; set; } = Color.Parse("#EBC805");
    public Color FalseColor { get; set; } = Color.Parse("#3D3D40");

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isActive && isActive)
        {
            return new SolidColorBrush(TrueColor);
        }
        return new SolidColorBrush(FalseColor);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts boolean to border brush for selected character indicator.
/// </summary>
public class BoolToBorderConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isSelected && isSelected)
        {
            // Selected - bright gold border
            return new SolidColorBrush(Color.Parse("#EBC805"));
        }
        // Not selected - dark border
        return new SolidColorBrush(Color.Parse("#404040"));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
