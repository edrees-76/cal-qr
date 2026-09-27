using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Data;
using CAL_QR.Models;

namespace CAL_QR.Services
{
    /// <summary>
    /// كلمة سرّ النسخ الاحتياطيّ على هذا الجهاز. تُحفظ لأنّ النسخ المجدول يعمل بلا أحد
    /// يكتبها وقتها.
    /// </summary>
    public interface IBackupPasswordStore
    {
        /// <summary>الكلمة المحفوظة، أو null إن لم تُضبط. تعذّر قراءتها ⇒ استثناء لا null.</summary>
        Task<string?> GetPasswordAsync();

        Task SetPasswordAsync(string password);

        Task<bool> IsSetAsync();
    }

    /// <summary>
    /// تُحفظ الكلمة في AppSettings مقفلة بـDPAPI على مستوى الجهاز (LocalMachine):
    /// القيمة المخزّنة لا تُفكّ إلّا على هذا الحاسوب. فنسخة من قاعدة البيانات على
    /// جهاز آخر لا تكشف كلمة السرّ، والنسخة المشفَّرة تُفتح هناك بكتابتها يدوياً.
    ///
    /// LocalMachine لا CurrentUser: النسخ المجدول يجب أن يعمل أيّاً كان مستخدم ويندوز
    /// المسجَّل، والقاعدة نفسها مقروءة لكلّ مستخدمي الجهاز أصلاً.
    /// </summary>
    public class DpapiBackupPasswordStore : IBackupPasswordStore
    {
        public const string SettingKey = "BackupPasswordProtected";

        // إنتروبيا ثابتة تربط القيمة بهذا الغرض: قيمة DPAPI منسوخة من برنامج آخر
        // على الجهاز نفسه لا تُقبل هنا.
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("CAL-QR backup password v1");

        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;

        public DpapiBackupPasswordStore(IDbContextFactory<CalQrDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<bool> IsSetAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var setting = await context.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == SettingKey);
            return !string.IsNullOrWhiteSpace(setting?.Value);
        }

        public async Task<string?> GetPasswordAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var setting = await context.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == SettingKey);
            string? stored = setting?.Value;
            if (string.IsNullOrWhiteSpace(stored))
            {
                return null;
            }

            try
            {
                byte[] protectedBytes = Convert.FromBase64String(stored);
                byte[] plain = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.LocalMachine);
                return Encoding.UTF8.GetString(plain);
            }
            catch (Exception ex) when (ex is CryptographicException || ex is FormatException)
            {
                throw new InvalidOperationException(
                    "كلمة سرّ النسخ الاحتياطيّ المحفوظة لا تُقرأ على هذا الجهاز. أعد ضبطها من «الإعدادات».", ex);
            }
        }

        public async Task SetPasswordAsync(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentException("كلمة السرّ فارغة.", nameof(password));
            }

            byte[] protectedBytes = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(password), Entropy, DataProtectionScope.LocalMachine);
            string value = Convert.ToBase64String(protectedBytes);

            using var context = await _contextFactory.CreateDbContextAsync();
            var setting = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == SettingKey);
            if (setting == null)
            {
                context.AppSettings.Add(new AppSetting { Key = SettingKey, Value = value });
            }
            else
            {
                setting.Value = value;
            }

            await context.SaveChangesAsync();
        }
    }
}
