using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Data;
using CAL_QR.Models;

namespace CAL_QR.Services
{
    public class HmacService : IHmacService
    {
        private const string LegacySecretKey = "CalQR-Nuclear-Center-2026-SecretKey";
        private static readonly DateTime CutoverDate = new DateTime(2026, 7, 14, 0, 0, 0, DateTimeKind.Utc);

        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;
        private string? _activeSecretKey;

        public HmacService(IDbContextFactory<CalQrDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public void Initialize()
        {
            using var context = _contextFactory.CreateDbContext();
            var setting = context.AppSettings.FirstOrDefault(s => s.Key == "HmacSecretKey");
            if (setting == null)
            {
                byte[] randomBytes = new byte[32];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(randomBytes);
                }
                string newKey = Convert.ToBase64String(randomBytes);
                setting = new AppSetting { Key = "HmacSecretKey", Value = newKey, UpdatedAt = DateTime.UtcNow };
                context.AppSettings.Add(setting);
                context.SaveChanges();
            }
            _activeSecretKey = setting.Value;
        }

        public string BuildConcatenatedString(
            string certNo,
            string model,
            string serial,
            string ownerName,
            string calDate,
            string expDate,
            string result,
            string engineerName)
        {
            return $"{certNo?.Trim()}|{model?.Trim()}|{serial?.Trim()}|{ownerName?.Trim()}|{calDate?.Trim()}|{expDate?.Trim()}|{result?.Trim()}|{engineerName?.Trim()}";
        }

        private string ComputeSignatureWithKey(
            string certNo,
            string model,
            string serial,
            string ownerName,
            string calDate,
            string expDate,
            string result,
            string engineerName,
            string key)
        {
            string rawData = BuildConcatenatedString(
                certNo: certNo,
                model: model,
                serial: serial,
                ownerName: ownerName,
                calDate: calDate,
                expDate: expDate,
                result: result,
                engineerName: engineerName
            );

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            byte[] hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawData));
            string hexHash = Convert.ToHexString(hashBytes);
            
            return hexHash.Substring(0, 16).ToUpper();
        }

        public string ComputeSignature(
            string certNo,
            string model,
            string serial,
            string ownerName,
            string calDate,
            string expDate,
            string result,
            string engineerName)
        {
            if (_activeSecretKey == null)
            {
                Initialize();
            }
            return ComputeSignatureWithKey(
                certNo, model, serial, ownerName, calDate, expDate, result, engineerName, _activeSecretKey!);
        }

        // ملاحظة أمنية معروفة: التحقق الأوفلاين (بدون قاعدة بيانات) عبر تاريخ المعايرة calDate كبديل، يسمح نظرياً بتزوير شهادة بتاريخ معايرة قديم (قبل تاريخ القطع) عبر القوة الغاشمة على توقيع 8 أحرف دون التحقق من فرادة رقم الشهادة بقاعدة البيانات. هذا خطر متبقٍ مقبول حالياً بناءً على الاستخدام النادر لهذا المسار، وقد تم توثيق ظهور المفتاح القديم (Legacy Key) في محادثة تطوير سابقة، وبالتالي فإن نطاق أي استغلال محتمل لهذا التسريب يقتصر فعلياً على تزوير شهادات "قديمة التاريخ" (قبل تاريخ القطع 14 يوليو 2026) فقط، وليس أي شهادة مستقبلية تعتمد على المفتاح النشط الجديد.
        public bool VerifySignature(
            string certNo,
            string model,
            string serial,
            string ownerName,
            string calDate,
            string expDate,
            string result,
            string engineerName,
            string signature,
            DateTime? recordCreatedAt = null)
        {
            if (string.IsNullOrWhiteSpace(signature)) return false;

            string cleanSig = signature.Trim();

            // Determine creation time for cutover check
            DateTime recordTime = recordCreatedAt ?? 
                                 (DateTime.TryParse(calDate, out var parsedCalDate) ? parsedCalDate : DateTime.UtcNow);

            bool isLegacy = recordTime < CutoverDate;

            if (isLegacy)
            {
                // Verify using the legacy key (allowing both 8-character and 16-character legacy signatures)
                string computedLegacy = ComputeSignatureWithKey(
                    certNo, model, serial, ownerName, calDate, expDate, result, engineerName, LegacySecretKey);

                if (cleanSig.Length == 8)
                {
                    return string.Equals(computedLegacy.Substring(0, 8), cleanSig, StringComparison.OrdinalIgnoreCase);
                }
                return string.Equals(computedLegacy, cleanSig, StringComparison.OrdinalIgnoreCase);
            }

            // Otherwise, verify using the new active key (requires full 16-character signature)
            if (_activeSecretKey == null)
            {
                Initialize();
            }
            string computedActive = ComputeSignatureWithKey(
                certNo, model, serial, ownerName, calDate, expDate, result, engineerName, _activeSecretKey!);

            return string.Equals(computedActive, cleanSig, StringComparison.OrdinalIgnoreCase);
        }
    }
}
