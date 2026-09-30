using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Services;
using CAL_QR.Repositories;

namespace CAL_QR.Tests
{
    [Collection("SharedDiskFolders")]
    public class SystemResetServiceTests
    {
        // تهيئة يدويّة بديلة عن DevTestDataSeeder: تبني حالة معروفة صغيرة مباشرةً
        // عبر السياق. فُصل الاختبار عن السيدر عمداً تمهيداً لإزالة السيدر — الجوهر
        // المُختبَر (رفض العبارة الخاطئة، ومسح العبارة الصحيحة لكل شيء مع إعادة بذر
        // الأنواع وتنظيف القرص) لا يعتمد على حجم بيانات السيدر ولا على وسمها.
        // يبني: 3 جهات، 3 أجهزة (نوع 1)، 3 سجلات، وتنبيهاً واحداً. يعيد آخر معرّف سجل.
        private static async Task SeedManualDataAsync(IDbContextFactory<CalQrDbContext> factory)
        {
            using var context = await factory.CreateDbContextAsync();

            for (int i = 1; i <= 3; i++)
            {
                var owner = new Owner { Name = $"جهة {i}", IsSeedTestData = false };
                context.Owners.Add(owner);
                await context.SaveChangesAsync();

                var device = new Device
                {
                    Model = $"Model {i}",
                    SerialNumber = $"SN-{i:0000}",
                    OwnerId = owner.Id,
                    DeviceTypeId = 1,
                    IsSeedTestData = false
                };
                context.Devices.Add(device);
                await context.SaveChangesAsync();

                var record = new CalibrationRecord
                {
                    DeviceId = device.Id,
                    CertificateNumber = $"CERT-{i:0000}",
                    CalibrationDate = DateTime.Today.AddDays(-10),
                    ExpiryDate = DateTime.Today.AddDays(355),
                    EngineerName = $"مهندس {i}",
                    Result = "Passed",
                    IsSeedTestData = false
                };
                context.CalibrationRecords.Add(record);
                await context.SaveChangesAsync();
            }
        }

        [Fact]
        public async Task FactoryResetAsync_WrongConfirmationPhrase_DoesNothing()
        {
            string dbPath = Path.Combine(Path.GetTempPath(), $"cal_qr_wrong_phrase_reset_test_{Guid.NewGuid():N}.db");
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            var factory = new TestDbContextFactory(options);

            using (var context = new CalQrDbContext(options))
            {
                DatabaseMigrator.RunMigrations(context);
            }

            try
            {
                await SeedManualDataAsync(factory);

                // عبارة خاطئة ⇒ لا شيء يُمسّ
                var resetResult = await SystemResetService.FactoryResetAsync(factory, "INVALID-PHRASE");

                Assert.False(resetResult.Success);
                Assert.Contains("تأكيد غير صحيح", resetResult.Message);

                using (var context = await factory.CreateDbContextAsync())
                {
                    Assert.Equal(3, await context.Owners.CountAsync());
                    Assert.Equal(3, await context.Devices.CountAsync());
                    Assert.Equal(3, await context.CalibrationRecords.CountAsync());
                }
            }
            finally
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                if (File.Exists(dbPath))
                {
                    try { File.Delete(dbPath); } catch { }
                }
            }
        }

        [Fact]
        public async Task FactoryResetAsync_CorrectPhrase_WipesAllDataAndFiles()
        {
            string dbPath = Path.Combine(Path.GetTempPath(), $"cal_qr_factory_reset_test_{Guid.NewGuid():N}.db");
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            var factory = new TestDbContextFactory(options);

            string qrFolder = Path.Combine(Path.GetTempPath(), $"cal_qr_factoryreset_qr_{Guid.NewGuid():N}");
            string attachmentsFolder = Path.Combine(Path.GetTempPath(), $"cal_qr_factoryreset_att_{Guid.NewGuid():N}");
            Directory.CreateDirectory(qrFolder);
            Directory.CreateDirectory(attachmentsFolder);

            using (var context = new CalQrDbContext(options))
            {
                DatabaseMigrator.RunMigrations(context);
                context.AppSettings.Single(s => s.Key == "QrOutputPath").Value = qrFolder;
                context.AppSettings.Single(s => s.Key == "AttachmentsPath").Value = attachmentsFolder;
                context.SaveChanges();
            }

            var auditLogRepo = new AuditLogRepository(factory, new TestCurrentUserService());

            try
            {
                // بيانات معروفة: 3 جهات/أجهزة/سجلات + تنبيه واحد على آخر سجل
                await SeedManualDataAsync(factory);

                string realCertNo;
                using (var context = await factory.CreateDbContextAsync())
                {
                    var lastRecord = await context.CalibrationRecords.OrderBy(r => r.Id).LastAsync();
                    realCertNo = lastRecord.CertificateNumber;
                    context.AcknowledgedExpiredDevices.Add(new AcknowledgedExpiredDevice
                    {
                        DeviceId = lastRecord.DeviceId,
                        CalibrationRecordId = lastRecord.Id,
                        AcknowledgedDate = DateTime.UtcNow
                    });
                    await context.SaveChangesAsync();

                    Assert.Equal(3, await context.Owners.CountAsync());
                    Assert.Equal(3, await context.Devices.CountAsync());
                    Assert.Equal(3, await context.CalibrationRecords.CountAsync());
                    Assert.Equal(1, await context.AcknowledgedExpiredDevices.CountAsync());
                }

                // ملف QR ومجلد مرفقات على القرص
                string realQrPath = Path.Combine(qrFolder, $"{realCertNo}.png");
                File.WriteAllText(realQrPath, "QR_IMAGE_BYTES");
                Assert.True(File.Exists(realQrPath));

                string realAttFolder = Path.Combine(attachmentsFolder, realCertNo);
                Directory.CreateDirectory(realAttFolder);
                File.WriteAllText(Path.Combine(realAttFolder, "report.pdf"), "PDF_REPORT_BYTES");
                Assert.True(Directory.Exists(realAttFolder));

                // التصفير بالعبارة الصحيحة
                var resetResult = await SystemResetService.FactoryResetAsync(factory, "RESET-ALL-DATA", auditLogRepo);

                Assert.True(resetResult.Success);
                Assert.Equal(3, resetResult.OwnersRemoved);
                Assert.Equal(3, resetResult.DevicesRemoved);
                Assert.Equal(3, resetResult.RecordsRemoved);
                Assert.True(resetResult.QrFilesRemoved >= 1);
                Assert.True(resetResult.AttachmentFoldersRemoved >= 1);

                using (var context = await factory.CreateDbContextAsync())
                {
                    Assert.Equal(0, await context.Owners.CountAsync());
                    Assert.Equal(0, await context.Devices.CountAsync());
                    Assert.Equal(0, await context.CalibrationRecords.CountAsync());
                    Assert.Equal(0, await context.AcknowledgedExpiredDevices.CountAsync());

                    var deviceTypes = await context.DeviceTypes.Where(t => !t.IsDeleted).ToListAsync();
                    Assert.Equal(6, deviceTypes.Count);
                    Assert.Equal(
                        DeviceTypeCatalog.CanonicalNames.OrderBy(n => n).ToArray(),
                        deviceTypes.Select(t => t.Name).OrderBy(n => n).ToArray());

                    Assert.True(await context.DeviceTypeFunctionalCheckTemplates.CountAsync() > 0);

                    Assert.True(await context.AppSettings.CountAsync() > 0);

                    // سجل التدقيق مُسِح ضمن التصفير، ثم كُتب سطر التصفير وحيداً بعد
                    // الـcommit. فالسجل النظيف يحوي سطراً واحداً بالضبط هو التصفير —
                    // تأكيد أقوى من NotEmpty: يكشف أي تسرّب لأسطر قديمة لم تُمسَح.
                    var auditLogs = await context.AuditLogs.ToListAsync();
                    Assert.Single(auditLogs);
                    Assert.Equal("تصفير كامل للنظام", auditLogs[0].Action);
                }

                Assert.False(File.Exists(realQrPath), "Production QR code file must be deleted");
                Assert.False(Directory.Exists(realAttFolder), "Production Attachment folder must be deleted");
                Assert.True(Directory.Exists(qrFolder), "QR root directory itself should remain intact");
                Assert.True(Directory.Exists(attachmentsFolder), "Attachments root directory itself should remain intact");
            }
            finally
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                if (File.Exists(dbPath))
                {
                    try { File.Delete(dbPath); } catch { }
                }
                try { Directory.Delete(qrFolder, recursive: true); } catch { }
                try { Directory.Delete(attachmentsFolder, recursive: true); } catch { }
            }
        }

        private static DbContextOptions<CalQrDbContext> NewSqliteOptions(string dbPath) =>
            new DbContextOptionsBuilder<CalQrDbContext>().UseSqlite($"Data Source={dbPath}").Options;

        private static void CleanUp(string dbPath, params string[] folders)
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                try { File.Delete(dbPath); } catch (IOException) { /* ملفّ مؤقّت ما زال مقفلاً؛ بقايا غير مؤذية */ }
            }
            foreach (var folder in folders)
            {
                try { Directory.Delete(folder, recursive: true); } catch (IOException) { /* كما أعلاه */ }
            }
        }

        [Fact]
        public void IsSafeToClean_RejectsEmptyDriveRootAndFoldersContainingProtectedPaths()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "cal_qr_guard_" + Guid.NewGuid().ToString("N"));
            string backups = Path.Combine(tempRoot, "backups");
            var protectedPaths = new[] { backups };

            Assert.False(SystemResetService.IsSafeToClean("   ", protectedPaths, out _));
            Assert.False(SystemResetService.IsSafeToClean(Path.GetPathRoot(tempRoot)!, protectedPaths, out string rootReason));
            Assert.Contains("جذر", rootReason);

            // يحتوي المسار المحميّ ⇒ مرفوض
            Assert.False(SystemResetService.IsSafeToClean(tempRoot, protectedPaths, out string containsReason));
            Assert.Contains("محميّ", containsReason);
            // يساويه ⇒ مرفوض
            Assert.False(SystemResetService.IsSafeToClean(backups, protectedPaths, out _));

            // داخل المسار المحميّ أو بجواره ⇒ مسموح
            Assert.True(SystemResetService.IsSafeToClean(Path.Combine(backups, "poster"), protectedPaths, out _));
            Assert.True(SystemResetService.IsSafeToClean(Path.Combine(tempRoot, "poster"), protectedPaths, out _));
        }

        [Fact]
        public async Task ResolveCleanupFoldersAsync_ReturnsSettingsAndFallsBackToDefaults()
        {
            string dbPath = Path.Combine(Path.GetTempPath(), $"cal_qr_resolve_test_{Guid.NewGuid():N}.db");
            var options = NewSqliteOptions(dbPath);
            var factory = new TestDbContextFactory(options);
            try
            {
                using (var context = new CalQrDbContext(options))
                {
                    DatabaseMigrator.RunMigrations(context);
                    context.AppSettings.Single(s => s.Key == "QrOutputPath").Value = @"X:\custom\poster";
                    context.AppSettings.Single(s => s.Key == "AttachmentsPath").Value = string.Empty;
                    context.SaveChanges();
                }

                var (qr, attachments) = await SystemResetService.ResolveCleanupFoldersAsync(factory);
                Assert.Equal(@"X:\custom\poster", qr);
                Assert.Equal(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Attachments"), attachments);
                Assert.Equal(0, await SystemResetService.CountCertificatesAsync(factory));
            }
            finally
            {
                CleanUp(dbPath);
            }
        }

        [Fact]
        public async Task FactoryResetAsync_DefaultMode_ResetsCertificateSequence()
        {
            string dbPath = Path.Combine(Path.GetTempPath(), $"cal_qr_reset_seq_default_{Guid.NewGuid():N}.db");
            var options = NewSqliteOptions(dbPath);
            var factory = new TestDbContextFactory(options);
            string qrFolder = Path.Combine(Path.GetTempPath(), $"cal_qr_reset_qr_{Guid.NewGuid():N}");
            string attachmentsFolder = Path.Combine(Path.GetTempPath(), $"cal_qr_reset_att_{Guid.NewGuid():N}");
            Directory.CreateDirectory(qrFolder);
            Directory.CreateDirectory(attachmentsFolder);
            try
            {
                using (var context = new CalQrDbContext(options))
                {
                    DatabaseMigrator.RunMigrations(context);
                    context.AppSettings.Single(s => s.Key == "QrOutputPath").Value = qrFolder;
                    context.AppSettings.Single(s => s.Key == "AttachmentsPath").Value = attachmentsFolder;
                    context.CertificateSequence.Add(new CertificateSequence { Year = 2026, LastNumber = 42 });
                    context.SaveChanges();
                }

                var result = await SystemResetService.FactoryResetAsync(factory, "RESET-ALL-DATA");

                Assert.True(result.Success);
                using var check = new CalQrDbContext(options);
                Assert.Equal(0, await check.CertificateSequence.CountAsync());
            }
            finally
            {
                CleanUp(dbPath, qrFolder, attachmentsFolder);
            }
        }

        [Fact]
        public async Task FactoryResetAsync_LiveSystemMode_KeepsCertificateSequenceAndAuditLog()
        {
            string dbPath = Path.Combine(Path.GetTempPath(), $"cal_qr_reset_live_{Guid.NewGuid():N}.db");
            var options = NewSqliteOptions(dbPath);
            var factory = new TestDbContextFactory(options);
            string qrFolder = Path.Combine(Path.GetTempPath(), $"cal_qr_reset_qr_{Guid.NewGuid():N}");
            string attachmentsFolder = Path.Combine(Path.GetTempPath(), $"cal_qr_reset_att_{Guid.NewGuid():N}");
            Directory.CreateDirectory(qrFolder);
            Directory.CreateDirectory(attachmentsFolder);
            var auditLogRepo = new AuditLogRepository(factory, new TestCurrentUserService());
            try
            {
                int logsBefore;
                using (var context = new CalQrDbContext(options))
                {
                    DatabaseMigrator.RunMigrations(context);
                    context.AppSettings.Single(s => s.Key == "QrOutputPath").Value = qrFolder;
                    context.AppSettings.Single(s => s.Key == "AttachmentsPath").Value = attachmentsFolder;
                    context.CertificateSequence.Add(new CertificateSequence { Year = 2026, LastNumber = 42 });
                    context.AuditLogs.Add(new AuditLog { Action = "إجراء قديم", EntityName = "Test", EntityId = "1", Details = "سطر قديم" });
                    context.SaveChanges();
                    logsBefore = context.AuditLogs.Count();
                }
                await SeedManualDataAsync(factory);

                var result = await SystemResetService.FactoryResetAsync(
                    factory, "RESET-ALL-DATA", auditLogRepo,
                    resetCertificateSequence: false, clearAuditLog: false);

                Assert.True(result.Success);
                Assert.Contains("عدّاد ترقيم", result.Message);

                using var check = new CalQrDbContext(options);
                Assert.Equal(0, await check.Owners.CountAsync());
                Assert.Equal(0, await check.CalibrationRecords.CountAsync());

                var sequences = await check.CertificateSequence.ToListAsync();
                Assert.Contains(sequences, x => x.Year == 2026 && x.LastNumber == 42);

                var logs = await check.AuditLogs.OrderBy(l => l.Id).ToListAsync();
                Assert.Contains(logs, l => l.Action == "إجراء قديم");
                Assert.Equal("تصفير كامل للنظام", logs.Last().Action);
                Assert.True(logs.Count >= logsBefore + 1);
            }
            finally
            {
                CleanUp(dbPath, qrFolder, attachmentsFolder);
            }
        }

        [Fact]
        public async Task FactoryResetAsync_SkipsFolderContainingProtectedPath_AndStillCleansTheOther()
        {
            string dbPath = Path.Combine(Path.GetTempPath(), $"cal_qr_reset_guard_{Guid.NewGuid():N}.db");
            var options = NewSqliteOptions(dbPath);
            var factory = new TestDbContextFactory(options);
            string qrFolder = Path.Combine(Path.GetTempPath(), $"cal_qr_reset_qr_{Guid.NewGuid():N}");
            string attachmentsFolder = Path.Combine(Path.GetTempPath(), $"cal_qr_reset_att_{Guid.NewGuid():N}");
            string protectedInsideQr = Path.Combine(qrFolder, "backups");
            Directory.CreateDirectory(protectedInsideQr);
            Directory.CreateDirectory(attachmentsFolder);
            string qrFile = Path.Combine(qrFolder, "label.png");
            string backupFile = Path.Combine(protectedInsideQr, "backup.cqbak");
            File.WriteAllText(qrFile, "QR");
            File.WriteAllText(backupFile, "BACKUP");
            string attachmentChild = Path.Combine(attachmentsFolder, "77");
            Directory.CreateDirectory(attachmentChild);
            File.WriteAllText(Path.Combine(attachmentChild, "report.pdf"), "PDF");
            try
            {
                using (var context = new CalQrDbContext(options))
                {
                    DatabaseMigrator.RunMigrations(context);
                    context.AppSettings.Single(s => s.Key == "QrOutputPath").Value = qrFolder;
                    context.AppSettings.Single(s => s.Key == "AttachmentsPath").Value = attachmentsFolder;
                    context.SaveChanges();
                }

                var result = await SystemResetService.FactoryResetAsync(
                    factory, "RESET-ALL-DATA", protectedPaths: new[] { protectedInsideQr });

                Assert.True(result.Success);
                Assert.Contains("تُخطّي", result.Message);
                Assert.Equal(0, result.QrFilesRemoved);
                Assert.True(File.Exists(qrFile), "مجلّد QR يحتوي مساراً محميّاً فلا يُنظَّف");
                Assert.True(File.Exists(backupFile), "النسخة الاحتياطيّة لا تُمسّ أبداً");
                Assert.False(Directory.Exists(attachmentChild), "مجلّد المرفقات الآخر يُنظَّف كالمعتاد");
            }
            finally
            {
                CleanUp(dbPath, qrFolder, attachmentsFolder);
            }
        }

        private class TestDbContextFactory : IDbContextFactory<CalQrDbContext>
        {
            private readonly DbContextOptions<CalQrDbContext> _options;

            public TestDbContextFactory(DbContextOptions<CalQrDbContext> options)
            {
                _options = options;
            }

            public CalQrDbContext CreateDbContext()
            {
                return new CalQrDbContext(_options);
            }

            public Task<CalQrDbContext> CreateDbContextAsync(System.Threading.CancellationToken cancellationToken = default)
            {
                return Task.FromResult(CreateDbContext());
            }
        }
    }
}
