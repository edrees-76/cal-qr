using System;

namespace CAL_QR.Validation
{
    /// <summary>
    /// قواعد اشتقاق القيم القياسية النصية.
    /// </summary>
    public static class MeasurementValueRules
    {
        // علامات الاتجاه ثنائي الاتجاه — قد تسبق الإشارة في نص عربي.
        private const char LeftToRightMark = '‎';
        private const char RightToLeftMark = '‏';
        private const char ArabicLetterMark = '؜';

        /// <summary>
        /// AE = |RE| — قيمة مطلقة **نصية**: إزالة إشارة البداية (سالبة أو موجبة صريحة) فقط،
        /// بلا Parse ولا Math.Abs ولا إعادة تنسيق.
        ///
        /// السبب (وهو قرار معتمد لا تفصيل تنفيذي): القيم تُخزَّن نصاً للحفاظ على
        /// الأرقام المعنوية تحت ISO/IEC 17025، وبعضها غير رقمي أصلاً.
        ///   • "N/A" و"&lt; 0.1" لا قيمة مطلقة رقمية لهما — تمر كما هي.
        ///   • double.Parse("-5.40").ToString() يعيد "-5.4"، فتعرض الشهادة دقة
        ///     أقل مما قيس فعلاً.
        /// تُغطّى الإشارات: - (U+002D) و− (U+2212) و﹣ (U+FE63) و－ (U+FF0D)،
        /// مسبوقة أو غير مسبوقة بعلامة اتجاه.
        /// </summary>
        public static string? AbsoluteOf(string? relativeError)
        {
            if (relativeError == null)
            {
                return null;
            }

            string value = relativeError.Trim();
            if (value.Length == 0)
            {
                return value;
            }

            int index = 0;
            while (index < value.Length && IsBidiMark(value[index]))
            {
                index++;
            }

            if (index >= value.Length || !(IsMinusSign(value[index]) || IsPlusSign(value[index])))
            {
                // لا إشارة بادئة — القيمة مطلقة أصلاً أو غير رقمية
                return value;
            }

            // إسقاط الإشارة (سالبة أو موجبة صريحة) وعلامات الاتجاه التي سبقتها،
            // مع الإبقاء على بقية النص حرفياً
            return value.Substring(index + 1).Trim();
        }

        private static bool IsBidiMark(char c) =>
            c == LeftToRightMark || c == RightToLeftMark || c == ArabicLetterMark;

        private static bool IsMinusSign(char c) =>
            c == '-' || c == '−' || c == '﹣' || c == '－';

        // + (U+002B) و﹢ (U+FE62) و＋ (U+FF0B): إشارة موجبة صريحة لا معنى لها في القيمة المطلقة.
        private static bool IsPlusSign(char c) =>
            c == '+' || c == '﹢' || c == '＋';
    }
}
