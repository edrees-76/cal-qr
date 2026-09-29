using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QRCoder;

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

            // ── صورة QR على يمين الملصق ──
            double qrSizePx = 0;
            if (!string.IsNullOrWhiteSpace(job.VerifyCode))
            {
                // حجم QR مربّع: يملأ ارتفاع الملصق مع الحواشي، لكن لا يتجاوز 40% من العرض
                qrSizePx = Math.Min(heightPx - padPx * 2, widthPx * 0.40);
                qrSizePx = Math.Max(qrSizePx, 20);

                try
                {
                    var qrBitmap = GenerateQrBitmapSource(job.VerifyCode, (int)Math.Ceiling(qrSizePx));
                    double qrX = xPx + widthPx - padPx - qrSizePx;
                    double qrY = yPx + (heightPx - qrSizePx) / 2.0;
                    dc.DrawImage(qrBitmap, new Rect(qrX, qrY, qrSizePx, qrSizePx));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PrintService] QR generation failed: {ex.Message}");
                    qrSizePx = 0;
                }
            }

            // منطقة النص على اليسار (مع فراغ فاصل بين النص والـ QR)
            double textGap = qrSizePx > 0 ? padPx : 0;
            double innerWidth = widthPx - padPx * 2 - qrSizePx - textGap;
            double bodyFontSize = Math.Clamp(heightPx / 16.0, 6, 11);
            double titleFontSize = Math.Clamp(bodyFontSize + 3, bodyFontSize, 16);

            double cursorX = xPx + padPx;
            double cursorY = yPx + padPx;

            // ── العنوان: رقم الشهادة (عريض، مضمون دائمًا) ──
            var certText = new FormattedText(
                job.CertificateNumber ?? string.Empty,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, typefaceBold, titleFontSize, brushDark, 96.0)
            { MaxTextWidth = Math.Max(1, innerWidth) };
            dc.DrawText(certText, new Point(cursorX, cursorY));
            cursorY += certText.Height + padPx * 0.5;

            // ── كود التحقّق: يُحجز له مكانه أسفل الملصق مسبقًا (نصّ للقراءة اليدويّة) ──
            FormattedText? verifyText = null;
            double verifyBlockHeight = 0;
            if (!string.IsNullOrWhiteSpace(job.VerifyCode))
            {
                verifyText = new FormattedText(
                    $"Verify: {job.VerifyCode}",
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, typefaceBold, bodyFontSize, brushDark, 96.0)
                { MaxTextWidth = Math.Max(1, innerWidth) };
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
                { MaxTextWidth = Math.Max(1, innerWidth) };
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
                // السطور تحمل تسميتها (CF/CFavg) من مصدرها — لا بادئة ثابتة هنا.
                string fullCfText = string.Join("\n", cfLines);
                if (!TryDraw(fullCfText, typefaceRegular, brushBody))
                    TryDraw($"{job.CorrectionFactorLabel}: see certificate", typefaceRegular, brushBody);
            }

            if (!string.IsNullOrWhiteSpace(job.Model))
                TryDraw(job.Model, typefaceRegular, brushBody);

            if (!string.IsNullOrWhiteSpace(job.DeviceType))
                TryDraw(job.DeviceType, typefaceRegular, brushBody);

            // ── رسم كود التحقّق نصّاً أسفل الملصق (بعد أن حُجز له مكانه) ──
            if (verifyText != null)
            {
                dc.DrawText(verifyText, new Point(cursorX, yPx + heightPx - padPx - verifyText.Height));
            }
        }

        // يولّد صورة QR من النصّ بحجم محدّد (بكسل)، بلا شعار (لاتّساع الملصق الصغير)
        private static BitmapSource GenerateQrBitmapSource(string content, int sizePx)
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.H);
            using var qrCode = new QRCode(qrCodeData);

            int pixelsPerModule = Math.Max(1, sizePx / 33);
            using var qrBitmap = qrCode.GetGraphic(
                pixelsPerModule: pixelsPerModule,
                darkColor: Color.Black,
                lightColor: Color.White,
                icon: null,
                iconSizePercent: 0);

            using var resized = new Bitmap(qrBitmap, new System.Drawing.Size(sizePx, sizePx));
            using var ms = new MemoryStream();
            resized.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;

            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = ms;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();
            bitmapImage.Freeze();
            return bitmapImage;
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
