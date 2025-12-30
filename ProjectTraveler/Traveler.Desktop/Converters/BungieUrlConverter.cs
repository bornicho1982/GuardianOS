using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Traveler.Desktop.Converters;

public class BungieUrlConverter : IValueConverter
{
    private const string BaseUrl = "https://www.bungie.net";

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string path && !string.IsNullOrEmpty(path))
        {
            if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }
            return $"{BaseUrl}{path}";
        }
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
