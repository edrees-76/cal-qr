using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Repositories;

namespace CAL_QR.Services
{
    /// <summary>
    /// خدمة التصفير الكامل للنظام (Full Factory Reset).
    /// تقوم بمشح كلي لجميع البيانات من الجداول الرئيسية والملفات ذات الصلة على القرص الصلب،
    /// مع إعادة إضافة أنواع الأجهزة الستة الافتراضية، والحفاظ على المستخدمين والإعدادات.
    /// سجل التدقيق يُمسَح ضمن التصفير، ويصبح تسجيل العملية نفسها أول سطر في السجل النظيف.
    ///
    /// للنظام الحيّ (فيه شهادات حقيقيّة صدرت) يُستدعى بـ resetCertificateSequence=false
    /// وclearAuditLog=false: يبقى عدّاد الترقيم فلا يتكرّر رقم شهادة ورقيّة سابقة، ويبقى
    /// سجلّ التدقيق أثراً لا يُمحى. الافتراضيّ (true/true) لتنظيف بيانات التجربة.
    /// تنظيف مجلّدَي QR والمرفقات يتخطّى أيّ مجلّد يفشل في حارس الأمان (IsSafeToClean).
    /// </summary>
    public static class SystemResetService
    {
        public const string RequiredConfirmationPhrase = "RESET-ALL-DATA";

        public static async Task<(bool Success, string Message, int OwnersRemoved, int DevicesRemoved, int RecordsRemoved, int QrFilesRemoved, int AttachmentFoldersRemoved)> FactoryResetAsync(
            IDbContextFactory<CalQrDbContext> contextFactory,
            string confirmationPhrase,
            IAuditLogRepository? auditLogRepository = null,
            bool resetCertificateSequence = true,
            bool clearAuditLog = true,
            IEnumerable<string>? protectedPaths = null)
        {
            if (confirmationPhrase != RequiredConfirmationPhrase)
            {
                return (false, "تأكيد غير صحيح. لم يتم تنفيذ أي عملية تصفير.", 0, 0, 0, 0, 0);
            }

            using var context = await contextFactory.CreateDbContextAsync();

            int ownersCount = await context.Owners.CountAsync();
            int devicesCount = await context.Devices.CountAsync();
            int recordsCount = await context.CalibrationRecords.CountAsync();

            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                // 1. مسح كافة الشهادات قبل سجلات المعايرة، لأن علاقة Certificate → CalibrationRecord
                // بسلوك Restrict فيمنع حذف السجل قبل شهادته.
                // الجداول الأبناء الثلاثة للشهادة — وأرشيف رموز التحقق — تُحذف
                // تلقائياً بسلوك Cascade.
                context.Certificates.RemoveRange(await context.Certificates.ToListAsync());

                // 1.ب تصفير عدّاد أرقام الشهادات. قاعدة «الرقم لا يعود للاستخدام»
                // تحكم النظام العامل، لا التصفير الكامل: تصفير المصنع يمحو الشهادات
                // نفسها، فإبقاء العدّاد على ٤٢ كان سيجعل أول شهادة في نظام «نظيف»
                // تحمل الرقم ٠٠٤٣ بلا سلف.
                // للنظام الحيّ (resetCertificateSequence=false) يبقى العدّاد كما هو فلا يتكرّر رقم.
                if (resetCertificateSequence)
                {
                    context.CertificateSequence.RemoveRange(await context.CertificateSequence.ToListAsync());
                }

                // 2. مسح كافة سجلات المعايرة
                context.CalibrationRecords.RemoveRange(await context.CalibrationRecords.ToListAsync());

                // 3. مسح كافة سجلات التنبيهات المعتمدة (AcknowledgedExpiredDevices)
                context.AcknowledgedExpiredDevices.RemoveRange(await context.AcknowledgedExpiredDevices.ToListAsync());

                // 4. مسح كافة الأجهزة
                context.Devices.RemoveRange(await context.Devices.ToListAsync());

                // 5. مسح كافة الجهات المالكة
                context.Owners.RemoveRange(await context.Owners.ToListAsync());

                // 6. مسح كافة أنواع الأجهزة وإعادة بذر الأنواع الخمسة بقوالبها
                // لربط النظام بحالة تثبيت نظيفة.
                //
                // ⚠ الأسماء تُقرأ من DeviceTypeCatalog ولا تُكتب هنا. كانت مصفوفة
                // أسماء مضمّنة في هذا الموضع، وثلاثة من خمسة فيها تخالف أسماء
                // النماذج الفعلية — فتصفير مصنع واحد كان يُنتج قائمة مضاعفة
                // وقوالب معلّقة على أنواع بلا أجهزة.
                context.DeviceTypes.RemoveRange(await context.DeviceTypes.ToListAsync());

                // 7. مسح كامل لسجل التدقيق (AuditLogs) ضمن التصفير.
                // مستقلّ بلا مفاتيح أجنبية نحو ما نحذفه: علاقته الوحيدة UserId→User،
                // والمستخدمون يبقون، فلا قيد يتأثر ولا ترتيب يلزم. أول سطر في السجل
                // النظيف بعد الـcommit سيكون تسجيل عملية التصفير نفسها (أدناه).
                // للنظام الحيّ (clearAuditLog=false) يبقى السجلّ ويُضاف إليه سطر التصفير.
                if (clearAuditLog)
                {
                    context.AuditLogs.RemoveRange(await context.AuditLogs.ToListAsync());
                }
                await context.SaveChangesAsync();

                // Apply لا SeedIfNeeded: علم البذر مضبوط سلفاً على قاعدة عاملة،
                // والتصفير يجب أن يُعيد البناء رغمه.
                DeviceTypeSeeder.Apply(context);

                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"حدث خطأ أثناء التصفير الكامل لقاعدة البيانات: {ex.Message}", 0, 0, 0, 0, 0);
            }

            // مرحلة ما بعد الحذف في قاعدة البيانات (Post-Commit Phase): تنظيف الملفات من القرص
            int qrFilesDeleted = 0;
            int attachmentFoldersDeleted = 0;

            string qrFolder = CAL_QR.Helpers.QrPaths.DefaultFolder();
            string attachmentsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Attachments");

            try
            {
                (qrFolder, attachmentsFolder) = await ResolveCleanupFoldersAsync(contextFactory);
            }
            catch (Exception ex)
            {
                // تعذّرت قراءة مسارات الإعدادات: تُستخدم المجلدات الافتراضية للتنظيف.
                Console.WriteLine($"[Warning] Failed to read QR/Attachments paths from settings; using default folders: {ex.Message}");
            }

            // حارس الأمان: لا يُنظَّف مجلّد يحتوي مسارات البرنامج أو القاعدة أو النسخ الاحتياطيّة
            // أو مجلّدات المستخدم الكبرى؛ يُتخطّى ويُبلَّغ عنه ولا يُفشل التصفير.
            var guardPaths = BuildGuardPaths(context, protectedPaths);
            var skippedFolders = new List<string>();
            bool qrAllowed = IsSafeToClean(qrFolder, guardPaths, out string qrReason);
            if (!qrAllowed) skippedFolders.Add($"مجلّد QR ({qrFolder}): {qrReason}");
            bool attachmentsAllowed = IsSafeToClean(attachmentsFolder, guardPaths, out string attachmentsReason);
            if (!attachmentsAllowed) skippedFolders.Add($"مجلّد المرفقات ({attachmentsFolder}): {attachmentsReason}");

            // 4.1 تنظيف محتويات مجلد QR بالكامل (مسح كل الملفات دون حذف المجلد الرئيسي نفسه)
            if (qrAllowed && Directory.Exists(qrFolder))
            {
                try
                {
                    var qrFiles = Directory.GetFiles(qrFolder, "*", SearchOption.AllDirectories);
                    foreach (var filePath in qrFiles)
                    {
                        try
                        {
                            if (FileSystemRetryHelper.TryDeleteFile(filePath))
                            {
                                qrFilesDeleted++;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Warning] Failed to delete QR file {filePath}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Warning] Error listing files in QR directory {qrFolder}: {ex.Message}");
                }
            }

            // 4.2 تنظيف محتويات مجلد المرفقات بالكامل (مسح كل المجلدات والملفات الفرعية دون حذف مجلد Attachments الرئيسي)
            if (attachmentsAllowed && Directory.Exists(attachmentsFolder))
            {
                try
                {
                    var subDirectories = Directory.GetDirectories(attachmentsFolder);
                    foreach (var subDir in subDirectories)
                    {
                        try
                        {
                            if (FileSystemRetryHelper.TryDeleteDirectory(subDir))
                            {
                                attachmentFoldersDeleted++;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Warning] Failed to delete attachments subfolder {subDir}: {ex.Message}");
                        }
                    }

                    // مسح أي ملفات مستقلة في جذر مجلد Attachments إن وجدت
                    var rootFiles = Directory.GetFiles(attachmentsFolder);
                    foreach (var rootFile in rootFiles)
                    {
                        try
                        {
                            FileSystemRetryHelper.TryDeleteFile(rootFile);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Warning] Failed to delete attachments root file {rootFile}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Warning] Error listing attachments directory {attachmentsFolder}: {ex.Message}");
                }
            }

            // 8. تسجيل عملية التصفير — أول سطر في سجل التدقيق النظيف بعد مسحه أعلاه.
            //    يُكتب بعد الـcommit عمداً كي يعكس أرقام ملفات القرص المحذوفة.
            if (auditLogRepository != null)
            {
                try
                {
                    await auditLogRepository.LogAsync(
                        action: "تصفير كامل للنظام",
                        entityName: "System",
                        entityId: "FactoryReset",
                        details: $"تم التصفير الكامل للنظام وحذف كافة البيانات والملفات: {recordsCount} سجل معايرة، {devicesCount} جهاز، {ownersCount} جهة، {qrFilesDeleted} ملف QR، {attachmentFoldersDeleted} مجلد مرفقات."
                            + (resetCertificateSequence ? string.Empty : " أُبقي عدّاد ترقيم الشهادات.")
                            + (clearAuditLog ? string.Empty : " أُبقي سجلّ التدقيق.")
                            + (skippedFolders.Count == 0 ? string.Empty : $" تُخطّي تنظيف: {string.Join("؛ ", skippedFolders)}."),
                        userId: null
                    );
                }
                catch (Exception ex)
                {
                    // فشل تسجيل التصفير في سجل التدقيق لا يُبطل التصفير المكتمل.
                    Console.WriteLine($"[Warning] Failed to write factory-reset audit log entry: {ex.Message}");
                }
            }

            string message = $"تم تصفير النظام بالكامل وإعادته لحالة التثبيت النظيفة!\nالجهات المحذوفة: {ownersCount}\nالأجهزة المحذوفة: {devicesCount}\nالسجلات المحذوفة: {recordsCount}\nملفات QR المحذوفة: {qrFilesDeleted}\nمجلدات المرفقات المحذوفة: {attachmentFoldersDeleted}";
            if (!resetCertificateSequence) message += "\nأُبقي عدّاد ترقيم الشهادات: لن يتكرّر رقم شهادة سابقة.";
            if (!clearAuditLog) message += "\nأُبقي سجلّ التدقيق.";
            if (skippedFolders.Count > 0)
                message += "\n\nتنبيه: تُخطّي تنظيف المجلّدات التالية لأسباب أمان (لم يُحذف منها شيء):\n" + string.Join("\n", skippedFolders);

            return (
                true,
                message,
                ownersCount,
                devicesCount,
                recordsCount,
                qrFilesDeleted,
                attachmentFoldersDeleted
            );
        }

        /// <summary>
        /// مجلّدا QR والمرفقات اللذان سيُنظَّفان (من الإعدادات، وإلّا الافتراضيّ بجوار البرنامج).
        /// يُستعمل لعرض المسارين في رسالة التأكيد قبل التنفيذ ولتنفيذ التنظيف نفسه، فلا يختلفان.
        /// </summary>
        public static async Task<(string QrFolder, string AttachmentsFolder)> ResolveCleanupFoldersAsync(
            IDbContextFactory<CalQrDbContext> contextFactory)
        {
            string qrFolder = CAL_QR.Helpers.QrPaths.DefaultFolder();
            string attachmentsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Attachments");

            using var readContext = await contextFactory.CreateDbContextAsync();
            var qrSetting = await readContext.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "QrOutputPath");
            if (qrSetting != null && !string.IsNullOrWhiteSpace(qrSetting.Value))
            {
                qrFolder = qrSetting.Value;
            }

            var attSetting = await readContext.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "AttachmentsPath");
            if (attSetting != null && !string.IsNullOrWhiteSpace(attSetting.Value))
            {
                attachmentsFolder = attSetting.Value;
            }

            return (qrFolder, attachmentsFolder);
        }

        /// <summary>عدد الشهادات في القاعدة بما فيها الملغاة (كلّها استهلكت أرقاماً).</summary>
        public static async Task<int> CountCertificatesAsync(IDbContextFactory<CalQrDbContext> contextFactory)
        {
            using var context = await contextFactory.CreateDbContextAsync();
            return await context.Certificates.CountAsync();
        }

        /// <summary>
        /// دالّة نقيّة لحارس الأمان: false إذا كان المجلّد فارغاً/غير صالح، أو جذر قرص،
        /// أو يساوي أو يحتوي أحد المسارات المحميّة (مجلّد البرنامج، مجلّد القاعدة، النسخ
        /// الاحتياطيّة، مجلّدات المستخدم الكبرى: الملفّ الشخصيّ والمستندات وسطح المكتب
        /// وProgram Files وWindows). المجلّد الفرعيّ داخل مسار محميّ مسموح.
        /// </summary>
        public static bool IsSafeToClean(string folder, IEnumerable<string> protectedPaths, out string reason)
        {
            reason = string.Empty;
            if (string.IsNullOrWhiteSpace(folder))
            {
                reason = "المسار فارغ";
                return false;
            }

            string full;
            try
            {
                full = Path.GetFullPath(folder);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
            {
                reason = "المسار غير صالح";
                return false;
            }

            string root = Path.GetPathRoot(full) ?? string.Empty;
            if (root.Length > 0 && string.Equals(WithTrailingSeparator(root), WithTrailingSeparator(full), StringComparison.OrdinalIgnoreCase))
            {
                reason = "جذر قرص";
                return false;
            }

            foreach (string protectedPath in protectedPaths)
            {
                if (string.IsNullOrWhiteSpace(protectedPath)) continue;

                string protectedFull;
                try
                {
                    protectedFull = Path.GetFullPath(protectedPath);
                }
                catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
                {
                    continue;
                }

                if (IsSameOrAncestor(full, protectedFull))
                {
                    reason = $"يحتوي مساراً محميّاً ({protectedFull})";
                    return false;
                }
            }

            return true;
        }

        private static List<string> BuildGuardPaths(CalQrDbContext context, IEnumerable<string>? extraProtectedPaths)
        {
            var paths = new List<string> { AppDomain.CurrentDomain.BaseDirectory };

            foreach (var special in new[]
            {
                Environment.SpecialFolder.UserProfile,
                Environment.SpecialFolder.MyDocuments,
                Environment.SpecialFolder.DesktopDirectory,
                Environment.SpecialFolder.ProgramFiles,
                Environment.SpecialFolder.ProgramFilesX86,
                Environment.SpecialFolder.Windows,
            })
            {
                string path = Environment.GetFolderPath(special);
                if (!string.IsNullOrWhiteSpace(path)) paths.Add(path);
            }

            try
            {
                string dataSource = context.Database.GetDbConnection().DataSource;
                if (!string.IsNullOrWhiteSpace(dataSource) && !dataSource.StartsWith(":", StringComparison.Ordinal))
                {
                    string? dbDirectory = Path.GetDirectoryName(Path.GetFullPath(dataSource));
                    if (!string.IsNullOrWhiteSpace(dbDirectory)) paths.Add(dbDirectory);
                }
            }
            catch (InvalidOperationException)
            {
                // مزوّد بلا اتّصال علائقيّ (اختبارات InMemory): لا مجلّد قاعدة لحمايته.
            }

            if (extraProtectedPaths != null) paths.AddRange(extraProtectedPaths);
            return paths;
        }

        private static string WithTrailingSeparator(string path) =>
            path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
                ? path
                : path + Path.DirectorySeparatorChar;

        private static bool IsSameOrAncestor(string ancestor, string path)
        {
            string a = WithTrailingSeparator(ancestor);
            string p = WithTrailingSeparator(path);
            return p.StartsWith(a, StringComparison.OrdinalIgnoreCase);
        }
    }
}
