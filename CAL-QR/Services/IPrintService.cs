using System.Collections.Generic;
using System.Windows.Media.Imaging;

namespace CAL_QR.Services
{
    public interface IPrintService
    {
        IEnumerable<string> GetAvailablePrinters();
        void PrintQrLabel(QrPrintJob job);
        void PrintMultipleQrLabels(IEnumerable<QrPrintJob> jobs);
        BitmapSource RenderLabelPreview(QrPrintJob job, Models.PaperTemplate template);
    }
}
