using System.Collections.Generic;
using Xunit;
using CAL_QR.Validation;

namespace CAL_QR.Tests
{
    /// <summary>
    /// اختبارات وحدة على TextInputRules.Clean.
    ///
    /// المحارف هنا مكتوبة بقائمة صريحة مستقلّة عن ثوابت TextInputRules عمداً:
    /// مصدر واحد للطرفين يعني أنّ محرفاً نُسي في الإنتاج سيُنسى في الاختبار
    /// كذلك فيمرّ الخلل — وهذا ينقض غرض الاختبار أصلاً.
    /// </summary>
    public class TextInputRulesTests
    {
        public static IEnumerable<object[]> HiddenDirectionalMarks()
        {
            yield return new object[] { (char)0x200E, "LRM" };
            yield return new object[] { (char)0x200F, "RLM" };
            yield return new object[] { (char)0x061C, "ALM" };
            yield return new object[] { (char)0x202A, "LRE" };
            yield return new object[] { (char)0x202B, "RLE" };
            yield return new object[] { (char)0x202C, "PDF" };
            yield return new object[] { (char)0x202D, "LRO" };
            yield return new object[] { (char)0x202E, "RLO" };
            yield return new object[] { (char)0x2066, "LRI" };
            yield return new object[] { (char)0x2067, "RLI" };
            yield return new object[] { (char)0x2068, "FSI" };
            yield return new object[] { (char)0x2069, "PDI" };
            yield return new object[] { (char)0x200B, "ZWSP" };
            yield return new object[] { (char)0xFEFF, "BOM" };
        }

        [Theory]
        [MemberData(nameof(HiddenDirectionalMarks))]
        public void Clean_RemovesTheHiddenMark_LeavingTheRestIntact(char mark, string label)
        {
            string input = "قبل" + mark + "بعد";

            string? result = TextInputRules.Clean(input);

            // Assert.True برسالة لا Assert.Equal: الفشل هنا يخصّ محرفًا لا يُرى، ورسالة
            // xUnit الافتراضيّة كانت ستعرض «قبلبعد» مقابل «قبلبعد» بلا فرق ظاهر. الاسم
            // ورقم الترميز في الرسالة هما ما يجعل الفشل مقروءًا. وهذا أيضًا يستعمل label
            // (xUnit1026) ويُسقط التوكيد الزائد الذي أنتج CS8604.
            Assert.True(
                result == "قبلبعد",
                $"{label} (U+{(int)mark:X4}) لم يُحذف أو أتلف النصّ. الناتج: {result}");
        }

        /// <summary>
        /// حارس قرار لا سلوك: ZWNJ (U+200C) وZWJ (U+200D) لا تُحذف، لأنّهما
        /// يغيّران الشكل المرسوم للحرف العربيّ والفارسيّ فعلاً. من يحذفهما
        /// لاحقاً بحجّة «محارف خفيّة» يبدّل ما تراه العين، وهو عيب جديد لا إصلاح.
        /// </summary>
        [Fact]
        public void Clean_PreservesZwnjAndZwj_BecauseTheyChangeGlyphShape()
        {
            char zwnj = (char)0x200C;
            char zwj = (char)0x200D;
            string input = "می" + zwnj + "خواهم" + zwj + "درست";

            string? result = TextInputRules.Clean(input);

            Assert.Equal(input, result);
        }

        [Fact]
        public void Clean_Null_ReturnsNull()
        {
            Assert.Null(TextInputRules.Clean(null));
        }

        [Fact]
        public void Clean_EmptyString_ReturnsNull()
        {
            Assert.Null(TextInputRules.Clean(""));
        }

        [Fact]
        public void Clean_WhitespaceOnly_ReturnsNull()
        {
            Assert.Null(TextInputRules.Clean("   "));
        }

        [Fact]
        public void Clean_RlmOnly_ReturnsNull()
        {
            string input = ((char)0x200F).ToString();

            Assert.Null(TextInputRules.Clean(input));
        }

        [Fact]
        public void Clean_RlmSurroundedBySpaces_ReturnsNull()
        {
            string input = "  " + (char)0x200F + "  ";

            Assert.Null(TextInputRules.Clean(input));
        }

        /// <summary>Trim يقع بعد حذف المحارف الخفيّة لا قبله.</summary>
        [Fact]
        public void Clean_TrimsAfterRemovingHiddenMarks_NotBefore()
        {
            char rlm = (char)0x200F;
            string input = " " + rlm + " نصّ " + rlm + " ";

            string? result = TextInputRules.Clean(input);

            Assert.Equal("نصّ", result);
        }

        [Fact]
        public void Clean_OrdinaryText_IsReturnedUnchanged()
        {
            string input = "القيمة: 5.40 ± 0.10 °C، النطاق < 10/1";

            string? result = TextInputRules.Clean(input);

            Assert.Equal(input, result);
        }
    }
}
