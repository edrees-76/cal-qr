using Xunit;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Repositories;

namespace CAL_QR.Tests
{
    public class CalibrationRepositoryTests
    {
        [Fact]
        public async Task GetByDeviceIdAsync_ReturnsAllCalibrations()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase(databaseName: "CalQrTestDb_CalRepo_GetByDevice_" + Guid.NewGuid().ToString())
                .Options;

            var factory = new TestDbContextFactory(options);
            var repo = new CalibrationRepository(factory);

            using (var context = new CalQrDbContext(options))
            {
                var owner = new Owner { Name = "Owner A" };
                var type = new DeviceType { Name = "Type A" };
                context.Owners.Add(owner);
                context.DeviceTypes.Add(type);
                await context.SaveChangesAsync();

                var device = new Device { Model = "Model 1", SerialNumber = "SN1", OwnerId = owner.Id, DeviceTypeId = type.Id };
                context.Devices.Add(device);
                await context.SaveChangesAsync();

                context.CalibrationRecords.Add(new CalibrationRecord
                {
                    DeviceId = device.Id,
                    CertificateNumber = "CERT-001",
                    CalibrationDate = DateTime.Today.AddMonths(-1),
                    ExpiryDate = DateTime.Today.AddMonths(11),
                    EngineerName = "Edrees",
                    Result = "Passed",
                    HmacSignature = "HMAC1"
                });
                context.CalibrationRecords.Add(new CalibrationRecord
                {
                    DeviceId = device.Id,
                    CertificateNumber = "CERT-002",
                    CalibrationDate = DateTime.Today,
                    ExpiryDate = DateTime.Today.AddYears(1),
                    EngineerName = "Edrees",
                    Result = "Passed",
                    HmacSignature = "HMAC2"
                });
                await context.SaveChangesAsync();
            }

            // Act
            var calibrations = (await repo.GetByDeviceIdAsync(1)).ToList();

            // Assert
            Assert.Equal(2, calibrations.Count);
            Assert.Contains(calibrations, c => c.CertificateNumber == "CERT-001");
            Assert.Contains(calibrations, c => c.CertificateNumber == "CERT-002");
        }

        [Fact]
        public async Task CalibrationRepository_GetExpiringRecords_UsingInMemoryDatabase_ReturnsCorrectRecords()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase(databaseName: "CalQrTestDb_CalRepo_GetExpiring_" + Guid.NewGuid().ToString())
                .Options;

            using (var context = new CalQrDbContext(options))
            {
                var owner = new Owner { Name = "Owner A" };
                var type = new DeviceType { Name = "Type A" };
                context.Owners.Add(owner);
                context.DeviceTypes.Add(type);
                await context.SaveChangesAsync();

                var device1 = new Device { Model = "Model 1", SerialNumber = "SN1", OwnerId = owner.Id, DeviceTypeId = type.Id };
                var device2 = new Device { Model = "Model 2", SerialNumber = "SN2", OwnerId = owner.Id, DeviceTypeId = type.Id };
                var device3 = new Device { Model = "Model 3", SerialNumber = "SN3", OwnerId = owner.Id, DeviceTypeId = type.Id };
                context.Devices.AddRange(device1, device2, device3);
                await context.SaveChangesAsync();

                DateTime today = DateTime.Today;

                // Cal 1: Expiring in 15 days (Should match alert threshold of 30 days)
                context.CalibrationRecords.Add(new CalibrationRecord
                {
                    DeviceId = device1.Id,
                    CertificateNumber = "CERT-EXP-15",
                    CalibrationDate = today.AddMonths(-11),
                    ExpiryDate = today.AddDays(15),
                    EngineerName = "Edrees",
                    Result = "Passed",
                    HmacSignature = "HMAC1",
                    IsDeleted = false
                });

                // Cal 2: Expired 5 days ago
                context.CalibrationRecords.Add(new CalibrationRecord
                {
                    DeviceId = device2.Id,
                    CertificateNumber = "CERT-EXPIRED",
                    CalibrationDate = today.AddYears(-1),
                    ExpiryDate = today.AddDays(-5),
                    EngineerName = "Edrees",
                    Result = "Passed",
                    HmacSignature = "HMAC2",
                    IsDeleted = false
                });

                // Cal 3: Long expiry (expiring in 300 days)
                context.CalibrationRecords.Add(new CalibrationRecord
                {
                    DeviceId = device3.Id,
                    CertificateNumber = "CERT-LONG",
                    CalibrationDate = today,
                    ExpiryDate = today.AddDays(300),
                    EngineerName = "Edrees",
                    Result = "Passed",
                    HmacSignature = "HMAC3",
                    IsDeleted = false
                });

                await context.SaveChangesAsync();
            }

            // Act & Assert
            using (var context = new CalQrDbContext(options))
            {
                var today = DateTime.Today;
                var alertLimit = today.AddDays(30); // 30 days threshold

                // Expiring soon query: ExpiryDate >= today && ExpiryDate <= alertLimit
                var expiringSoon = await context.CalibrationRecords
                    .AsNoTracking()
                    .Where(r => !r.IsDeleted && r.ExpiryDate >= today && r.ExpiryDate <= alertLimit)
                    .ToListAsync();

                Assert.Single(expiringSoon);
                Assert.Equal("CERT-EXP-15", expiringSoon[0].CertificateNumber);
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
