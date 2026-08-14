using Xunit;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Repositories;
using CAL_QR.Services;
using CAL_QR.ViewModels;

#pragma warning disable CS8625

namespace CAL_QR.Tests
{
    [Collection("MessageBoxMock")]
    public class EndToEndIntegrationTests : IDisposable
    {
        public EndToEndIntegrationTests()
        {
            // Intercept Message Box dialogs
            DeviceDetailViewModel.MessageBoxShowMock = (msg, caption, btn, img) => System.Windows.MessageBoxResult.Yes;
        }

        private class TestDbContextFactory : IDbContextFactory<CalQrDbContext>
        {
            private readonly DbContextOptions<CalQrDbContext> _options;
            public TestDbContextFactory(DbContextOptions<CalQrDbContext> options)
            {
                _options = options;
            }
            public CalQrDbContext CreateDbContext() => new CalQrDbContext(_options);
        }

        private class MockSearchService : ISearchService
        {
            public Task<System.Collections.Generic.List<SearchResultItem>> SearchAsync(string query, System.Threading.CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new System.Collections.Generic.List<SearchResultItem>());
            }
        }

        [Fact]
        public async Task Scenario1_FullCertificateLifecycle()
        {
            // Arrange
            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "E2E_Scenario1_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDir);
            string dbPath = Path.Combine(testDir, "test.db");

            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            var factory = new TestDbContextFactory(options);

            using (var context = new CalQrDbContext(options))
            {
                context.Database.EnsureCreated();
            }

            var hmacService = new HmacService(factory);
            hmacService.Initialize();

            // 1. Create Owner, DeviceType, Device
            Owner owner;
            DeviceType type;
            Device device;
            using (var context = new CalQrDbContext(options))
            {
                owner = new Owner { Name = "Nuclear Research Center" };
                type = new DeviceType { Name = "Geiger Counter" };
                context.Owners.Add(owner);
                context.DeviceTypes.Add(type);
                await context.SaveChangesAsync();

                device = new Device { Model = "Ludlum-12", SerialNumber = "SN-E2E-1", OwnerId = owner.Id, DeviceTypeId = type.Id };
                context.Devices.Add(device);
                await context.SaveChangesAsync();
            }

            // 2. Register Calibration Record (legacy HMAC signature, unrelated to certificate verification below)
            string legacyCertNo = "CERT-E2E-101";
            string calDateStr = "2026-07-18";
            string expDateStr = "2027-07-18";
            string result = "Passed";
            string engineer = "Edrees";

            string legacySignature = hmacService.ComputeSignature(
                certNo: legacyCertNo,
                model: device.Model,
                serial: device.SerialNumber,
                ownerName: owner.Name,
                calDate: calDateStr,
                expDate: expDateStr,
                result: result,
                engineerName: engineer
            );

            int calibrationRecordId;
            using (var context = new CalQrDbContext(options))
            {
                var record = new CalibrationRecord
                {
                    DeviceId = device.Id,
                    CertificateNumber = legacyCertNo,
                    CalibrationDate = DateTime.Parse(calDateStr),
                    ExpiryDate = DateTime.Parse(expDateStr),
                    Result = result,
                    EngineerName = engineer,
                    HmacSignature = legacySignature
                };
                context.CalibrationRecords.Add(record);
                await context.SaveChangesAsync();
                calibrationRecordId = record.Id;
            }

            // 3. Issue a real Certificate (current signature path) via CertificateRepository
            var signatureService = new CertificateSignatureService(hmacService);
            var certificateRepository = new CertificateRepository(
                factory,
                new CertificateNumberService(factory),
                signatureService);

            var certificate = new Certificate
            {
                CalibrationRecordId = calibrationRecordId,
                ClientName = owner.Name,
                DeviceModel = device.Model,
                DeviceSerialNumber = device.SerialNumber,
                CalibrationDate = DateTime.Parse(calDateStr),
                IssueDate = DateTime.Parse(calDateStr)
            };

            string certNo = await certificateRepository.AddAsync(certificate);
            var issued = await certificateRepository.GetByCertificateNumberAsync(certNo);

            // 4. Verify via QrVerifyViewModel (Paste path)
            string qrText = signatureService.BuildQrPayload(issued!);

            var verifyVm = new QrVerifyViewModel(certificateRepository);
            verifyVm.ConcatenatedText = qrText;
            await verifyVm.VerifyPastedTextAsync();

            Assert.True(verifyVm.IsValidated);
            Assert.True(verifyVm.IsAuthentic);
            Assert.True(verifyVm.IsSuccess);
            Assert.Equal(owner.Name, verifyVm.Owner);
            Assert.Equal(device.Model, verifyVm.Model);
            Assert.Equal(device.SerialNumber, verifyVm.Serial);
            Assert.Equal(certNo, verifyVm.CertNo);

            // 5. Verify via QrVerifyViewModel (Quick Verify code path)
            verifyVm.ClearCommand.Execute(null);
            Assert.Empty(verifyVm.CertNo);

            verifyVm.QuickVerifyCode = issued!.VerifyCode!;
            await verifyVm.QuickVerifyAsync();

            Assert.True(verifyVm.IsValidated);
            Assert.True(verifyVm.IsAuthentic);
            Assert.True(verifyVm.IsSuccess);
            Assert.Equal(certNo, verifyVm.CertNo);
            Assert.Equal(owner.Name, verifyVm.Owner);
            Assert.Equal(device.Model, verifyVm.Model);

            // Cleanup
            try { Directory.Delete(testDir, true); } catch {}
        }

        [Fact]
        public async Task Scenario2_EndToEndPermissions()
        {
            // Arrange
            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "E2E_Scenario2_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDir);
            string dbPath = Path.Combine(testDir, "test.db");

            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            var factory = new TestDbContextFactory(options);

            using (var context = new CalQrDbContext(options))
            {
                context.Database.EnsureCreated();
            }

            var authService = new TestCurrentUserService();
            var auditRepo = new AuditLogRepository(factory, authService);

            // 1. Create a user with only Reports permission
            var standardUser = new User
            {
                Username = "reporter",
                Role = UserRole.User,
                Permissions = SystemPermissions.Reports,
                IsActive = true
            };
            authService.SetCurrentUser(standardUser);

            var searchService = new MockSearchService();
            var mainVm = new MainViewModel(factory, searchService, authService);

            // 2. Assert standard user visibility
            Assert.True(mainVm.IsReportsVisible);
            Assert.False(mainVm.IsRecordsVisible);
            Assert.False(mainVm.IsVerificationVisible);
            Assert.False(mainVm.IsOwnersVisible);
            Assert.False(mainVm.IsDeviceTypesVisible);
            Assert.False(mainVm.IsSettingsVisible);
            Assert.False(mainVm.IsUserManagementVisible);

            // 3. Create Admin user
            var adminUser = new User
            {
                Username = "admin",
                Role = UserRole.Admin,
                Permissions = SystemPermissions.None, // Admins have all permissions by default
                IsActive = true
            };
            authService.SetCurrentUser(adminUser);

            // 4. Assert admin user visibility
            Assert.True(mainVm.IsReportsVisible);
            Assert.True(mainVm.IsRecordsVisible);
            Assert.True(mainVm.IsVerificationVisible);
            Assert.True(mainVm.IsOwnersVisible);
            Assert.True(mainVm.IsDeviceTypesVisible);
            Assert.True(mainVm.IsSettingsVisible);
            Assert.True(mainVm.IsUserManagementVisible);

            // 5. Check Delete capability in DeviceDetailViewModel
            var calRepo = new CalibrationRepository(factory);
            var devRepo = new DeviceRepository(factory);
            var detailVm = new DeviceDetailViewModel(factory, calRepo, devRepo, authService, auditRepo, null!, null!);

            // Prepare device and multiple calibration records
            int deviceId;
            using (var context = new CalQrDbContext(options))
            {
                var owner = new Owner { Name = "Owner" };
                var type = new DeviceType { Name = "Type" };
                context.Owners.Add(owner);
                context.DeviceTypes.Add(type);
                await context.SaveChangesAsync();

                var device = new Device { Model = "Model", SerialNumber = "SN", OwnerId = owner.Id, DeviceTypeId = type.Id };
                context.Devices.Add(device);
                await context.SaveChangesAsync();
                deviceId = device.Id;

                context.CalibrationRecords.Add(new CalibrationRecord { DeviceId = deviceId, CertificateNumber = "CERT-1", CalibrationDate = DateTime.Today, ExpiryDate = DateTime.Today.AddYears(1), Result = "Passed", HmacSignature = "HMAC1" });
                context.CalibrationRecords.Add(new CalibrationRecord { DeviceId = deviceId, CertificateNumber = "CERT-2", CalibrationDate = DateTime.Today, ExpiryDate = DateTime.Today.AddYears(1), Result = "Passed", HmacSignature = "HMAC2" });
                await context.SaveChangesAsync();
            }

            detailVm.LoadDeviceDetails(deviceId);

            // Admin can delete
            Assert.True(detailVm.DeleteRecordCommand.CanExecute(detailVm.Calibrations[0]));

            // Change to non-admin user
            authService.SetCurrentUser(standardUser);
            // Standard user cannot delete
            Assert.False(detailVm.DeleteRecordCommand.CanExecute(detailVm.Calibrations[0]));

            // Cleanup
            try { Directory.Delete(testDir, true); } catch {}
        }

        [Fact]
        public async Task Scenario3_BackupAndRestore()
        {
            // Arrange
            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "E2E_Scenario3_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDir);
            string dbPath = Path.Combine(testDir, "test.db");
            string backupFolder = Path.Combine(testDir, "Backups");
            Directory.CreateDirectory(backupFolder);

            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            var factory = new TestDbContextFactory(options);

            using (var context = new CalQrDbContext(options))
            {
                context.Database.EnsureCreated();
                // Set BackupPath in settings
                context.AppSettings.Add(new AppSetting { Key = "BackupPath", Value = backupFolder });
                await context.SaveChangesAsync();
            }

            // Create some data
            using (var context = new CalQrDbContext(options))
            {
                var owner = new Owner { Name = "Owner Backup" };
                context.Owners.Add(owner);
                await context.SaveChangesAsync();
            }

            var authService = new TestCurrentUserService();
            var auditRepo = new AuditLogRepository(factory, authService);
            var backupService = new BackupService(factory, auditRepo);

            // Act - Perform Backup
            await backupService.BackupNowAsync(backupFolder);

            // Assert backup file created
            var zipFiles = Directory.GetFiles(backupFolder, "CalQR_Backup_*.zip");
            Assert.Single(zipFiles);
            string zipFilePath = zipFiles[0];

            // Wipe database
            using (var context = new CalQrDbContext(options))
            {
                context.Owners.RemoveRange(context.Owners);
                await context.SaveChangesAsync();
                Assert.Empty(await context.Owners.ToListAsync());
            }

            // Perform Restore
            await backupService.RestoreAsync(zipFilePath);

            // Verify Restored data
            using (var context = new CalQrDbContext(options))
            {
                var restoredOwners = await context.Owners.ToListAsync();
                Assert.Single(restoredOwners);
                Assert.Equal("Owner Backup", restoredOwners[0].Name);
            }

            // Cleanup
            try { Directory.Delete(testDir, true); } catch {}
        }

        public void Dispose()
        {
            DeviceDetailViewModel.MessageBoxShowMock = null;
        }
    }
}
