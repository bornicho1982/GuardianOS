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
        if (value is string path && !string.IsNullOrEmpty(path))
        {
            // Already absolute URL
            if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }
            // Relative path from Bungie API
            if (path.StartsWith("/"))
            {
                return $"{BaseUrl}{path}";
            }
            return path;
        }
        
        // Return placeholder for null/empty
        return Placeholder;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
