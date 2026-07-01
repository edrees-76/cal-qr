using Xunit;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Repositories;
using CAL_QR.ViewModels;

namespace CAL_QR.Tests
{
    public class DashboardViewModelTests
    {
        [Fact]
        public async Task DashboardStatistics_Calculation_IsCorrect()
        {
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase(databaseName: "CalQrTestDb_Dashboard_" + Guid.NewGuid().ToString())
                .Options;

            var factory = new TestDbContextFactory(options);

            using (var context = new CalQrDbContext(options))
            {
                context.AppSettings.Add(new AppSetting { Key = "AlertDaysThreshold", Value = "30" });

                var owner = new Owner { Name = "مركز البحوث" };
                context.Owners.Add(owner);

                var type = new DeviceType { Name = "جهاز مسح" };
                context.DeviceTypes.Add(type);
                await context.SaveChangesAsync();

                var d1 = new Device { Model = "ValidModel", SerialNumber = "SN001", OwnerId = owner.Id, DeviceTypeId = type.Id };
                context.Devices.Add(d1);

                var d2 = new Device { Model = "WarningModel", SerialNumber = "SN002", OwnerId = owner.Id, DeviceTypeId = type.Id };
                context.Devices.Add(d2);

                var d3 = new Device { Model = "ExpiredModel", SerialNumber = "SN003", OwnerId = owner.Id, DeviceTypeId = type.Id };
                context.Devices.Add(d3);

                var d4 = new Device { Model = "NoCalModel", SerialNumber = "SN004", OwnerId = owner.Id, DeviceTypeId = type.Id };
                context.Devices.Add(d4);

                await context.SaveChangesAsync();

                DateTime today = DateTime.Today;

                context.CalibrationRecords.Add(new CalibrationRecord
                {
                    DeviceId = d1.Id,
                    CertificateNumber = "C-001",
                    CalibrationDate = today.AddDays(-10),
                    ExpiryDate = today.AddYears(1),
                    Result = "سار",
                    EngineerName = "إدريس"
                });

                context.CalibrationRecords.Add(new CalibrationRecord
                {
                    DeviceId = d2.Id,
                    CertificateNumber = "C-002",
                    CalibrationDate = today.AddDays(-5),
                    ExpiryDate = today.AddDays(15),
                    Result = "سار",
                    EngineerName = "إدريس"
                });

                context.CalibrationRecords.Add(new CalibrationRecord
                {
                    DeviceId = d3.Id,
                    CertificateNumber = "C-003",
                    CalibrationDate = today.AddYears(-2),
                    ExpiryDate = today.AddDays(-10),
                    Result = "غير سار",
                    EngineerName = "إدريس"
                });

                await context.SaveChangesAsync();
            }

            var deviceRepo = new DeviceRepository(factory);
            var calRepo = new CalibrationRepository(factory);
            using (var vm = new DashboardViewModel(factory, deviceRepo, calRepo))
            {
                await vm.LoadDataAsync();

                Assert.Equal(4, vm.TotalDevices);
                Assert.Equal(1, vm.ValidDevices);
                Assert.Equal(1, vm.ExpiringSoonDevices);
                Assert.Equal(2, vm.ExpiredDevices);
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
        }
    }
}
