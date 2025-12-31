using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Traveler.Desktop.Converters;

/// <summary>
/// Converts Bungie API relative paths to absolute URLs.
/// Returns placeholder for null/empty values.
/// </summary>
public class BungieUrlConverter : IValueConverter
{
    private const string BaseUrl = "https://www.bungie.net";
    private const string Placeholder = "avares://Traveler.Desktop/Assets/placeholder.png";

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Si el valor es nulo o vacío, devuelve null
        if (value is not string path || string.IsNullOrEmpty(path))
        {
            return null;
        }
        
        // Si ya es una URL absoluta, devuélvela tal cual
        if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }
        
        // Si es una ruta relativa, añade el dominio de Bungie
        return $"{BaseUrl}{path}";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
