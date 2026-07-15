using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Repositories;

namespace CAL_QR.Services
{
    public class BackupService : IBackupService
    {
        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;
        private readonly IAuditLogRepository _auditLogRepository;
        private Timer? _backupTimer;

        public BackupService(IDbContextFactory<CalQrDbContext> contextFactory, IAuditLogRepository auditLogRepository)
        {
            _contextFactory = contextFactory;
            _auditLogRepository = auditLogRepository;
        }

        private string GetDatabaseFilePath()
        {
            using var context = _contextFactory.CreateDbContext();
            var connectionString = context.Database.GetDbConnection().ConnectionString;
            var builder = new SqliteConnectionStringBuilder(connectionString);
            return Path.GetFullPath(builder.DataSource);
        }

        private string GetAttachmentsPath()
        {
            using var context = _contextFactory.CreateDbContext();
            var setting = context.AppSettings.AsNoTracking().FirstOrDefault(s => s.Key == "AttachmentsPath");
            if (setting != null && !string.IsNullOrWhiteSpace(setting.Value))
            {
                return setting.Value;
            }
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Attachments");
        }

        private async Task<string> GetQrOutputPathAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var setting = await context.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "QrOutputPath");
            if (setting != null && !string.IsNullOrWhiteSpace(setting.Value))
            {
                return setting.Value;
            }
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "QR_Output");
        }

        public async Task BackupNowAsync(string destinationFolder)
        {
            await Task.Run(async () =>
            {
                if (string.IsNullOrWhiteSpace(destinationFolder) || !Path.IsPathRooted(destinationFolder))
                {
                    throw new ArgumentException("لم يتم تحديد مسار مطلق صالح لحفظ النسخة الاحتياطية.");
                }

                string dbPath = GetDatabaseFilePath();
                string attachmentsPath = GetAttachmentsPath();
                string qrOutputPath = await GetQrOutputPathAsync();

                if (!Directory.Exists(destinationFolder))
                {
                    Directory.CreateDirectory(destinationFolder);
                }

                string tempDir = Path.Combine(Path.GetTempPath(), "CalQrBackup_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);

                string zipFileName = $"CalQR_Backup_{DateTime.Now:yyyy-MM-dd_HH-mm}.zip";
                string zipFilePath = Path.Combine(destinationFolder, zipFileName);

                try
                {
                    string tempDbPath = Path.Combine(tempDir, "cal-qr.db");

                    // 1. Online SQLite consistent snapshot
                    using (var source = new SqliteConnection($"Data Source={dbPath}"))
                    {
                        source.Open();
                        
                        // Checkpoint WAL frames to db file
                        using (var cmd = source.CreateCommand())
                        {
                            cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                            cmd.ExecuteNonQuery();
                        }

                        using (var destination = new SqliteConnection($"Data Source={tempDbPath}"))
                        {
                            destination.Open();
                            source.BackupDatabase(destination);
                        }
                    }

                    // Force release SQLite pooled connection handles on the temp database file
                    SqliteConnection.ClearAllPools();

                    // 2. Compress into ZIP
                    using (var zipStream = new FileStream(zipFilePath, FileMode.Create))
                    using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
                    {
                        archive.CreateEntryFromFile(tempDbPath, "cal-qr.db");

                        if (Directory.Exists(attachmentsPath))
                        {
                            var files = Directory.GetFiles(attachmentsPath, "*", SearchOption.AllDirectories);
                            foreach (var file in files)
                            {
                                string relativePath = Path.GetRelativePath(attachmentsPath, file).Replace('\\', '/');
                                archive.CreateEntryFromFile(file, "Attachments/" + relativePath);
                            }
                        }

                        if (Directory.Exists(qrOutputPath))
                        {
                            var files = Directory.GetFiles(qrOutputPath, "*", SearchOption.AllDirectories);
                            foreach (var file in files)
                            {
                                string relativePath = Path.GetRelativePath(qrOutputPath, file).Replace('\\', '/');
                                archive.CreateEntryFromFile(file, "QR_Output/" + relativePath);
                            }
                        }
                    }

                    // 3. Keep last 10 backups
                    var oldBackups = Directory.GetFiles(destinationFolder, "CalQR_Backup_*.zip")
                        .Select(f => new FileInfo(f))
                        .OrderByDescending(f => f.CreationTime)
                        .Skip(10)
                        .ToList();

                    foreach (var old in oldBackups)
                    {
                        try { old.Delete(); } catch { }
                    }

                    // 4. Add Audit log entry
                    await _auditLogRepository.LogAsync("نسخ احتياطي", "نظام", "Backup", $"إنشاء نسخة احتياطية بنجاح: {zipFileName}");
                }
                catch (Exception ex)
                {
                    try
                    {
                        await _auditLogRepository.LogAsync("نسخ احتياطي", "نظام", "Backup", $"فشل إنشاء نسخة احتياطية: {ex.Message}");
                    }
                    catch { }
                    throw;
                }
                finally
                {
                    SqliteConnection.ClearAllPools();
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
            });
        }

        public async Task RestoreAsync(string zipFilePath)
        {
            await Task.Run(async () =>
            {
                if (string.IsNullOrWhiteSpace(zipFilePath) || !File.Exists(zipFilePath))
                {
                    throw new FileNotFoundException("ملف النسخة الاحتياطية المحدد غير موجود أو غير صالح.");
                }

                string dbPath = GetDatabaseFilePath();
                string attachmentsPath = GetAttachmentsPath();
                string qrOutputPath = await GetQrOutputPathAsync();

                string? stagingParent = Path.GetDirectoryName(attachmentsPath);
                if (string.IsNullOrWhiteSpace(stagingParent))
                {
                    stagingParent = AppDomain.CurrentDomain.BaseDirectory;
                }
                string tempDir = Path.Combine(stagingParent, "CalQrRestoreStaging_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);

                try
                {
                    using (var archive = ZipFile.OpenRead(zipFilePath))
                    {
                        var dbEntry = archive.GetEntry("cal-qr.db");
                        if (dbEntry == null)
                        {
                            throw new InvalidOperationException("ملف النسخة الاحتياطية غير صالح (لا يحتوي على قاعدة البيانات).");
                        }

                        string tempDbPath = Path.Combine(tempDir, "cal-qr.db");
                        dbEntry.ExtractToFile(tempDbPath, true);

                        // 1. Live SQLite restore via reverse SQLite Backup API
                        using (var source = new SqliteConnection($"Data Source={tempDbPath}"))
                        {
                            source.Open();
                            using (var destination = new SqliteConnection($"Data Source={dbPath}"))
                            {
                                destination.Open();
                                source.BackupDatabase(destination);
                            }
                        }

                        // 2. Staging extraction paths
                        string tempAttachments = Path.Combine(tempDir, "StagedAttachments");
                        Directory.CreateDirectory(tempAttachments);

                        string tempQrOutput = Path.Combine(tempDir, "StagedQrOutput");
                        bool hasQrFolderInZip = archive.Entries.Any(e => e.FullName.StartsWith("QR_Output/", StringComparison.OrdinalIgnoreCase));
                        if (hasQrFolderInZip)
                        {
                            Directory.CreateDirectory(tempQrOutput);
                        }

                        foreach (var entry in archive.Entries)
                        {
                            if (entry.FullName.StartsWith("Attachments/", StringComparison.OrdinalIgnoreCase))
                            {
                                string relativePath = entry.FullName.Substring("Attachments/".Length);
                                if (!string.IsNullOrEmpty(relativePath))
                                {
                                    string destPath = Path.Combine(tempAttachments, relativePath);
                                    string destDir = Path.GetDirectoryName(destPath)!;
                                    if (!Directory.Exists(destDir))
                                    {
                                        Directory.CreateDirectory(destDir);
                                    }
                                    entry.ExtractToFile(destPath, true);
                                }
                            }
                            else if (entry.FullName.StartsWith("QR_Output/", StringComparison.OrdinalIgnoreCase))
                            {
                                string relativePath = entry.FullName.Substring("QR_Output/".Length);
                                if (!string.IsNullOrEmpty(relativePath))
                                {
                                    string destPath = Path.Combine(tempQrOutput, relativePath);
                                    string destDir = Path.GetDirectoryName(destPath)!;
                                    if (!Directory.Exists(destDir))
                                    {
                                        Directory.CreateDirectory(destDir);
                                    }
                                    entry.ExtractToFile(destPath, true);
                                }
                            }
                        }

                        // 3. Swap staged directories with active directories (Atomic Swap)
                        if (Directory.Exists(attachmentsPath))
                        {
                            Directory.Delete(attachmentsPath, true);
                        }
                        Directory.Move(tempAttachments, attachmentsPath);

                        if (hasQrFolderInZip)
                        {
                            if (Directory.Exists(qrOutputPath))
                            {
                                Directory.Delete(qrOutputPath, true);
                            }
                            Directory.Move(tempQrOutput, qrOutputPath);
                        }
                    }

                    // 3. Add Audit log entry
                    await _auditLogRepository.LogAsync("استعادة نسخة احتياطية", "نظام", "Backup", $"استعادة قاعدة البيانات والمرفقات من: {Path.GetFileName(zipFilePath)}");
                }
                catch (Exception ex)
                {
                    try
                    {
                        await _auditLogRepository.LogAsync("استعادة نسخة احتياطية", "نظام", "Backup", $"فشل استعادة نسخة احتياطية: {ex.Message}");
                    }
                    catch { }
                    throw;
                }
                finally
                {
                    SqliteConnection.ClearAllPools();
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
            });
        }

        public void StartScheduledBackupTimer()
        {
            _backupTimer?.Dispose();
            // Run check every 1 hour (first execution after 1 minute)
            _backupTimer = new Timer(async _ => await CheckAndRunScheduledBackupAsync(), null, TimeSpan.FromMinutes(1), TimeSpan.FromHours(1));
        }

        private async Task CheckAndRunScheduledBackupAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var scheduleSetting = await context.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "BackupSchedule");
                var pathSetting = await context.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "BackupPath");
                var lastSetting = await context.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "LastBackupDateTime");

                if (scheduleSetting == null || string.IsNullOrEmpty(scheduleSetting.Value) || scheduleSetting.Value == "None" ||
                    pathSetting == null || string.IsNullOrEmpty(pathSetting.Value) || !Path.IsPathRooted(pathSetting.Value))
                {
                    return;
                }

                DateTime lastBackup = DateTime.MinValue;
                if (lastSetting != null && DateTime.TryParse(lastSetting.Value, out var dt))
                {
                    lastBackup = dt;
                }

                DateTime today = DateTime.Today;
                bool shouldBackup = false;

                if (scheduleSetting.Value == "Daily")
                {
                    shouldBackup = (today - lastBackup.Date).TotalDays >= 1;
                }
                else if (scheduleSetting.Value == "Weekly")
                {
                    shouldBackup = (today - lastBackup.Date).TotalDays >= 7;
                }
                else if (scheduleSetting.Value == "Monthly")
                {
                    shouldBackup = (today - lastBackup.Date).TotalDays >= 30;
                }



                if (shouldBackup)
                {
                    await BackupNowAsync(pathSetting.Value);

                    using var updateContext = await _contextFactory.CreateDbContextAsync();
                    var lastBkp = await updateContext.AppSettings.FirstOrDefaultAsync(s => s.Key == "LastBackupDateTime");
                    if (lastBkp == null)
                    {
                        lastBkp = new AppSetting { Key = "LastBackupDateTime", Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };
                        updateContext.AppSettings.Add(lastBkp);
                    }
                    else
                    {
                        lastBkp.Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    }
                    await updateContext.SaveChangesAsync();
                }
            }
            catch
            {
                // Background thread - swallow exceptions to prevent process crash
            }
        }
    }
}
