using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Services;

namespace CAL_QR.Tests
{
    public class DevTestDataSeederTests
    {
        [Fact]
        public async Task SeedTestDataAsync_CreatesExactRequestedCountsAndValidSignatures()
        {
            string dbPath = Path.Combine(Path.GetTempPath(), $"cal_qr_seeder_test_{Guid.NewGuid():N}.db");
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            var factory = new TestDbContextFactory(options);

            using (var context = new CalQrDbContext(options))
            {
                DatabaseMigrator.RunMigrations(context);
            }

            var hmacService = new HmacService(factory);

            try
            {
                // 1. First execution
                var result = await DevTestDataSeeder.SeedTestDataAsync(factory, hmacService);

                Assert.True(result.Success);
                Assert.Equal(10, result.OwnersAdded);
                Assert.Equal(100, result.DevicesAdded);
                Assert.Equal(200, result.RecordsAdded);
                Assert.Equal(120, result.ValidCount);
                Assert.Equal(30, result.NearExpiryCount);
                Assert.Equal(50, result.ExpiredCount);

                // 2. Verify database records directly
                using (var context = await factory.CreateDbContextAsync())
                {
                    int ownerCount = await context.Owners.CountAsync(o => !o.IsDeleted && o.IsSeedTestData);
                    int deviceTypeCount = await context.DeviceTypes.CountAsync(t => !t.IsDeleted);
                    int deviceCount = await context.Devices.CountAsync(d => !d.IsDeleted && d.IsSeedTestData);
                    int recordCount = await context.CalibrationRecords.CountAsync(r => !r.IsDeleted && r.IsSeedTestData);

                    Assert.Equal(10, ownerCount);
                    Assert.True(deviceTypeCount >= 5, "DeviceTypes count should be at least 5");
                    Assert.Equal(100, deviceCount);
                    Assert.Equal(200, recordCount);

                    // Check snapshot saved in AppSettings
                    var snapshotSetting = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == "DevSeededDataSnapshot");
                    Assert.NotNull(snapshotSetting);
                    Assert.False(string.IsNullOrWhiteSpace(snapshotSetting!.Value));

                    // Check unique SerialNumbers
                    var serials = await context.Devices.Select(d => d.SerialNumber).ToListAsync();
                    Assert.Equal(100, serials.Distinct().Count());

                    // Check unique CertificateNumbers
                    var certs = await context.CalibrationRecords.Select(c => c.CertificateNumber).ToListAsync();
                    Assert.Equal(200, certs.Distinct().Count());

                    // Verify HMAC signature for ALL 200 records
                    var records = await context.CalibrationRecords
                        .Include(r => r.Device)
                        .ThenInclude(d => d!.Owner)
                        .Where(r => !r.IsDeleted)
                        .ToListAsync();

                    foreach (var rec in records)
                    {
                        Assert.NotNull(rec.Device);
                        Assert.NotNull(rec.Device!.Owner);

                        bool isValid = hmacService.VerifySignature(
                            rec.CertificateNumber,
                            rec.Device.Model,
                            rec.Device.SerialNumber,
                            rec.Device.Owner!.Name,
                            rec.CalibrationDate.ToString("yyyy-MM-dd"),
                            rec.ExpiryDate.ToString("yyyy-MM-dd"),
                            rec.Result,
                            rec.EngineerName,
                            rec.HmacSignature,
                            rec.CreatedAt
                        );

                        Assert.True(isValid, $"HMAC signature verification failed for CertificateNumber: {rec.CertificateNumber}");
                    }
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
        public async Task SeedTestDataAsync_ConsecutiveExecutions_SucceedsWithoutRejectionAndReplacesPreviousData()
        {
            string dbPath = Path.Combine(Path.GetTempPath(), $"cal_qr_repeat_seeder_test_{Guid.NewGuid():N}.db");
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            var factory = new TestDbContextFactory(options);

            using (var context = new CalQrDbContext(options))
            {
                DatabaseMigrator.RunMigrations(context);
            }

            var hmacService = new HmacService(factory);

            try
            {
                // First seed
                var firstRunResult = await DevTestDataSeeder.SeedTestDataAsync(factory, hmacService);
                Assert.True(firstRunResult.Success);

                // Second consecutive seed -> should succeed and replace the first batch (no accumulation)
                var secondRunResult = await DevTestDataSeeder.SeedTestDataAsync(factory, hmacService);
                Assert.True(secondRunResult.Success);
                Assert.Equal(10, secondRunResult.OwnersAdded);

                using (var context = await factory.CreateDbContextAsync())
                {
                    int ownerCount = await context.Owners.CountAsync(o => !o.IsDeleted);
                    int deviceCount = await context.Devices.CountAsync(d => !d.IsDeleted);
                    int recordCount = await context.CalibrationRecords.CountAsync(r => !r.IsDeleted);
                    int deviceTypeCount = await context.DeviceTypes.CountAsync(t => !t.IsDeleted);

                    Assert.Equal(10, ownerCount);
                    Assert.Equal(100, deviceCount);
                    Assert.Equal(200, recordCount);
                    Assert.True(deviceTypeCount >= 5, "DeviceTypes should be preserved and not duplicated");
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
        public async Task ClearSeedTestDataAsync_RemovesTaggedDataEvenIfSnapshotIsMissingOrDeletedManually()
        {
            string dbPath = Path.Combine(Path.GetTempPath(), $"cal_qr_snapshot_missing_test_{Guid.NewGuid():N}.db");
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            var factory = new TestDbContextFactory(options);

            using (var context = new CalQrDbContext(options))
            {
                DatabaseMigrator.RunMigrations(context);

                // Add pre-existing real entity (not seed test data)
                context.Owners.Add(new Owner { Name = "جهة حقيقية مسبقة الوجود", IsSeedTestData = false, IsDeleted = false, CreatedAt = DateTime.UtcNow });
                context.SaveChanges();
            }

            var hmacService = new HmacService(factory);

            try
            {
                // Seed test data
                var seedResult = await DevTestDataSeeder.SeedTestDataAsync(factory, hmacService);
                Assert.True(seedResult.Success);

                using (var context = await factory.CreateDbContextAsync())
                {
                    Assert.Equal(11, await context.Owners.CountAsync(o => !o.IsDeleted));

                    // Manually delete Snapshot setting from AppSettings to simulate missing snapshot scenario
                    var snapshotSetting = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == "DevSeededDataSnapshot");
                    if (snapshotSetting != null)
                    {
                        context.AppSettings.Remove(snapshotSetting);
                        await context.SaveChangesAsync();
                    }
                }

                // Clear test data -> should succeed via IsSeedTestData tag despite missing Snapshot!
                var clearResult = await DevTestDataSeeder.ClearSeedTestDataAsync(factory);
                Assert.True(clearResult.Success);
                Assert.Equal(10, clearResult.OwnersRemoved);
                Assert.Equal(100, clearResult.DevicesRemoved);
                Assert.Equal(200, clearResult.RecordsRemoved);

                using (var context = await factory.CreateDbContextAsync())
                {
                    // Verify pre-existing owner still exists!
                    int ownerCount = await context.Owners.CountAsync(o => !o.IsDeleted);
                    Assert.Equal(1, ownerCount);
                    var remainingOwner = await context.Owners.FirstOrDefaultAsync();
                    Assert.Equal("جهة حقيقية مسبقة الوجود", remainingOwner?.Name);

                    Assert.Equal(0, await context.Devices.CountAsync(d => !d.IsDeleted));
                    Assert.Equal(0, await context.CalibrationRecords.CountAsync(r => !r.IsDeleted));
                    Assert.True(await context.DeviceTypes.CountAsync(t => !t.IsDeleted) >= 5, "DeviceTypes must never be deleted");
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
        public async Task SeedAndClear_AchievesAbsoluteZero_IncludingFilesAndOrphanedRows()
        {
            string dbPath = Path.Combine(Path.GetTempPath(), $"cal_qr_absolute_zero_test_{Guid.NewGuid():N}.db");
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            var factory = new TestDbContextFactory(options);

            using (var context = new CalQrDbContext(options))
            {
                DatabaseMigrator.RunMigrations(context);
            }

            var hmacService = new HmacService(factory);

            try
            {
                // 1. Seed the test data
                var seedResult = await DevTestDataSeeder.SeedTestDataAsync(factory, hmacService);
                Assert.True(seedResult.Success);

                string sampleCertNo;
                int sampleDeviceId;
                int sampleRecordId;

                using (var context = await factory.CreateDbContextAsync())
                {
                    Assert.Equal(10, await context.Owners.CountAsync(o => !o.IsDeleted && o.IsSeedTestData));
                    Assert.Equal(100, await context.Devices.CountAsync(d => !d.IsDeleted && d.IsSeedTestData));
                    Assert.Equal(200, await context.CalibrationRecords.CountAsync(r => !r.IsDeleted && r.IsSeedTestData));

                    var sampleRecord = await context.CalibrationRecords.FirstAsync(r => r.IsSeedTestData);
                    sampleCertNo = sampleRecord.CertificateNumber;
                    sampleDeviceId = sampleRecord.DeviceId;
                    sampleRecordId = sampleRecord.Id;

                    // Simulate an acknowledged expired device record linked to a seeded device
                    context.AcknowledgedExpiredDevices.Add(new AcknowledgedExpiredDevice
                    {
                        DeviceId = sampleDeviceId,
                        CalibrationRecordId = sampleRecordId,
                        AcknowledgedDate = DateTime.UtcNow
                    });
                    await context.SaveChangesAsync();

                    Assert.Equal(1, await context.AcknowledgedExpiredDevices.CountAsync(a => a.DeviceId == sampleDeviceId));
                }

                // Create a sample QR file and attachment folder on disk
                string qrFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "QR");
                Directory.CreateDirectory(qrFolder);
                string sampleQrPath = Path.Combine(qrFolder, $"{sampleCertNo}.png");
                File.WriteAllText(sampleQrPath, "DUMMY_QR_DATA");
                Assert.True(File.Exists(sampleQrPath));

                string attachmentsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Attachments");
                string sampleAttFolder = Path.Combine(attachmentsFolder, sampleCertNo);
                Directory.CreateDirectory(sampleAttFolder);
                string sampleAttFilePath = Path.Combine(sampleAttFolder, "attachment.pdf");
                File.WriteAllText(sampleAttFilePath, "DUMMY_ATTACHMENT");
                Assert.True(Directory.Exists(sampleAttFolder));

                // 2. Clear test data
                var clearResult = await DevTestDataSeeder.ClearSeedTestDataAsync(factory);
                Assert.True(clearResult.Success);
                Assert.True(clearResult.QrFilesRemoved >= 1, "Should report at least 1 QR file removed");
                Assert.True(clearResult.AttachmentFoldersRemoved >= 1, "Should report at least 1 attachment folder removed");

                // 3. Verify absolute zero in database and disk
                using (var context = await factory.CreateDbContextAsync())
                {
                    Assert.Equal(0, await context.Owners.CountAsync(o => o.IsSeedTestData));
                    Assert.Equal(0, await context.Devices.CountAsync(d => d.IsSeedTestData));
                    Assert.Equal(0, await context.CalibrationRecords.CountAsync(r => r.IsSeedTestData));
                    Assert.Equal(0, await context.AcknowledgedExpiredDevices.CountAsync(a => a.DeviceId == sampleDeviceId));
                    Assert.True(await context.DeviceTypes.CountAsync(t => !t.IsDeleted) >= 5, "DeviceTypes must be preserved");
                }

                Assert.False(File.Exists(sampleQrPath), "QR code file must be deleted from disk");
                Assert.False(Directory.Exists(sampleAttFolder), "Attachment folder must be deleted from disk");

                // 4. Re-run SeedTestDataAsync to confirm repeatable cycle
                var reSeedResult = await DevTestDataSeeder.SeedTestDataAsync(factory, hmacService);
                Assert.True(reSeedResult.Success);
                Assert.Equal(10, reSeedResult.OwnersAdded);
                Assert.Equal(100, reSeedResult.DevicesAdded);
                Assert.Equal(200, reSeedResult.RecordsAdded);
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
