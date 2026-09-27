using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using CAL_QR.Services;

namespace CAL_QR.Tests
{
    /// <summary>
    /// مخزن كلمة سرّ في الذاكرة لاختبارات BackupService: DPAPI يُختبر وحده في
    /// BackupPasswordStoreTests، وهنا يكفي أن تصل الكلمة إلى الخدمة.
    /// </summary>
    public class TestBackupPasswordStore : IBackupPasswordStore
    {
        public const string DefaultPassword = "test-backup-password";

        public string? Password { get; set; }

        public TestBackupPasswordStore(string? password = DefaultPassword)
        {
            Password = password;
        }

        public Task<string?> GetPasswordAsync() => Task.FromResult(Password);

        public Task SetPasswordAsync(string password)
        {
            Password = password;
            return Task.CompletedTask;
        }

        public Task<bool> IsSetAsync() => Task.FromResult(!string.IsNullOrEmpty(Password));

        /// <summary>يفكّ نسخة .cqbak ويفتح الأرشيف من الذاكرة لفحص محتواه.</summary>
        public static ZipArchive OpenArchive(string encryptedBackupPath, string password = DefaultPassword)
        {
            string tempZip = Path.Combine(Path.GetTempPath(), "CalQrTestArchive_" + System.Guid.NewGuid().ToString("N") + ".zip");
            try
            {
                BackupEncryption.DecryptFile(encryptedBackupPath, tempZip, password);
                var bytes = File.ReadAllBytes(tempZip);
                return new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            }
            finally
            {
                if (File.Exists(tempZip)) File.Delete(tempZip);
            }
        }
    }
}
