using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PLCDataLogger.Converters
{
    public class EnumToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return Visibility.Collapsed;

            string parameterString = parameter.ToString() ?? string.Empty;

            if (Enum.IsDefined(value.GetType(), value) == false)
                return Visibility.Collapsed;

            object parameterValue = Enum.Parse(value.GetType(), parameterString);

            return parameterValue.Equals(value) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
