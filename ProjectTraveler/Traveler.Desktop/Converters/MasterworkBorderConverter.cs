using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Traveler.Desktop.Converters;

/// <summary>
/// Converts a boolean (IsMasterwork) to masterwork gold border brush, otherwise transparent.
/// </summary>
public class MasterworkBorderConverter : IValueConverter
{
    // DIM Masterwork Border Color: #eade8b
    private static readonly IBrush MasterworkBrush = new SolidColorBrush(Color.Parse("#eade8b"));
    private static readonly IBrush TransparentBrush = Brushes.Transparent;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isMasterwork && isMasterwork)
        {
            return MasterworkBrush;
        }
        return TransparentBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
