using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Traveler.Desktop.Converters;

/// <summary>
/// Converts tier type string to background color using DIM color scheme.
/// </summary>
public class TierBackgroundConverter : IValueConverter
{
    // DIM Color Scheme
    private static readonly IBrush LegendaryBrush = new SolidColorBrush(Color.Parse("#513065"));
    private static readonly IBrush ExoticBrush = new SolidColorBrush(Color.Parse("#c3a019"));
    private static readonly IBrush RareBrush = new SolidColorBrush(Color.Parse("#5076a3"));
    private static readonly IBrush CommonBrush = new SolidColorBrush(Color.Parse("#366e42"));
    private static readonly IBrush DefaultBrush = new SolidColorBrush(Color.Parse("#333333"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string tierType)
        {
            return tierType switch
            {
                "Exotic" => ExoticBrush,
                "Legendary" => LegendaryBrush,
                "Rare" => RareBrush,
                "Common" or "Uncommon" => CommonBrush,
                _ => DefaultBrush
            };
        }
        return DefaultBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
