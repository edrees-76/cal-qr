using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using CAL_QR.Helpers;
using CAL_QR.Data;

namespace CAL_QR.Services
{
    public class QrService : IQrService
    {
        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;

        public QrService(IDbContextFactory<CalQrDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }
        public BitmapSource GenerateQrCodeImage(string content, int sizePx)
        {
            string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Logo", "nuclear-center-logo.png");
            if (!File.Exists(logoPath))
            {
                logoPath = Path.Combine("d:\\cal-qr\\CAL-QR", "Assets", "Logo", "nuclear-center-logo.png");
            }

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

            return ConvertToBitmapSource(resizedBitmap);
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

        public void SaveQrCodeImage(string content, string certificateNumber)
        {
            string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "QR");
            try
            {
                using (var context = _contextFactory.CreateDbContext())
                {
                    var setting = context.AppSettings.AsNoTracking().FirstOrDefault(s => s.Key == "QrOutputPath");
                    if (setting != null && !string.IsNullOrWhiteSpace(setting.Value))
                    {
                        folderPath = setting.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[QrService Error] Reading QrOutputPath failed: {ex.Message}");
            }

            FileHelper.EnsureDirectoryExists(folderPath);

            string destPath = Path.Combine(folderPath, $"{certificateNumber.Trim()}.png");

            var bitmapSource = GenerateQrCodeImage(content, 600);

            using var fileStream = new FileStream(destPath, FileMode.Create);
            BitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
            encoder.Save(fileStream);
        }

        public void GenerateAndSaveQrForRecord(
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
            string content = GenerateVerificationText(
                ownerName: ownerName,
                deviceType: deviceType,
                model: model,
                serial: serial,
                certNo: certNo,
                calDate: calDate,
                expDate: expDate,
                engineerName: engineerName,
                description: description,
                result: result,
                verifyCode: verifyCode
            );

            SaveQrCodeImage(content, certNo);
        }

        private BitmapSource ConvertToBitmapSource(Bitmap bitmap)
        {
            using var memory = new MemoryStream();
            bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
            memory.Position = 0;
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
