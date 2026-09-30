using System;

namespace CAL_QR.Helpers
{
    public static class DateHelper
    {
        public static string Format(DateTime date)
        {
            return date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string Format(DateTime? date)
        {
            return date?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        }

        public static DateTime? Parse(string dateStr)
        {
            if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date))
            {
                return date;
            }
            return null;
        }
    }
}
