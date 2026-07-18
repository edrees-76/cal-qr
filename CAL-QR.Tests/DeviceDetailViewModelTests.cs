using Xunit;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Repositories;
using CAL_QR.ViewModels;
using CAL_QR.Services;

#pragma warning disable CS1998

namespace CAL_QR.Tests
{
    public class DeviceDetailViewModelTests
    {
        public DeviceDetailViewModelTests()
        {
            // Intercept message box to prevent blocking headless test runners
            DeviceDetailViewModel.MessageBoxShowMock = (msg, caption, btn, img) => MessageBoxResult.Yes;
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

        [Fact]
        public async Task DeleteRecordCommand_CanExecute_ReturnsFalse_WhenUserIsNotAdmin()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase(databaseName: "CalQrTestDb_DetailsVM_CanExecute_" + Guid.NewGuid().ToString())
                .Options;

            var factory = new TestDbContextFactory(options);
            var calRepo = new CalibrationRepository(factory);
            var devRepo = new DeviceRepository(factory);
            var authService = new TestCurrentUserService();
            var auditRepo = new AuditLogRepository(factory, authService);

            // User is not admin (role = User)
            authService.SetCurrentUser(new User { Username = "editor", Role = UserRole.User });

            var vm = new DeviceDetailViewModel(factory, calRepo, devRepo, authService, auditRepo);

            // Act
            bool canDelete = vm.DeleteRecordCommand.CanExecute(new CalibrationRecord());

            // Assert
            Assert.False(canDelete);
        }

        [Fact]
        public async Task DeleteRecordCommand_CanExecute_ReturnsTrue_WhenUserIsAdmin()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase(databaseName: "CalQrTestDb_DetailsVM_CanExecuteAdmin_" + Guid.NewGuid().ToString())
                .Options;

            var factory = new TestDbContextFactory(options);
            var calRepo = new CalibrationRepository(factory);
            var devRepo = new DeviceRepository(factory);
            var authService = new TestCurrentUserService();
            var auditRepo = new AuditLogRepository(factory, authService);

            // User is admin
            authService.SetCurrentUser(new User { Username = "admin", Role = UserRole.Admin });

            var vm = new DeviceDetailViewModel(factory, calRepo, devRepo, authService, auditRepo);

            // Act
            bool canDelete = vm.DeleteRecordCommand.CanExecute(new CalibrationRecord());

            // Assert
            Assert.True(canDelete);
        }

        [Fact]
        public async Task DeleteRecordCommand_Execute_IsBlocked_WhenOnlyOneRecordRemaining()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase(databaseName: "CalQrTestDb_DetailsVM_BlockLast_" + Guid.NewGuid().ToString())
                .Options;

            var factory = new TestDbContextFactory(options);
            var calRepo = new CalibrationRepository(factory);
            var devRepo = new DeviceRepository(factory);
            var authService = new TestCurrentUserService();
            var auditRepo = new AuditLogRepository(factory, authService);

            authService.SetCurrentUser(new User { Username = "admin", Role = UserRole.Admin });

            int deviceId;
            CalibrationRecord record;

            using (var context = new CalQrDbContext(options))
            {
                var owner = new Owner { Name = "Owner A" };
                var type = new DeviceType { Name = "Type A" };
                context.Owners.Add(owner);
                context.DeviceTypes.Add(type);
                await context.SaveChangesAsync();

                var device = new Device { Model = "Ludlum", SerialNumber = "SN100", OwnerId = owner.Id, DeviceTypeId = type.Id };
                context.Devices.Add(device);
                await context.SaveChangesAsync();
                deviceId = device.Id;

                record = new CalibrationRecord
                {
                    DeviceId = deviceId,
                    CertificateNumber = "CERT-LAST",
                    CalibrationDate = DateTime.Today,
                    ExpiryDate = DateTime.Today.AddYears(1),
                    Result = "Passed",
                    HmacSignature = "HMAC"
                };
                context.CalibrationRecords.Add(record);
                await context.SaveChangesAsync();
            }

            var vm = new DeviceDetailViewModel(factory, calRepo, devRepo, authService, auditRepo);
            vm.LoadDeviceDetails(deviceId);

            bool dialogShown = false;
            DeviceDetailViewModel.MessageBoxShowMock = (msg, caption, btn, img) =>
            {
                if (caption == "تنبيه الحماية")
                {
                    dialogShown = true;
                }
                return MessageBoxResult.OK;
            };

            // Act
            vm.DeleteRecordCommand.Execute(vm.Calibrations.First());

            // Assert: record is NOT deleted, and protection warning dialog is displayed
            using (var context = new CalQrDbContext(options))
            {
                var dbRecord = await context.CalibrationRecords.FindAsync(record.Id);
                Assert.NotNull(dbRecord);
                Assert.False(dbRecord.IsDeleted);
            }
            Assert.True(dialogShown);
        }

        [Fact]
        public async Task DeleteRecordCommand_Execute_Succeeds_WhenMultipleRecordsExist()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase(databaseName: "CalQrTestDb_DetailsVM_DeleteSuccess_" + Guid.NewGuid().ToString())
                .Options;

            var factory = new TestDbContextFactory(options);
            var calRepo = new CalibrationRepository(factory);
            var devRepo = new DeviceRepository(factory);
            var authService = new TestCurrentUserService();
            var auditRepo = new AuditLogRepository(factory, authService);

            authService.SetCurrentUser(new User { Username = "admin", Role = UserRole.Admin });

            int deviceId;
            CalibrationRecord record1;
            CalibrationRecord record2;

            using (var context = new CalQrDbContext(options))
            {
                var owner = new Owner { Name = "Owner A" };
                var type = new DeviceType { Name = "Type A" };
                context.Owners.Add(owner);
                context.DeviceTypes.Add(type);
                await context.SaveChangesAsync();

                var device = new Device { Model = "Ludlum", SerialNumber = "SN100", OwnerId = owner.Id, DeviceTypeId = type.Id };
                context.Devices.Add(device);
                await context.SaveChangesAsync();
                deviceId = device.Id;

                record1 = new CalibrationRecord
                {
                    DeviceId = deviceId,
                    CertificateNumber = "CERT-001",
                    CalibrationDate = DateTime.Today.AddMonths(-1),
                    ExpiryDate = DateTime.Today.AddMonths(11),
                    Result = "Passed",
                    HmacSignature = "HMAC1"
                };
                record2 = new CalibrationRecord
                {
                    DeviceId = deviceId,
                    CertificateNumber = "CERT-002",
                    CalibrationDate = DateTime.Today,
                    ExpiryDate = DateTime.Today.AddYears(1),
                    Result = "Passed",
                    HmacSignature = "HMAC2"
                };
                context.CalibrationRecords.AddRange(record1, record2);
                await context.SaveChangesAsync();
            }

            var vm = new DeviceDetailViewModel(factory, calRepo, devRepo, authService, auditRepo);
            vm.LoadDeviceDetails(deviceId);

            bool savedEventRaised = false;
            vm.Saved += (s, e) => savedEventRaised = true;

            DeviceDetailViewModel.MessageBoxShowMock = (msg, caption, btn, img) => MessageBoxResult.Yes;

            // Act
            vm.DeleteRecordCommand.Execute(vm.Calibrations.First(c => c.CertificateNumber == "CERT-001"));

            // Assert
            using (var context = new CalQrDbContext(options))
            {
                var dbRecord1 = await context.CalibrationRecords.FindAsync(record1.Id);
                var dbRecord2 = await context.CalibrationRecords.FindAsync(record2.Id);

                Assert.NotNull(dbRecord1);
                Assert.True(dbRecord1.IsDeleted);

                Assert.NotNull(dbRecord2);
                Assert.False(dbRecord2.IsDeleted);
            }
            Assert.True(savedEventRaised);
            Assert.Single(vm.Calibrations);
            Assert.Equal("CERT-002", vm.Calibrations[0].CertificateNumber);
        }
    }
}
