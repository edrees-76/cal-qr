using System;
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
    /// مع إعادة إضافة أنواع الأجهزة الخمسة الافتراضية، والحفاظ على الإعدادات وسجل AuditLog.
    /// </summary>
    public static class SystemResetService
    {
        public const string RequiredConfirmationPhrase = "RESET-ALL-DATA";

        public static async Task<(bool Success, string Message, int OwnersRemoved, int DevicesRemoved, int RecordsRemoved, int QrFilesRemoved, int AttachmentFoldersRemoved)> FactoryResetAsync(
            IDbContextFactory<CalQrDbContext> contextFactory,
            string confirmationPhrase,
            IAuditLogRepository? auditLogRepository = null)
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
                // الجداول الأبناء الثلاثة للشهادة تُحذف تلقائياً بسلوك Cascade.
                context.Certificates.RemoveRange(await context.Certificates.ToListAsync());

                // 2. مسح كافة سجلات المعايرة
                context.CalibrationRecords.RemoveRange(await context.CalibrationRecords.ToListAsync());

                // 3. مسح كافة سجلات التنبيهات المعتمدة (AcknowledgedExpiredDevices)
                context.AcknowledgedExpiredDevices.RemoveRange(await context.AcknowledgedExpiredDevices.ToListAsync());

                // 4. مسح كافة الأجهزة
                context.Devices.RemoveRange(await context.Devices.ToListAsync());

                // 5. مسح كافة الجهات المالكة
                context.Owners.RemoveRange(await context.Owners.ToListAsync());

                // 6. مسح كافة أنواع الأجهزة وإعادة إضافة الأنواع الخمسة الافتراضية لربط النظام بحالة تثبيت نظيفة
                context.DeviceTypes.RemoveRange(await context.DeviceTypes.ToListAsync());

                var defaultDeviceTypes = new[]
                {
                    "Pancake Probe",
                    "Gamma Probe",
                    "Beta Scintillator Probe",
                    "PED",
                    "Dose Rate Meter"
                };

                foreach (var name in defaultDeviceTypes)
                {
                    context.DeviceTypes.Add(new DeviceType
                    {
                        Name = name,
                        IsDeleted = false,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                // 7. إزالة سجل الـ Snapshot المتبقي في AppSettings إن وجد (مع الحفاظ الكامل على بقية الإعدادات وسجل AuditLog)
                var snapshotSetting = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == "DevSeededDataSnapshot");
                if (snapshotSetting != null)
                {
                    context.AppSettings.Remove(snapshotSetting);
                }

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

            string qrFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "QR");
            string attachmentsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Attachments");

            try
            {
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
            }
            catch { }

            // 4.1 تنظيف محتويات مجلد QR بالكامل (مسح كل الملفات دون حذف المجلد الرئيسي نفسه)
            if (Directory.Exists(qrFolder))
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
            if (Directory.Exists(attachmentsFolder))
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
                        catch { }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Warning] Error listing attachments directory {attachmentsFolder}: {ex.Message}");
                }
            }

            // 5. تسجيل العملية في سجل العمليات (AuditLog) للتوثيق النهائي الناجي من التصفير
            if (auditLogRepository != null)
            {
                try
                {
                    await auditLogRepository.LogAsync(
                        action: "تصفير كامل للنظام",
                        entityName: "System",
                        entityId: "FactoryReset",
                        details: $"تم التصفير الكامل للنظام وحذف كافة البيانات والملفات: {recordsCount} سجل معايرة، {devicesCount} جهاز، {ownersCount} جهة، {qrFilesDeleted} ملف QR، {attachmentFoldersDeleted} مجلد مرفقات.",
                        userId: null
                    );
                }
                catch { }
            }

            return (
                true,
                $"تم تصفير النظام بالكامل وإعادته لحالة التثبيت النظيفة!\nالجهات المحذوفة: {ownersCount}\nالأجهزة المحذوفة: {devicesCount}\nالسجلات المحذوفة: {recordsCount}\nملفات QR المحذوفة: {qrFilesDeleted}\nمجلدات المرفقات المحذوفة: {attachmentFoldersDeleted}",
                ownersCount,
                devicesCount,
                recordsCount,
                qrFilesDeleted,
                attachmentFoldersDeleted
            );
        }
    }
}
