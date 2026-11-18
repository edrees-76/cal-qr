namespace CAL_QR.Validation
{
    /// <summary>
    /// لاحقة "%" لقيم uc وU في شريط ملخّص الشهادة (قالب رضا). اشتقاق وقت العرض من بيانات
    /// مجمّدة: لا يغيّر القيمة المخزَّنة ولا حمولة التوقيع.
    /// </summary>
    public static class PercentDisplayRules
    {
        /// <summary>
        /// يُلحق " %" إلّا إن انتهت القيمة بعلامة نسبة. العلامة العربيّة "٪" (U+066A) تُكتب
        /// من لوحة المفاتيح العربيّة؛ كان الفحص يرى "%" وحدها فيُطبع "2.00٪ %".
        /// </summary>
        public static string WithPercent(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            string trimmed = value.Trim();
            return trimmed.EndsWith('%') || trimmed.EndsWith('\u066A')
                ? trimmed
                : trimmed + " %";
        }
    }
}
