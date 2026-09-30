using Xunit;
using CAL_QR.Validation;

namespace CAL_QR.Tests
{
    public class UserPasswordRulesTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("1234567")]
        public void Validate_RejectsEmptyOrShort(string? password)
        {
            Assert.False(string.IsNullOrWhiteSpace(UserPasswordRules.Validate(password)));
        }

        [Theory]
        [InlineData("12345678")]
        [InlineData("كلمة_مرور_طويلة")]
        public void Validate_AcceptsMinLengthOrMore(string password)
        {
            Assert.Null(UserPasswordRules.Validate(password));
        }
    }
}
