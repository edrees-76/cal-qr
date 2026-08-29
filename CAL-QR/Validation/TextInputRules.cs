using System;
using System.Text;

namespace CAL_QR.Validation
{
    /// <summary>
    /// تنظيف محارف الاتّجاه الخفيّة وما شابهها من نصوص المستخدم، قبل دخولها القاعدة.
    /// </summary>
    public static class TextInputRules
    {
        // محارف اتّجاه/تحكّم خفيّة تعبر PayloadNormalizer سالمة، فتُنتج وثيقتين
        // متطابقتين على الورق برمزَي تحقّق مختلفين. تُكتب بهروب Unicode صراحةً
        // لا بمحارف حرفيّة في المصدر: محرف خفيّ داخل الكود لا يراه المراجع ولا
        // ينجو من كلّ محرّر.
        private const char LeftToRightMark = (char)0x200E;
        private const char RightToLeftMark = (char)0x200F;
        private const char ArabicLetterMark = (char)0x061C;
        private const char LeftToRightEmbedding = (char)0x202A;
        private const char RightToLeftEmbedding = (char)0x202B;
        private const char PopDirectionalFormatting = (char)0x202C;
        private const char LeftToRightOverride = (char)0x202D;
        private const char RightToLeftOverride = (char)0x202E;
        private const char LeftToRightIsolate = (char)0x2066;
        private const char RightToLeftIsolate = (char)0x2067;
        private const char FirstStrongIsolate = (char)0x2068;
        private const char PopDirectionalIsolate = (char)0x2069;
        private const char ZeroWidthSpace = (char)0x200B;
        private const char ByteOrderMark = (char)0xFEFF;

        // استثناء مقصود: U+200C (ZWNJ) و U+200D (ZWJ) لا تُحذف. هذان يغيّران
        // الشكل المرسوم للحرف العربيّ والفارسيّ فعلاً، وحذفهما يبدّل ما تراه
        // العين — وهو نقيض غرض هذا الصنف. من يضيفهما لاحقاً بحجّة «محارف خفيّة»
        // يُدخل عيباً جديداً.

        /// <summary>
        /// يحذف محارف الاتّجاه الخفيّة أعلاه ثمّ يُطبّق Trim.
        ///
        /// العودة بـnull حين يخلو الناتج مقصودة: خانة لا تحوي إلّا محرف اتّجاه
        /// (RLM مثلاً) كانت تُحسب مملوءة (IsNullOrWhiteSpace = false لهذه
        /// المحارف) وتُطبع بياضاً — فيغطّي التوقيع محرفاً لم يُطبع قطّ. العودة
        /// بـnull تُعيدها إلى رمز الخلوّ ~ في نصّ التوقيع.
        /// </summary>
        public static string? Clean(string? value)
        {
            if (value == null)
            {
                return null;
            }

            var builder = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (!IsHiddenDirectionalMark(c))
                {
                    builder.Append(c);
                }
            }

            string cleaned = builder.ToString().Trim();
            return cleaned.Length == 0 ? null : cleaned;
        }

        private static bool IsHiddenDirectionalMark(char c) =>
            c == LeftToRightMark ||
            c == RightToLeftMark ||
            c == ArabicLetterMark ||
            c == LeftToRightEmbedding ||
            c == RightToLeftEmbedding ||
            c == PopDirectionalFormatting ||
            c == LeftToRightOverride ||
            c == RightToLeftOverride ||
            c == LeftToRightIsolate ||
            c == RightToLeftIsolate ||
            c == FirstStrongIsolate ||
            c == PopDirectionalIsolate ||
            c == ZeroWidthSpace ||
            c == ByteOrderMark;
    }
}
