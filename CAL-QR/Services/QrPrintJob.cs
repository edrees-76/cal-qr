using System.Windows.Media.Imaging;
using CAL_QR.Models;

namespace CAL_QR.Services
{
    public class QrPrintJob
    {
        public BitmapSource? QrImage { get; set; }
        public PaperTemplate? Template { get; set; }
        public string PrinterName { get; set; } = string.Empty;
        public int StartColumn { get; set; }
        public int StartRow { get; set; }
        public string CertificateNumber { get; set; } = string.Empty;
        public string DeviceInfoText { get; set; } = string.Empty;
    }
}
