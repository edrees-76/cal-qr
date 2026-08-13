using System.Collections.Generic;
using CAL_QR.Models;

namespace CAL_QR.Services
{
    public class QrPrintJob
    {
        // ── تخطيط الورق (بلا تغيير) ──
        public PaperTemplate? Template { get; set; }
        public string PrinterName { get; set; } = string.Empty;
        public int StartColumn { get; set; }
        public int StartRow { get; set; }

        // ── هويّة الشهادة والجهاز (نصّ الملصق الجديد) ──
        public string CertificateNumber { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string CalibrationDate { get; set; } = string.Empty;
        public string ExpiryDate { get; set; } = string.Empty;

        // ── كود التحقّق: من Certificates.VerifyCode لا HmacSignature (يُملأ في الالتزام ٢) ──
        public string VerifyCode { get; set; } = string.Empty;

        // ── سطور CFavg لكلّ نويدة، جاهزة للطباعة (مثل "Co-60 = 1.02"). قد تكون فارغة للمبسّطة. ──
        public List<string> NuclideLines { get; set; } = new List<string>();
    }
}
