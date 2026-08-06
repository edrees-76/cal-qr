using System;
using System.Globalization;
using System.Windows.Data;

namespace CAL_QR.Converters
{
    /// <summary>
    /// يختصر رقم الشهادة من "TNRC-SSDL-2026-0001" إلى "2026-0001"
    /// للعرض فقط في جدول السجلات.
    /// </summary>
    public class ShortCertificateNumberConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string fullNumber || string.IsNullOrWhiteSpace(fullNumber))
                return value;

            // TNRC-SSDL-2026-0001 → split by '-' → take last two parts → "2026-0001"
            var parts = fullNumber.Split('-');
            if (parts.Length >= 2)
                return $"{parts[^2]}-{parts[^1]}";

            return fullNumber;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
