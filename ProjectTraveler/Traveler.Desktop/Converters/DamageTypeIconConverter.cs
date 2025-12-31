using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace Traveler.Desktop.Converters;

/// <summary>
/// Converts damage type name to local Avalonia resource URI string.
/// </summary>
public class DamageTypeIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string type && !string.IsNullOrEmpty(type))
        {
            // Asegúrate de que los nombres coincidan con tus archivos png (arc, solar, void, stasis, strand, kinetic)
            return $"avares://Traveler.Desktop/Assets/Icons/Elements/{type.ToLower()}.png";
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts damage type name to Bungie CDN URL path.
/// </summary>
public class DamageTypeUrlConverter : IValueConverter
{
    private static readonly Dictionary<string, string> DamageTypeUrls = new()
    {
        { "arc", "/img/destiny_content/damage_types/destiny2/arc_trans.png" },
        { "solar", "/img/destiny_content/damage_types/destiny2/thermal_trans.png" },
        { "void", "/img/destiny_content/damage_types/destiny2/void_trans.png" },
        { "stasis", "/img/destiny_content/damage_types/destiny2/stasis-white-96x96.png" },
        { "strand", "/img/destiny_content/damage_types/destiny2/strand-white-185x185.png" },
        { "kinetic", "/img/destiny_content/damage_types/destiny2/kinetic_trans.png" },
    };

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string damageTypeName || string.IsNullOrEmpty(damageTypeName))
            return null;

        if (DamageTypeUrls.TryGetValue(damageTypeName, out var path))
        {
            return $"https://www.bungie.net{path}";
        }
        
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
