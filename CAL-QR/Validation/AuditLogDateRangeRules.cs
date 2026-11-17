using System;

namespace CAL_QR.Validation
{
    /// <summary>
    /// تحويل الأيّام التي يختارها المستخدم في فلتر سجلّ العمليّات إلى مدى بتوقيت UTC.
    ///
    /// لماذا: AuditLog.ActionAt يُحفظ بتوقيت UTC، والمستخدم يختار يوماً بتوقيته المحلّيّ. المقارنة
    /// المباشرة كانت تنسب عمليّات أوّل ساعتين من اليوم (UTC+2) إلى اليوم السابق. المنطقة الزمنيّة
    /// وسيط ليُختبر التحويل بمنطقة ثابتة على أيّ جهاز.
    /// </summary>
    public static class AuditLogDateRangeRules
    {
        /// <summary>
        /// بداية المدى: منتصف ليل اليوم الأوّل محلّيّاً بتوقيت UTC (شاملة). نهايته: منتصف ليل اليوم
        /// التالي لليوم الأخير محلّيّاً بتوقيت UTC (غير شاملة). يومٌ غير محدَّد يبقى null.
        /// </summary>
        public static (DateTime? StartUtc, DateTime? EndUtcExclusive) ToUtcRange(
            DateTime? startLocalDay, DateTime? endLocalDay, TimeZoneInfo zone)
        {
            if (zone == null) throw new ArgumentNullException(nameof(zone));

            DateTime? start = startLocalDay.HasValue ? LocalMidnightToUtc(startLocalDay.Value.Date, zone) : null;
            DateTime? end = endLocalDay.HasValue ? LocalMidnightToUtc(endLocalDay.Value.Date.AddDays(1), zone) : null;
            return (start, end);
        }

        /// <summary>وقت مخزَّن بتوقيت UTC معروضاً بتوقيت المنطقة المعطاة.</summary>
        public static DateTime ToLocal(DateTime storedUtc, TimeZoneInfo zone)
        {
            if (zone == null) throw new ArgumentNullException(nameof(zone));
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(storedUtc, DateTimeKind.Utc), zone);
        }

        private static DateTime LocalMidnightToUtc(DateTime localMidnight, TimeZoneInfo zone)
        {
            var local = DateTime.SpecifyKind(localMidnight, DateTimeKind.Unspecified);

            // منتصف ليل غير موجود (قفزة توقيت صيفيّ عنده): أوّل لحظة موجودة بعده هي بداية اليوم.
            while (zone.IsInvalidTime(local))
                local = local.AddMinutes(30);

            return TimeZoneInfo.ConvertTimeToUtc(local, zone);
        }
    }
}
