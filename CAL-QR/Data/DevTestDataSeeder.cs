using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Models;
using CAL_QR.Services;
using CAL_QR.Repositories;

namespace CAL_QR.Data
{
    public class DevSeededDataSnapshot
    {
        public List<int> OwnerIds { get; set; } = new();
        public List<int> DeviceTypeIds { get; set; } = new();
        public List<int> DeviceIds { get; set; } = new();
        public List<int> CalibrationRecordIds { get; set; } = new();
    }

    /// <summary>
    /// أداة توليد بيانات تجريبية (Test Data Seeder) آمنة وقابلة للإزالة لغرض الاختبار والتحقق من الأداء والواجهات.
    /// تعتمد على وسم صريح (IsSeedTestData = true) على الكيانات لضمان الموثوقية التامة في التوليد والحذف.
    /// </summary>
    public static class DevTestDataSeeder
    {
        public static async Task<(bool Success, string Message, int OwnersAdded, int DevicesAdded, int RecordsAdded, int ValidCount, int NearExpiryCount, int ExpiredCount)> SeedTestDataAsync(
            IDbContextFactory<CalQrDbContext> contextFactory,
            IHmacService hmacService,
            IAuditLogRepository? auditLogRepository = null)
        {
            // 1. تنظيف أي بيانات تجريبية موسومة سابقة لضمان حالة نظيفة بلا تراكم
            await ClearSeedTestDataAsync(contextFactory);

            using var context = await contextFactory.CreateDbContextAsync();

            // 1. أنواع الأجهزة (DeviceTypes) - 5 أنواع واقعية (أنواع حقيقية معتمدة لا تُوسم ولا تُحذف)
            var requiredDeviceTypeNames = new[]
            {
                "Pancake Probe",
                "Gamma Probe",
                "Beta Scintillator Probe",
                "PED",
                "Dose Rate Meter"
            };

            var existingTypes = await context.DeviceTypes.Where(t => !t.IsDeleted).ToListAsync();
            var deviceTypesList = new List<DeviceType>();
            var newlyCreatedDeviceTypeIds = new List<int>();

            foreach (var typeName in requiredDeviceTypeNames)
            {
                var existing = existingTypes.FirstOrDefault(t => t.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase));
                if (existing == null)
                {
                    var newType = new DeviceType
                    {
                        Name = typeName,
                        IsDeleted = false,
                        CreatedAt = DateTime.UtcNow
                    };
                    context.DeviceTypes.Add(newType);
                    deviceTypesList.Add(newType);
                }
                else
                {
                    deviceTypesList.Add(existing);
                }
            }
            await context.SaveChangesAsync();

            foreach (var dt in deviceTypesList)
            {
                if (!existingTypes.Any(e => e.Id == dt.Id))
                {
                    newlyCreatedDeviceTypeIds.Add(dt.Id);
                }
            }

            // 2. الجهات المالكة (Owners) - 10 جهات موسومة بـ IsSeedTestData = true
            var ownerSeedData = new[]
            {
                (Name: "مستشفى بنغازي الطبي", Address: "بنغازي - الهواري", Phone: "0912345678", Person: "د. عبد السلام المصراتي"),
                (Name: "شركة الخليج العربي للبترول", Address: "بنغازي - الكيش", Phone: "0923456789", Person: "م. محمد الزوي"),
                (Name: "مصنع الإسمنت الوطني", Address: "الخمس - المنطقة الصناعية", Phone: "0919876543", Person: "أ. علي الورفلي"),
                (Name: "مركز طرابلس الطبي", Address: "طرابلس - الفرناج", Phone: "0921112233", Person: "د. حاتم الترهوني"),
                (Name: "شركة الواحة للنفط", Address: "طرابلس - شارع الجلاية", Phone: "0915554433", Person: "م. صلاح الشريف"),
                (Name: "مستشفى الجلاء للجراحة والسانحات", Address: (string?)null, Phone: (string?)null, Person: (string?)null),
                (Name: "شركة سرت لإنتاج وتصنيع النفط والغاز", Address: (string?)null, Phone: (string?)null, Person: (string?)null),
                (Name: "مستشفى المرج التعليمي", Address: (string?)null, Phone: (string?)null, Person: (string?)null),
                (Name: "شركة رأس لانوف للمصنعات النفطية", Address: (string?)null, Phone: (string?)null, Person: (string?)null),
                (Name: "مصنع حديد ومصلب مصراتة", Address: (string?)null, Phone: (string?)null, Person: (string?)null)
            };

            var createdOwners = new List<Owner>();
            foreach (var seed in ownerSeedData)
            {
                var owner = new Owner
                {
                    Name = seed.Name,
                    Address = seed.Address,
                    ContactPhone = seed.Phone,
                    ContactPerson = seed.Person,
                    IsDeleted = false,
                    IsSeedTestData = true,
                    CreatedAt = DateTime.UtcNow
                };
                context.Owners.Add(owner);
                createdOwners.Add(owner);
            }
            await context.SaveChangesAsync();

            // 3. الأجهزة (Devices) - 100 جهاز فريد الرقم التسلسلي وموسوم بـ IsSeedTestData = true
            var modelOptions = new[]
            {
                "Ludlum 44-9",
                "Thermo FH 40 G",
                "Mirion RDS-31",
                "Canberra Colibri",
                "Ludlum 2241",
                "Eberline E-600",
                "Fluke 451P",
                "Polimaster PM1610"
            };

            var random = new Random(42);
            var createdDevices = new List<Device>();
            var usedSerials = new HashSet<string>(await context.Devices.Select(d => d.SerialNumber).ToListAsync());

            for (int i = 1; i <= 100; i++)
            {
                string serial;
                do
                {
                    int randNum = random.Next(100000, 999999);
                    serial = $"PR{randNum}";
                } while (usedSerials.Contains(serial));

                usedSerials.Add(serial);

                var owner = createdOwners[random.Next(createdOwners.Count)];
                var deviceType = deviceTypesList[random.Next(deviceTypesList.Count)];
                var model = modelOptions[random.Next(modelOptions.Length)];

                var device = new Device
                {
                    Model = model,
                    SerialNumber = serial,
                    OwnerId = owner.Id,
                    DeviceTypeId = deviceType.Id,
                    IsDeleted = false,
                    IsSeedTestData = true,
                    CreatedAt = DateTime.UtcNow
                };

                context.Devices.Add(device);
                createdDevices.Add(device);
            }
            await context.SaveChangesAsync();

            // 4. سجلات المعايرة (CalibrationRecords) - 200 سجل موسوم بـ IsSeedTestData = true
            var engineerNames = new[]
            {
                "م. أحمد الشريف",
                "م. سالم العبيدي",
                "م. طارق الزوي",
                "م. فاطمة الفيتوري",
                "م. علي الفرجاني"
            };

            var today = DateTime.Today;
            var createdRecords = new List<CalibrationRecord>();
            int validCount = 0;
            int nearExpiryCount = 0;
            int expiredCount = 0;

            var existingCerts = new HashSet<string>(await context.CalibrationRecords.Select(c => c.CertificateNumber).ToListAsync());

            var targetCategories = new List<string>();
            for (int i = 0; i < 120; i++) targetCategories.Add("Valid");
            for (int i = 0; i < 30; i++) targetCategories.Add("NearExpiry");
            for (int i = 0; i < 50; i++) targetCategories.Add("Expired");

            targetCategories = targetCategories.OrderBy(_ => random.Next()).ToList();

            var targetResults = new List<string>();
            for (int i = 0; i < 160; i++) targetResults.Add("Passed");
            for (int i = 0; i < 20; i++) targetResults.Add("Failed");
            for (int i = 0; i < 20; i++) targetResults.Add("Conditional");
            targetResults = targetResults.OrderBy(_ => random.Next()).ToList();

            for (int i = 0; i < 200; i++)
            {
                string certNo;
                int certSeq = i + 1;
                do
                {
                    certNo = $"TEST-2026-{certSeq:D4}";
                    certSeq++;
                } while (existingCerts.Contains(certNo));
                existingCerts.Add(certNo);

                string category = targetCategories[i];
                DateTime expiryDate;

                if (category == "Valid")
                {
                    int daysInFuture = random.Next(31, 730);
                    expiryDate = today.AddDays(daysInFuture);
                    validCount++;
                }
                else if (category == "NearExpiry")
                {
                    int daysInFuture = random.Next(0, 31);
                    expiryDate = today.AddDays(daysInFuture);
                    nearExpiryCount++;
                }
                else // Expired
                {
                    int daysInPast = random.Next(1, 730);
                    expiryDate = today.AddDays(-daysInPast);
                    expiredCount++;
                }

                int calDurationMonths = random.Next(6, 25);
                DateTime calDate = expiryDate.AddMonths(-calDurationMonths);

                Device device = (i < 100) ? createdDevices[i] : createdDevices[random.Next(createdDevices.Count)];
                var owner = createdOwners.First(o => o.Id == device.OwnerId);

                string result = targetResults[i];
                string engineerName = engineerNames[random.Next(engineerNames.Length)];
                string? calDescription = (i % 3 == 0) ? "معايرة دورية شملت فحص الكاشف والجرعة الإشعاعية" : null;

                string calDateStr = calDate.ToString("yyyy-MM-dd");
                string expDateStr = expiryDate.ToString("yyyy-MM-dd");

                string signature = hmacService.ComputeSignature(
                    certNo,
                    device.Model,
                    device.SerialNumber,
                    owner.Name,
                    calDateStr,
                    expDateStr,
                    result,
                    engineerName
                );

                var now = DateTime.UtcNow;

                var record = new CalibrationRecord
                {
                    DeviceId = device.Id,
                    CertificateNumber = certNo,
                    CalibrationDate = calDate,
                    ExpiryDate = expiryDate,
                    EngineerName = engineerName,
                    CalibrationDescription = calDescription,
                    Result = result,
                    HmacSignature = signature,
                    IsDeleted = false,
                    IsSeedTestData = true,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                context.CalibrationRecords.Add(record);
                createdRecords.Add(record);
            }

            await context.SaveChangesAsync();

            // حفظ الـ Snapshot في AppSettings كسجل توثيقي/تدقيقي إضافي
            var snapshot = new DevSeededDataSnapshot
            {
                OwnerIds = createdOwners.Select(o => o.Id).ToList(),
                DeviceTypeIds = newlyCreatedDeviceTypeIds,
                DeviceIds = createdDevices.Select(d => d.Id).ToList(),
                CalibrationRecordIds = createdRecords.Select(r => r.Id).ToList()
            };

            string json = JsonSerializer.Serialize(snapshot);
            var existingSnapshotSetting = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == "DevSeededDataSnapshot");
            if (existingSnapshotSetting != null)
            {
                existingSnapshotSetting.Value = json;
                existingSnapshotSetting.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                context.AppSettings.Add(new AppSetting
                {
                    Key = "DevSeededDataSnapshot",
                    Value = json,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            await context.SaveChangesAsync();

            if (auditLogRepository != null)
            {
                try
                {
                    await auditLogRepository.LogAsync(
                        action: "توليد بيانات تجريبية",
                        entityName: "System",
                        entityId: "Seeder",
                        details: $"تم توليد {createdOwners.Count} جهات، {createdDevices.Count} أجهزة، {createdRecords.Count} سجلات معايرة ({validCount} سارٍ، {nearExpiryCount} قريب الانتهاء، {expiredCount} منتهٍ).",
                        userId: null
                    );
                }
                catch { }
            }

            return (
                true,
                $"تم توليد البيانات بنجاح!\nالجهات: {createdOwners.Count}\nالأجهزة: {createdDevices.Count}\nالسجلات: {createdRecords.Count} (سارية: {validCount}، قريبة الانتهاء: {nearExpiryCount}، منتهية: {expiredCount})",
                createdOwners.Count,
                createdDevices.Count,
                createdRecords.Count,
                validCount,
                nearExpiryCount,
                expiredCount
            );
        }

        public static async Task<(bool Success, string Message, int OwnersRemoved, int DevicesRemoved, int RecordsRemoved, int QrFilesRemoved, int AttachmentFoldersRemoved)> ClearSeedTestDataAsync(
            IDbContextFactory<CalQrDbContext> contextFactory,
            IAuditLogRepository? auditLogRepository = null)
        {
            using var context = await contextFactory.CreateDbContextAsync();

            // الاستعلام المباشر عن الكيانات الموسومة صراحة بـ IsSeedTestData = true
            var recordsToDelete = await context.CalibrationRecords
                .Where(r => r.IsSeedTestData)
                .ToListAsync();

            var devicesToDelete = await context.Devices
                .Where(d => d.IsSeedTestData)
                .ToListAsync();

            var ownersToDelete = await context.Owners
                .Where(o => o.IsSeedTestData)
                .ToListAsync();

            if (recordsToDelete.Count == 0 && devicesToDelete.Count == 0 && ownersToDelete.Count == 0)
            {
                return (false, "لا توجد بيانات تجريبية موسومة للحذف حالياً.", 0, 0, 0, 0, 0);
            }

            // 1. تجميع البيانات المرجعية قبل إجراء عمليات الحذف
            var certificateNumbers = recordsToDelete.Select(r => r.CertificateNumber).Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
            var deviceIds = devicesToDelete.Select(d => d.Id).ToList();
            var calibrationRecordIds = recordsToDelete.Select(r => r.Id).ToList();

            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                // 1. حذف سجلات المعايرة الموسومة
                context.CalibrationRecords.RemoveRange(recordsToDelete);

                // 2. حذف الأجهزة الموسومة
                context.Devices.RemoveRange(devicesToDelete);

                // 3. حذف الجهات المالكة الموسومة
                context.Owners.RemoveRange(ownersToDelete);

                // 4. حذف سجلات التنبيهات المعتمدة المرتبطة بهذه الأجهزة/السجلات (AcknowledgedExpiredDevices)
                // الملاحظة التوثيقية: يحتوي جدول AcknowledgedExpiredDevices على الأعمدة: Id, DeviceId, CalibrationRecordId, AcknowledgedDate
                var orphanedAcks = await context.AcknowledgedExpiredDevices
                    .Where(a => deviceIds.Contains(a.DeviceId) || calibrationRecordIds.Contains(a.CalibrationRecordId))
                    .ToListAsync();
                context.AcknowledgedExpiredDevices.RemoveRange(orphanedAcks);

                // 5. إزالة سجل الـ Snapshot المتبقي في AppSettings إن وجد (تنظيف توثيقي)
                var setting = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == "DevSeededDataSnapshot");
                if (setting != null)
                {
                    context.AppSettings.Remove(setting);
                }

                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"حدث خطأ أثناء حذف البيانات التجريبية من قاعدة البيانات: {ex.Message}", 0, 0, 0, 0, 0);
            }

            // مرحلة ما بعد الحذف في قاعدة البيانات (Post-Commit Phase): تنظيف الملفات من القرص
            int qrFilesDeleted = 0;
            int attachmentFoldersDeleted = 0;

            string qrFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "QR");
            string attachmentsFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Attachments");

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

            // 3.1 حذف ملفات كود QR
            foreach (var certNo in certificateNumbers)
            {
                try
                {
                    string qrPath = System.IO.Path.Combine(qrFolder, $"{certNo.Trim()}.png");
                    if (System.IO.File.Exists(qrPath))
                    {
                        for (int attempt = 0; attempt < 3; attempt++)
                        {
                            try
                            {
                                System.IO.File.Delete(qrPath);
                                break;
                            }
                            catch when (attempt < 2)
                            {
                                System.Threading.Thread.Sleep(50);
                            }
                        }

                        if (!System.IO.File.Exists(qrPath))
                        {
                            qrFilesDeleted++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Warning] Failed to delete QR file for certificate {certNo}: {ex.Message}");
                }
            }

            // 3.2 حذف مجلدات المرفقات بالكامل
            foreach (var certNo in certificateNumbers)
            {
                try
                {
                    string certAttachmentFolder = System.IO.Path.Combine(attachmentsFolder, certNo.Trim());
                    if (System.IO.Directory.Exists(certAttachmentFolder))
                    {
                        for (int attempt = 0; attempt < 3; attempt++)
                        {
                            try
                            {
                                System.IO.Directory.Delete(certAttachmentFolder, recursive: true);
                                break;
                            }
                            catch when (attempt < 2)
                            {
                                System.Threading.Thread.Sleep(50);
                            }
                        }

                        if (!System.IO.Directory.Exists(certAttachmentFolder))
                        {
                            attachmentFoldersDeleted++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Warning] Failed to delete attachments folder for certificate {certNo}: {ex.Message}");
                }
            }

            // سجل عمليات الحذف
            if (auditLogRepository != null)
            {
                try
                {
                    await auditLogRepository.LogAsync(
                        action: "حذف بيانات تجريبية",
                        entityName: "System",
                        entityId: "Seeder",
                        details: $"تم حذف البيانات التجريبية نهائياً: {recordsToDelete.Count} سجل معايرة، {devicesToDelete.Count} جهاز، {ownersToDelete.Count} جهة، {qrFilesDeleted} ملف QR، {attachmentFoldersDeleted} مجلد مرفقات.",
                        userId: null
                    );
                }
                catch { }
            }

            return (
                true,
                $"تم حذف البيانات التجريبية بنجاح!\nالجهات المحذوفة: {ownersToDelete.Count}\nالأجهزة المحذوفة: {devicesToDelete.Count}\nالسجلات المحذوفة: {recordsToDelete.Count}\nملفات QR المحذوفة: {qrFilesDeleted}\nمجلدات المرفقات المحذوفة: {attachmentFoldersDeleted}",
                ownersToDelete.Count,
                devicesToDelete.Count,
                recordsToDelete.Count,
                qrFilesDeleted,
                attachmentFoldersDeleted
            );
        }
    }
}
