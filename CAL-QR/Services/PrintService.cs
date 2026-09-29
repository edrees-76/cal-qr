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
            if (job.Template == null) return;

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
                    double xMm = (double)template.MarginLeftMm + (job.StartColumn - 1) * ((double)template.LabelWidthMm + (double)template.HorizontalGapMm);
                    double yMm = (double)template.MarginTopMm + (job.StartRow - 1) * ((double)template.LabelHeightMm + (double)template.VerticalGapMm);

                    double xPx = xMm * MmToPx;
                    double yPx = yMm * MmToPx;

                    DrawSingleLabel(dc, job, template, xPx, yPx);
                }
            }
            return visual;
        }

        private void DrawSingleLabel(DrawingContext dc, QrPrintJob job, Models.PaperTemplate template, double xPx, double yPx)
        {
            double widthPx = (double)template.LabelWidthMm * MmToPx;
            double heightPx = (double)template.LabelHeightMm * MmToPx;

            var typefaceBold = new Typeface(new FontFamily("Cairo"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
            var typefaceRegular = new Typeface(new FontFamily("Cairo"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            var brushDark = Brushes.Black;
            var brushBody = Brushes.DarkSlateGray;

            double padPx = Math.Max(4, Math.Min(widthPx, heightPx) * 0.06);
            double innerWidth = widthPx - padPx * 2;
            double bodyFontSize = Math.Clamp(heightPx / 16.0, 6, 11);
            double titleFontSize = Math.Clamp(bodyFontSize + 3, bodyFontSize, 16);

            double cursorX = xPx + padPx;
            double cursorY = yPx + padPx;

            // ── العنوان: رقم الشهادة (عريض، مضمون دائمًا) ──
            var certText = new FormattedText(
                job.CertificateNumber ?? string.Empty,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, typefaceBold, titleFontSize, brushDark, 96.0)
            { MaxTextWidth = innerWidth };
            dc.DrawText(certText, new Point(cursorX, cursorY));
            cursorY += certText.Height + padPx * 0.5;

            // ── كود التحقّق: مضمون دائمًا، يُحجز له مكانه أسفل الملصق مسبقًا ──
            FormattedText? verifyText = null;
            double verifyBlockHeight = 0;
            if (!string.IsNullOrWhiteSpace(job.VerifyCode))
            {
                verifyText = new FormattedText(
                    $"Verify: {job.VerifyCode}",
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, typefaceBold, bodyFontSize, brushDark, 96.0)
                { MaxTextWidth = innerWidth };
                verifyBlockHeight = verifyText.Height + padPx * 0.5;
            }

            double bodyBottomY = yPx + heightPx - padPx - verifyBlockHeight;

            // احتياط: ملصق ضيّق جدًّا لا يتّسع لأيّ سطر جسد — اكتفِ بالعنوان وVerify.
            if (innerWidth <= 0 || bodyBottomY <= cursorY)
            {
                if (verifyText != null)
                {
                    dc.DrawText(verifyText, new Point(cursorX, yPx + heightPx - padPx - verifyText.Height));
                }
                return;
            }

            // يرسم السطر فقط إن اتّسع كاملًا ضمن المساحة المتبقية؛ بلا قصّ صامت.
            bool TryDraw(string text, Typeface tf, Brush brush)
            {
                var ft = new FormattedText(
                    text,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, tf, bodyFontSize, brush, 96.0)
                { MaxTextWidth = innerWidth };
                if (cursorY + ft.Height > bodyBottomY) return false;
                dc.DrawText(ft, new Point(cursorX, cursorY));
                cursorY += ft.Height;
                return true;
            }

            // ── سلّم الأولويّة الاختياريّ ──
            if (!string.IsNullOrWhiteSpace(job.SerialNumber))
                TryDraw($"S/N: {job.SerialNumber}", typefaceRegular, brushBody);

            var calDueParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(job.CalibrationDate)) calDueParts.Add($"Cal: {job.CalibrationDate}");
            if (!string.IsNullOrWhiteSpace(job.ExpiryDate)) calDueParts.Add($"Due: {job.ExpiryDate}");
            if (calDueParts.Count > 0)
                TryDraw(string.Join(" | ", calDueParts), typefaceRegular, brushBody);

            var cfLines = job.NuclideLines?.Where(n => !string.IsNullOrWhiteSpace(n)).ToList() ?? new List<string>();
            if (cfLines.Count > 0)
            {
                string fullCfText = string.Join("\n", cfLines.Select(l => $"CF {l}"));
                if (!TryDraw(fullCfText, typefaceRegular, brushBody))
                    TryDraw("CF: see certificate", typefaceRegular, brushBody);
            }

            if (!string.IsNullOrWhiteSpace(job.Model))
                TryDraw(job.Model, typefaceRegular, brushBody);

            if (!string.IsNullOrWhiteSpace(job.DeviceType))
                TryDraw(job.DeviceType, typefaceRegular, brushBody);

            // ── رسم كود التحقّق أسفل الملصق (بعد أن حُجز له مكانه) ──
            if (verifyText != null)
            {
                dc.DrawText(verifyText, new Point(cursorX, yPx + heightPx - padPx - verifyText.Height));
            }
        }

        public BitmapSource RenderLabelPreview(QrPrintJob job, Models.PaperTemplate template)
        {
            double widthPx = (double)template.LabelWidthMm * MmToPx;
            double heightPx = (double)template.LabelHeightMm * MmToPx;

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, widthPx, heightPx));
                DrawSingleLabel(dc, job, template, 0, 0);
            }

            int pxW = (int)Math.Ceiling(widthPx);
            int pxH = (int)Math.Ceiling(heightPx);
            var rtb = new RenderTargetBitmap(pxW, pxH, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);
            rtb.Freeze();
            return rtb;
        }
    }
}
