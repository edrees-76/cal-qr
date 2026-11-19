using Xunit;
using CAL_QR.Validation;

namespace CAL_QR.Tests
{
    public class RadiationSourceRulesTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Warning_SimplifiedWithEmptySource_WarnsAndShowsReferenceSentence(string? source)
        {
            string? warning = RadiationSourceRules.Warning(true, true, source);

            Assert.NotNull(warning);
            Assert.Contains(RadiationSourceRules.ReferenceSentence, warning);
        }

        [Fact]
        public void Warning_SourceWritten_NoWarning()
        {
            Assert.Null(RadiationSourceRules.Warning(true, true,
                "The calibration was performed using a Co-60 reference gamma source."));
        }

        [Fact]
        public void Warning_NotSimplifiedLayout_NoWarning()
        {
            Assert.Null(RadiationSourceRules.Warning(false, true, null));
        }

        [Fact]
        public void Warning_MethodologyHidden_NoWarning()
        {
            // قسم المنهجية لا يُطبع أصلاً حين يكون مخفياً، فلا صفّ ناقص يُحذَّر منه.
            Assert.Null(RadiationSourceRules.Warning(true, false, null));
        }
    }
}
