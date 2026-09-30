using System.Globalization;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Repositories;
using CAL_QR.Services;

namespace CAL_QR.DemoData;

/// <summary>
/// يولّد قاعدة بيانات تجريبيّة منفصلة بالمسار الحقيقيّ للمنظومة: المُرحِّل، مفتاح HMAC، وإصدار كلّ شهادة
/// عبر CertificateRepository.AddAsync (ترقيم TNRC-SSDL-YYYY-XXXX + توقيع SIG1 + حمولة QR)، فتتحقّق كلّها
/// بـ VerifyByCodeAsync. كلّ القيم القياسيّة خياليّة. الجهات والأجهزة والسجلّات موسومة IsSeedTestData.
/// </summary>
public static class DemoDataGenerator
{
    private sealed class Factory : IDbContextFactory<CalQrDbContext>
    {
        private readonly DbContextOptions<CalQrDbContext> _options;
        public Factory(string dbPath) =>
            _options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseSqlite($"Data Source={dbPath};Default Timeout=5").Options;
        public CalQrDbContext CreateDbContext() => new CalQrDbContext(_options);
    }

    private sealed record Plan(int DeviceIndex, DateTime CalibrationDate, int EngineerIndex);

    public static async Task<DemoDataSummary> GenerateAsync(string dbPath, DemoDataOptions options)
    {
        if (options.Owners < 1 || options.Devices < 1 || options.Certificates < options.Devices)
            throw new ArgumentException("الأعداد غير صالحة: يجب أن تكون الشهادات ≥ الأجهزة ≥ 1 والجهات ≥ 1.");
        if (options.FailedCertificates < 0 || options.FailedCertificates > options.Certificates)
            throw new ArgumentException("عدد الشهادات المرفوضة يتجاوز إجماليّ الشهادات.");

        PrepareTarget(dbPath, options.OverwriteExisting);

        var factory = new Factory(dbPath);
        var rng = new Random(options.Seed);

        // ── 1. المخطّط والإعدادات والمستخدم ──
        using (var context = factory.CreateDbContext())
        {
            DatabaseMigrator.RunMigrations(context);
        }

        var recovery = new RecoveryAnswerService(factory);
        User admin;
        using (var context = factory.CreateDbContext())
        {
            Set(context, "DatabasePath", dbPath);
            Set(context, "SecurityQuestion", "سؤال الاسترداد التجريبيّ؟");
            Set(context, "SecurityAnswer", recovery.HashAnswer("demo"));
            Set(context, "FirstRunCompleted", "true");

            admin = new User
            {
                FullName = "مدير النظام التجريبي",
                Username = options.AdminUsername,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(options.AdminPassword),
                Role = UserRole.Admin,
                Permissions = SystemPermissions.Records | SystemPermissions.Verification | SystemPermissions.Owners |
                              SystemPermissions.DeviceTypes | SystemPermissions.Reports | SystemPermissions.Settings |
                              SystemPermissions.BackupRestore | SystemPermissions.UserManagement,
                IsEditor = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            context.Users.Add(admin);
            await context.SaveChangesAsync();
        }

        // ── 2. الخدمات الحقيقيّة ──
        var hmac = new HmacService(factory);
        hmac.Initialize();
        var signature = new CertificateSignatureService(hmac);
        var numbers = new CertificateNumberService(factory);
        var certificates = new CertificateRepository(factory, numbers, signature);
        var currentUser = new CurrentUserService();
        currentUser.SetCurrentUser(admin);
        var audit = new AuditLogRepository(factory, currentUser);
        var draftBuilder = new CertificateDraftBuilder();

        List<DeviceType> types;
        using (var context = factory.CreateDbContext())
        {
            types = await context.DeviceTypes
                .Include(t => t.FunctionalCheckTemplates)
                .Include(t => t.UncertaintyComponentTemplates)
                .Where(t => !t.IsDeleted)
                .OrderBy(t => t.Id)
                .ToListAsync();
        }
        if (types.Count == 0)
            throw new InvalidOperationException("لا أنواع أجهزة مبذورة في القاعدة.");

        // ── 3. الجهات والأجهزة ──
        var ownerIds = new List<int>();
        using (var context = factory.CreateDbContext())
        {
            for (int i = 0; i < options.Owners; i++)
            {
                var owner = new Owner
                {
                    Name = i < DemoData.OwnerNames.Length ? DemoData.OwnerNames[i] : $"جهة تجريبية {i + 1}",
                    Address = $"{DemoData.Cities[i % DemoData.Cities.Length]}، شارع {10 + i}",
                    ContactPhone = $"010{(10000000 + i * 137).ToString(CultureInfo.InvariantCulture)}",
                    ContactPerson = DemoData.Engineers[i % DemoData.Engineers.Length],
                    IsSeedTestData = true
                };
                context.Owners.Add(owner);
            }
            await context.SaveChangesAsync();
            ownerIds = await context.Owners.OrderBy(o => o.Id).Select(o => o.Id).ToListAsync();
        }

        var deviceIds = new List<int>();
        var deviceTypeOf = new List<DeviceType>();
        using (var context = factory.CreateDbContext())
        {
            for (int d = 0; d < options.Devices; d++)
            {
                var type = types[d % types.Count];
                var device = new Device
                {
                    Model = DemoData.ModelFor(type.Name, d),
                    SerialNumber = $"SN-{(d + 1).ToString("D4", CultureInfo.InvariantCulture)}",
                    OwnerId = ownerIds[d % ownerIds.Count],
                    DeviceTypeId = type.Id,
                    IsSeedTestData = true
                };
                context.Devices.Add(device);
                deviceTypeOf.Add(type);
            }
            await context.SaveChangesAsync();
            deviceIds = await context.Devices.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync();
        }

        // ── 4. مخطّط المعايرات: أعمار آخر معايرة موزّعة لتظهر حالات الصلاحيّة كلّها في اللوحة ──
        int expiredCount = (int)Math.Round(options.Devices * 0.12);
        int soonCount = (int)Math.Round(options.Devices * 0.14);
        var plans = new List<Plan>();
        int perDevice = options.Certificates / options.Devices;
        int remainder = options.Certificates % options.Devices;
        for (int d = 0; d < options.Devices; d++)
        {
            int lastAge = d < expiredCount ? 380 + rng.Next(0, 120)
                        : d < expiredCount + soonCount ? 340 + rng.Next(0, 20)
                        : 15 + rng.Next(0, 300);
            int count = perDevice + (d < remainder ? 1 : 0);
            for (int k = 0; k < count; k++)
            {
                int age = lastAge + 365 * k + (k == 0 ? 0 : rng.Next(-10, 11));
                plans.Add(new Plan(d, options.Today.Date.AddDays(-age), rng.Next(DemoData.Engineers.Length)));
            }
        }
        // ترتيب زمنيّ: الأرقام تُخصَّص وقت الإصدار، فتزداد مع التاريخ داخل كلّ سنة كما في التشغيل الحقيقيّ.
        plans = plans.OrderBy(p => p.CalibrationDate).ThenBy(p => p.DeviceIndex).ToList();

        var failedIndexes = new HashSet<int>();
        if (options.FailedCertificates > 0)
        {
            int step = Math.Max(1, plans.Count / options.FailedCertificates);
            for (int j = 0; j < options.FailedCertificates; j++)
                failedIndexes.Add(Math.Min(plans.Count - 1, j * step + step / 2));
            // ضمان العدد بدقّة لو تصادمت الفهارس
            for (int i = 0; failedIndexes.Count < options.FailedCertificates && i < plans.Count; i++)
                failedIndexes.Add(i);
        }

        // ── 5. السجلّ ثمّ الشهادة لكلّ معايرة ──
        string firstNumber = string.Empty, lastNumber = string.Empty;
        for (int i = 0; i < plans.Count; i++)
        {
            var plan = plans[i];
            bool failed = failedIndexes.Contains(i);
            var type = deviceTypeOf[plan.DeviceIndex];
            string engineer = DemoData.Engineers[plan.EngineerIndex];

            CalibrationRecord record;
            Device device;
            Owner owner;
            using (var context = factory.CreateDbContext())
            {
                record = new CalibrationRecord
                {
                    DeviceId = deviceIds[plan.DeviceIndex],
                    CalibrationDate = plan.CalibrationDate,
                    ExpiryDate = plan.CalibrationDate.AddYears(1),
                    EngineerName = engineer,
                    CalibrationDescription = failed
                        ? "فشل الجهاز في معايرة الاستجابة: تجاوز الخطأ النسبي حدّ القبول."
                        : "معايرة دورية باستخدام مصادر إشعاعية مرجعية.",
                    Result = failed ? "Failed" : "Passed",
                    HmacSignature = string.Empty,
                    IsSeedTestData = true
                };
                context.CalibrationRecords.Add(record);
                await context.SaveChangesAsync();

                device = await context.Devices.AsNoTracking().FirstAsync(x => x.Id == record.DeviceId);
                owner = await context.Owners.AsNoTracking().FirstAsync(o => o.Id == device.OwnerId);
            }

            var draft = draftBuilder.Build(type, record, device, owner);
            var cert = draft.Certificate;
            Fill(cert, type, plan, engineer, failed, options.Today.Date, rng);

            string number = await certificates.AddAsync(cert);
            if (firstNumber.Length == 0) firstNumber = number;
            lastNumber = number;

            using (var context = factory.CreateDbContext())
            {
                var stored = await context.CalibrationRecords.FirstAsync(r => r.Id == record.Id);
                stored.CertificateNumber = number;
                await context.SaveChangesAsync();
            }

            await audit.LogAsync("إصدار شهادة", "Certificate", cert.Id.ToString(),
                $"إصدار الشهادة رقم {number} لسجل المعايرة {record.Id} (بيانات تجريبيّة)");

            // الشهادات الأقدم من 60 يوماً تُعدّ مطبوعة (تنويع حالة اللوحة).
            if ((options.Today.Date - cert.IssueDate.Date).TotalDays > 60)
                await certificates.MarkPrintedAsync(cert.Id);
        }

        return new DemoDataSummary(options.Owners, options.Devices, plans.Count, failedIndexes.Count,
            firstNumber, lastNumber, options.AdminUsername, options.AdminPassword);
    }

    // ───────────────────────────── ملء حقول الشهادة ─────────────────────────────

    private static void Fill(Certificate c, DeviceType type, Plan plan, string engineer, bool failed,
                             DateTime today, Random rng)
    {
        var inv = CultureInfo.InvariantCulture;

        DateTime issue = plan.CalibrationDate.AddDays(rng.Next(0, 3));
        if (issue > today) issue = today;
        c.IssueDate = issue;

        c.DeviceManufacturer = DemoData.Manufacturer;

        if (!string.IsNullOrWhiteSpace(DeviceTypeCatalog.Resolve(type.Name)?.ReadoutUnitLabel))
        {
            c.SurveyMeterModel = "RM-200";
            c.SurveyMeterSerialNumber = $"RM-{(plan.DeviceIndex + 1).ToString("D4", inv)}";
        }

        c.Temperature = $"{(20 + rng.NextDouble() * 4).ToString("F1", inv)} ± 0.5 °C";
        c.RelativeHumidity = $"{rng.Next(35, 56)} ± 5 % RH";
        c.AtmosphericPressure = $"{(100.6 + rng.NextDouble() * 1.4).ToString("F1", inv)} ± 0.5 kPa";

        c.ReferenceNo = $"REF-{issue.Year.ToString(inv)}-{rng.Next(1, 999).ToString("D3", inv)}";

        bool isProbe = type.Name is "Pancake Probe" or "Beta Scintillation Probe";
        string[] nuclides = type.Name switch
        {
            "Pancake Probe" => new[] { "Sr-90/Y-90", "Cs-137" },
            "Beta Scintillation Probe" => new[] { "Sr-90/Y-90" },
            "Personal Electronic Dosimeter (PED)" => new[] { "Cs-137" },
            _ => new[] { "Cs-137", "Co-60" }
        };
        string unit = isProbe ? "kCPM" : type.Name.StartsWith("Personal", StringComparison.Ordinal) ? "µSv" : "µSv/h";
        double[] levels = isProbe ? new[] { 5.0, 20.0, 80.0 } : new[] { 10.0, 100.0, 1000.0 };

        c.CalibrationResults.Clear();
        c.NuclideSummaries.Clear();
        int failRow = failed ? rng.Next(0, nuclides.Length * levels.Length) : -1;
        int rowIndex = 0;
        foreach (string nuclide in nuclides)
        {
            var cfs = new List<double>();
            foreach (double level in levels)
            {
                // نجاح: خطأ نسبيّ ضمن ±9%؛ رفض: صفّ واحد على الأقلّ يتجاوز ±25%
                double err = rowIndex == failRow
                    ? (rng.Next(0, 2) == 0 ? 1 : -1) * (26 + rng.NextDouble() * 14)
                    : (rng.NextDouble() * 2 - 1) * 9;
                double measured = level * (1 + err / 100.0);
                double cf = level / measured;
                cfs.Add(cf);

                c.CalibrationResults.Add(new CertificateCalibrationResult
                {
                    SourceId = $"SRC-{nuclide.Split('/')[0].Replace("-", string.Empty)}-01",
                    Radionuclide = nuclide,
                    Scale = type.Name == "Dose Rate Meter" ? "x1" : null,
                    ReferenceDoseLevel = isProbe ? null : level.ToString("F1", inv),
                    ReferenceValue = level.ToString("F2", inv),
                    MeasuredReading = measured.ToString("F2", inv),
                    CorrectionFactor = cf.ToString("F3", inv),
                    RelativeError = err.ToString("F1", inv),
                    Unit = unit
                });
                rowIndex++;
            }

            c.NuclideSummaries.Add(new CertificateNuclideSummary
            {
                Radionuclide = nuclide,
                AverageCorrectionFactor = cfs.Average().ToString("F3", inv)
            });
        }

        if (type.UncertaintyEnabled)
        {
            double[] standard = { 1.2, 0.5, 0.9, 0.7, 1.0 };
            double sumSquares = standard.Sum(u => u * u);
            double combined = Math.Sqrt(sumSquares);
            c.CombinedUncertainty = combined.ToString("F2", inv) + " %";
            c.ExpandedUncertainty = (2 * combined).ToString("F2", inv) + " %";
            c.CoverageFactor = "2";

            int n = 0;
            foreach (var component in c.UncertaintyComponents)
            {
                double u = standard[Math.Min(n, standard.Length - 1)];
                component.StandardUncertainty = u.ToString("F2", inv);
                component.ContributionPercent = (u * u / sumSquares * 100).ToString("F1", inv);
                n++;
            }
        }

        if (failed)
        {
            var check = c.FunctionalChecks.OrderBy(f => f.SortOrder).Skip(1).FirstOrDefault()
                        ?? c.FunctionalChecks.FirstOrDefault();
            if (check != null)
            {
                check.Result = "Failed";
                check.Remarks = "Out of specified range";
            }
            string verdict = c.ComplianceVerdict ?? string.Empty;
            c.ComplianceVerdict = verdict.Contains("APPROVED", StringComparison.Ordinal)
                ? verdict.Replace("APPROVED", "NOT APPROVED", StringComparison.Ordinal)
                : "NOT APPROVED FOR OPERATIONAL USE";
        }

        c.CalibratedByName = engineer;
        c.CalibratedByTitle = "Calibration Engineer";
        c.CalibratedByDate = issue;
        c.ReviewedByName = "د. عادل سعيد";
        c.ReviewedByTitle = "Quality Reviewer";
        c.ReviewedByDate = issue;
        c.ApprovedByName = "د. ليلى فاروق";
        c.ApprovedByTitle = "SSDL Head";
        c.ApprovedByDate = issue;
        c.AuthorizedByName = "م. ياسر عثمان";
        c.AuthorizedByTitle = "Radiation Protection Manager";
        c.AuthorizedByDate = issue;
    }

    // ───────────────────────────── مساعدات ─────────────────────────────

    private static void PrepareTarget(string dbPath, bool overwrite)
    {
        if (File.Exists(dbPath))
        {
            if (!overwrite)
                throw new InvalidOperationException(
                    $"الملفّ موجود ولن يُكتب فوقه: {dbPath}\nاستعمل --force لاستبداله (ملفّ تجريبيّ فقط).");

            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(dbPath);
            foreach (var suffix in new[] { "-wal", "-shm" })
            {
                if (File.Exists(dbPath + suffix)) File.Delete(dbPath + suffix);
            }
        }

        string? folder = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
    }

    private static void Set(CalQrDbContext context, string key, string value)
    {
        var existing = context.AppSettings.FirstOrDefault(s => s.Key == key);
        if (existing == null)
            context.AppSettings.Add(new AppSetting { Key = key, Value = value, UpdatedAt = DateTime.UtcNow });
        else
        {
            existing.Value = value;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        context.SaveChanges();
    }
}
