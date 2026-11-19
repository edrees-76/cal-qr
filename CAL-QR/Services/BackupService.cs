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
using CAL_QR.Validation;

namespace CAL_QR.Services
{
    public class BackupService : IBackupService
    {
        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly IBackupPasswordStore _passwordStore;
        private readonly ICertificateCounterFloorStore? _counterFloorStore;
        private Timer? _backupTimer;

        public const string NoPasswordMessage =
            "لم تُضبط كلمة سرّ النسخ الاحتياطيّ بعد، فلم تُنشأ أيّ نسخة. اضبطها من «الإعدادات» ← «النسخ الاحتياطي والاستعادة». " +
            "النسخة غير المقفلة تكشف مفتاح توقيع الشهادات لمن يحصل عليها.";

        public const string NoSafetyPasswordMessage =
            "لا تتمّ الاستعادة بلا نسخة أمان من الوضع الحاليّ، ونسخة الأمان تحتاج كلمة سرّ: " +
            "اضبط كلمة سرّ النسخ الاحتياطيّ من «الإعدادات» ← «النسخ الاحتياطي والاستعادة»، " +
            "أو اكتب كلمة سرّ النسخة المستعادة في خانتها، ثمّ أعد المحاولة.";

        public const string PreRestoreFilePrefix = "CalQR_PreRestore_";

        private const string BackupFilePattern = "CalQR_Backup_*" + BackupEncryption.FileExtension;

        public BackupService(
            IDbContextFactory<CalQrDbContext> contextFactory,
            IAuditLogRepository auditLogRepository,
            IBackupPasswordStore passwordStore,
            ICertificateCounterFloorStore? counterFloorStore = null)
        {
            _contextFactory = contextFactory;
            _auditLogRepository = auditLogRepository;
            _passwordStore = passwordStore;
            _counterFloorStore = counterFloorStore;
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
            return CAL_QR.Helpers.AppPaths.DefaultAttachmentsFolder();
        }

        /// <summary>PRAGMA integrity_check على ملفّ قاعدة؛ يُلقي إن لم تكن النتيجة "ok".</summary>
        private static void EnsureDatabaseIntegrity(string dbFile, string what)
        {
            string? result;
            try
            {
                using var connection = new SqliteConnection($"Data Source={dbFile};Pooling=False");
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "PRAGMA integrity_check;";
                result = cmd.ExecuteScalar() as string;
            }
            catch (SqliteException ex)
            {
                // ملفّ ليس قاعدة SQLite أصلاً (تالف كلّيّاً).
                throw new InvalidOperationException($"فشل فحص سلامة {what}: {ex.Message}", ex);
            }

            if (result != "ok")
            {
                throw new InvalidOperationException($"فشل فحص سلامة {what}: {result}");
            }
        }

        private async Task<string> GetQrOutputPathAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var setting = await context.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "QrOutputPath");
            if (setting != null && !string.IsNullOrWhiteSpace(setting.Value))
            {
                return setting.Value;
            }
            return CAL_QR.Helpers.QrPaths.DefaultFolder();
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
                using var maintenanceGate = await MaintenanceGate.EnterAsync();

                if (string.IsNullOrWhiteSpace(destinationFolder) || !Path.IsPathRooted(destinationFolder))
                {
                    throw new ArgumentException("لم يتم تحديد مسار مطلق صالح لحفظ النسخة الاحتياطية.");
                }

                // قبل أيّ كتابة: لا نسخة مكشوفة أبداً. قاعدة البيانات تحمل مفتاح HMAC،
                // فنسخة بلا تشفير تُمكّن من يحصل عليها من توقيع شهادات مزوَّرة.
                string? password = await _passwordStore.GetPasswordAsync();
                if (string.IsNullOrEmpty(password))
                {
                    throw new InvalidOperationException(NoPasswordMessage);
                }

                string dbPath = GetDatabaseFilePath();
                string attachmentsPath = GetAttachmentsPath();
                string qrOutputPath = await GetQrOutputPathAsync();

                if (!Directory.Exists(destinationFolder))
                {
                    Directory.CreateDirectory(destinationFolder);
                }

                string backupFileName = $"CalQR_Backup_{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", System.Globalization.CultureInfo.InvariantCulture)}{BackupEncryption.FileExtension}";
                string backupFilePath = Path.Combine(destinationFolder, backupFileName);
                // نسختان في الثانية نفسها: لا تُطمس الأولى.
                for (int suffix = 2; File.Exists(backupFilePath); suffix++)
                {
                    backupFileName = $"{Path.GetFileNameWithoutExtension(backupFileName).Split('~')[0]}~{suffix}{BackupEncryption.FileExtension}";
                    backupFilePath = Path.Combine(destinationFolder, backupFileName);
                }
                bool cloudCopySuccess = true;

                try
                {
                    // 1–2. لقطة القاعدة + الأرشيف + التشفير. نسخة من قاعدة تالفة لا تُحفظ على أنّها سليمة.
                    WriteEncryptedSnapshot(dbPath, attachmentsPath, qrOutputPath, backupFilePath, password, verifyIntegrity: true);

                    // 3. Keep last 10 backups
                    var oldBackups = Directory.GetFiles(destinationFolder, BackupFilePattern)
                        .Select(f => new FileInfo(f))
                        .OrderByDescending(f => f.CreationTime)
                        .Skip(10)
                        .ToList();

                    foreach (var old in oldBackups)
                    {
                        try { old.Delete(); } catch (System.Exception swallowEx1) { CAL_QR.Services.AppLog.Warn("BackupService.cs:176: " + swallowEx1.Message); }
                    }

                    // 4. Add Audit log entry
                    await _auditLogRepository.LogAsync("نسخ احتياطي", "نظام", "Backup", $"إنشاء نسخة احتياطية بنجاح: {backupFileName}");

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

                                string cloudBackupFilePath = Path.Combine(cloudBackupPath, backupFileName);
                                File.Copy(backupFilePath, cloudBackupFilePath, true);

                                // Keep last 10 backups in cloud backup path independently
                                var oldCloudBackups = Directory.GetFiles(cloudBackupPath, BackupFilePattern)
                                    .Select(f => new FileInfo(f))
                                    .OrderByDescending(f => f.CreationTime)
                                    .Skip(10)
                                    .ToList();

                                foreach (var old in oldCloudBackups)
                                {
                                    try { old.Delete(); } catch (System.Exception swallowEx2) { CAL_QR.Services.AppLog.Warn("BackupService.cs:212: " + swallowEx2.Message); }
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
                    catch (Exception logEx)
                    {
                        AppLog.Error($"BackupService: تعذّر تسجيل فشل العمليّة في سجلّ التدقيق (السبب الأصليّ: {ex.Message})", logEx);
                    }
                    throw;
                }
                finally
                {
                    SqliteConnection.ClearAllPools();
                }
            });
        }

        /// <summary>
        /// لقطة متّسقة من القاعدة الحيّة + المرفقات + مجلّد QR، في ملفّ مشفَّر واحد. لا تأخذ قفل
        /// الصيانة: يستدعيها «نسخ الآن» والاستعادة (نسخة الأمان) وكلاهما يحمل القفل.
        /// الأرشيف المكشوف يُبنى في مجلّد مؤقّت ويُحذف معه؛ ما يصل إلى الوجهة هو المشفَّر فقط.
        /// </summary>
        private static void WriteEncryptedSnapshot(
            string dbPath, string attachmentsPath, string qrOutputPath,
            string backupFilePath, string password, bool verifyIntegrity)
        {
            // مخلَّفات انقطاع سابق (تيّار/إنهاء قسريّ) تُزال قبل بناء لقطة جديدة.
            CleanupStaleSnapshotFolders();

            string tempDir = Path.Combine(Path.GetTempPath(), SnapshotFolderPrefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                string tempDbPath = Path.Combine(tempDir, "cal-qr.db");
                string plainZipPath = Path.Combine(tempDir, "backup.zip");

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

                if (verifyIntegrity)
                {
                    EnsureDatabaseIntegrity(tempDbPath, "لقطة القاعدة");
                }

                // 2. Compress into ZIP
                using (var zipStream = new FileStream(plainZipPath, FileMode.Create))
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
                            archive.CreateEntryFromFile(file, "poster/" + relativePath);
                        }
                    }
                }

                // 2.ب التشفير بكلمة سرّ النسخ الاحتياطيّ
                BackupEncryption.EncryptFile(plainZipPath, backupFilePath, password);
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                // حذف بإعادة محاولة: قفل عابر (مضادّ فيروسات/مفهرس) لا يُفشل النسخة ولا يُبقي
                // اللقطة المكشوفة؛ وإن تعذّر الحذف يسجّله المساعد ويزيله التنظيف عند الإقلاع أو النسخة التالية.
                FileSystemRetryHelper.TryDeleteDirectory(tempDir);
            }
        }

        public const string SnapshotFolderPrefix = "CalQrBackup_";

        /// <summary>أقلّ عمر لمجلّد لقطة قبل اعتباره مخلَّفاً (حماية من لقطة جارية في عمليّة أخرى).</summary>
        public static readonly TimeSpan StaleSnapshotMinAge = TimeSpan.FromHours(2);

        /// <summary>
        /// يحذف مجلّدات لقطات النسخ الاحتياطيّ المتروكة في المجلّد المؤقّت (تحمل قاعدة البيانات
        /// ومفتاح التوقيع غير مشفَّرين). لا يمسّ إلا ما اسمه <c>CalQrBackup_*</c> وأقدم من
        /// <paramref name="minAge"/>. يُرجع عدد ما حُذف. لا يُلقي استثناءً: فشل التنظيف لا يُفشل شيئاً.
        /// </summary>
        public static int CleanupStaleSnapshotFolders(string? tempRoot = null, TimeSpan? minAge = null)
            => CleanupStaleFolders(tempRoot ?? Path.GetTempPath(), SnapshotFolderPrefix, minAge ?? StaleSnapshotMinAge);

        public const string RestoreStagingFolderPrefix = "CalQrRestoreStaging_";

        /// <summary>
        /// يحذف مجلّدات مسرح الاستعادة المتروكة (<c>CalQrRestoreStaging_*</c>، فيها قاعدة مفكوكة
        /// التشفير) في <paramref name="stagingRoot"/> (الافتراضيّ مجلّد بيانات المنظومة)، بالشروط
        /// نفسها: الأقدم من <paramref name="minAge"/> فقط، ولا يُلقي استثناءً.
        /// </summary>
        public static int CleanupStaleRestoreStagingFolders(string? stagingRoot = null, TimeSpan? minAge = null)
            => CleanupStaleFolders(stagingRoot ?? CAL_QR.Helpers.AppPaths.DataRoot, RestoreStagingFolderPrefix, minAge ?? StaleSnapshotMinAge);

        private static int CleanupStaleFolders(string root, string prefix, TimeSpan minAge)
        {
            int removed = 0;
            try
            {
                if (!Directory.Exists(root))
                {
                    return 0;
                }

                DateTime cutoff = DateTime.UtcNow - minAge;
                foreach (string dir in Directory.GetDirectories(root, prefix + "*"))
                {
                    if (Directory.GetLastWriteTimeUtc(dir) <= cutoff
                        && FileSystemRetryHelper.TryDeleteDirectory(dir))
                    {
                        removed++;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                AppLog.Warn($"BackupService: تعذّر تنظيف مجلّدات {prefix}* المتروكة: " + ex.Message);
            }

            return removed;
        }

        /// <summary>
        /// يحسم مسار الاستخراج ويتحقّق أنّه داخل مجلّد المسرح (منع Zip-Slip).
        /// أيّ اسم مطلق أو يخرج بـ ".." يُرفض برسالة واضحة قبل أيّ مرحلة مدمِّرة.
        /// </summary>
        private static string ResolveSafeExtractionPath(string stagingRoot, string relativePath)
        {
            string root = Path.GetFullPath(stagingRoot);
            if (!root.EndsWith(Path.DirectorySeparatorChar))
            {
                root += Path.DirectorySeparatorChar;
            }

            string normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(relativePath) || Path.IsPathRooted(normalized))
            {
                throw new InvalidDataException($"ملف النسخة الاحتياطية غير آمن: المسار المطلق غير مسموح ({relativePath}).");
            }

            string full = Path.GetFullPath(Path.Combine(root, normalized));
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"ملف النسخة الاحتياطية غير آمن: مسار يخرج عن مجلّد الاستعادة ({relativePath}).");
            }

            return full;
        }

        public async Task<RestoreResult> RestoreAsync(string backupFilePath, string? password = null)
        {
            return await Task.Run(async () =>
            {
                CertificateCounterRestoreReport? counterReport = null;
                string safetyBackupPath;
                string? counterWarning = null;

                using var maintenanceGate = await MaintenanceGate.EnterAsync();

                if (string.IsNullOrWhiteSpace(backupFilePath) || !File.Exists(backupFilePath))
                {
                    throw new FileNotFoundException("ملف النسخة الاحتياطية المحدد غير موجود أو غير صالح.");
                }

                string dbPath = GetDatabaseFilePath();
                string attachmentsPath = GetAttachmentsPath();
                string qrOutputPath = await GetQrOutputPathAsync();

                string backupPathRaw;
                string cloudBackupPathRaw;
                string? backupPasswordRaw;
                using (var pathsContext = await _contextFactory.CreateDbContextAsync())
                {
                    var backupPathSetting = await pathsContext.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "BackupPath");
                    var cloudBackupPathSetting = await pathsContext.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "CloudBackupPath");
                    var backupPasswordSetting = await pathsContext.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == DpapiBackupPasswordStore.SettingKey);
                    backupPathRaw = backupPathSetting?.Value ?? string.Empty;
                    cloudBackupPathRaw = cloudBackupPathSetting?.Value ?? string.Empty;
                    backupPasswordRaw = backupPasswordSetting?.Value;
                }

                bool isEncrypted = BackupEncryption.IsEncryptedBackup(backupFilePath);
                string? effectivePassword = null;
                if (isEncrypted)
                {
                    // كلمة مكتوبة عند الاستعادة تُقدَّم: نسخة من جهاز آخر أو بكلمة سابقة.
                    effectivePassword = string.IsNullOrEmpty(password)
                        ? await _passwordStore.GetPasswordAsync()
                        : password;

                    if (string.IsNullOrEmpty(effectivePassword))
                    {
                        throw new BackupPasswordException(
                            "هذه النسخة مقفلة بكلمة سرّ ولا كلمة مضبوطة على هذا الجهاز. اكتب كلمة سرّ النسخة ثمّ أعد المحاولة.");
                    }
                }

                // نسخة الأمان تُقفل بكلمة هذا الجهاز كبقيّة نسخه؛ وفي جهاز جديد بلا كلمة بعدُ
                // بالكلمة المكتوبة للاستعادة (يعرفها من كتبها). بلا أيّ منهما لا استعادة.
                string? devicePassword = await _passwordStore.GetPasswordAsync();
                string? safetyPassword = !string.IsNullOrEmpty(devicePassword) ? devicePassword
                    : !string.IsNullOrEmpty(password) ? password
                    : null;
                if (safetyPassword == null)
                {
                    throw new InvalidOperationException(NoSafetyPasswordMessage);
                }

                string? stagingParent = Path.GetDirectoryName(attachmentsPath);
                if (string.IsNullOrWhiteSpace(stagingParent))
                {
                    stagingParent = CAL_QR.Helpers.AppPaths.DataRoot;
                }
                // مخلَّفات استعادة سابقة انقطعت (قاعدة مفكوكة التشفير) تُزال قبل مسرح جديد.
                CleanupStaleRestoreStagingFolders(stagingParent);

                string tempDir = Path.Combine(stagingParent, RestoreStagingFolderPrefix + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);

                try
                {
                    string tempDbPath = Path.Combine(tempDir, "cal-qr.db");
                    string tempAttachments = Path.Combine(tempDir, "StagedAttachments");
                    string tempQrOutput = Path.Combine(tempDir, "StagedQrOutput");
                    bool hasAttachmentsInZip;
                    bool hasQrFolderInZip;

                    // المرحلة ٠ — فكّ التشفير إلى مجلّد المسرح. كلمة سرّ خاطئة أو ملفّ
                    // معدَّل يتوقّفان هنا قبل أيّ مساس بالنظام.
                    string zipFilePath = backupFilePath;
                    if (isEncrypted)
                    {
                        zipFilePath = Path.Combine(tempDir, "restore.zip");
                        BackupEncryption.DecryptFile(backupFilePath, zipFilePath, effectivePassword!);
                    }

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

                        // يدعم كلاً من "poster/" (النسخ الجديدة) و "QR_Output/" (النسخ القديمة)
                        hasQrFolderInZip = archive.Entries.Any(e =>
                            e.FullName.StartsWith("poster/", StringComparison.OrdinalIgnoreCase) ||
                            e.FullName.StartsWith("QR_Output/", StringComparison.OrdinalIgnoreCase));
                        if (hasQrFolderInZip)
                        {
                            Directory.CreateDirectory(tempQrOutput);
                        }

                        foreach (var entry in archive.Entries)
                        {
                            if (entry.FullName.StartsWith("Attachments/", StringComparison.OrdinalIgnoreCase))
                            {
                                string relativePath = entry.FullName.Substring("Attachments/".Length);
                                if (!string.IsNullOrEmpty(relativePath) && !string.IsNullOrEmpty(entry.Name))
                                {
                                    string destPath = ResolveSafeExtractionPath(tempAttachments, relativePath);
                                    string destDir = Path.GetDirectoryName(destPath)!;
                                    if (!Directory.Exists(destDir))
                                    {
                                        Directory.CreateDirectory(destDir);
                                    }
                                    entry.ExtractToFile(destPath, true);
                                }
                            }
                            else if (entry.FullName.StartsWith("poster/", StringComparison.OrdinalIgnoreCase))
                            {
                                string relativePath = entry.FullName.Substring("poster/".Length);
                                if (!string.IsNullOrEmpty(relativePath) && !string.IsNullOrEmpty(entry.Name))
                                {
                                    string destPath = ResolveSafeExtractionPath(tempQrOutput, relativePath);
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
                                // توافق خلفيّ مع النسخ القديمة التي تستخدم "QR_Output/"
                                string relativePath = entry.FullName.Substring("QR_Output/".Length);
                                if (!string.IsNullOrEmpty(relativePath) && !string.IsNullOrEmpty(entry.Name))
                                {
                                    string destPath = ResolveSafeExtractionPath(tempQrOutput, relativePath);
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

                    // القاعدة المستخرجة تُفحص قبل أيّ مساس بالنظام الحيّ: نسخة تالفة تُرفض هنا.
                    EnsureDatabaseIntegrity(tempDbPath, "القاعدة داخل النسخة الاحتياطيّة");

                    // نسخة أمان من الوضع الحاليّ قبل أيّ مساس به، فاستعادة خاطئة يمكن التراجع عنها
                    // باستعادة هذه النسخة (ومعها ما صدر بعد تاريخ النسخة المستعادة). فشلها يوقف
                    // الاستعادة هنا ولم يتغيّر شيء. بلا فحص سلامة: قد تكون الاستعادة سببها خلل في
                    // القاعدة الحاليّة، ونسختها كما هي أفضل من لا شيء.
                    safetyBackupPath = Path.Combine(
                        ResolveSafetyBackupFolder(backupPathRaw, backupFilePath),
                        $"{PreRestoreFilePrefix}{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", System.Globalization.CultureInfo.InvariantCulture)}{BackupEncryption.FileExtension}");
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(safetyBackupPath)!);
                        WriteEncryptedSnapshot(dbPath, attachmentsPath, qrOutputPath, safetyBackupPath, safetyPassword, verifyIntegrity: false);
                    }
                    catch (Exception safetyEx)
                    {
                        try { if (File.Exists(safetyBackupPath)) File.Delete(safetyBackupPath); }
                        catch (Exception cleanupEx) { AppLog.Warn("BackupService: تعذّر حذف نسخة أمان ناقصة: " + cleanupEx.Message); }
                        throw new InvalidOperationException(
                            "تعذّر إنشاء نسخة أمان من الوضع الحاليّ قبل الاستعادة، فلم تُستعد النسخة ولم يتغيّر شيء. " +
                            $"تحقّق من مجلّد النسخ ومساحة القرص ثمّ أعد المحاولة. ({safetyEx.Message})", safetyEx);
                    }

                    // قبل الاستبدال: عدّادات القاعدة الحيّة تُحفظ خارجها، فشهادات صدرت بعد
                    // تاريخ النسخة لا تُعطى أرقامها من جديد. الفشل لا يوقف الاستعادة (الحدّ
                    // يُحدَّث أيضاً عند كلّ إصدار)، لكنّه يُسجَّل ويُعرض على المدير في النافذة.
                    if (_counterFloorStore != null)
                    {
                        try
                        {
                            var (liveLedger, liveCounters) = ReadCertificateCounters(dbPath);
                            if (liveLedger != null)
                            {
                                _counterFloorStore.Raise(liveLedger, liveCounters);
                            }
                        }
                        catch (Exception counterEx)
                        {
                            AppLog.Error("BackupService: تعذّر حفظ عدّاد الشهادات الحيّ قبل الاستعادة", counterEx);
                            counterWarning = "تعذّر حفظ عدّاد الشهادات من القاعدة الحاليّة قبل الاستعادة؛ " +
                                             "راجع آخر رقم صدر فعلاً في السجلّ الورقيّ قبل التأكيد.";
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

                            // كلمة سرّ النسخ الاحتياطيّ مقفلة بـDPAPI على الجهاز الذي ضبطها:
                            // القيمة الآتية من نسخة جهاز آخر لا تُفكّ هنا. تبقى قيمة هذا
                            // الجهاز، أو لا شيء إن لم تُضبط بعد، فيُطلب ضبطها.
                            var restoredPassword = await restoredPathsContext.AppSettings
                                .FirstOrDefaultAsync(s => s.Key == DpapiBackupPasswordStore.SettingKey);
                            if (backupPasswordRaw == null)
                            {
                                if (restoredPassword != null)
                                {
                                    restoredPathsContext.AppSettings.Remove(restoredPassword);
                                }
                            }
                            else if (restoredPassword != null)
                            {
                                restoredPassword.Value = backupPasswordRaw;
                            }
                            else
                            {
                                restoredPathsContext.AppSettings.Add(new AppSetting
                                {
                                    Key = DpapiBackupPasswordStore.SettingKey,
                                    Value = backupPasswordRaw
                                });
                            }

                            await restoredPathsContext.SaveChangesAsync();
                        }
                    }
                    catch (System.Exception swallowEx3)
                    {
                        CAL_QR.Services.AppLog.Error("BackupService.cs:548", swallowEx3);
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
                            catch (System.Exception swallowEx4) { CAL_QR.Services.AppLog.Warn("BackupService.cs:568: " + swallowEx4.Message); }
                        }

                        // التراجع لا يشترط وجود أثر ما قبل الاستعادة: غيابه يعني أنّ المجلّد لم يكن
                        // موجودًا أصلًا فيُحذف المستعاد، ووجوده بلا تثبيت المستعاد يعني أنّ النقل
                        // الثاني فشل فيُعاد الأصل. فشل التراجع يُسجَّل ولا يُخفى.
                        RollbackFolderSwap(qrOutputPath, qrPreRestore, qrSwapped);
                        RollbackFolderSwap(attachmentsPath, attachmentsPreRestore, attachmentsSwapped);

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
                        catch (Exception logEx)
                        {
                            AppLog.Error("BackupService: تعذّر تسجيل فشل الاستعادة في سجلّ التدقيق", logEx);
                        }

                        throw;
                    }

                    // 3. Add Audit log entry
                    await _auditLogRepository.LogAsync("استعادة نسخة احتياطية", "نظام", "Backup", $"استعادة قاعدة البيانات والمرفقات من: {Path.GetFileName(backupFilePath)}. نسخة أمان لما قبلها: {safetyBackupPath}");

                    // المرحلة ٣ — تنظيف آثار ما قبل الاستعادة عند النجاح وحده. فشل
                    // التنظيف لا يُفشل استعادة ناجحة.
                    try { if (Directory.Exists(attachmentsPreRestore)) Directory.Delete(attachmentsPreRestore, true); } catch (System.Exception swallowEx7) { CAL_QR.Services.AppLog.Warn("BackupService.cs:636: " + swallowEx7.Message); }
                    try { if (Directory.Exists(qrPreRestore)) Directory.Delete(qrPreRestore, true); } catch (System.Exception swallowEx8) { CAL_QR.Services.AppLog.Warn("BackupService.cs:637: " + swallowEx8.Message); }
                    try { if (File.Exists(dbSnapshot)) File.Delete(dbSnapshot); } catch (System.Exception swallowEx9) { CAL_QR.Services.AppLog.Warn("BackupService.cs:638: " + swallowEx9.Message); }

                    if (_counterFloorStore != null)
                    {
                        counterReport = BuildCounterReport(dbPath, counterWarning);
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        await _auditLogRepository.LogAsync("استعادة نسخة احتياطية", "نظام", "Backup", $"فشل استعادة نسخة احتياطية: {ex.Message}");
                    }
                    catch (Exception logEx)
                    {
                        AppLog.Error($"BackupService: تعذّر تسجيل فشل العمليّة في سجلّ التدقيق (السبب الأصليّ: {ex.Message})", logEx);
                    }
                    throw;
                }
                finally
                {
                    SqliteConnection.ClearAllPools();
                    // حذف بإعادة محاولة: قفل عابر بعد استعادة ناجحة لا يقلبها إلى خطأ، ولا يُبقي
                    // القاعدة المفكوكة؛ وإن تعذّر الحذف يزيله التنظيف عند الإقلاع أو الاستعادة التالية.
                    FileSystemRetryHelper.TryDeleteDirectory(tempDir);
                }

                return new RestoreResult(safetyBackupPath, counterReport);
            });
        }

        /// <summary>
        /// تراجع تبديل مجلّد أثناء الاستعادة: إن ثُبِّت المستعاد حُذف، ثمّ يُعاد الأصل من أثر
        /// ما قبل الاستعادة إن وُجد ولم يكن للمسار الحيّ مجلّد.
        /// </summary>
        internal static void RollbackFolderSwap(string livePath, string preRestorePath, bool installed)
        {
            try
            {
                if (installed && Directory.Exists(livePath))
                {
                    Directory.Delete(livePath, true);
                }
                if (Directory.Exists(preRestorePath) && !Directory.Exists(livePath))
                {
                    Directory.Move(preRestorePath, livePath);
                }
            }
            catch (System.Exception ex)
            {
                AppLog.Warn("BackupService.RollbackFolderSwap: " + ex.Message);
            }
        }

        /// <summary>
        /// مجلّد نسخة الأمان: مجلّد النسخ المضبوط إن كان مساراً مطلقاً، وإلّا مجلّد النسخة التي
        /// تُستعاد (موجود حتماً). اسمها CalQR_PreRestore_* لا يطابق نمط الاحتفاظ بآخر 10 نسخ،
        /// فلا تحذفها النسخ المجدولة اللاحقة.
        /// </summary>
        internal static string ResolveSafetyBackupFolder(string? backupPathSetting, string backupFilePath)
        {
            if (!string.IsNullOrWhiteSpace(backupPathSetting) && Path.IsPathRooted(backupPathSetting))
            {
                return backupPathSetting;
            }
            return Path.GetDirectoryName(Path.GetFullPath(backupFilePath))!;
        }

        /// <summary>
        /// الاقتراح للسنة الحاليّة بعد استعادة ناجحة. فشل القراءة لا يُفشل استعادة تمّت:
        /// يُرجع اقتراحاً ممّا أمكن مع تحذير، والمدير يكتب الرقم الصحيح.
        /// </summary>
        private CertificateCounterRestoreReport BuildCounterReport(string dbPath, string? warning)
        {
            int year = DateTime.Today.Year;
            string? ledger = null;
            int restored = 0;
            try
            {
                var (restoredLedger, restoredCounters) = ReadCertificateCounters(dbPath);
                ledger = restoredLedger;
                restored = restoredCounters.TryGetValue(year, out int r) ? r : 0;
            }
            catch (Exception ex)
            {
                AppLog.Error("BackupService: تعذّرت قراءة عدّاد الشهادات من القاعدة المستعادة", ex);
                warning = AppendWarning(warning, "تعذّرت قراءة عدّاد الشهادات من النسخة المستعادة.");
            }

            int floor = 0;
            if (ledger != null)
            {
                try
                {
                    floor = _counterFloorStore!.Read(ledger).TryGetValue(year, out int f) ? f : 0;
                }
                catch (Exception ex)
                {
                    AppLog.Error("BackupService: تعذّرت قراءة حدّ عدّاد الشهادات", ex);
                    warning = AppendWarning(warning, "تعذّرت قراءة الحدّ المحفوظ لعدّاد الشهادات.");
                }
            }

            return new CertificateCounterRestoreReport(ledger, year, restored, Math.Max(restored, floor), warning);
        }

        private static string AppendWarning(string? existing, string addition) =>
            string.IsNullOrEmpty(existing) ? addition : existing + "\n" + addition;

        public void ConfirmCertificateCounter(CertificateCounterRestoreReport report, int lastNumber)
        {
            var (value, error) = CertificateCounterFloorRules.ParseConfirmed(
                lastNumber.ToString(System.Globalization.CultureInfo.InvariantCulture), report.ProposedLastNumber);
            if (value == null) throw new ArgumentOutOfRangeException(nameof(lastNumber), error);

            // القاعدة المستعادة لم تمرّ بالترحيلات بعد، فالكتابة SQL مباشرة على الجدول إن وُجد،
            // لا عبر EF. رفع فقط: MAX يمنع أيّ خفض.
            string dbPath = GetDatabaseFilePath();
            using (var connection = new SqliteConnection($"Data Source={dbPath};Pooling=False"))
            {
                connection.Open();
                if (TableExists(connection, "CertificateSequence"))
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText =
                        "INSERT INTO CertificateSequence (Year, LastNumber) VALUES ($year, $last) " +
                        "ON CONFLICT(Year) DO UPDATE SET LastNumber = MAX(LastNumber, excluded.LastNumber);";
                    cmd.Parameters.AddWithValue("$year", report.Year);
                    cmd.Parameters.AddWithValue("$last", value.Value);
                    cmd.ExecuteNonQuery();
                }
            }

            // الحدّ خارج القاعدة يحمي حتّى لو لم يكن الجدول موجوداً في نسخة قديمة جدّاً:
            // الإقلاع التالي يرفع العدّاد إليه بعد الترحيلات.
            if (report.LedgerId != null)
            {
                _counterFloorStore?.Raise(report.LedgerId, new Dictionary<int, int> { [report.Year] = value.Value });
            }
        }

        /// <summary>هويّة سجلّ الترقيم وعدّاداته من ملفّ قاعدة، بلا EF (المخطّط قد يكون أقدم).</summary>
        internal static (string? LedgerId, Dictionary<int, int> Counters) ReadCertificateCounters(string dbFile)
        {
            var counters = new Dictionary<int, int>();
            string? key = null;

            using (var connection = new SqliteConnection($"Data Source={dbFile};Pooling=False"))
            {
                connection.Open();

                if (TableExists(connection, "AppSettings"))
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = "SELECT Value FROM AppSettings WHERE Key = $key LIMIT 1;";
                    cmd.Parameters.AddWithValue("$key", CertificateCounterFloor.HmacKeySetting);
                    key = cmd.ExecuteScalar() as string;
                }

                if (TableExists(connection, "CertificateSequence"))
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = "SELECT Year, LastNumber FROM CertificateSequence;";
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        counters[reader.GetInt32(0)] = reader.GetInt32(1);
                    }
                }
            }

            return (CertificateCounterFloorRules.LedgerId(key), counters);
        }

        private static bool TableExists(SqliteConnection connection, string table)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
            cmd.Parameters.AddWithValue("$name", table);
            return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
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
                if (lastSetting != null && DateTime.TryParse(lastSetting.Value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dt))
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
                        lastBkp = new AppSetting { Key = "LastBackupDateTime", Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) };
                        updateContext.AppSettings.Add(lastBkp);
                    }
                    else
                    {
                        lastBkp.Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
                    }
                    await updateContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                // خيط خلفيّ: الاستثناء لا يُرمى (يُسقط البرنامج) لكنّه لا يُبتلع أيضاً.
                // كان هنا catch صامت، فلو فشل النسخ المجدول — ومنه غياب كلمة السرّ —
                // لم يعلم أحد. يُسجَّل في سجلّ العمليات، ولا تُحدَّث LastBackupDateTime
                // فيُعاد المحاولة في الفحص التالي.
                try
                {
                    await _auditLogRepository.LogAsync("نسخ احتياطي", "نظام", "Backup", $"فشل النسخ الاحتياطي المجدول: {ex.Message}");
                }
                catch (Exception logEx)
                {
                    System.Diagnostics.Trace.TraceError($"فشل النسخ الاحتياطي المجدول ({ex.Message}) وتعذّر تسجيله: {logEx.Message}");
                }
            }
        }
    }
}
