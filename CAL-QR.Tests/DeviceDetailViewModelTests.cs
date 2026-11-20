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
    [Collection("MessageBoxMock")]
    public class DeviceDetailViewModelTests : IDisposable
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
        public async Task LoadDeviceDetails_WithPreferredRecordId_SetsSelectedRecordToPreferred()
        {
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase(databaseName: "CalQrTestDb_DetailsVM_PreferredRecord_" + Guid.NewGuid().ToString())
                .Options;

            var factory = new TestDbContextFactory(options);
            var calRepo = new CalibrationRepository(factory);
            var devRepo = new DeviceRepository(factory);
            var authService = new TestCurrentUserService();

            int deviceId;
            CalibrationRecord recordOlder, recordNewer;

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

                recordOlder = new CalibrationRecord
                {
                    DeviceId = deviceId,
                    CertificateNumber = "CERT-OLDER",
                    CalibrationDate = DateTime.Today.AddMonths(-12),
                    ExpiryDate = DateTime.Today,
                    Result = "Passed",
                    HmacSignature = "HMAC1"
                };
                recordNewer = new CalibrationRecord
                {
                    DeviceId = deviceId,
                    CertificateNumber = "CERT-NEWER",
                    CalibrationDate = DateTime.Today,
                    ExpiryDate = DateTime.Today.AddYears(1),
                    Result = "Passed",
                    HmacSignature = "HMAC2"
                };
                context.CalibrationRecords.AddRange(recordOlder, recordNewer);
                await context.SaveChangesAsync();
            }

            var vm = new DeviceDetailViewModel(factory, calRepo, devRepo, authService, null!, null!);

            // 1. Without preferredRecordId -> defaults to FirstOrDefault (latest)
            await vm.LoadDeviceDetailsAsync(deviceId);
            Assert.Equal("CERT-NEWER", vm.SelectedRecord?.CertificateNumber);

            // 2. With preferredRecordId -> sets SelectedRecord to the specific older record requested
            await vm.LoadDeviceDetailsAsync(deviceId, recordOlder.Id);
            Assert.Equal("CERT-OLDER", vm.SelectedRecord?.CertificateNumber);
        }

        public void Dispose()
        {
            DeviceDetailViewModel.MessageBoxShowMock = null;
        }
    }
}
