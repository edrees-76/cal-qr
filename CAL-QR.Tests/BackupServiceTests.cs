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

            string originalAttachmentsPath = Path.Combine(testDir, "Attachments");
            Directory.CreateDirectory(originalAttachmentsPath);

            string originalQrOutputPath = Path.Combine(testDir, "QR_Output");
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
                context.AppSettings.Add(new AppSetting { Key = "AttachmentsPath", Value = originalAttachmentsPath });
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
                    var alertSetting = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == "AlertDaysThreshold");
                    if (alertSetting != null)
                    {
                        context.AppSettings.Remove(alertSetting);
                        await context.SaveChangesAsync();
                    }

                    // Check cleared
                    Assert.Null(await context.AppSettings.FirstOrDefaultAsync(s => s.Key == "AlertDaysThreshold"));
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
            }
        }

        [Fact]
        public async Task BackupAndRestore_RevertsUserTableToBackupTimestamp_IncludingPasswordAndNewUserRemoval()
        {
            // 1. Arrange - Setup paths in test execution folder
            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BackupRestoreUserTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDir);

            string testDbFilePath = Path.Combine(testDir, "test-active-user.db");
            string testBackupFolder = Path.Combine(testDir, "Backups");
            Directory.CreateDirectory(testBackupFolder);

            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseSqlite($"Data Source={testDbFilePath}")
                .Options;

            var factory = new TestDbContextFactory(options);

            // Ensure DB created and seed original user
            string initialPasswordHash = BCrypt.Net.BCrypt.HashPassword("InitialAdminPassword");
            using (var context = new CalQrDbContext(options))
            {
                context.Database.EnsureCreated();
                
                // Add Admin user
                var admin = new User
                {
                    Username = "admin_test",
                    PasswordHash = initialPasswordHash,
                    FullName = "Administrator Test",
                    Role = UserRole.Admin,
                    Permissions = (SystemPermissions)255,
                    IsEditor = true,
                    IsActive = true
                };
                context.Users.Add(admin);

                context.AppSettings.Add(new AppSetting { Key = "BackupPath", Value = testBackupFolder });
                context.AppSettings.Add(new AppSetting { Key = "BackupSchedule", Value = "None" });
                await context.SaveChangesAsync();
            }

            var auditLogRepo = new AuditLogRepository(factory, new TestCurrentUserService());
            var backupService = new BackupService(factory, auditLogRepo);

            try
            {
                // 2. Act 1 - Run Backup
                await backupService.BackupNowAsync(testBackupFolder);

                var zipFiles = Directory.GetFiles(testBackupFolder, "CalQR_Backup_*.zip");
                Assert.Single(zipFiles);
                string zipFilePath = zipFiles[0];
                Assert.True(File.Exists(zipFilePath));

                // 3. Act 2 - Simulate subsequent changes (After Backup)
                string postBackupPasswordHash = BCrypt.Net.BCrypt.HashPassword("PostBackupPassword");
                using (var context = new CalQrDbContext(options))
                {
                    // Modify PasswordHash of User 1 (Admin)
                    var adminUser = await context.Users.FirstAsync(u => u.Username == "admin_test");
                    adminUser.PasswordHash = postBackupPasswordHash;

                    // Add a second new user
                    var newUser = new User
                    {
                        Username = "new_user_post_backup",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("NewUserPassword"),
                        FullName = "Post Backup Added User",
                        Role = UserRole.User,
                        Permissions = SystemPermissions.None,
                        IsEditor = false,
                        IsActive = true
                    };
                    context.Users.Add(newUser);
                    await context.SaveChangesAsync();
                }

                // Verify the changes actually exist in the active database before restoring
                using (var context = new CalQrDbContext(options))
                {
                    var adminUser = await context.Users.FirstAsync(u => u.Username == "admin_test");
                    Assert.Equal(postBackupPasswordHash, adminUser.PasswordHash);

                    var hasNewUser = await context.Users.AnyAsync(u => u.Username == "new_user_post_backup");
                    Assert.True(hasNewUser);
                }

                // 4. Act 3 - Run Restore
                await backupService.RestoreAsync(zipFilePath);

                // 5. Assert - Reverts to the backup timestamp state
                using (var context = new CalQrDbContext(options))
                {
                    // Assert User 1 (Admin) password hash reverted to initial state
                    var adminUser = await context.Users.FirstOrDefaultAsync(u => u.Username == "admin_test");
                    Assert.NotNull(adminUser);
                    Assert.Equal(initialPasswordHash, adminUser.PasswordHash);

                    // Assert User 2 (added after backup) is completely deleted/removed
                    var postBackupUser = await context.Users.FirstOrDefaultAsync(u => u.Username == "new_user_post_backup");
                    Assert.Null(postBackupUser);

                    // Total users count should be 1
                    var totalUsers = await context.Users.CountAsync();
                    Assert.Equal(1, totalUsers);
                }
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                if (Directory.Exists(testDir))
                {
                    Directory.Delete(testDir, true);
                }
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

            string originalAttachmentsPath = Path.Combine(testDir, "Attachments");
            Directory.CreateDirectory(originalAttachmentsPath);

            string originalQrOutputPath = Path.Combine(testDir, "QR_Output");

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
                context.AppSettings.Add(new AppSetting { Key = "AttachmentsPath", Value = originalAttachmentsPath });
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
                    var backupPathSetting = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == "BackupPath");
                    if (backupPathSetting != null)
                    {
                        context.AppSettings.Remove(backupPathSetting);
                        await context.SaveChangesAsync();
                    }
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

            string originalAttachmentsPath = Path.Combine(testDir, "Attachments");
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
                context.AppSettings.Add(new AppSetting { Key = "AttachmentsPath", Value = originalAttachmentsPath });
                await context.SaveChangesAsync();
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
        }

        [Fact]
        public async Task BackupNowAsync_WithEmptyCloudBackupPath_DoesNotAttemptCloudCopy()
        {
            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BackupCloudEmpty_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDir);
            string testDbFilePath = Path.Combine(testDir, "test.db");
            string testBackupFolder = Path.Combine(testDir, "Backups");
            Directory.CreateDirectory(testBackupFolder);

            var options = new DbContextOptionsBuilder<CalQrDbContext>().UseSqlite($"Data Source={testDbFilePath}").Options;
            var factory = new TestDbContextFactory(options);

            using (var context = new CalQrDbContext(options))
            {
                context.Database.EnsureCreated();
                context.AppSettings.Add(new AppSetting { Key = "CloudBackupPath", Value = "" });
                await context.SaveChangesAsync();
            }

            var auditLogRepo = new AuditLogRepository(factory, new TestCurrentUserService());
            var backupService = new BackupService(factory, auditLogRepo);

            bool result = await backupService.BackupNowAsync(testBackupFolder);

            Assert.True(result);
            var zipFiles = Directory.GetFiles(testBackupFolder, "CalQR_Backup_*.zip");
            Assert.Single(zipFiles);

            SqliteConnection.ClearAllPools();
            if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
        }

        [Fact]
        public async Task BackupNowAsync_WithValidCloudBackupPath_CopiesZipToCloudPathSuccessfully()
        {
            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BackupCloudValid_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDir);
            string testDbFilePath = Path.Combine(testDir, "test.db");
            string testBackupFolder = Path.Combine(testDir, "Backups");
            string testCloudFolder = Path.Combine(testDir, "CloudBackups");
            Directory.CreateDirectory(testBackupFolder);
            Directory.CreateDirectory(testCloudFolder);

            var options = new DbContextOptionsBuilder<CalQrDbContext>().UseSqlite($"Data Source={testDbFilePath}").Options;
            var factory = new TestDbContextFactory(options);

            using (var context = new CalQrDbContext(options))
            {
                context.Database.EnsureCreated();
                context.AppSettings.Add(new AppSetting { Key = "CloudBackupPath", Value = testCloudFolder });
                await context.SaveChangesAsync();
            }

            var auditLogRepo = new AuditLogRepository(factory, new TestCurrentUserService());
            var backupService = new BackupService(factory, auditLogRepo);

            bool result = await backupService.BackupNowAsync(testBackupFolder);

            Assert.True(result);
            var localZipFiles = Directory.GetFiles(testBackupFolder, "CalQR_Backup_*.zip");
            var cloudZipFiles = Directory.GetFiles(testCloudFolder, "CalQR_Backup_*.zip");
            Assert.Single(localZipFiles);
            Assert.Single(cloudZipFiles);
            Assert.Equal(Path.GetFileName(localZipFiles[0]), Path.GetFileName(cloudZipFiles[0]));

            SqliteConnection.ClearAllPools();
            if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
        }

        [Fact]
        public async Task BackupNowAsync_WithInvalidCloudBackupPath_DoesNotFailLocalBackup()
        {
            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BackupCloudInvalid_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDir);
            string testDbFilePath = Path.Combine(testDir, "test.db");
            string testBackupFolder = Path.Combine(testDir, "Backups");
            Directory.CreateDirectory(testBackupFolder);

            var options = new DbContextOptionsBuilder<CalQrDbContext>().UseSqlite($"Data Source={testDbFilePath}").Options;
            var factory = new TestDbContextFactory(options);

            using (var context = new CalQrDbContext(options))
            {
                context.Database.EnsureCreated();
                context.AppSettings.Add(new AppSetting { Key = "CloudBackupPath", Value = "relative/cloud/path" });
                await context.SaveChangesAsync();
            }

            var auditLogRepo = new AuditLogRepository(factory, new TestCurrentUserService());
            var backupService = new BackupService(factory, auditLogRepo);

            bool result = await backupService.BackupNowAsync(testBackupFolder);

            Assert.False(result); // Cloud copy failed, but local backup succeeded
            var localZipFiles = Directory.GetFiles(testBackupFolder, "CalQR_Backup_*.zip");
            Assert.Single(localZipFiles);

            SqliteConnection.ClearAllPools();
            if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
        }

        [Fact]
        public async Task BackupNowAsync_KeepsOnlyLast10BackupsInCloudPathToo()
        {
            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BackupCloudLimit_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDir);
            string testDbFilePath = Path.Combine(testDir, "test.db");
            string testBackupFolder = Path.Combine(testDir, "Backups");
            string testCloudFolder = Path.Combine(testDir, "CloudBackups");
            Directory.CreateDirectory(testBackupFolder);
            Directory.CreateDirectory(testCloudFolder);

            // Pre-populate 12 old backup files in cloud folder
            DateTime baseTime = DateTime.Now.AddDays(-20);
            for (int i = 0; i < 12; i++)
            {
                string oldFile = Path.Combine(testCloudFolder, $"CalQR_Backup_2026-01-01_10-{i:D2}.zip");
                await File.WriteAllTextAsync(oldFile, "mock backup");
                File.SetCreationTime(oldFile, baseTime.AddMinutes(i));
            }

            var options = new DbContextOptionsBuilder<CalQrDbContext>().UseSqlite($"Data Source={testDbFilePath}").Options;
            var factory = new TestDbContextFactory(options);

            using (var context = new CalQrDbContext(options))
            {
                context.Database.EnsureCreated();
                context.AppSettings.Add(new AppSetting { Key = "CloudBackupPath", Value = testCloudFolder });
                await context.SaveChangesAsync();
            }

            var auditLogRepo = new AuditLogRepository(factory, new TestCurrentUserService());
            var backupService = new BackupService(factory, auditLogRepo);

            bool result = await backupService.BackupNowAsync(testBackupFolder);

            Assert.True(result);
            var cloudZipFiles = Directory.GetFiles(testCloudFolder, "CalQR_Backup_*.zip");
            Assert.Equal(10, cloudZipFiles.Length);

            SqliteConnection.ClearAllPools();
            if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
        }



        [Fact]
        public async Task RestoreAsync_KeepsThisMachinePathSettings()
        {
            // Arrange
            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RestoreKeepsPaths_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDir);

            string testDbFilePath = Path.Combine(testDir, "test-active.db");
            string testBackupFolder = Path.Combine(testDir, "Backups");
            Directory.CreateDirectory(testBackupFolder);

            string attachmentsPathA = Path.Combine(testDir, "Attachments_A");
            Directory.CreateDirectory(attachmentsPathA);
            string qrOutputPathA = Path.Combine(testDir, "QR_Output_A");
            Directory.CreateDirectory(qrOutputPathA);
            string backupPathA = Path.Combine(testDir, "Backup_A");
            string cloudBackupPathA = Path.Combine(testDir, "Cloud_A");

            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseSqlite($"Data Source={testDbFilePath}")
                .Options;

            var factory = new TestDbContextFactory(options);

            using (var context = new CalQrDbContext(options))
            {
                context.Database.EnsureCreated();
                context.AppSettings.Add(new AppSetting { Key = "AttachmentsPath", Value = attachmentsPathA });
                context.AppSettings.Add(new AppSetting { Key = "QrOutputPath", Value = qrOutputPathA });
                context.AppSettings.Add(new AppSetting { Key = "BackupPath", Value = backupPathA });
                context.AppSettings.Add(new AppSetting { Key = "CloudBackupPath", Value = cloudBackupPathA });
                await context.SaveChangesAsync();
            }

            var auditLogRepo = new AuditLogRepository(factory, new TestCurrentUserService());
            var backupService = new BackupService(factory, auditLogRepo);

            try
            {
                // Act 1 - Backup while settings point at the "A" machine paths
                await backupService.BackupNowAsync(testBackupFolder);
                var zipFiles = Directory.GetFiles(testBackupFolder, "CalQR_Backup_*.zip");
                Assert.Single(zipFiles);
                string zipFilePath = zipFiles[0];

                // Change the live settings to "B" machine paths before restoring
                string attachmentsPathB = Path.Combine(testDir, "Attachments_B");
                Directory.CreateDirectory(attachmentsPathB);
                string qrOutputPathB = Path.Combine(testDir, "QR_Output_B");
                Directory.CreateDirectory(qrOutputPathB);
                string backupPathB = Path.Combine(testDir, "Backup_B");
                string cloudBackupPathB = Path.Combine(testDir, "Cloud_B");

                using (var context = new CalQrDbContext(options))
                {
                    (await context.AppSettings.FirstAsync(s => s.Key == "AttachmentsPath")).Value = attachmentsPathB;
                    (await context.AppSettings.FirstAsync(s => s.Key == "QrOutputPath")).Value = qrOutputPathB;
                    (await context.AppSettings.FirstAsync(s => s.Key == "BackupPath")).Value = backupPathB;
                    (await context.AppSettings.FirstAsync(s => s.Key == "CloudBackupPath")).Value = cloudBackupPathB;

                    // الشاهد أُضيف بعد النسخة فليس فيها. غيابه بعد الاستعادة يثبت أنّ
                    // القاعدة استُبدلت حقًّا، فلا يمرّ الاختبار لمجرّد أنّ الاستعادة لم تفعل شيئًا.
                    context.AppSettings.Add(new AppSetting { Key = "MarkerAddedAfterBackup", Value = "1" });

                    await context.SaveChangesAsync();
                }

                // Act 2 - Restore
                await backupService.RestoreAsync(zipFilePath);

                // Assert - the "B" machine paths survive the restore, not the backed-up "A" ones
                using (var context = new CalQrDbContext(options))
                {
                    Assert.Equal(attachmentsPathB, (await context.AppSettings.FirstAsync(s => s.Key == "AttachmentsPath")).Value);
                    Assert.Equal(qrOutputPathB, (await context.AppSettings.FirstAsync(s => s.Key == "QrOutputPath")).Value);
                    Assert.Equal(backupPathB, (await context.AppSettings.FirstAsync(s => s.Key == "BackupPath")).Value);
                    Assert.Equal(cloudBackupPathB, (await context.AppSettings.FirstAsync(s => s.Key == "CloudBackupPath")).Value);
                    Assert.Null(await context.AppSettings.FirstOrDefaultAsync(s => s.Key == "MarkerAddedAfterBackup"));
                }
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
            }
        }

        [Fact]
        public async Task RestoreAsync_WithoutAttachmentsInBackup_KeepsExistingFiles()
        {
            // Arrange
            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RestoreNoAttachmentsInZip_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDir);

            string testDbFilePath = Path.Combine(testDir, "test-active.db");
            string testBackupFolder = Path.Combine(testDir, "Backups");
            Directory.CreateDirectory(testBackupFolder);

            string attachmentsPath = Path.Combine(testDir, "Attachments");
            Directory.CreateDirectory(attachmentsPath); // empty at backup time

            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseSqlite($"Data Source={testDbFilePath}")
                .Options;

            var factory = new TestDbContextFactory(options);

            using (var context = new CalQrDbContext(options))
            {
                context.Database.EnsureCreated();
                context.AppSettings.Add(new AppSetting { Key = "AttachmentsPath", Value = attachmentsPath });
                await context.SaveChangesAsync();
            }

            var auditLogRepo = new AuditLogRepository(factory, new TestCurrentUserService());
            var backupService = new BackupService(factory, auditLogRepo);

            try
            {
                // Act 1 - Backup an empty Attachments folder, so the zip has no "Attachments/" entries
                await backupService.BackupNowAsync(testBackupFolder);
                var zipFiles = Directory.GetFiles(testBackupFolder, "CalQR_Backup_*.zip");
                Assert.Single(zipFiles);
                string zipFilePath = zipFiles[0];

                using (var archive = ZipFile.OpenRead(zipFilePath))
                {
                    Assert.DoesNotContain(archive.Entries, e => e.FullName.StartsWith("Attachments/", StringComparison.OrdinalIgnoreCase));
                }

                // Now a file appears in the live Attachments folder after that backup was taken
                string existingFile = Path.Combine(attachmentsPath, "existing.txt");
                await File.WriteAllTextAsync(existingFile, "keep me");

                // Act 2 - Restore from the backup that never had any attachments
                await backupService.RestoreAsync(zipFilePath);

                // Assert - the file was not wiped out just because the backup had no attachments
                Assert.True(File.Exists(existingFile));
                Assert.Equal("keep me", await File.ReadAllTextAsync(existingFile));
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
            }
        }

        [Fact]
        public async Task RestoreAsync_RoundTripsNestedSignedCopyFolder()
        {
            // Arrange
            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RestoreNestedSignedCopy_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDir);

            string testDbFilePath = Path.Combine(testDir, "test-active.db");
            string testBackupFolder = Path.Combine(testDir, "Backups");
            Directory.CreateDirectory(testBackupFolder);

            string attachmentsPath = Path.Combine(testDir, "Attachments");
            string nestedSignedDir = Path.Combine(attachmentsPath, "7", "signed");
            Directory.CreateDirectory(nestedSignedDir);
            string nestedSignedFile = Path.Combine(nestedSignedDir, "signed_copy.pdf");
            await File.WriteAllTextAsync(nestedSignedFile, "signed pdf bytes");

            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseSqlite($"Data Source={testDbFilePath}")
                .Options;

            var factory = new TestDbContextFactory(options);

            using (var context = new CalQrDbContext(options))
            {
                context.Database.EnsureCreated();
                context.AppSettings.Add(new AppSetting { Key = "AttachmentsPath", Value = attachmentsPath });
                await context.SaveChangesAsync();
            }

            var auditLogRepo = new AuditLogRepository(factory, new TestCurrentUserService());
            var backupService = new BackupService(factory, auditLogRepo);

            try
            {
                // Act 1 - Backup the nested signed-copy file
                await backupService.BackupNowAsync(testBackupFolder);
                var zipFiles = Directory.GetFiles(testBackupFolder, "CalQR_Backup_*.zip");
                Assert.Single(zipFiles);
                string zipFilePath = zipFiles[0];

                // Wipe the whole Attachments folder, as if the machine lost it
                Directory.Delete(attachmentsPath, true);

                // Act 2 - Restore
                await backupService.RestoreAsync(zipFilePath);

                // Assert - the file comes back at the same nested relative path with the same content
                Assert.True(File.Exists(nestedSignedFile));
                Assert.Equal("signed pdf bytes", await File.ReadAllTextAsync(nestedSignedFile));
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
            }
        }

        [Fact]
        public async Task RestoreAsync_WhenSwapFails_RollsBackAttachmentsAndDatabase()
        {
            // Arrange
            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RestoreSwapFailsRollback_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDir);

            string testDbFilePath = Path.Combine(testDir, "test-active.db");
            string testBackupFolder = Path.Combine(testDir, "Backups");
            Directory.CreateDirectory(testBackupFolder);

            string attachmentsPath = Path.Combine(testDir, "Attachments");
            Directory.CreateDirectory(attachmentsPath);
            string attachmentFile = Path.Combine(attachmentsPath, "att.txt");
            await File.WriteAllTextAsync(attachmentFile, "original attachment content");

            string qrOutputPath = Path.Combine(testDir, "QR_Output");
            Directory.CreateDirectory(qrOutputPath);
            string qrFile = Path.Combine(qrOutputPath, "qr.png");
            await File.WriteAllTextAsync(qrFile, "original qr content");

            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseSqlite($"Data Source={testDbFilePath}")
                .Options;

            var factory = new TestDbContextFactory(options);

            using (var context = new CalQrDbContext(options))
            {
                context.Database.EnsureCreated();
                context.AppSettings.Add(new AppSetting { Key = "AttachmentsPath", Value = attachmentsPath });
                context.AppSettings.Add(new AppSetting { Key = "QrOutputPath", Value = qrOutputPath });
                await context.SaveChangesAsync();
            }

            var auditLogRepo = new AuditLogRepository(factory, new TestCurrentUserService());
            var backupService = new BackupService(factory, auditLogRepo);

            FileStream? lockingStream = null;

            try
            {
                // Act 1 - Backup while both Attachments and QR_Output have content
                await backupService.BackupNowAsync(testBackupFolder);
                var zipFiles = Directory.GetFiles(testBackupFolder, "CalQR_Backup_*.zip");
                Assert.Single(zipFiles);
                string zipFilePath = zipFiles[0];

                // Changes made after the backup was taken
                string distinctiveContent = "content written after the backup - must survive a failed restore";
                await File.WriteAllTextAsync(attachmentFile, distinctiveContent);

                using (var context = new CalQrDbContext(options))
                {
                    context.AppSettings.Add(new AppSetting { Key = "MarkerAddedAfterBackup", Value = "1" });
                    await context.SaveChangesAsync();
                }

                // Lock a file inside QR_Output so Directory.Move on that folder fails
                // after the Attachments swap has already succeeded
                lockingStream = new FileStream(qrFile, FileMode.Open, FileAccess.Read, FileShare.None);

                // Act 2 - Restore should fail while trying to swap QR_Output
                await Assert.ThrowsAnyAsync<Exception>(async () => await backupService.RestoreAsync(zipFilePath));

                lockingStream.Dispose();
                lockingStream = null;

                // Assert - the attachments swap was rolled back to the post-backup content
                Assert.True(File.Exists(attachmentFile));
                Assert.Equal(distinctiveContent, await File.ReadAllTextAsync(attachmentFile));

                // Assert - the database was never replaced
                using (var context = new CalQrDbContext(options))
                {
                    Assert.NotNull(await context.AppSettings.FirstOrDefaultAsync(s => s.Key == "MarkerAddedAfterBackup"));
                }
            }
            finally
            {
                lockingStream?.Dispose();
                SqliteConnection.ClearAllPools();
                if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
            }
        }

        [Fact]
        public async Task RestoreAsync_OnSuccess_LeavesNoPreRestoreArtifacts()
        {
            // Arrange
            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RestoreSuccessNoArtifacts_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDir);

            string testDbFilePath = Path.Combine(testDir, "test-active.db");
            string testBackupFolder = Path.Combine(testDir, "Backups");
            Directory.CreateDirectory(testBackupFolder);

            string attachmentsPath = Path.Combine(testDir, "Attachments");
            Directory.CreateDirectory(attachmentsPath);
            await File.WriteAllTextAsync(Path.Combine(attachmentsPath, "att.txt"), "attachment content");

            string qrOutputPath = Path.Combine(testDir, "QR_Output");
            Directory.CreateDirectory(qrOutputPath);
            await File.WriteAllTextAsync(Path.Combine(qrOutputPath, "qr.png"), "qr content");

            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseSqlite($"Data Source={testDbFilePath}")
                .Options;

            var factory = new TestDbContextFactory(options);

            using (var context = new CalQrDbContext(options))
            {
                context.Database.EnsureCreated();
                context.AppSettings.Add(new AppSetting { Key = "AttachmentsPath", Value = attachmentsPath });
                context.AppSettings.Add(new AppSetting { Key = "QrOutputPath", Value = qrOutputPath });
                await context.SaveChangesAsync();
            }

            var auditLogRepo = new AuditLogRepository(factory, new TestCurrentUserService());
            var backupService = new BackupService(factory, auditLogRepo);

            try
            {
                // Act 1 - Backup, then Act 2 - a plain, successful restore
                await backupService.BackupNowAsync(testBackupFolder);
                var zipFiles = Directory.GetFiles(testBackupFolder, "CalQR_Backup_*.zip");
                Assert.Single(zipFiles);
                await backupService.RestoreAsync(zipFiles[0]);

                // Assert - no pre-restore folder artifacts left next to Attachments/QR_Output
                string? attachmentsParent = Path.GetDirectoryName(attachmentsPath);
                Assert.NotNull(attachmentsParent);
                Assert.Empty(Directory.GetDirectories(attachmentsParent!, "*_preRestore_*"));

                string? qrParent = Path.GetDirectoryName(qrOutputPath);
                Assert.NotNull(qrParent);
                Assert.Empty(Directory.GetDirectories(qrParent!, "*_preRestore_*"));

                // Assert - no database snapshot file left next to the live db file
                string? dbParent = Path.GetDirectoryName(testDbFilePath);
                Assert.NotNull(dbParent);
                Assert.Empty(Directory.GetFiles(dbParent!, "*.preRestore_*"));
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
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
