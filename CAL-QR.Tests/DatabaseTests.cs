using Xunit;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Data;
using CAL_QR.Models;
using System;
using System.Linq;

namespace CAL_QR.Tests
{
    public class DatabaseTests
    {
        [Fact]
        public void Database_ShouldInitializeAndSeedDefaultSettings()
        {
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase(databaseName: "CalQrTestDb_" + Guid.NewGuid().ToString())
                .Options;

            using (var context = new CalQrDbContext(options))
            {
                DatabaseMigrator.RunMigrations(context);

                var settings = context.AppSettings.ToList();
                Assert.NotEmpty(settings);
                Assert.Contains(settings, s => s.Key == "DatabasePath");
                Assert.Contains(settings, s => s.Key == "Language");
                Assert.Contains(settings, s => s.Key == "DateFormat");
            }
        }

        [Fact]
        public void Database_ShouldAllowAddingAndRetrievingModels()
        {
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase(databaseName: "CalQrTestDb_" + Guid.NewGuid().ToString())
                .Options;

            using (var context = new CalQrDbContext(options))
            {
                var owner = new Owner { Name = "Test Owner" };
                var deviceType = new DeviceType { Name = "Geiger Counter" };
                
                context.Owners.Add(owner);
                context.DeviceTypes.Add(deviceType);
                context.SaveChanges();

                var device = new Device
                {
                    Model = "Model-100",
                    SerialNumber = "SN-12345",
                    OwnerId = owner.Id,
                    DeviceTypeId = deviceType.Id
                };

                context.Devices.Add(device);
                context.SaveChanges();

                var record = new CalibrationRecord
                {
                    DeviceId = device.Id,
                    CertificateNumber = "CERT-999",
                    CalibrationDate = DateTime.Today,
                    ExpiryDate = DateTime.Today.AddYears(1),
                    EngineerName = "Edrees",
                    Result = "Passed",
                    HmacSignature = "HMAC1234"
                };

                context.CalibrationRecords.Add(record);
                context.SaveChanges();
            }

            using (var context = new CalQrDbContext(options))
            {
                var devices = context.Devices
                    .Include(d => d.Owner)
                    .Include(d => d.DeviceType)
                    .Include(d => d.CalibrationRecords)
                    .ToList();

                Assert.Single(devices);
                var device = devices.First();
                Assert.Equal("Model-100", device.Model);
                Assert.Equal("Test Owner", device.Owner?.Name);
                Assert.Equal("Geiger Counter", device.DeviceType?.Name);
                Assert.Single(device.CalibrationRecords);
                Assert.Equal("CERT-999", device.CalibrationRecords.First().CertificateNumber);
            }
        }
    }
}