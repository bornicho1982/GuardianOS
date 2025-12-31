using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Traveler.Desktop.Converters;

public class ItemBorderColorConverter : IMultiValueConverter
{
    public static readonly ItemBorderColorConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        // Bindings: IsMasterwork, HasDeepsight, IsExotic
        if (values.Count < 3) return Brushes.Transparent;

        var isMasterwork = values[0] is true;
        var hasDeepsight = values[1] is true;
        var isExotic = values[2] is true;

        if (hasDeepsight)
        {
            return SolidColorBrush.Parse("#d25336"); // Deepsight Red
        }

        if (isMasterwork)
        {
            if (isExotic) return SolidColorBrush.Parse("#EBC805"); // Exotic Gold
            return SolidColorBrush.Parse("#EBC805"); // Legendary Gold (same for now, simplifies)
        }

        if (isExotic)
        {
             return SolidColorBrush.Parse("#ceae33"); // Yellowish for exotics
        }

        return Brushes.Transparent; // No border by default
    }
}
