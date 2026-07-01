using System.Collections.Generic;

namespace CAL_QR.Services
{
    public interface IPrintService
    {
        IEnumerable<string> GetAvailablePrinters();
        void PrintQrLabel(QrPrintJob job);
        void PrintMultipleQrLabels(IEnumerable<QrPrintJob> jobs);
    }
}
