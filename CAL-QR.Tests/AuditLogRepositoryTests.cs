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
    public class AuditLogRepositoryTests
    {
        [Fact]
        public async Task AuditLogRepository_AddAndGetLogs_WritesAndSortsCorrectly()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase(databaseName: "CalQrTestDb_Audit_" + Guid.NewGuid().ToString())
                .Options;

            var factory = new TestDbContextFactory(options);
            var repo = new AuditLogRepository(factory, new TestCurrentUserService());

            // Act - Write logs
            await repo.LogAsync("إضافة معايرة", "CalibrationRecord", "1", "تمت إضافة سجل 1");
            await Task.Delay(10); // Ensure timestamp difference
            await repo.LogAsync("تصدير تقرير", "نظام", "Reports", "تصدير تقرير Excel");

            // Assert
            var logs = (await repo.GetAllAsync()).ToList();

            Assert.Equal(2, logs.Count);
            
            // Should be sorted descending by ActionAt (newest first)
            Assert.Equal("تصدير تقرير", logs[0].Action);
            Assert.Equal("إضافة معايرة", logs[1].Action);

            Assert.Equal("Reports", logs[0].EntityId);
            Assert.Equal("1", logs[1].EntityId);
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
