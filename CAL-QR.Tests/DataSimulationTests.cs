using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Services;

namespace CAL_QR.Tests
{
    public class DataSimulationTests
    {
        [Fact]
        public async Task SimulateRealisticData()
        {
            // 1. Resolve paths to the WPF bin directory
            string testDir = AppDomain.CurrentDomain.BaseDirectory;
            string sourceDb = Path.GetFullPath(Path.Combine(testDir, @"..\..\..\..\CAL-QR\bin\Debug\net8.0-windows\cal-qr.db"));
            string destDb = Path.GetFullPath(Path.Combine(testDir, @"..\..\..\..\CAL-QR\bin\Debug\net8.0-windows\cal-qr-simulation.db"));

            // Ensure source directory exists
            string wpfBinDir = Path.GetDirectoryName(sourceDb)!;
            if (!Directory.Exists(wpfBinDir))
            {
                Directory.CreateDirectory(wpfBinDir);
            }

            // Copy to simulation database file
            if (File.Exists(sourceDb))
            {
                File.Copy(sourceDb, destDb, overwrite: true);
            }

            // 2. Configure DbContext targeting simulation DB exclusively
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseSqlite($"Data Source={destDb}")
                .Options;

            using (var context = new CalQrDbContext(options))
            {
                // Verify schema is initialized
                DatabaseMigrator.RunMigrations(context);

                // 3. Clear existing simulation data (except Templates, AppSettings, Users)
                context.CalibrationRecords.RemoveRange(context.CalibrationRecords);
                context.Devices.RemoveRange(context.Devices);
                context.Owners.RemoveRange(context.Owners);
                context.AuditLogs.RemoveRange(context.AuditLogs);
                await context.SaveChangesAsync();

                // 4. Seeding 10 Owners with varying name lengths
                var owners = new List<Owner>
                {
                    new Owner { Name = "وزارة الصحة", ContactPerson = "د. أحمد علي", ContactPhone = "0911234567", IsDeleted = false },
                    new Owner { Name = "إدارة الرقابة الإشعاعية والوقاية من الإشعاع", ContactPerson = "م. خالد مصطفى", ContactPhone = "0921234567", IsDeleted = false },
                    new Owner { Name = "مستشفى الجلاء التعليمي بطرابلس", ContactPerson = "د. عمر محمود", ContactPhone = "0917654321", IsDeleted = false },
                    new Owner { Name = "شركة التقنية المتقدمة للخدمات البيئية", ContactPerson = "م. نجيب الصادق", ContactPhone = "0941234567", IsDeleted = false },
                    new Owner { Name = "مركز البحوث الطبية", ContactPerson = "د. فاطمة سالم", ContactPhone = "0919876543", IsDeleted = false },
                    new Owner { Name = "مصلحة الجمارك الليبية", ContactPerson = "العقيد يوسف بلقاسم", ContactPhone = "0923456789", IsDeleted = false },
                    new Owner { Name = "مؤسسة الطاقة الذرية", ContactPerson = "د. صالح المهدي", ContactPhone = "0914567890", IsDeleted = false },
                    new Owner { Name = "شركة الواحة للنفط", ContactPerson = "م. عبد السلام نصر", ContactPhone = "0916789012", IsDeleted = false },
                    new Owner { Name = "جامعة بنغازي - كلية العلوم", ContactPerson = "أ.د. عبد الله المختار", ContactPhone = "0929998877", IsDeleted = false },
                    new Owner { Name = "المركز الوطني لمكافحة الأمراض", ContactPerson = "د. فرج السنوسي", ContactPhone = "0918882211", IsDeleted = false }
                };

                await context.Owners.AddRangeAsync(owners);
                await context.SaveChangesAsync();

                // 5. Query device types to associate randomly
                var deviceTypes = await context.DeviceTypes.Where(t => !t.IsDeleted).ToListAsync();
                if (deviceTypes.Count == 0)
                {
                    // Add default types if missing
                    deviceTypes = new List<DeviceType>
                    {
                        new DeviceType { Name = "عداد غايغر - Geiger Counter", IsDeleted = false },
                        new DeviceType { Name = "مقياس الجرعات الجيبي - Pocket Dosimeter", IsDeleted = false },
                        new DeviceType { Name = "كاشف الوميض - Scintillation Detector", IsDeleted = false },
                        new DeviceType { Name = "غرفة التأين - Ionization Chamber", IsDeleted = false },
                        new DeviceType { Name = "مقياس المسح المحمول - Portable Survey Meter", IsDeleted = false }
                    };
                    await context.DeviceTypes.AddRangeAsync(deviceTypes);
                    await context.SaveChangesAsync();
                }

                // 6. Generate 500 Devices
                var devices = new List<Device>();
                var rand = new Random(42); // Seeded random for deterministic output

                string[] models = { "GeigerPro-100", "RadMonitor-250", "DoseRate-5X", "PocketRad-99", "SurveyMaster-3000", "IonChamber-A1", "RadGuard-S1", "GammaFinder-X" };

                for (int i = 1; i <= 500; i++)
                {
                    var owner = owners[rand.Next(owners.Count)];
                    var type = deviceTypes[rand.Next(deviceTypes.Count)];
                    var model = models[rand.Next(models.Length)];
                    var serial = $"SN-{rand.Next(10000, 99999)}-{(char)rand.Next(65, 90)}{(char)rand.Next(65, 90)}";

                    devices.Add(new Device
                    {
                        Model = model,
                        SerialNumber = serial,
                        OwnerId = owner.Id,
                        DeviceTypeId = type.Id,
                        IsDeleted = false
                    });
                }

                await context.Devices.AddRangeAsync(devices);
                await context.SaveChangesAsync();

                // 7. Generate Calibration Records
                var records = new List<CalibrationRecord>();
                var hmacService = new HmacService();
                var today = DateTime.Today;

                // We distribute the 500 devices:
                // Group 1: 300 devices (Active, ~60%)
                // Group 2: 75 devices (Near Expiry, ~15%)
                // Group 3: 75 devices (Expired, ~15%)
                // Group 4: 25 devices (No Calibration Record, ~5%)
                // Group 5: 25 devices (Multiple Consecutive Records, ~5%)

                for (int i = 0; i < devices.Count; i++)
                {
                    var device = devices[i];
                    int groupIndex = i / 25; // 0 to 19

                    // Group 4: No calibration record (25 devices)
                    if (groupIndex == 18)
                    {
                        continue;
                    }

                    // Group 5: Multiple consecutive records (25 devices)
                    if (groupIndex == 19)
                    {
                        // Record 1 (Old, expired)
                        var dateOld = today.AddYears(-2).AddDays(rand.Next(1, 30));
                        var expOld = dateOld.AddYears(1);
                        var recOld = CreateRecord(device, dateOld, expOld, "Failed", "مهندس المعايرة الأول", hmacService);
                        records.Add(recOld);

                        // Record 2 (Current, active)
                        var dateNew = today.AddMonths(-rand.Next(2, 6));
                        var expNew = dateNew.AddYears(1);
                        var recNew = CreateRecord(device, dateNew, expNew, "Passed", "مهندس المعايرة الثاني", hmacService);
                        records.Add(recNew);

                        continue;
                    }

                    // Standard Groups (0 to 17)
                    DateTime calDate;
                    DateTime expDate;
                    string resultState = "Passed";

                    // Randomize result: 80% Passed, 10% Failed, 10% Conditional
                    int resRand = rand.Next(10);
                    if (resRand == 8) resultState = "Failed";
                    else if (resRand == 9) resultState = "Conditional";

                    if (groupIndex < 12)
                    {
                        // Group 1: Active (300 devices) - CalibrationDate from 2 to 10 months ago
                        calDate = today.AddMonths(-rand.Next(2, 10)).AddDays(rand.Next(1, 28));
                        expDate = calDate.AddYears(1);
                    }
                    else if (groupIndex < 15)
                    {
                        // Group 2: Near Expiry (75 devices) - ExpiryDate within 30 days
                        // ExpiryDate from today + 2 days to today + 28 days. So CalibrationDate is 1 year before that.
                        expDate = today.AddDays(rand.Next(2, 28));
                        calDate = expDate.AddYears(-1);
                    }
                    else
                    {
                        // Group 3: Expired (75 devices) - CalibrationDate from 13 to 24 months ago
                        calDate = today.AddMonths(-rand.Next(13, 24)).AddDays(rand.Next(1, 28));
                        expDate = calDate.AddYears(1);
                    }

                    records.Add(CreateRecord(device, calDate, expDate, resultState, "مهندس المعايرة الرئيسي", hmacService));
                }

                await context.CalibrationRecords.AddRangeAsync(records);
                await context.SaveChangesAsync();

                // 8. Generate some mock Audit Logs for realism
                var logs = new List<AuditLog>();
                for (int i = 1; i <= 50; i++)
                {
                    logs.Add(new AuditLog
                    {
                        Action = i % 2 == 0 ? "إضافة جهاز" : "معايرة جهاز",
                        EntityName = i % 2 == 0 ? "Device" : "CalibrationRecord",
                        EntityId = i.ToString(),
                        Details = i % 2 == 0 ? $"إضافة جهاز محاكى موديل {models[rand.Next(models.Length)]}" : $"إجراء معايرة دورية ناجحة للشهادة CERT-SIM-{1000 + i}",
                        ActionAt = today.AddDays(-rand.Next(1, 60))
                    });
                }
                await context.AuditLogs.AddRangeAsync(logs);
                await context.SaveChangesAsync();
            }
        }

        private static int _certCounter = 10000;

        private CalibrationRecord CreateRecord(Device device, DateTime calDate, DateTime expDate, string result, string engineer, HmacService hmacService)
        {
            string certNo = $"CERT-SIM-{System.Threading.Interlocked.Increment(ref _certCounter)}";

            string signature = hmacService.ComputeSignature(
                certNo: certNo,
                model: device.Model,
                serial: device.SerialNumber,
                ownerName: device.Owner?.Name ?? "غير محدد",
                calDate: calDate.ToString("yyyy-MM-dd"),
                expDate: expDate.ToString("yyyy-MM-dd"),
                result: result,
                engineerName: engineer
            );

            return new CalibrationRecord
            {
                DeviceId = device.Id,
                CertificateNumber = certNo,
                CalibrationDate = calDate,
                ExpiryDate = expDate,
                Result = result,
                EngineerName = engineer,
                CalibrationDescription = "معايرة ومطابقة دورية محاكاة لضمان كفاءة أجهزة المسح الإشعاعي.",
                HmacSignature = signature,
                IsDeleted = false
            };
        }
    }
}
