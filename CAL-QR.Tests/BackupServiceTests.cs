using Xunit;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Repositories;
using CAL_QR.Services;

namespace CAL_QR.Tests
{
    public class BackupServiceTests
    {
        [Fact]
        public async Task BackupAndRestore_CreatesZipAndRestoresLive_FilesAndDataExist()
        {
            // 1. Arrange - Setup paths in test execution folder
            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BackupRestoreTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDir);

            string testDbFilePath = Path.Combine(testDir, "test-active.db");
            string testBackupFolder = Path.Combine(testDir, "Backups");
            Directory.CreateDirectory(testBackupFolder);

            // Re-route AppDomain.CurrentDomain.BaseDirectory attachments to test folder if needed, 
            // but since BackupService builds Attachments path using BaseDirectory, 
            // let's physically write to AppDomain.CurrentDomain.BaseDirectory/Attachments for testing,
            // then cleanup.
            string originalAttachmentsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Attachments");
            if (Directory.Exists(originalAttachmentsPath))
            {
                Directory.Delete(originalAttachmentsPath, true);
            }
            Directory.CreateDirectory(originalAttachmentsPath);

            // Create a mock attachment file
            string mockAttachmentFile = Path.Combine(originalAttachmentsPath, "test_file.txt");
            await File.WriteAllTextAsync(mockAttachmentFile, "This is a mock attachment contents.");

            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseSqlite($"Data Source={testDbFilePath}")
                .Options;

            var factory = new TestDbContextFactory(options);

            // Run migration & seed
            using (var context = new CalQrDbContext(options))
            {
                context.Database.EnsureCreated();
                context.AppSettings.Add(new AppSetting { Key = "AlertDaysThreshold", Value = "30" });
                context.AppSettings.Add(new AppSetting { Key = "BackupPath", Value = testBackupFolder });
                context.AppSettings.Add(new AppSetting { Key = "BackupSchedule", Value = "None" });
                await context.SaveChangesAsync();
            }

            var auditLogRepo = new AuditLogRepository(factory);
            var backupService = new BackupService(factory, auditLogRepo);

            try
            {
                // 2. Act - Run Backup
                await backupService.BackupNowAsync(testBackupFolder);

                // 3. Assert Backup created
                var zipFiles = Directory.GetFiles(testBackupFolder, "CalQR_Backup_*.zip");
                Assert.Single(zipFiles);

                string zipFilePath = zipFiles[0];
                Assert.True(File.Exists(zipFilePath));

                // Verify ZIP contents
                using (var archive = ZipFile.OpenRead(zipFilePath))
                {
                    Assert.NotNull(archive.GetEntry("cal-qr.db"));
                    Assert.NotNull(archive.GetEntry("Attachments/test_file.txt"));
                }

                // Delete mock attachment file and modify database to test restore
                File.Delete(mockAttachmentFile);

                using (var context = new CalQrDbContext(options))
                {
                    var settings = await context.AppSettings.ToListAsync();
                    context.AppSettings.RemoveRange(settings);
                    await context.SaveChangesAsync();

                    // Check cleared
                    Assert.Empty(await context.AppSettings.ToListAsync());
                }

                // 4. Act - Run Restore
                await backupService.RestoreAsync(zipFilePath);

                // 5. Assert Restore succeeded
                // Assert DB contents restored
                using (var context = new CalQrDbContext(options))
                {
                    var alertSetting = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == "AlertDaysThreshold");
                    Assert.NotNull(alertSetting);
                    Assert.Equal("30", alertSetting.Value);
                }

                // Assert Attachment restored
                Assert.True(File.Exists(mockAttachmentFile));
                string restoredContent = await File.ReadAllTextAsync(mockAttachmentFile);
                Assert.Equal("This is a mock attachment contents.", restoredContent);
            }
            finally
            {
                // Force release SQLite file locks
                SqliteConnection.ClearAllPools();
                
                // Clean up test directories
                if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
                if (Directory.Exists(originalAttachmentsPath)) Directory.Delete(originalAttachmentsPath, true);
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
