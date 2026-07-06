using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CAL_QR.Helpers
{
    public class DoubleToGridLengthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double val)
            {
                if (parameter != null && parameter.ToString() == "Remaining")
                {
                    return new GridLength(Math.Max(0, 100.0 - val), GridUnitType.Star);
                }
                return new GridLength(Math.Max(0, val), GridUnitType.Star);
            }
            return new GridLength(0, GridUnitType.Star);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
