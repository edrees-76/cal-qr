using CAL_QR.Enums;
using CAL_QR.Models;

namespace CAL_QR.Services
{
    // الملصق هو المخرج الوحيد الذي يخرج ملتصقًا بالجهاز؛ من يقرؤه في الميدان
    // لا يملك الورقة ولا البرنامج، فلا يجوز أن يطبع ما لا تسنده شهادة معايرة.
    public static class CalibrationLabelPolicy
    {
        public const string NoCertificateReason = "لم تصدر لهذا السجلّ شهادة بعد، فلا ملصق له.";
        public const string StatusReportReason = "صدر لهذا السجلّ تقرير حالة، والجهاز لم يُعاير، فلا ملصق معايرة له.";

        public static string? GetBlockReason(Certificate? certificate)
        {
            if (certificate == null)
                return NoCertificateReason;

            if (certificate.DocumentType == CertificateDocumentType.CalibrationStatusReport)
                return StatusReportReason;

            return null;
        }
    }
}
