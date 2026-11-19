using CAL_QR.Validation;
using Xunit;

namespace CAL_QR.Tests
{
    public class CoverageFactorRulesTests
    {
        [Theory]
        [InlineData("2")]
        [InlineData("2.0")]
        [InlineData("2.00")]
        [InlineData("2,00")]
        [InlineData(" 2 ")]
        [InlineData("٢")]
        public void IsTwo_TrueForNumericTwo(string value)
        {
            Assert.True(CoverageFactorRules.IsTwo(value));
            Assert.Equal(" (95% confidence level)", CoverageFactorRules.ConfidenceSuffix(value));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("1")]
        [InlineData("3")]
        [InlineData("1.96")]
        [InlineData("2.5")]
        [InlineData("N/A")]
        [InlineData("k=2")]
        public void IsTwo_FalseOtherwise(string? value)
        {
            Assert.False(CoverageFactorRules.IsTwo(value));
            Assert.Equal(string.Empty, CoverageFactorRules.ConfidenceSuffix(value));
        }
    }
}
