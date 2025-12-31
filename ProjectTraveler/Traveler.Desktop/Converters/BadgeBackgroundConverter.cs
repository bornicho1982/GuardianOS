using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Traveler.Desktop.Converters;

/// <summary>
/// Converts masterwork status + tier type to badge background color.
/// Masterwork items get gold background, others get tier color.
/// </summary>
public class BadgeBackgroundConverter : IMultiValueConverter
{
    public static readonly BadgeBackgroundConverter Instance = new();
    
    // DIM masterwork gold badge color
    private static readonly IBrush MasterworkBrush = new SolidColorBrush(Color.Parse("#eade8b"));
    
    // Tier colors (same as TierBackgroundConverter)
    private static readonly IBrush LegendaryBrush = new SolidColorBrush(Color.Parse("#522f65"));
    private static readonly IBrush ExoticBrush = new SolidColorBrush(Color.Parse("#ceae33"));
    private static readonly IBrush RareBrush = new SolidColorBrush(Color.Parse("#5076a3"));
    private static readonly IBrush UncommonBrush = new SolidColorBrush(Color.Parse("#366f42"));
    private static readonly IBrush CommonBrush = new SolidColorBrush(Color.Parse("#c3bcb4"));
    private static readonly IBrush DefaultBrush = new SolidColorBrush(Color.Parse("#333333"));

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2)
            return DefaultBrush;
            
        var isMasterwork = values[0] is bool mw && mw;
        var tierType = values[1] as string ?? "";
        
        // Masterwork items get gold badge
        if (isMasterwork)
            return MasterworkBrush;
        
        // Otherwise use tier color
        return tierType switch
        {
            "Exotic" => ExoticBrush,
            "Legendary" => LegendaryBrush,
            "Rare" => RareBrush,
            "Uncommon" => UncommonBrush,
            "Common" => CommonBrush,
            _ => DefaultBrush
        };
    }
}
