using System;
using System.Globalization;
using System.Windows.Data;

namespace CAL_QR.Helpers
{
    /// <summary>
    /// يحوّل قيمة نصّية إلى bool بمقارنتها بـ ConverterParameter.
    /// يُستخدم لربط RadioButton.IsChecked بخاصية string في الـViewModel.
    /// </summary>
    [ValueConversion(typeof(string), typeof(bool))]
    public sealed class StringEqualityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is string s && parameter is string p && s == p;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true ? parameter : Binding.DoNothing;
    }
}
