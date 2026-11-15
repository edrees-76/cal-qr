using Xunit;
using CAL_QR.Validation;

namespace CAL_QR.Tests
{
    public class IdleLockRulesTests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        public void Validate_RejectsZeroOrNegative(int minutes)
        {
            Assert.False(string.IsNullOrWhiteSpace(IdleLockRules.Validate(minutes)));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(600)]
        public void Validate_AcceptsOneMinuteOrMore(int minutes)
        {
            Assert.Null(IdleLockRules.Validate(minutes));
        }

        [Theory]
        [InlineData("1", 1)]
        [InlineData("15", 15)]
        [InlineData(" 30 ", 30)]
        public void EffectiveMinutes_UsesValidStoredValue(string stored, int expected)
        {
            Assert.Equal(expected, IdleLockRules.EffectiveMinutes(stored, IdleLockRules.DefaultMinutes));
        }

        // قاعدة قائمة حُفظ فيها صفر قبل هذا الإصلاح: المؤقّت يبقى على مدّته ولا يقفل فوراً.
        [Theory]
        [InlineData("0")]
        [InlineData("-5")]
        [InlineData("")]
        [InlineData("abc")]
        [InlineData(null)]
        public void EffectiveMinutes_KeepsCurrentWhenStoredValueIsInvalid(string? stored)
        {
            Assert.Equal(7, IdleLockRules.EffectiveMinutes(stored, 7));
        }

        [Fact]
        public void DefaultMinutes_IsItselfValid()
        {
            Assert.Null(IdleLockRules.Validate(IdleLockRules.DefaultMinutes));
        }
    }
}
