using System;
using System.Globalization;
using System.Windows.Data;

namespace GuardianOS.Core.Converters
{
    public class IndexToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int selectedIndex && parameter != null)
            {
                if (int.TryParse(parameter.ToString(), out int targetIndex))
                {
                    return selectedIndex == targetIndex;
                }
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isChecked && isChecked && parameter != null)
            {
                if (int.TryParse(parameter.ToString(), out int targetIndex))
                {
                    return targetIndex;
                }
            }
            return Binding.DoNothing;
        }
    }
}
