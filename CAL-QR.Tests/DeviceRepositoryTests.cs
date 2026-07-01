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
    public class DeviceRepositoryTests
    {
        [Fact]
        public async Task GetAllAsync_ReturnsOnlyNonDeletedDevices()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase(databaseName: "CalQrTestDb_DeviceRepo_GetAll_" + Guid.NewGuid().ToString())
                .Options;

            var factory = new TestDbContextFactory(options);
            var repo = new DeviceRepository(factory);

            using (var context = new CalQrDbContext(options))
            {
                var owner = new Owner { Name = "Owner A" };
                var type = new DeviceType { Name = "Type A" };
                context.Owners.Add(owner);
                context.DeviceTypes.Add(type);
                await context.SaveChangesAsync();

                context.Devices.Add(new Device { Model = "Model 1", SerialNumber = "SN1", OwnerId = owner.Id, DeviceTypeId = type.Id, IsDeleted = false });
                context.Devices.Add(new Device { Model = "Model 2", SerialNumber = "SN2", OwnerId = owner.Id, DeviceTypeId = type.Id, IsDeleted = true });
                await context.SaveChangesAsync();
            }

            // Act
            var devices = (await repo.GetAllAsync()).ToList();

            // Assert
            Assert.Single(devices);
            Assert.Equal("Model 1", devices[0].Model);
            Assert.False(devices[0].IsDeleted);
        }

        [Fact]
        public async Task SoftDeleteAsync_SetsIsDeletedToTrue()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase(databaseName: "CalQrTestDb_DeviceRepo_SoftDelete_" + Guid.NewGuid().ToString())
                .Options;

            var factory = new TestDbContextFactory(options);
            var repo = new DeviceRepository(factory);
            int deviceId;

            using (var context = new CalQrDbContext(options))
            {
                var owner = new Owner { Name = "Owner A" };
                var type = new DeviceType { Name = "Type A" };
                context.Owners.Add(owner);
                context.DeviceTypes.Add(type);
                await context.SaveChangesAsync();

                var device = new Device { Model = "Model 1", SerialNumber = "SN1", OwnerId = owner.Id, DeviceTypeId = type.Id, IsDeleted = false };
                context.Devices.Add(device);
                await context.SaveChangesAsync();
                deviceId = device.Id;
            }

            // Act
            await repo.SoftDeleteAsync(deviceId);

            // Assert
            using (var context = new CalQrDbContext(options))
            {
                var device = await context.Devices.FindAsync(deviceId);
                Assert.NotNull(device);
                Assert.True(device.IsDeleted);
            }
        }

        [Fact]
        public async Task SearchFiltering_InMemoryDatabase_FiltersDevicesCorrectly()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase(databaseName: "CalQrTestDb_DeviceRepo_Search_" + Guid.NewGuid().ToString())
                .Options;

            using (var context = new CalQrDbContext(options))
            {
                var owner1 = new Owner { Name = "المركز الوطني" };
                var owner2 = new Owner { Name = "مستشفى الهلال" };
                context.Owners.AddRange(owner1, owner2);

                var type = new DeviceType { Name = "جهاز قياس" };
                context.DeviceTypes.Add(type);
                await context.SaveChangesAsync();

                context.Devices.Add(new Device { Model = "Ludlum-3", SerialNumber = "LUD3001", OwnerId = owner1.Id, DeviceTypeId = type.Id, IsDeleted = false });
                context.Devices.Add(new Device { Model = "Ludlum-9", SerialNumber = "LUD9002", OwnerId = owner2.Id, DeviceTypeId = type.Id, IsDeleted = false });
                context.Devices.Add(new Device { Model = "FH40G", SerialNumber = "FH5566", OwnerId = owner1.Id, DeviceTypeId = type.Id, IsDeleted = false });
                await context.SaveChangesAsync();
            }

            // Act & Assert for Model Search
            using (var context = new CalQrDbContext(options))
            {
                var search = "ludlum";
                var results = await context.Devices
                    .Where(d => d.Model.ToLower().Contains(search) || d.SerialNumber.ToLower().Contains(search) || d.Owner!.Name.ToLower().Contains(search))
                    .ToListAsync();
                Assert.Equal(2, results.Count);
                Assert.Contains(results, r => r.Model == "Ludlum-3");
                Assert.Contains(results, r => r.Model == "Ludlum-9");
            }

            // Act & Assert for SerialNumber Search
            using (var context = new CalQrDbContext(options))
            {
                var search = "5566";
                var results = await context.Devices
                    .Where(d => d.Model.ToLower().Contains(search) || d.SerialNumber.ToLower().Contains(search) || d.Owner!.Name.ToLower().Contains(search))
                    .ToListAsync();
                Assert.Single(results);
                Assert.Equal("FH40G", results[0].Model);
            }

            // Act & Assert for OwnerName Search
            using (var context = new CalQrDbContext(options))
            {
                var search = "الهلال";
                var results = await context.Devices
                    .Where(d => d.Model.ToLower().Contains(search) || d.SerialNumber.ToLower().Contains(search) || d.Owner!.Name.ToLower().Contains(search))
                    .ToListAsync();
                Assert.Single(results);
                Assert.Equal("Ludlum-9", results[0].Model);
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
