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

            string originalAttachmentsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Attachments");
            if (Directory.Exists(originalAttachmentsPath))
            {
                Directory.Delete(originalAttachmentsPath, true);
            }
            Directory.CreateDirectory(originalAttachmentsPath);

            string originalQrOutputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "QR_Output");
            if (Directory.Exists(originalQrOutputPath))
            {
                Directory.Delete(originalQrOutputPath, true);
            }
            Directory.CreateDirectory(originalQrOutputPath);

            // Create a mock attachment file
            string mockAttachmentFile = Path.Combine(originalAttachmentsPath, "test_file.txt");
            await File.WriteAllTextAsync(mockAttachmentFile, "This is a mock attachment contents.");

            // Create a mock QR file
            string mockQrFile = Path.Combine(originalQrOutputPath, "test_qr.png");
            await File.WriteAllTextAsync(mockQrFile, "This is a mock QR image.");

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
                context.AppSettings.Add(new AppSetting { Key = "QrOutputPath", Value = originalQrOutputPath });
                await context.SaveChangesAsync();
            }

            var auditLogRepo = new AuditLogRepository(factory, new TestCurrentUserService());
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
                    Assert.NotNull(archive.GetEntry("QR_Output/test_qr.png"));
                }

                // Delete mock files and modify database to test restore
                File.Delete(mockAttachmentFile);
                File.Delete(mockQrFile);

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

                // Assert QR Output restored
                Assert.True(File.Exists(mockQrFile));
                string restoredQrContent = await File.ReadAllTextAsync(mockQrFile);
                Assert.Equal("This is a mock QR image.", restoredQrContent);
            }
            finally
            {
                // Force release SQLite file locks
                SqliteConnection.ClearAllPools();
                
                // Clean up test directories
                if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
                if (Directory.Exists(originalAttachmentsPath)) Directory.Delete(originalAttachmentsPath, true);
                if (Directory.Exists(originalQrOutputPath)) Directory.Delete(originalQrOutputPath, true);
            }
        }

        [Fact]
        public async Task Restore_OldBackupWithoutQrOutput_RestoresSuccessfully()
        {
            // Arrange
            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OldBackupRestoreTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDir);

            string testDbFilePath = Path.Combine(testDir, "test-active-old.db");
            string testBackupFolder = Path.Combine(testDir, "Backups");
            Directory.CreateDirectory(testBackupFolder);

            string originalAttachmentsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Attachments");
            if (Directory.Exists(originalAttachmentsPath)) Directory.Delete(originalAttachmentsPath, true);
            Directory.CreateDirectory(originalAttachmentsPath);

            string originalQrOutputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "QR_Output");
            if (Directory.Exists(originalQrOutputPath)) Directory.Delete(originalQrOutputPath, true);

            string mockAttachmentFile = Path.Combine(originalAttachmentsPath, "test_file.txt");
            await File.WriteAllTextAsync(mockAttachmentFile, "Attachment content.");

            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseSqlite($"Data Source={testDbFilePath}")
                .Options;

            var factory = new TestDbContextFactory(options);

            using (var context = new CalQrDbContext(options))
            {
                context.Database.EnsureCreated();
                context.AppSettings.Add(new AppSetting { Key = "BackupPath", Value = testBackupFolder });
                context.AppSettings.Add(new AppSetting { Key = "BackupSchedule", Value = "None" });
                context.AppSettings.Add(new AppSetting { Key = "QrOutputPath", Value = originalQrOutputPath });
                await context.SaveChangesAsync();
            }

            var auditLogRepo = new AuditLogRepository(factory, new TestCurrentUserService());
            var backupService = new BackupService(factory, auditLogRepo);

            try
            {
                // Create backup zip using BackupNowAsync (since QR Output folder doesn't exist, it won't be packed)
                await backupService.BackupNowAsync(testBackupFolder);

                var zipFiles = Directory.GetFiles(testBackupFolder, "CalQR_Backup_*.zip");
                Assert.Single(zipFiles);
                string zipFilePath = zipFiles[0];

                // Verify ZIP contents (should not contain QR_Output folder)
                using (var archive = ZipFile.OpenRead(zipFilePath))
                {
                    Assert.NotNull(archive.GetEntry("cal-qr.db"));
                    Assert.NotNull(archive.GetEntry("Attachments/test_file.txt"));
                    Assert.Null(archive.GetEntry("QR_Output/"));
                }

                // Now create the local QR Output directory with a file to verify it won't be deleted during old restore
                Directory.CreateDirectory(originalQrOutputPath);
                string localQrFile = Path.Combine(originalQrOutputPath, "existing_local.png");
                await File.WriteAllTextAsync(localQrFile, "Keep this file.");

                // Clear active db settings to verify restore
                using (var context = new CalQrDbContext(options))
                {
                    context.AppSettings.RemoveRange(await context.AppSettings.ToListAsync());
                    await context.SaveChangesAsync();
                }

                // Act - Restore from the old backup
                await backupService.RestoreAsync(zipFilePath);

                // Assert DB restored
                using (var context = new CalQrDbContext(options))
                {
                    Assert.NotNull(await context.AppSettings.FirstOrDefaultAsync(s => s.Key == "BackupPath"));
                }

                // Assert local QR file remains untouched (backward compatibility)
                Assert.True(File.Exists(localQrFile));
                Assert.Equal("Keep this file.", await File.ReadAllTextAsync(localQrFile));
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
                if (Directory.Exists(originalAttachmentsPath)) Directory.Delete(originalAttachmentsPath, true);
                if (Directory.Exists(originalQrOutputPath)) Directory.Delete(originalQrOutputPath, true);
            }
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        [InlineData("relative/path/backups")]
        public async Task BackupNow_InvalidOrRelativePath_ThrowsArgumentException(string invalidPath)
        {
            // Arrange
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options;

            var factory = new TestDbContextFactory(options);

            using (var context = new CalQrDbContext(options))
            {
                context.Database.EnsureCreated();
            }
            var auditLogRepo = new AuditLogRepository(factory, new TestCurrentUserService());
            var backupService = new BackupService(factory, auditLogRepo);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () => 
                await backupService.BackupNowAsync(invalidPath)
            );
        }

        [Fact]
        public async Task Restore_InvalidArchive_DoesNotAlterActiveFolders()
        {
            // Arrange
            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Test_RestoreFailure_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDir);

            string testDbFilePath = Path.Combine(testDir, "test-active.db");
            string testBackupFolder = Path.Combine(testDir, "Backups");
            Directory.CreateDirectory(testBackupFolder);

            string originalAttachmentsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Attachments");
            if (Directory.Exists(originalAttachmentsPath)) Directory.Delete(originalAttachmentsPath, true);
            Directory.CreateDirectory(originalAttachmentsPath);
            string attachmentFile = Path.Combine(originalAttachmentsPath, "keep_me.txt");
            await File.WriteAllTextAsync(attachmentFile, "Do not delete this!");

            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseSqlite($"Data Source={testDbFilePath}")
                .Options;

            var factory = new TestDbContextFactory(options);
            using (var context = new CalQrDbContext(options))
            {
                context.Database.EnsureCreated();
            }

            var auditLogRepo = new AuditLogRepository(factory, new TestCurrentUserService());
            var backupService = new BackupService(factory, auditLogRepo);

            // Create a completely empty zip file (no db entry)
            string corruptZipPath = Path.Combine(testBackupFolder, "corrupt.zip");
            using (var fs = new FileStream(corruptZipPath, FileMode.Create))
            using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                // No entries added
            }

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () => 
                await backupService.RestoreAsync(corruptZipPath)
            );

            // Assert that the active attachments were NOT deleted
            Assert.True(Directory.Exists(originalAttachmentsPath));
            Assert.True(File.Exists(attachmentFile));
            Assert.Equal("Do not delete this!", await File.ReadAllTextAsync(attachmentFile));

            // Clean up
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
            if (Directory.Exists(originalAttachmentsPath)) Directory.Delete(originalAttachmentsPath, true);
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
