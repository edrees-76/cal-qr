using Xunit;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Repositories;
using CAL_QR.Services;
using CAL_QR.ViewModels;

#pragma warning disable CS8625

namespace CAL_QR.Tests
{
    public class PerformanceTests
    {
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
        public async Task LoadDataAsync_PerformanceTest_WithLargeScaleData()
        {
            // 1. Arrange - Setup database file in a unique temp directory
            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PerfTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDir);
            string dbPath = Path.Combine(testDir, "perf-test.db");

            // 2. Generate 500 devices and 2000 calibration records
            var simulation = new DataSimulationTests();
            await simulation.RunSimulationAsync(dbPath, numDevices: 500, numRecords: 2000);

            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            var factory = new TestDbContextFactory(options);
            var deviceRepo = new DeviceRepository(factory);
            var ownerRepo = new OwnerRepository(factory);
            var typeRepo = new DeviceTypeRepository(factory);
            var authService = new TestCurrentUserService();
            authService.SetCurrentUser(new User { Username = "admin", Role = UserRole.Admin });
            var auditRepo = new AuditLogRepository(factory, authService);
            var qrService = new QrService(factory);
            var templateRepo = new PaperTemplateRepository(factory);

            var vm = new DevicesViewModel(
                deviceRepo,
                factory,
                ownerRepo,
                typeRepo,
                auditRepo,
                qrService,
                null, // printService
                templateRepo,
                null, // deviceDetailDialogFactory
                null, // calibrationFormDialogFactory
                null, // certificateFormDialogFactory
                authService
            );

            // Set PageSize to 2000 to measure load time of the entire dataset at once (worst-case scenario)
            vm.PageSize = 2000;

            // 3. Act - Measure load time of LoadDataAsync()
            var stopwatch = Stopwatch.StartNew();
            await vm.LoadDataAsync();
            stopwatch.Stop();

            long elapsedMs = stopwatch.ElapsedMilliseconds;
            Console.WriteLine($"[Performance Test] Loaded {vm.TotalCount} records in {elapsedMs} ms.");

            // 4. Assert - Must load within 2 seconds (2000 milliseconds)
            Assert.True(elapsedMs < 2000, $"LoadDataAsync took too long: {elapsedMs} ms (expected < 2000 ms).");

            // Cleanup
            try { Directory.Delete(testDir, true); } catch {}
        }
    }
}
