using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CAL_QR.Services
{
    public class PrintService : IPrintService
    {
        private const double MmToPx = 3.7795;

        public IEnumerable<string> GetAvailablePrinters()
        {
            try
            {
                return PrinterSettings.InstalledPrinters.Cast<string>();
            }
            catch
            {
                return new[] { "Microsoft Print to PDF" };
            }
        }

        public void PrintQrLabel(QrPrintJob job)
        {
            if (job.Template == null || job.QrImage == null) return;

            var printDialog = new PrintDialog();
            if (!string.IsNullOrWhiteSpace(job.PrinterName))
            {
                try
                {
                    printDialog.PrintQueue = new System.Printing.LocalPrintServer().GetPrintQueue(job.PrinterName);
                }
                catch
                {
                    // Fallback
                }
            }

            var visual = DrawLabelsVisual(new[] { job }, job.Template);
            string description = $"Print QR Label - {job.CertificateNumber}";
            printDialog.PrintVisual(visual, description);
        }

        public void PrintMultipleQrLabels(IEnumerable<QrPrintJob> jobs)
        {
            var jobList = jobs.ToList();
            if (jobList.Count == 0) return;
            
            var firstJob = jobList.First();
            if (firstJob.Template == null) return;

            var sortedJobs = jobList.OrderBy(j => j.CertificateNumber).ToList();

            var printDialog = new PrintDialog();
            if (!string.IsNullOrWhiteSpace(firstJob.PrinterName))
            {
                try
                {
                    printDialog.PrintQueue = new System.Printing.LocalPrintServer().GetPrintQueue(firstJob.PrinterName);
                }
                catch
                {
                    // Fallback
                }
            }

            if (firstJob.Template.PaperType == "Roll")
            {
                foreach (var job in sortedJobs)
                {
                    var visual = DrawLabelsVisual(new[] { job }, firstJob.Template);
                    printDialog.PrintVisual(visual, $"Print QR Label - {job.CertificateNumber}");
                }
            }
            else
            {
                var visual = DrawLabelsVisual(sortedJobs, firstJob.Template);
                printDialog.PrintVisual(visual, "Print QR Labels Sheet");
            }
        }

        private DrawingVisual DrawLabelsVisual(IEnumerable<QrPrintJob> jobs, Models.PaperTemplate template)
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                double paperWidthPx = (double)template.PaperWidthMm * MmToPx;
                double paperHeightPx = (double)template.PaperHeightMm * MmToPx;
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, paperWidthPx, paperHeightPx));

                foreach (var job in jobs)
                {
                    if (job.QrImage == null) continue;

                    double xMm = (double)template.MarginLeftMm + (job.StartColumn - 1) * ((double)template.LabelWidthMm + (double)template.HorizontalGapMm);
                    double yMm = (double)template.MarginTopMm + (job.StartRow - 1) * ((double)template.LabelHeightMm + (double)template.VerticalGapMm);

                    double xPx = xMm * MmToPx;
                    double yPx = yMm * MmToPx;
                    double widthPx = (double)template.LabelWidthMm * MmToPx;
                    double heightPx = (double)template.LabelHeightMm * MmToPx;

                    double qrSizePx = Math.Min(widthPx, heightPx) * 0.85;
                    double qrXPx = xPx + (heightPx - qrSizePx) / 2;
                    double qrYPx = yPx + (heightPx - qrSizePx) / 2;

                    dc.DrawImage(job.QrImage, new Rect(qrXPx, qrYPx, qrSizePx, qrSizePx));

                    if (widthPx > qrSizePx + 50)
                    {
                        double textXPx = qrXPx + qrSizePx + 10;
                        double textYPx = yPx + 10;
                        double textWidth = widthPx - (qrSizePx + 20);

                        var typeface = new Typeface(new FontFamily("Cairo"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
                        
                        var formattedTextCert = new FormattedText(
                            $"Cert: {job.CertificateNumber}",
                            System.Globalization.CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            typeface,
                            10,
                            Brushes.Black,
                            96.0
                        );
                        dc.DrawText(formattedTextCert, new Point(textXPx, textYPx));

                        var typefaceRegular = new Typeface(new FontFamily("Cairo"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
                        var formattedTextInfo = new FormattedText(
                            job.DeviceInfoText,
                            System.Globalization.CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            typefaceRegular,
                            8,
                            Brushes.DarkSlateGray,
                            96.0
                        );
                        formattedTextInfo.MaxTextWidth = textWidth;
                        formattedTextInfo.MaxTextHeight = heightPx - 25;
                        dc.DrawText(formattedTextInfo, new Point(textXPx, textYPx + 15));
                    }
                }
            }
            return visual;
        }
    }
}
