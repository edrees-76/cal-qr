using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Repositories;
using CAL_QR.Services;
using CAL_QR.Validation;

namespace CAL_QR.Tests
{
    /// <summary>صيغة الملفّ المشفَّر نفسها (BackupEncryption) بمعزل عن BackupService.</summary>
    public class BackupEncryptionTests : IDisposable
    {
        private const int FastIterations = 10_000;
        private readonly string _dir;

        public BackupEncryptionTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "BackupEncryptionTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private string WritePlain(string name, byte[] content)
        {
            string path = Path.Combine(_dir, name);
            File.WriteAllBytes(path, content);
            return path;
        }

        private static byte[] SampleContent()
        {
            // أكبر من مخزن النسخ (81920) ليعبر أكثر من قطعة، وغير مضاعف لكتلة AES.
            var bytes = new byte[200_003];
            new Random(42).NextBytes(bytes);
            return bytes;
        }

        [Fact]
        public void EncryptThenDecrypt_ReturnsIdenticalBytes()
        {
            byte[] content = SampleContent();
            string plain = WritePlain("plain.bin", content);
            string enc = Path.Combine(_dir, "b.cqbak");
            string dec = Path.Combine(_dir, "dec.bin");

            BackupEncryption.EncryptFile(plain, enc, "correct horse battery", FastIterations);
            BackupEncryption.DecryptFile(enc, dec, "correct horse battery");

            Assert.Equal(content, File.ReadAllBytes(dec));
        }

        [Fact]
        public void EncryptedFile_DoesNotContainThePlaintext()
        {
            byte[] marker = Encoding.UTF8.GetBytes("HmacSecretKey=SUPER-SECRET-MARKER");
            byte[] content = Enumerable.Repeat(marker, 50).SelectMany(b => b).ToArray();
            string plain = WritePlain("plain.bin", content);
            string enc = Path.Combine(_dir, "b.cqbak");

            BackupEncryption.EncryptFile(plain, enc, "correct horse battery", FastIterations);

            string encText = Encoding.UTF8.GetString(File.ReadAllBytes(enc));
            Assert.DoesNotContain("SUPER-SECRET-MARKER", encText);
        }

        [Fact]
        public void WrongPassword_Throws_AndWritesNoDestination()
        {
            string plain = WritePlain("plain.bin", SampleContent());
            string enc = Path.Combine(_dir, "b.cqbak");
            string dec = Path.Combine(_dir, "dec.bin");
            BackupEncryption.EncryptFile(plain, enc, "correct horse battery", FastIterations);

            Assert.Throws<BackupPasswordException>(() => BackupEncryption.DecryptFile(enc, dec, "wrong horse battery"));
            Assert.False(File.Exists(dec));
        }

        [Theory]
        [InlineData(24)]    // داخل الرأس: عدد تكرارات PBKDF2
        [InlineData(100)]   // داخل النصّ المشفَّر
        [InlineData(-1)]    // آخر بايت: رمز المصادقة
        public void AnyModifiedByte_IsRejectedBeforeDecryption(int offset)
        {
            string plain = WritePlain("plain.bin", SampleContent());
            string enc = Path.Combine(_dir, "b.cqbak");
            string dec = Path.Combine(_dir, "dec.bin");
            BackupEncryption.EncryptFile(plain, enc, "correct horse battery", FastIterations);

            byte[] bytes = File.ReadAllBytes(enc);
            int index = offset < 0 ? bytes.Length + offset : offset;
            bytes[index] ^= 0x01;
            File.WriteAllBytes(enc, bytes);

            Assert.Throws<BackupPasswordException>(() => BackupEncryption.DecryptFile(enc, dec, "correct horse battery"));
            Assert.False(File.Exists(dec));
        }

        [Fact]
        public void TruncatedFile_IsRejected()
        {
            string plain = WritePlain("plain.bin", SampleContent());
            string enc = Path.Combine(_dir, "b.cqbak");
            BackupEncryption.EncryptFile(plain, enc, "correct horse battery", FastIterations);

            byte[] bytes = File.ReadAllBytes(enc);
            File.WriteAllBytes(enc, bytes.Take(bytes.Length - 100).ToArray());

            Assert.Throws<BackupPasswordException>(() =>
                BackupEncryption.DecryptFile(enc, Path.Combine(_dir, "dec.bin"), "correct horse battery"));
        }

        [Fact]
        public void IsEncryptedBackup_DistinguishesEncryptedFromPlainZipAndMissing()
        {
            string plainZip = Path.Combine(_dir, "old.zip");
            using (var archive = ZipFile.Open(plainZip, ZipArchiveMode.Create))
            {
                archive.CreateEntry("cal-qr.db");
            }

            string enc = Path.Combine(_dir, "b.cqbak");
            BackupEncryption.EncryptFile(plainZip, enc, "correct horse battery", FastIterations);

            Assert.True(BackupEncryption.IsEncryptedBackup(enc));
            Assert.False(BackupEncryption.IsEncryptedBackup(plainZip));
            Assert.False(BackupEncryption.IsEncryptedBackup(Path.Combine(_dir, "missing.cqbak")));
        }

        [Fact]
        public void EmptyPassword_IsRefusedOnEncrypt()
        {
            string plain = WritePlain("plain.bin", SampleContent());
            Assert.Throws<ArgumentException>(() =>
                BackupEncryption.EncryptFile(plain, Path.Combine(_dir, "b.cqbak"), "", FastIterations));
        }

        [Theory]
        [InlineData(null, null, false)]
        [InlineData("", "", false)]
        [InlineData("short", "short", false)]
        [InlineData(" leading-space-pw", " leading-space-pw", false)]
        [InlineData("long-enough-pw", "long-enough-px", false)]
        [InlineData("long-enough-pw", "long-enough-pw", true)]
        public void BackupPasswordRules_AcceptOnlyLongMatchingTrimmedPasswords(string? password, string? confirmation, bool accepted)
        {
            string? error = BackupPasswordRules.Validate(password, confirmation);
            Assert.Equal(accepted, error == null);
        }
    }

    /// <summary>
    /// حفظ كلمة السرّ بـDPAPI. الاختبارات تعمل على ويندوز (net8.0-windows) كما في CI.
    /// </summary>
    public class BackupPasswordStoreTests
    {
        private static IDbContextFactory<CalQrDbContext> NewFactory()
        {
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase("BackupPasswordStore_" + Guid.NewGuid().ToString("N"))
                .Options;
            return new InMemoryFactory(options);
        }

        [Fact]
        public async Task SetThenGet_RoundTrips_AndStoresNoPlaintext()
        {
            var factory = NewFactory();
            var store = new DpapiBackupPasswordStore(factory);

            Assert.False(await store.IsSetAsync());
            Assert.Null(await store.GetPasswordAsync());

            await store.SetPasswordAsync("my-backup-password-1");

            Assert.True(await store.IsSetAsync());
            Assert.Equal("my-backup-password-1", await store.GetPasswordAsync());

            using var context = factory.CreateDbContext();
            string stored = context.AppSettings.Single(s => s.Key == DpapiBackupPasswordStore.SettingKey).Value;
            Assert.DoesNotContain("my-backup-password-1", stored);
            Assert.DoesNotContain("my-backup-password-1", Encoding.UTF8.GetString(Convert.FromBase64String(stored)));
        }

        [Fact]
        public async Task UnreadableStoredValue_Throws_InsteadOfReturningNull()
        {
            // قيمة لا تُفكّ (قاعدة من جهاز آخر مثلاً): null كانت ستُقرأ «لم تُضبط» بصمت.
            var factory = NewFactory();
            using (var context = factory.CreateDbContext())
            {
                context.AppSettings.Add(new AppSetting
                {
                    Key = DpapiBackupPasswordStore.SettingKey,
                    Value = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
                });
                await context.SaveChangesAsync();
            }

            var store = new DpapiBackupPasswordStore(factory);
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.GetPasswordAsync());
        }

        private class InMemoryFactory : IDbContextFactory<CalQrDbContext>
        {
            private readonly DbContextOptions<CalQrDbContext> _options;
            public InMemoryFactory(DbContextOptions<CalQrDbContext> options) => _options = options;
            public CalQrDbContext CreateDbContext() => new CalQrDbContext(_options);
        }
    }

    /// <summary>سلوك BackupService مع القفل: لا نسخة مكشوفة، وكلمة خاطئة لا تمسّ النظام.</summary>
    public class BackupServiceEncryptionTests : IDisposable
    {
        private readonly string _dir;
        private readonly string _dbPath;
        private readonly string _backupFolder;
        private readonly DbContextOptions<CalQrDbContext> _options;
        private readonly TestDbContextFactory _factory;

        public BackupServiceEncryptionTests()
        {
            _dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BackupEncryptionService_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _dbPath = Path.Combine(_dir, "test-active.db");
            _backupFolder = Path.Combine(_dir, "Backups");
            Directory.CreateDirectory(_backupFolder);

            string attachments = Path.Combine(_dir, "Attachments");
            Directory.CreateDirectory(attachments);
            File.WriteAllText(Path.Combine(attachments, "att.txt"), "attachment");
            string qr = Path.Combine(_dir, "QR_Output");
            Directory.CreateDirectory(qr);

            _options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseSqlite($"Data Source={_dbPath}")
                .Options;
            _factory = new TestDbContextFactory(_options);

            using var context = new CalQrDbContext(_options);
            context.Database.EnsureCreated();
            context.AppSettings.Add(new AppSetting { Key = "AttachmentsPath", Value = attachments });
            context.AppSettings.Add(new AppSetting { Key = "QrOutputPath", Value = qr });
            context.SaveChanges();
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private BackupService NewService(TestBackupPasswordStore store) =>
            new BackupService(_factory, new AuditLogRepository(_factory, new TestCurrentUserService()), store);

        private void AddMarker(string key)
        {
            using var context = new CalQrDbContext(_options);
            context.AppSettings.Add(new AppSetting { Key = key, Value = "1" });
            context.SaveChanges();
        }

        private bool HasSetting(string key)
        {
            using var context = new CalQrDbContext(_options);
            return context.AppSettings.Any(s => s.Key == key);
        }

        private string? SettingValue(string key)
        {
            using var context = new CalQrDbContext(_options);
            return context.AppSettings.FirstOrDefault(s => s.Key == key)?.Value;
        }

        [Fact]
        public async Task BackupNow_WithoutPassword_RefusesAndWritesNothing()
        {
            var service = NewService(new TestBackupPasswordStore(password: null));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.BackupNowAsync(_backupFolder));

            Assert.Equal(BackupService.NoPasswordMessage, ex.Message);
            Assert.Empty(Directory.GetFiles(_backupFolder));
        }

        [Fact]
        public async Task BackupNow_WritesOnlyAnEncryptedFile()
        {
            var service = NewService(new TestBackupPasswordStore());

            await service.BackupNowAsync(_backupFolder);

            string file = Assert.Single(Directory.GetFiles(_backupFolder));
            Assert.EndsWith(BackupEncryption.FileExtension, file);
            Assert.True(BackupEncryption.IsEncryptedBackup(file));
            Assert.Throws<InvalidDataException>(() => ZipFile.OpenRead(file).Dispose());
        }

        [Fact]
        public async Task Restore_WithWrongPassword_FailsAndLeavesLiveDataUntouched()
        {
            var store = new TestBackupPasswordStore();
            var service = NewService(store);
            await service.BackupNowAsync(_backupFolder);
            string backup = Assert.Single(Directory.GetFiles(_backupFolder));

            AddMarker("MarkerAddedAfterBackup");

            await Assert.ThrowsAsync<BackupPasswordException>(() => service.RestoreAsync(backup, "not-the-password"));

            // الكلمة المكتوبة تُقدَّم على المحفوظة، والفشل لم يمسّ القاعدة الحيّة.
            Assert.True(HasSetting("MarkerAddedAfterBackup"));
        }

        [Fact]
        public async Task Restore_OnNewMachine_WithTypedPassword_Succeeds_AndDropsTheOldMachineProtectedPassword()
        {
            // الجهاز القديم: كلمة محفوظة (قيمة DPAPI مزيّفة تمثّلها) ونسخة مقفلة.
            using (var context = new CalQrDbContext(_options))
            {
                context.AppSettings.Add(new AppSetting { Key = DpapiBackupPasswordStore.SettingKey, Value = "old-machine-blob" });
                context.SaveChanges();
            }
            await NewService(new TestBackupPasswordStore()).BackupNowAsync(_backupFolder);
            string backup = Assert.Single(Directory.GetFiles(_backupFolder));

            // الجهاز الجديد: لا كلمة مضبوطة بعد.
            using (var context = new CalQrDbContext(_options))
            {
                context.AppSettings.Remove(context.AppSettings.Single(s => s.Key == DpapiBackupPasswordStore.SettingKey));
                context.SaveChanges();
            }
            AddMarker("MarkerAddedAfterBackup");

            var newMachine = NewService(new TestBackupPasswordStore(password: null));
            await newMachine.RestoreAsync(backup, TestBackupPasswordStore.DefaultPassword);

            Assert.False(HasSetting("MarkerAddedAfterBackup"));
            Assert.False(HasSetting(DpapiBackupPasswordStore.SettingKey));
        }

        [Fact]
        public async Task Restore_KeepsThisMachineProtectedPassword()
        {
            using (var context = new CalQrDbContext(_options))
            {
                context.AppSettings.Add(new AppSetting { Key = DpapiBackupPasswordStore.SettingKey, Value = "blob-at-backup-time" });
                context.SaveChanges();
            }
            var service = NewService(new TestBackupPasswordStore());
            await service.BackupNowAsync(_backupFolder);
            string backup = Assert.Single(Directory.GetFiles(_backupFolder));

            using (var context = new CalQrDbContext(_options))
            {
                context.AppSettings.Single(s => s.Key == DpapiBackupPasswordStore.SettingKey).Value = "blob-changed-later";
                context.SaveChanges();
            }

            await service.RestoreAsync(backup);

            Assert.Equal("blob-changed-later", SettingValue(DpapiBackupPasswordStore.SettingKey));
        }

        [Fact]
        public async Task Restore_LegacyUnencryptedZip_StillWorks()
        {
            var service = NewService(new TestBackupPasswordStore());
            await service.BackupNowAsync(_backupFolder);
            string backup = Assert.Single(Directory.GetFiles(_backupFolder));

            // نسخة قديمة من قبل القفل = الأرشيف نفسه بلا تشفير.
            string legacyZip = Path.Combine(_dir, "CalQR_Backup_legacy.zip");
            BackupEncryption.DecryptFile(backup, legacyZip, TestBackupPasswordStore.DefaultPassword);
            AddMarker("MarkerAddedAfterBackup");

            await NewService(new TestBackupPasswordStore(password: null)).RestoreAsync(legacyZip);

            Assert.False(HasSetting("MarkerAddedAfterBackup"));
        }

        private class TestDbContextFactory : IDbContextFactory<CalQrDbContext>
        {
            private readonly DbContextOptions<CalQrDbContext> _options;
            public TestDbContextFactory(DbContextOptions<CalQrDbContext> options) => _options = options;
            public CalQrDbContext CreateDbContext() => new CalQrDbContext(_options);
        }
    }
}
