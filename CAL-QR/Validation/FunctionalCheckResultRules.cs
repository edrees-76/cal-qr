using System.Collections.Generic;

namespace CAL_QR.Validation
{
    /// <summary>
    /// دلالة نتيجة الفحص الوظيفيّ. مصدر حقيقة واحد تقرأ منه الوثيقة
    /// المطبوعة (تلوين الخليّة الأحمر) وتحذيرات النموذج — النصّان
    /// "Failed" و"Not Performed" منقولان من قوالب م. رضا، وتكرارهما
    /// في موضعين كان يسمح لأحدهما أن يتغيّر دون الآخر.
    /// </summary>
    public static class FunctionalCheckResultRules
    {
        /// <summary>
        /// المفردات المسموحة في عمود Result، منقولة من قوالب م. رضا الستّة.
        /// null أوّلًا = «لم يُملأ بعد» وهي حالة مشروعة: صفّ جديد يولد فارغًا،
        /// وتقرير الحالة يُفرّغ النتائج عمدًا لأنّ القياس لم يقع.
        /// مصدر واحد يقرأ منه النموذج وIsNonPassing، فلا يتباعد النصّان.
        /// </summary>
        public static readonly IReadOnlyList<string?> AllowedResults =
            new string?[] { null, "Acceptable", "Failed", "Not Performed" };

        public static bool IsNonPassing(string? result)
        {
            if (string.IsNullOrWhiteSpace(result)) return false;
            var normalized = System.Text.RegularExpressions.Regex.Replace(result.Trim(), @"\s+", " ");
            return normalized.Equals("Failed", System.StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("Not Performed", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
