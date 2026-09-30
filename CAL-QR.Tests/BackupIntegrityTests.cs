using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Repositories;
using CAL_QR.Services;

namespace CAL_QR.Tests
{
    public class BackupIntegrityTests
    {
        [Fact]
        public async Task Restore_CorruptDatabaseInBackup_IsRejectedBeforeTouchingLiveData()
        {
            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Test_Integrity_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDir);
            string dbFile = Path.Combine(testDir, "live.db");
            string attachments = Path.Combine(testDir, "Attachments");
            Directory.CreateDirectory(attachments);
            string keep = Path.Combine(attachments, "keep.txt");
            await File.WriteAllTextAsync(keep, "keep");

            var options = new DbContextOptionsBuilder<CalQrDbContext>().UseSqlite($"Data Source={dbFile}").Options;
            var factory = new Factory(options);
            using (var context = new CalQrDbContext(options))
            {
                context.Database.EnsureCreated();
                context.AppSettings.Add(new AppSetting { Key = "AttachmentsPath", Value = attachments });
                await context.SaveChangesAsync();
            }

            string zip = Path.Combine(testDir, "corrupt.zip");
            using (var fs = new FileStream(zip, FileMode.Create))
            using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("cal-qr.db");
                using var stream = entry.Open();
                stream.Write(new byte[4096].AsSpan().ToArray(), 0, 4096);
                var junk = new byte[4096];
                new Random(1).NextBytes(junk);
                stream.Write(junk, 0, junk.Length);
            }

            try
            {
                var service = new BackupService(factory, new AuditLogRepository(factory, new TestCurrentUserService()), new TestBackupPasswordStore());
                await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.RestoreAsync(zip));
                Assert.Equal("keep", await File.ReadAllTextAsync(keep));
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
            }
        }

        [Fact]
        public async Task MaintenanceGate_SerializesConcurrentEntries()
        {
            int inside = 0, maxInside = 0;
            async Task Work()
            {
                using var _ = await MaintenanceGate.EnterAsync();
                int now = System.Threading.Interlocked.Increment(ref inside);
                maxInside = Math.Max(maxInside, now);
                await Task.Delay(20);
                System.Threading.Interlocked.Decrement(ref inside);
            }

            await Task.WhenAll(Work(), Work(), Work());
            Assert.Equal(1, maxInside);
        }

        private sealed class Factory : IDbContextFactory<CalQrDbContext>
        {
            private readonly DbContextOptions<CalQrDbContext> _options;
            public Factory(DbContextOptions<CalQrDbContext> options) => _options = options;
            public CalQrDbContext CreateDbContext() => new CalQrDbContext(_options);
        }
    }
}
