using Xunit;
using CAL_QR.Helpers;

namespace CAL_QR.Tests
{
    public class ExcelSafeTests
    {
        [Theory]
        [InlineData("=SUM(A1:A2)", "'=SUM(A1:A2)")]
        [InlineData("+1", "'+1")]
        [InlineData("-cmd", "'-cmd")]
        [InlineData("@x", "'@x")]
        [InlineData("\tx", "'\tx")]
        [InlineData("عادي", "عادي")]
        [InlineData("a=b", "a=b")]
        [InlineData("", "")]
        public void Text_PrefixesOnlyFormulaTriggers(string input, string expected)
        {
            Assert.Equal(expected, ExcelSafe.Text(input));
        }

        [Fact]
        public void Text_NullBecomesEmpty() => Assert.Equal(string.Empty, ExcelSafe.Text(null));
    }
}
