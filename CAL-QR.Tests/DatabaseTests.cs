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

        [Fact]
        public void ConnectionString_WithDefaultTimeout_ShouldBeSupportedAndParseCorrectly()
        {
            string dbPath = "test_timeout.db";
            string connString = $"Data Source={dbPath};Default Timeout=5";

            // Verify builder parses it without throwing
            var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connString);
            Assert.Equal(5, builder.DefaultTimeout);

            // Verify a SqliteConnection can open with it
            using (var connection = new Microsoft.Data.Sqlite.SqliteConnection(connString))
            {
                connection.Open();
                Assert.Equal(System.Data.ConnectionState.Open, connection.State);
                connection.Close();
                Microsoft.Data.Sqlite.SqliteConnection.ClearPool(connection);
            }

            if (System.IO.File.Exists(dbPath))
            {
                System.IO.File.Delete(dbPath);
            }
        }

        [Fact]
        public void DetermineFirstRun_WithDbPathFileAndValidDb_ShouldReturnFalse()
        {
            // Arrange
            string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
            System.IO.Directory.CreateDirectory(tempDir);
            
            try
            {
                string dbPath = System.IO.Path.Combine(tempDir, "migrated.db");
                
                // Create an empty database file in the destination path so that File.Exists(dbPath) returns true
                System.IO.File.WriteAllText(dbPath, "Fake SQLite File Content");
                
                // Write the path to db_path.txt
                string dbPathTxt = System.IO.Path.Combine(tempDir, "db_path.txt");
                System.IO.File.WriteAllText(dbPathTxt, dbPath);
                
                var options = new DbContextOptionsBuilder<CalQrDbContext>()
                    .UseInMemoryDatabase(databaseName: "FirstRunTestDb_" + Guid.NewGuid().ToString())
                    .Options;
                var factory = new TestDbContextFactory(options);

                // Act
                // Since db_path.txt exists and points to an existing file (migrated.db),
                // SplashWindow.DetermineFirstRun should return false (not first run), without even querying the DB.
                bool result = CAL_QR.Views.SplashWindow.DetermineFirstRun(tempDir, factory);

                // Assert
                Assert.False(result);
            }
            finally
            {
                // Clean up temp directory
                if (System.IO.Directory.Exists(tempDir))
                {
                    System.IO.Directory.Delete(tempDir, true);
                }
            }
        }

        [Fact]
        public void DetermineFirstRun_WithNoDbPathFileAndNoDefaultDb_ShouldReturnTrue()
        {
            // Arrange
            string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
            System.IO.Directory.CreateDirectory(tempDir);
            
            try
            {
                var options = new DbContextOptionsBuilder<CalQrDbContext>()
                    .UseInMemoryDatabase(databaseName: "FirstRunTestDb_" + Guid.NewGuid().ToString())
                    .Options;
                var factory = new TestDbContextFactory(options);

                // Act
                // Since neither db_path.txt nor the default DB (cal-qr-simulation.db) exists, it should return true (first run)
                bool result = CAL_QR.Views.SplashWindow.DetermineFirstRun(tempDir, factory);

                // Assert
                Assert.True(result);
            }
            finally
            {
                // Clean up
                if (System.IO.Directory.Exists(tempDir))
                {
                    System.IO.Directory.Delete(tempDir, true);
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
        }
    }
}