using System;
using System.Drawing;
using System.IO;
using System.Windows.Media.Imaging;
using QRCoder;

namespace CAL_QR.Services
{
    public class QrService : IQrService
    {
        public BitmapSource GenerateQrCodeImage(string content, int sizePx)
        {
            byte[] pngBytes = GenerateQrCodePngBytes(content, sizePx);
            return ConvertToBitmapSource(pngBytes);
        }

        public byte[] GenerateQrCodePngBytes(string content, int sizePx)
        {
            // الشعار يُنسخ مع مخرجات البناء، فمسار BaseDirectory هو الصحيح الوحيد.
            // كان هنا احتياطيّ بمسار جهاز المطوّر — يعني أنّ انكسار نسخ Assets كان
            // سيمرّ سليمًا على جهاز واحد ويُنتج رموزًا بلا شعار بصمت على غيره.
            // غياب الشعار يُحتمَل أدناه أصلًا.
            string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Logo", "nuclear-center-logo.png");

            Bitmap? logoImage = null;
            if (File.Exists(logoPath))
            {
                try
                {
                    logoImage = new Bitmap(logoPath);
                }
                catch
                {
                    // Ignore logo load error
                }
            }

            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.H);
            using var qrCode = new QRCode(qrCodeData);

            int pixelsPerModule = Math.Max(1, sizePx / 33);

            using Bitmap qrBitmap = qrCode.GetGraphic(
                pixelsPerModule: pixelsPerModule,
                darkColor: Color.Black,
                lightColor: Color.White,
                icon: logoImage,
                iconSizePercent: 15
            );

            using Bitmap resizedBitmap = new Bitmap(qrBitmap, new Size(sizePx, sizePx));

            logoImage?.Dispose();

            using var ms = new MemoryStream();
            resizedBitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            return ms.ToArray();
        }

        public string GenerateVerificationText(
            string ownerName,
            string deviceType,
            string model,
            string serial,
            string certNo,
            string calDate,
            string expDate,
            string engineerName,
            string description,
            string result,
            string verifyCode)
        {
            string resultStr = result switch
            {
                "Passed" => "✅ ناجح Passed",
                "Failed" => "❌ راسب Failed",
                "Conditional" => "⚠️ مشروط Conditional",
                _ => result
            };

            return "=== شهادة معايرة ===\n" +
                   $"الجهة: {ownerName?.Trim()}\n" +
                   $"النوع / Type: {deviceType?.Trim()}\n" +
                   $"الموديل / Model: {model?.Trim()}\n" +
                   $"الرقم التسلسلي / S/N: {serial?.Trim()}\n" +
                   $"رقم الشهادة: {certNo?.Trim()}\n" +
                   $"تاريخ المعايرة: {calDate?.Trim()}\n" +
                   $"تاريخ انتهاء المعايرة / Exp. Date: {expDate?.Trim()}\n" +
                   $"المهندس: {engineerName?.Trim()}\n" +
                   $"النتيجة / Result: {resultStr}\n" +
                   $"كود التحقق / Verify Code: {verifyCode?.Trim()}\n" +
                   "─────────────────────────────────\n" +
                   "الجهة المعايِرة / Calibrated by:\n" +
                   "مركز البحوث النووية\n" +
                   "إدارة الوقاية من الاشعاع\n" +
                   "قسم قياس وتقدير الجرعات الشخصية والمعايرة\n" +
                   "وحدة المعايرة";
        }

        private BitmapSource ConvertToBitmapSource(byte[] pngBytes)
        {
            using var memory = new MemoryStream(pngBytes);
            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = memory;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();
            bitmapImage.Freeze();
            return bitmapImage;
        }
    }
}
