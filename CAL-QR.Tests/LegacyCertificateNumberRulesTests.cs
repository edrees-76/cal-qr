using Xunit;
using CAL_QR.Validation;

namespace CAL_QR.Tests
{
    /// <summary>
    /// حقل الرقم القديم: الصيغة الرسميّة محجوزة للنظام، ورقم شهادة موجودة لا يُكتب يدويّاً،
    /// والقيمة المخزَّنة سابقاً لا تُرفض ما دامت لم تتغيّر.
    /// </summary>
    public class LegacyCertificateNumberRulesTests
    {
        [Theory]
        [InlineData("TNRC-SSDL-2026-0001")]
        [InlineData("TNRC-SSDL-2027-0042")]
        [InlineData("tnrc-ssdl-2026-0001")]
        [InlineData("  TNRC-SSDL-2026-0001  ")]
        [InlineData("TNRC-SSDL-2026-12345")]
        public void ReservedFormat_IsRecognised(string number)
        {
            Assert.True(LegacyCertificateNumberRules.IsReservedFormat(number));
        }

        [Theory]
        [InlineData("SSDL/2019/117")]
        [InlineData("TNRC-2019-117")]
        [InlineData("TNRC-SSDL-26-0001")]
        [InlineData("TNRC-SSDL-2026")]
        [InlineData("TNRC-SSDL-2026-00A1")]
        [InlineData("X-TNRC-SSDL-2026-0001")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void OtherNumbers_AreNotReserved(string? number)
        {
            Assert.False(LegacyCertificateNumberRules.IsReservedFormat(number));
        }

        [Fact]
        public void TypingReservedFormat_IsRejected()
        {
            Assert.Equal(LegacyCertificateNumberRules.ReservedFormatMessage,
                LegacyCertificateNumberRules.Validate("SSDL/2019/117", "TNRC-SSDL-2026-0042", false));
        }

        [Fact]
        public void TypingNumberOfExistingCertificate_IsRejected()
        {
            Assert.Equal(LegacyCertificateNumberRules.ExistingCertificateMessage,
                LegacyCertificateNumberRules.Validate("SSDL/2019/117", "IMPORTED-77", true));
        }

        [Fact]
        public void PaperNumber_IsAccepted()
        {
            Assert.Null(LegacyCertificateNumberRules.Validate("SSDL/2019/117", "SSDL/2019/118", false));
        }

        [Theory]
        [InlineData("TNRC-SSDL-2026-0005", "TNRC-SSDL-2026-0005", true)]   // رقم شهادة مُبطَلة منسوخ: لم يتغيّر
        [InlineData("TNRC-SSDL-2026-0005", "  TNRC-SSDL-2026-0005 ", true)]
        [InlineData("SSDL/2019/117", "SSDL/2019/117", true)]
        public void UnchangedStoredValue_IsAccepted(string original, string typed, bool belongs)
        {
            Assert.Null(LegacyCertificateNumberRules.Validate(original, typed, belongs));
        }

        [Theory]
        [InlineData("SSDL/2019/117", "")]
        [InlineData("SSDL/2019/117", "   ")]
        [InlineData("", null)]
        public void EmptyValue_IsLeftToOtherChecks(string original, string? typed)
        {
            Assert.Null(LegacyCertificateNumberRules.Validate(original, typed, false));
        }

        [Fact]
        public void ChangingOneReservedNumberToAnother_IsRejected()
        {
            Assert.Equal(LegacyCertificateNumberRules.ReservedFormatMessage,
                LegacyCertificateNumberRules.Validate("TNRC-SSDL-2026-0005", "TNRC-SSDL-2026-0042", false));
        }
    }
}
