using System;
using System.Globalization;

namespace CAL_QR.Validation
{
    /// <summary>
    /// نصّ ساعة الشريط العلويّ: «الثلاثاء 17/11/2026 06:56 م».
    /// أسماء الأيّام ثابتة هنا ولا تُؤخذ من ثقافة الجهاز، لأنّ بعض الثقافات العربيّة تعرض
    /// التقويم الهجريّ؛ والأرقام لاتينيّة كبقيّة الواجهة.
    /// </summary>
    public static class HeaderClockRules
    {
        private static readonly string[] DayNames =
        {
            "الأحد",     // Sunday
            "الاثنين",   // Monday
            "الثلاثاء",  // Tuesday
            "الأربعاء",  // Wednesday
            "الخميس",    // Thursday
            "الجمعة",    // Friday
            "السبت"      // Saturday
        };

        public static string Format(DateTime localNow)
        {
            string day = DayNames[(int)localNow.DayOfWeek];
            string date = localNow.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            string time = localNow.ToString("hh:mm", CultureInfo.InvariantCulture);
            string period = localNow.Hour < 12 ? "ص" : "م";
            return $"{day} {date} {time} {period}";
        }
    }
}
