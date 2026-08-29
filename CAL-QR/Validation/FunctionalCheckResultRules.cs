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
        public static bool IsNonPassing(string? result)
        {
            if (string.IsNullOrWhiteSpace(result)) return false;
            var normalized = System.Text.RegularExpressions.Regex.Replace(result.Trim(), @"\s+", " ");
            return normalized.Equals("Failed", System.StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("Not Performed", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
