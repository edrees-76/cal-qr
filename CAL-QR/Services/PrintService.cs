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

                var typefaceBold = new Typeface(new FontFamily("Cairo"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
                var typefaceRegular = new Typeface(new FontFamily("Cairo"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
                var brushDark = Brushes.Black;
                var brushBody = Brushes.DarkSlateGray;

                foreach (var job in jobs)
                {
                    double xMm = (double)template.MarginLeftMm + (job.StartColumn - 1) * ((double)template.LabelWidthMm + (double)template.HorizontalGapMm);
                    double yMm = (double)template.MarginTopMm + (job.StartRow - 1) * ((double)template.LabelHeightMm + (double)template.VerticalGapMm);

                    double xPx = xMm * MmToPx;
                    double yPx = yMm * MmToPx;
                    double widthPx = (double)template.LabelWidthMm * MmToPx;
                    double heightPx = (double)template.LabelHeightMm * MmToPx;

                    double padPx = 8;
                    double innerWidth = widthPx - padPx * 2;
                    double cursorX = xPx + padPx;
                    double cursorY = yPx + padPx;

                    // ── العنوان: رقم الشهادة (عريض) ──
                    var certText = new FormattedText(
                        job.CertificateNumber,
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight, typefaceBold, 11, brushDark, 96.0)
                    { MaxTextWidth = innerWidth };
                    dc.DrawText(certText, new Point(cursorX, cursorY));
                    cursorY += certText.Height + 4;

                    // ── جسد الحقول: تسمية + قيمة، سطرًا سطرًا ──
                    var bodyLines = new List<string>();
                    if (!string.IsNullOrWhiteSpace(job.ClientName))    bodyLines.Add($"Client: {job.ClientName}");
                    if (!string.IsNullOrWhiteSpace(job.DeviceType))    bodyLines.Add($"Type: {job.DeviceType}");
                    if (!string.IsNullOrWhiteSpace(job.Model))         bodyLines.Add($"Model: {job.Model}");
                    if (!string.IsNullOrWhiteSpace(job.SerialNumber))  bodyLines.Add($"S/N: {job.SerialNumber}");
                    if (!string.IsNullOrWhiteSpace(job.CalibrationDate)) bodyLines.Add($"Cal. Date: {job.CalibrationDate}");
                    if (!string.IsNullOrWhiteSpace(job.ExpiryDate))    bodyLines.Add($"Due Date: {job.ExpiryDate}");
                    foreach (var nuclide in job.NuclideLines)
                        if (!string.IsNullOrWhiteSpace(nuclide)) bodyLines.Add($"CFavg {nuclide}");

                    if (bodyLines.Count > 0)
                    {
                        var bodyText = new FormattedText(
                            string.Join("\n", bodyLines),
                            System.Globalization.CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight, typefaceRegular, 8, brushBody, 96.0)
                        { MaxTextWidth = innerWidth, MaxTextHeight = heightPx - (cursorY - yPx) - padPx - 14 };
                        dc.DrawText(bodyText, new Point(cursorX, cursorY));
                    }

                    // ── كود التحقّق: أسفل الملصق، عريض ──
                    if (!string.IsNullOrWhiteSpace(job.VerifyCode))
                    {
                        var codeText = new FormattedText(
                            $"Verify: {job.VerifyCode}",
                            System.Globalization.CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight, typefaceBold, 8, brushDark, 96.0)
                        { MaxTextWidth = innerWidth };
                        dc.DrawText(codeText, new Point(cursorX, yPx + heightPx - padPx - codeText.Height));
                    }
                }
            }
            return visual;
        }
    }
}
