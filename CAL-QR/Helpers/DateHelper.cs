using System;

namespace CAL_QR.Helpers
{
    public static class DateHelper
    {
        public static string Format(DateTime date)
        {
            return date.ToString("yyyy-MM-dd");
        }

        public static string Format(DateTime? date)
        {
            return date?.ToString("yyyy-MM-dd") ?? string.Empty;
        }

        public static DateTime? Parse(string dateStr)
        {
            if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var date))
            {
                return date;
            }
            return null;
        }
    }
}
