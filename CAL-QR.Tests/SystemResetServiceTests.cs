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
                context.Owners.Add(new Owner { Name = "جهة حقيقية مهمة جداً", IsSeedTestData = false });
                context.SaveChanges();
            }

            var hmacService = new HmacService(factory);

            try
            {
                // Seed test data
                var seedResult = await DevTestDataSeeder.SeedTestDataAsync(factory, hmacService);
                Assert.True(seedResult.Success);

                // Call FactoryResetAsync with wrong phrase
                var resetResult = await SystemResetService.FactoryResetAsync(factory, "INVALID-PHRASE");

                Assert.False(resetResult.Success);
                Assert.Contains("تأكيد غير صحيح", resetResult.Message);

                // Verify database records remain 100% intact
                using (var context = await factory.CreateDbContextAsync())
                {
                    Assert.Equal(11, await context.Owners.CountAsync());
                    Assert.Equal(100, await context.Devices.CountAsync());
                    Assert.Equal(200, await context.CalibrationRecords.CountAsync());
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

            // Isolated disk paths: the shared QR/Attachments folders under BaseDirectory are
            // written and wiped by other tests in the same run.
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

            var hmacService = new HmacService(factory);
            var auditLogRepo = new AuditLogRepository(factory, new TestCurrentUserService());

            try
            {
                // 1. Seed test data
                var seedResult = await DevTestDataSeeder.SeedTestDataAsync(factory, hmacService);
                Assert.True(seedResult.Success);

                // 2. Add real production data (IsSeedTestData = false)
                int realOwnerId;
                int realDeviceId;
                int realRecordId;
                string realCertNo = "REAL-CERT-2026-9999";

                using (var context = await factory.CreateDbContextAsync())
                {
                    var realOwner = new Owner { Name = "جهة حقيقية رقم 1", IsSeedTestData = false };
                    context.Owners.Add(realOwner);
                    await context.SaveChangesAsync();
                    realOwnerId = realOwner.Id;

                    var realDevice = new Device { Model = "Real Model", SerialNumber = "REAL-SN-1234", OwnerId = realOwnerId, DeviceTypeId = 1, IsSeedTestData = false };
                    context.Devices.Add(realDevice);
                    await context.SaveChangesAsync();
                    realDeviceId = realDevice.Id;

                    var realRecord = new CalibrationRecord
                    {
                        DeviceId = realDeviceId,
                        CertificateNumber = realCertNo,
                        CalibrationDate = DateTime.Today.AddDays(-10),
                        ExpiryDate = DateTime.Today.AddDays(355),
                        EngineerName = "مهندس مكسور",
                        Result = "Passed",
                        IsSeedTestData = false
                    };
                    context.CalibrationRecords.Add(realRecord);
                    await context.SaveChangesAsync();
                    realRecordId = realRecord.Id;

                    context.AcknowledgedExpiredDevices.Add(new AcknowledgedExpiredDevice
                    {
                        DeviceId = realDeviceId,
                        CalibrationRecordId = realRecordId,
                        AcknowledgedDate = DateTime.UtcNow
                    });
                    await context.SaveChangesAsync();

                    // Pre-verification
                    Assert.Equal(11, await context.Owners.CountAsync());
                    Assert.Equal(101, await context.Devices.CountAsync());
                    Assert.Equal(201, await context.CalibrationRecords.CountAsync());
                    Assert.Equal(1, await context.AcknowledgedExpiredDevices.CountAsync());
                }

                // 3. Create sample QR file and attachment folder on disk
                string realQrPath = Path.Combine(qrFolder, $"{realCertNo}.png");
                File.WriteAllText(realQrPath, "REAL_QR_IMAGE_BYTES");
                Assert.True(File.Exists(realQrPath));

                string realAttFolder = Path.Combine(attachmentsFolder, realCertNo);
                Directory.CreateDirectory(realAttFolder);
                string realAttFilePath = Path.Combine(realAttFolder, "report.pdf");
                File.WriteAllText(realAttFilePath, "REAL_PDF_REPORT_BYTES");
                Assert.True(Directory.Exists(realAttFolder));

                // 4. Run FactoryResetAsync with correct phrase
                var resetResult = await SystemResetService.FactoryResetAsync(factory, "RESET-ALL-DATA", auditLogRepo);

                Assert.True(resetResult.Success);
                Assert.Equal(11, resetResult.OwnersRemoved);
                Assert.Equal(101, resetResult.DevicesRemoved);
                Assert.Equal(201, resetResult.RecordsRemoved);
                Assert.True(resetResult.QrFilesRemoved >= 1);
                Assert.True(resetResult.AttachmentFoldersRemoved >= 1);

                // 5. Verify database wiped to fresh install state
                using (var context = await factory.CreateDbContextAsync())
                {
                    Assert.Equal(0, await context.Owners.CountAsync());
                    Assert.Equal(0, await context.Devices.CountAsync());
                    Assert.Equal(0, await context.CalibrationRecords.CountAsync());
                    Assert.Equal(0, await context.AcknowledgedExpiredDevices.CountAsync());

                    // DeviceTypes must be exactly the 6 re-seeded defaults.
                    // الأسماء تُقارَن بـ DeviceTypeCatalog لا بسلسلة مكتوبة هنا:
                    // نسخة مكتوبة يدوياً في الاختبار كانت ستُبقي الازدواج الذي
                    // عالجه مصدر الحقيقة الواحد، وتمرّ حتى لو تباعد التصفير عن الهجرة.
                    var deviceTypes = await context.DeviceTypes.Where(t => !t.IsDeleted).ToListAsync();
                    Assert.Equal(6, deviceTypes.Count);
                    Assert.Equal(
                        DeviceTypeCatalog.CanonicalNames.OrderBy(n => n).ToArray(),
                        deviceTypes.Select(t => t.Name).OrderBy(n => n).ToArray());

                    // والقوالب أُعيد بناؤها معها لا الأسماء وحدها
                    Assert.True(await context.DeviceTypeFunctionalCheckTemplates.CountAsync() > 0);

                    // AppSettings preserved (except Snapshot key removed)
                    Assert.True(await context.AppSettings.CountAsync() > 0);
                    Assert.Null(await context.AppSettings.FirstOrDefaultAsync(s => s.Key == "DevSeededDataSnapshot"));

                    // AuditLog preserved and contains FactoryReset entry
                    var auditLogs = await context.AuditLogs.ToListAsync();
                    Assert.NotEmpty(auditLogs);
                    Assert.Contains(auditLogs, log => log.Action == "تصفير كامل للنظام");
                }

                // 6. Verify files/folders removed from disk, but root folders exist
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
