using Xunit;
using CAL_QR.Enums;
using CAL_QR.Models;
using CAL_QR.Services;

namespace CAL_QR.Tests
{
    /// <summary>
    /// دالّة نقية، فلا حاجة لقاعدة بيانات هنا.
    /// </summary>
    public class CalibrationLabelPolicyTests
    {
        [Fact]
        public void NullCertificate_ReturnsNoCertificateReason()
        {
            Assert.Equal(
                CalibrationLabelPolicy.NoCertificateReason,
                CalibrationLabelPolicy.GetBlockReason(null));
        }

        [Fact]
        public void StatusReportCertificate_ReturnsStatusReportReason()
        {
            var certificate = new Certificate
            {
                DocumentType = CertificateDocumentType.CalibrationStatusReport
            };

            Assert.Equal(
                CalibrationLabelPolicy.StatusReportReason,
                CalibrationLabelPolicy.GetBlockReason(certificate));
        }

        [Fact]
        public void CalibrationCertificate_ReturnsNull()
        {
            var certificate = new Certificate
            {
                DocumentType = CertificateDocumentType.CalibrationCertificate
            };

            Assert.Null(CalibrationLabelPolicy.GetBlockReason(certificate));
        }
    }
}
