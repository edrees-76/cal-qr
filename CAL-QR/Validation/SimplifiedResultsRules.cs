using System.Collections.Generic;
using System.Linq;
using CAL_QR.Models;

namespace CAL_QR.Validation
{
    /// <summary>
    /// العرض المبسّط (Gamma · Teletector · PED · Dose Rate) يطبع لكلّ صفّ نتيجة سطرَي CF وAE%
    /// فقط، بلا مصدر ولا نظير ولا مستوى (قالب G501: قيمة واحدة). أكثر من صفّ مطبوع يُنتج
    /// أزواجاً متكرّرة لا يميّز القارئ بينها. تحذير لا منع: قرار الوحدة أنّ لا حقل يمنع الحفظ.
    /// </summary>
    public static class SimplifiedResultsRules
    {
        /// <summary>عدد الصفوف التي تُطبع فعلاً في العرض المبسّط (CF أو AE غير فارغ).</summary>
        public static int PrintedRowCount(IEnumerable<CertificateCalibrationResult>? rows) =>
            (rows ?? Enumerable.Empty<CertificateCalibrationResult>()).Count(r =>
                r != null &&
                (!string.IsNullOrWhiteSpace(r.CorrectionFactor) || !string.IsNullOrWhiteSpace(r.AbsoluteRelativeError)));

        /// <summary>رسالة التحذير، أو null إن لم يكن العرض مبسّطاً أو كان فيه صفّ مطبوع واحد على الأكثر.</summary>
        public static string? Warning(bool simplifiedLayout, IEnumerable<CertificateCalibrationResult>? rows)
        {
            if (!simplifiedLayout) return null;

            int printed = PrintedRowCount(rows);
            return printed > 1
                ? $"هذا النوع يُطبع بقيمة CF واحدة مع خطئها النسبيّ، وفي الجدول {printed} صفوف مطبوعة — " +
                  "ستظهر في الشهادة أزواج CF/AE متكرّرة بلا نظير ولا مستوى يميّزها."
                : null;
        }
    }
}
