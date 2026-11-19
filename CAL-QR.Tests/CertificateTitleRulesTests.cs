using Xunit;
using CAL_QR.Validation;

namespace CAL_QR.Tests
{
    public class CertificateTitleRulesTests
    {
        [Fact]
        public void Subject_ModelAlreadyNamesType_PrintsModelOnly()
        {
            // قالب G501: حقل الموديل فيه مسافة مزدوجة، والعنوان بلا تكرار لاسم النوع.
            Assert.Equal("Gamma Scintillation Probe (Model 44-2)",
                CertificateTitleRules.Subject("  Gamma Scintillation  Probe (Model 44-2) ", "Gamma Scintillation Probe"));
        }

        [Fact]
        public void Subject_ModelNamesType_IgnoringCase()
        {
            // قالب Teletector: «TELETECTOR» بأحرف كبيرة في الموديل.
            Assert.Equal("TELETECTOR Gamma Probe (Model 6150 AD-T)",
                CertificateTitleRules.Subject("TELETECTOR Gamma Probe (Model 6150 AD-T)", "Teletector Gamma Probe"));
        }

        [Fact]
        public void Subject_ShortModelCode_IsPrefixedWithType()
        {
            Assert.Equal("Dose Rate Meter (DRM-620)", CertificateTitleRules.Subject("DRM-620", "Dose Rate Meter"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Subject_FallsBackToTemplateType_WhenModelBlank(string? model)
        {
            Assert.Equal("Dose Rate Meter", CertificateTitleRules.Subject(model, "Dose Rate Meter"));
        }

        [Fact]
        public void Subject_UsesModel_WhenTypeBlank()
        {
            Assert.Equal("DRM-620", CertificateTitleRules.Subject("DRM-620", " "));
        }

        [Fact]
        public void Subject_IsNull_WhenBothBlank()
        {
            Assert.Null(CertificateTitleRules.Subject(" ", null));
        }
    }
}
