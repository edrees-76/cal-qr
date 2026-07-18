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

            // Run standard simulation with 500 devices and 2000 records
            await RunSimulationAsync(destDb, 500, 2000);
        }

        internal async Task RunSimulationAsync(string destDb, int numDevices = 500, int numRecords = 2000)
        {
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseSqlite($"Data Source={destDb}")
                .Options;

            using (var context = new CalQrDbContext(options))
            {
                // Verify schema is initialized
                DatabaseMigrator.RunMigrations(context);

                // Clear existing simulation data (except Templates, AppSettings, Users)
                context.CalibrationRecords.RemoveRange(context.CalibrationRecords);
                context.Devices.RemoveRange(context.Devices);
                context.Owners.RemoveRange(context.Owners);
                context.AuditLogs.RemoveRange(context.AuditLogs);
                await context.SaveChangesAsync();

                // Seed 10 Owners with varying name lengths
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

                // Query device types to associate randomly
                var deviceTypes = await context.DeviceTypes.Where(t => !t.IsDeleted).ToListAsync();
                if (deviceTypes.Count == 0)
                {
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

                // Generate numDevices Devices
                var devices = new List<Device>();
                var rand = new Random(42);

                string[] models = { "GeigerPro-100", "RadMonitor-250", "DoseRate-5X", "PocketRad-99", "SurveyMaster-3000", "IonChamber-A1", "RadGuard-S1", "GammaFinder-X" };

                for (int i = 1; i <= numDevices; i++)
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

                // Generate Calibration Records
                var records = new List<CalibrationRecord>();
                var factory = new TestDbContextFactory(options);
                var hmacService = new HmacService(factory);
                hmacService.Initialize();
                var today = DateTime.Today;

                // We reserve 25 devices to have NO calibration records
                int activeDevicesCount = numDevices - 25;
                if (activeDevicesCount <= 0) activeDevicesCount = numDevices;

                // Distribute numRecords among active devices
                int[] recordsPerDevice = new int[activeDevicesCount];
                for (int i = 0; i < activeDevicesCount; i++)
                {
                    recordsPerDevice[i] = 1;
                }

                int remaining = numRecords - activeDevicesCount;
                while (remaining > 0)
                {
                    int idx = rand.Next(activeDevicesCount);
                    if (recordsPerDevice[idx] < 8)
                    {
                        recordsPerDevice[idx]++;
                        remaining--;
                    }
                }

                for (int i = 0; i < activeDevicesCount; i++)
                {
                    var device = devices[i];
                    int totalRecs = recordsPerDevice[i];

                    // Determine the state of the latest record
                    // 60% Active, 15% Near Expiry, 25% Expired
                    DateTime latestCalDate;
                    DateTime latestExpDate;
                    string latestResult = "Passed";

                    int resRand = rand.Next(10);
                    if (resRand == 8) latestResult = "Failed";
                    else if (resRand == 9) latestResult = "Conditional";

                    int stateRand = rand.Next(100);
                    if (stateRand < 60)
                    {
                        // Active
                        latestCalDate = today.AddMonths(-rand.Next(2, 10)).AddDays(rand.Next(1, 28));
                        latestExpDate = latestCalDate.AddYears(1);
                    }
                    else if (stateRand < 75)
                    {
                        // Near Expiry
                        latestExpDate = today.AddDays(rand.Next(2, 28));
                        latestCalDate = latestExpDate.AddYears(-1);
                    }
                    else
                    {
                        // Expired
                        latestCalDate = today.AddMonths(-rand.Next(13, 24)).AddDays(rand.Next(1, 28));
                        latestExpDate = latestCalDate.AddYears(1);
                    }

                    // Create records
                    for (int j = 0; j < totalRecs; j++)
                    {
                        DateTime calDate = latestCalDate.AddYears(-j);
                        DateTime expDate = latestExpDate.AddYears(-j);
                        string resultState = j == 0 ? latestResult : (rand.Next(10) == 0 ? "Failed" : "Passed");
                        string engineer = j == 0 ? "مهندس المعايرة الرئيسي" : "مهندس المعايرة السابق";

                        records.Add(CreateRecord(device, calDate, expDate, resultState, engineer, hmacService));
                    }
                }

                await context.CalibrationRecords.AddRangeAsync(records);
                await context.SaveChangesAsync();

                // Generate some mock Audit Logs for realism
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
