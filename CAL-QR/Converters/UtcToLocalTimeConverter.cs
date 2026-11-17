using System;
using System.Globalization;
using System.Windows.Data;
using CAL_QR.Validation;

namespace CAL_QR.Converters
{
    /// <summary>
    /// يعرض وقتاً مخزَّناً بتوقيت UTC (مثل AuditLog.ActionAt) بتوقيت الجهاز المحلّيّ، للعرض فقط.
    /// </summary>
    public class UtcToLocalTimeConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not DateTime storedUtc)
                return value;

            return AuditLogDateRangeRules.ToLocal(storedUtc, TimeZoneInfo.Local);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
