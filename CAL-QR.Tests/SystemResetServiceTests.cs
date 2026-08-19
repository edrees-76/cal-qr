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

                    var auditLogs = await context.AuditLogs.ToListAsync();
                    Assert.NotEmpty(auditLogs);
                    Assert.Contains(auditLogs, log => log.Action == "تصفير كامل للنظام");
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
