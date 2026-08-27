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

        private async Task<string> GetCloudBackupPathAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var setting = await context.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "CloudBackupPath");
            return setting?.Value ?? string.Empty;
        }

        public async Task<bool> BackupNowAsync(string destinationFolder)
        {
            return await Task.Run(async () =>
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
                bool cloudCopySuccess = true;

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

                    // 5. Cloud Backup Copy
                    string cloudBackupPath = await GetCloudBackupPathAsync();
                    if (!string.IsNullOrWhiteSpace(cloudBackupPath))
                    {
                        if (!Path.IsPathRooted(cloudBackupPath))
                        {
                            cloudCopySuccess = false;
                            await _auditLogRepository.LogAsync("نسخ احتياطي", "نظام", "Backup", $"فشل نسخ النسخة الاحتياطية إلى المسار السحابي: المسار غير مطلق.");
                        }
                        else
                        {
                            try
                            {
                                if (!Directory.Exists(cloudBackupPath))
                                {
                                    Directory.CreateDirectory(cloudBackupPath);
                                }

                                string cloudZipPath = Path.Combine(cloudBackupPath, zipFileName);
                                File.Copy(zipFilePath, cloudZipPath, true);

                                // Keep last 10 backups in cloud backup path independently
                                var oldCloudBackups = Directory.GetFiles(cloudBackupPath, "CalQR_Backup_*.zip")
                                    .Select(f => new FileInfo(f))
                                    .OrderByDescending(f => f.CreationTime)
                                    .Skip(10)
                                    .ToList();

                                foreach (var old in oldCloudBackups)
                                {
                                    try { old.Delete(); } catch { }
                                }

                                await _auditLogRepository.LogAsync("نسخ احتياطي", "نظام", "Backup", $"تم نسخ النسخة الاحتياطية أيضاً إلى المسار السحابي: {cloudBackupPath}");
                            }
                            catch (Exception ex)
                            {
                                cloudCopySuccess = false;
                                await _auditLogRepository.LogAsync("نسخ احتياطي", "نظام", "Backup", $"فشل نسخ النسخة الاحتياطية إلى المسار السحابي: {ex.Message}");
                            }
                        }
                    }

                    return cloudCopySuccess;
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

                string backupPathRaw;
                string cloudBackupPathRaw;
                using (var pathsContext = await _contextFactory.CreateDbContextAsync())
                {
                    var backupPathSetting = await pathsContext.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "BackupPath");
                    var cloudBackupPathSetting = await pathsContext.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "CloudBackupPath");
                    backupPathRaw = backupPathSetting?.Value ?? string.Empty;
                    cloudBackupPathRaw = cloudBackupPathSetting?.Value ?? string.Empty;
                }

                string? stagingParent = Path.GetDirectoryName(attachmentsPath);
                if (string.IsNullOrWhiteSpace(stagingParent))
                {
                    stagingParent = AppDomain.CurrentDomain.BaseDirectory;
                }
                string tempDir = Path.Combine(stagingParent, "CalQrRestoreStaging_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);

                try
                {
                    string tempDbPath = Path.Combine(tempDir, "cal-qr.db");
                    string tempAttachments = Path.Combine(tempDir, "StagedAttachments");
                    string tempQrOutput = Path.Combine(tempDir, "StagedQrOutput");
                    bool hasAttachmentsInZip;
                    bool hasQrFolderInZip;

                    // المرحلة ١ — تحضير فقط: نقرأ من الأرشيف ونكتب في مجلّد المسرح
                    // المؤقّت، بلا أيّ مساس بالقاعدة أو المجلّدات الحيّة.
                    using (var archive = ZipFile.OpenRead(zipFilePath))
                    {
                        var dbEntry = archive.GetEntry("cal-qr.db");
                        if (dbEntry == null)
                        {
                            throw new InvalidOperationException("ملف النسخة الاحتياطية غير صالح (لا يحتوي على قاعدة البيانات).");
                        }

                        dbEntry.ExtractToFile(tempDbPath, true);

                        // 2. Staging extraction paths
                        hasAttachmentsInZip = archive.Entries.Any(e => e.FullName.StartsWith("Attachments/", StringComparison.OrdinalIgnoreCase));
                        if (hasAttachmentsInZip)
                        {
                            Directory.CreateDirectory(tempAttachments);
                        }

                        hasQrFolderInZip = archive.Entries.Any(e => e.FullName.StartsWith("QR_Output/", StringComparison.OrdinalIgnoreCase));
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
                    }

                    // المرحلة ٢ — كلّ ما قبل هذه النقطة تحضير لا يمسّ النظام؛ وهنا وحدها
                    // تبدأ العمليّات المدمّرة، مرتّبة بحيث يكون الأثقل تراجعًا آخرها.
                    // المجلّدات تُعاد تسميتها لا تُحذف، والقاعدة تُصوَّر قبل الكتابة فوقها،
                    // فأيّ عطل في المنتصف يعود بالنظام إلى ما كان.
                    string attachmentsPreRestore = attachmentsPath + "_preRestore_" + Guid.NewGuid().ToString("N");
                    string qrPreRestore = qrOutputPath + "_preRestore_" + Guid.NewGuid().ToString("N");
                    string dbSnapshot = dbPath + ".preRestore_" + Guid.NewGuid().ToString("N");
                    bool attachmentsSwapped = false;
                    bool qrSwapped = false;
                    bool dbSnapshotTaken = false;
                    bool dbRestoreStarted = false;

                    try
                    {
                        // نسخة بلا مرفقات تعني «لم تكن هناك مرفقات وقتها»، لا «امحُ ما عندك».
                        // ملفّ زائد على القرص أهون من ملفّ ضائع، والسلوك الآن مطابق لحارس QR.
                        // أ) المرفقات
                        if (hasAttachmentsInZip)
                        {
                            if (Directory.Exists(attachmentsPath))
                            {
                                Directory.Move(attachmentsPath, attachmentsPreRestore);
                            }
                            Directory.Move(tempAttachments, attachmentsPath);
                            attachmentsSwapped = true;
                        }

                        // ب) QR
                        if (hasQrFolderInZip)
                        {
                            if (Directory.Exists(qrOutputPath))
                            {
                                Directory.Move(qrOutputPath, qrPreRestore);
                            }
                            Directory.Move(tempQrOutput, qrOutputPath);
                            qrSwapped = true;
                        }

                        // ج) لقطة القاعدة قبل الكتابة فوقها، بنفس أسلوب BackupNowAsync
                        using (var liveConnection = new SqliteConnection($"Data Source={dbPath}"))
                        {
                            liveConnection.Open();
                            using (var cmd = liveConnection.CreateCommand())
                            {
                                cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                                cmd.ExecuteNonQuery();
                            }
                            using (var snapshotConnection = new SqliteConnection($"Data Source={dbSnapshot}"))
                            {
                                snapshotConnection.Open();
                                liveConnection.BackupDatabase(snapshotConnection);
                            }
                        }
                        SqliteConnection.ClearAllPools();
                        dbSnapshotTaken = true;

                        // د) استعادة القاعدة عبر Backup API العكسيّ
                        dbRestoreStarted = true;
                        using (var source = new SqliteConnection($"Data Source={tempDbPath}"))
                        {
                            source.Open();
                            using (var destination = new SqliteConnection($"Data Source={dbPath}"))
                            {
                                destination.Open();
                                source.BackupDatabase(destination);
                            }
                        }

                        // ملفّ القاعدة كُتب فوقه من تحت اتّصالات المجمّع؛ نحرّرها قبل أن
                        // نقرأ أو نكتب عبر EF، تمامًا كما يفعل BackupNowAsync بعد النسخ.
                        SqliteConnection.ClearAllPools();

                        // هـ) المسارات تصف هذا الحاسوب لا بيانات المعايرة. النسخة الاحتياطيّة
                        // تحمل الملفّات لا العناوين. بدون هذه الكتلة تصير القاعدة تشير إلى
                        // مسار الجهاز القديم بينما تهبط الملفّات في مسار الجهاز الحاليّ،
                        // فتصبح كلّ المرفقات والنسخ الموقّعة يتيمة. DatabasePath مستثنى
                        // لأنّه ليس مصدر حقيقة — المسار الحيّ من سلسلة الاتّصال وdb_path.txt.
                        using (var restoredPathsContext = await _contextFactory.CreateDbContextAsync())
                        {
                            var pathValues = new (string Key, string Value)[]
                            {
                                ("AttachmentsPath", attachmentsPath),
                                ("QrOutputPath", qrOutputPath),
                                ("BackupPath", backupPathRaw),
                                ("CloudBackupPath", cloudBackupPathRaw),
                            };

                            foreach (var (key, value) in pathValues)
                            {
                                var existing = await restoredPathsContext.AppSettings.FirstOrDefaultAsync(s => s.Key == key);
                                if (existing != null)
                                {
                                    existing.Value = value;
                                }
                                else
                                {
                                    restoredPathsContext.AppSettings.Add(new AppSetting { Key = key, Value = value });
                                }
                            }

                            await restoredPathsContext.SaveChangesAsync();
                        }
                    }
                    catch
                    {
                        // التراجع بالعكس، كلّ خطوة في try/catch خاصّ بها حتّى لا يُسقط
                        // فشلُ خطوةٍ بقيّةَ التراجع.
                        if (dbSnapshotTaken && dbRestoreStarted)
                        {
                            try
                            {
                                SqliteConnection.ClearAllPools();
                                using (var snapshotConnection = new SqliteConnection($"Data Source={dbSnapshot}"))
                                {
                                    snapshotConnection.Open();
                                    using (var destination = new SqliteConnection($"Data Source={dbPath}"))
                                    {
                                        destination.Open();
                                        snapshotConnection.BackupDatabase(destination);
                                    }
                                }
                                SqliteConnection.ClearAllPools();
                            }
                            catch { }
                        }

                        // غياب أثر ما قبل الاستعادة يعني أنّ المجلّد لم يكن موجودًا أصلًا،
                        // فحذف ما نقلناه هو التراجع الصحيح ولا نقل بعده. الشرط يمنع أن
                        // يختفي فشل تراجع حقيقيّ داخل catch صامت.
                        if (qrSwapped && Directory.Exists(qrPreRestore))
                        {
                            try
                            {
                                if (Directory.Exists(qrOutputPath))
                                {
                                    Directory.Delete(qrOutputPath, true);
                                }
                                Directory.Move(qrPreRestore, qrOutputPath);
                            }
                            catch { }
                        }

                        if (attachmentsSwapped && Directory.Exists(attachmentsPreRestore))
                        {
                            try
                            {
                                if (Directory.Exists(attachmentsPath))
                                {
                                    Directory.Delete(attachmentsPath, true);
                                }
                                Directory.Move(attachmentsPreRestore, attachmentsPath);
                            }
                            catch { }
                        }

                        try
                        {
                            var survivingArtifacts = new System.Collections.Generic.List<string>();
                            if (File.Exists(dbSnapshot))
                            {
                                survivingArtifacts.Add(dbSnapshot);
                            }
                            if (Directory.Exists(attachmentsPreRestore))
                            {
                                survivingArtifacts.Add(attachmentsPreRestore);
                            }
                            if (Directory.Exists(qrPreRestore))
                            {
                                survivingArtifacts.Add(qrPreRestore);
                            }

                            string artifactsMessage = survivingArtifacts.Count > 0
                                ? string.Join(", ", survivingArtifacts)
                                : "لا آثار محفوظة.";

                            await _auditLogRepository.LogAsync("استعادة نسخة احتياطية", "نظام", "Backup",
                                $"فشل استعادة نسخة احتياطية وتمّ التراجع عن التغييرات. آثار محفوظة: {artifactsMessage}");
                        }
                        catch { }

                        throw;
                    }

                    // 3. Add Audit log entry
                    await _auditLogRepository.LogAsync("استعادة نسخة احتياطية", "نظام", "Backup", $"استعادة قاعدة البيانات والمرفقات من: {Path.GetFileName(zipFilePath)}");

                    // المرحلة ٣ — تنظيف آثار ما قبل الاستعادة عند النجاح وحده. فشل
                    // التنظيف لا يُفشل استعادة ناجحة.
                    try { if (Directory.Exists(attachmentsPreRestore)) Directory.Delete(attachmentsPreRestore, true); } catch { }
                    try { if (Directory.Exists(qrPreRestore)) Directory.Delete(qrPreRestore, true); } catch { }
                    try { if (File.Exists(dbSnapshot)) File.Delete(dbSnapshot); } catch { }
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
