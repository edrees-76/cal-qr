using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Repositories;
using CAL_QR.Services;

namespace CAL_QR.Tests
{
    /// <summary>
    /// مصفاة محارف الاتّجاه الخفيّة (المرحلة أ)، عبر الواجهة العامّة للمستودع
    /// فقط — لا انعكاس على SanitizeUserText نفسها.
    /// </summary>
    public class CertificateTextSanitizationTests
    {
        private class TestDbContextFactory : IDbContextFactory<CalQrDbContext>
        {
            private readonly DbContextOptions<CalQrDbContext> _options;
            public TestDbContextFactory(DbContextOptions<CalQrDbContext> options) => _options = options;
            public CalQrDbContext CreateDbContext() => new CalQrDbContext(_options);
        }

        private static string NewDbPath(string tag) =>
            Path.Combine(Path.GetTempPath(), $"cal_qr_text_sanitize_{tag}_{Guid.NewGuid():N}.db");

        private static DbContextOptions<CalQrDbContext> OptionsFor(string dbPath) =>
            new DbContextOptionsBuilder<CalQrDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

        private static void CleanUp(string dbPath)
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                try { File.Delete(dbPath); } catch { }
            }
        }

        private sealed class Harness : IDisposable
        {
            public string DbPath { get; }
            public DbContextOptions<CalQrDbContext> Options { get; }
            public TestDbContextFactory Factory { get; }
            public CertificateRepository Repository { get; }
            public int CalibrationRecordId { get; }

            public Harness(string tag)
            {
                DbPath = NewDbPath(tag);
                Options = OptionsFor(DbPath);
                Factory = new TestDbContextFactory(Options);

                using (var context = new CalQrDbContext(Options))
                {
                    DatabaseMigrator.RunMigrations(context);
                    CalibrationRecordId = SeedCalibrationRecord(context, "REC-" + tag);
                }

                var hmac = new HmacService(Factory);
                hmac.Initialize();

                Repository = new CertificateRepository(
                    Factory,
                    new CertificateNumberService(Factory),
                    new CertificateSignatureService(hmac));
            }

            public void Dispose() => CleanUp(DbPath);
        }

        private static int SeedCalibrationRecord(CalQrDbContext context, string tag)
        {
            var owner = new Owner { Name = "مستشفى بنغازي الطبي" };
            var deviceType = new DeviceType { Name = "Pancake Probe" };
            context.Owners.Add(owner);
            context.DeviceTypes.Add(deviceType);
            context.SaveChanges();

            var device = new Device
            {
                Model = "Ludlum 44-9",
                SerialNumber = "SN-" + tag,
                OwnerId = owner.Id,
                DeviceTypeId = deviceType.Id
            };
            context.Devices.Add(device);
            context.SaveChanges();

            var record = new CalibrationRecord
            {
                DeviceId = device.Id,
                CertificateNumber = tag,
                CalibrationDate = new DateTime(2026, 1, 15),
                ExpiryDate = new DateTime(2027, 1, 15),
                EngineerName = "م. أحمد الشريف",
                Result = "Passed",
                HmacSignature = "SIG-" + tag
            };
            context.CalibrationRecords.Add(record);
            context.SaveChanges();

            return record.Id;
        }

        private static Certificate NewCertificate(int calibrationRecordId) => new Certificate
        {
            CalibrationRecordId = calibrationRecordId,
            CertificateTemplateType = "Pancake Probe",
            ClientName = "مستشفى بنغازي الطبي",
            DeviceModel = "Ludlum 44-9",
            DeviceSerialNumber = "PR-105",
            CalibrationDate = new DateTime(2026, 1, 15),
            IssueDate = new DateTime(2026, 1, 20),
            CalibrationResults =
            {
                new CertificateCalibrationResult
                {
                    SortOrder = 1,
                    Radionuclide = "Cs-137",
                    ReferenceValue = "5.40",
                    MeasuredReading = "5.20",
                    CorrectionFactor = "1.038",
                    RelativeError = "-3.70",
                    Unit = "kCPM"
                }
            }
        };

        // نفس المحارف الأربعة عشر من TextInputRulesTests، بقائمة مستقلّة عمداً
        // هنا أيضاً: هذا الاختبار يحرس النموذج (بالانعكاس)، لا قائمة الإنتاج
        // اليدويّة في SanitizeUserText — فمصدر واحد للقائمتين كان سيُخفي عمود
        // نُسي في الإنتاج بنفس النسيان في التحقّق.
        private static readonly char[] HiddenMarks =
        {
            (char)0x200E, (char)0x200F, (char)0x061C,
            (char)0x202A, (char)0x202B, (char)0x202C, (char)0x202D, (char)0x202E,
            (char)0x2066, (char)0x2067, (char)0x2068, (char)0x2069,
            (char)0x200B, (char)0xFEFF
        };

        // يولّده النظام لا المستخدم — مستثنى من الحقن ومن التحقّق كليهما.
        private static readonly string[] SystemGeneratedProperties =
        {
            nameof(Certificate.CertificateNumber),
            nameof(Certificate.QrPayload),
            nameof(Certificate.QrPayloadVersion),
            nameof(Certificate.VerifyCode),
            nameof(Certificate.SignaturePayloadVersion)
        };

        private static void InjectMarkIntoEveryStringProperty(object target, string[] excludedNames, char mark)
        {
            foreach (var prop in target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.PropertyType != typeof(string) || !prop.CanRead || !prop.CanWrite) continue;
                if (excludedNames.Contains(prop.Name)) continue;

                string? current = (string?)prop.GetValue(target);
                string baseValue = string.IsNullOrEmpty(current) ? "قيمة" : current;
                prop.SetValue(target, baseValue + mark);
            }
        }

        private static void AssertNoStringPropertyContainsAHiddenMark(object target, string[] excludedNames)
        {
            foreach (var prop in target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.PropertyType != typeof(string)) continue;
                if (excludedNames.Contains(prop.Name)) continue;

                string? value = (string?)prop.GetValue(target);
                if (value == null) continue;

                foreach (char mark in HiddenMarks)
                {
                    Assert.DoesNotContain(mark, value);
                }
            }
        }

        /// <summary>
        /// الكنس الانعكاسيّ: أهمّ اختبار في هذا الملفّ.
        ///
        /// الانعكاس يقع على النموذج (Certificate وصفوفه الأربعة)، لا على قائمة
        /// SanitizeUserText اليدويّة. الغرض: عمود نصّيّ جديد يُضاف بعد سنة ولا
        /// يُنظَّف ⇒ هذا الاختبار أحمر تلقائياً بلا حاجة لتذكّر تحديثه — لأنّ
        /// القائمة اليدويّة في الإنتاج تُكتب مرّة، والنموذج ينمو، فالحارس يجب أن
        /// يكون على ما ينمو لا على ما كُتب مرّة.
        ///
        /// الفحص بعد AddAsync يقع على قراءة جديدة من القاعدة عبر GetByIdAsync
        /// لا على الكائن الذي في اليد: الغرض إثبات أنّ النظيف هو ما حُفظ
        /// فعلياً، لا أنّ الكائن في الذاكرة نُظِّف قبل أن يُنسى.
        /// </summary>
        [Fact]
        public async Task AddAsync_SanitizesEveryUserTextProperty_ReflectivelyAcrossTheWholeModel()
        {
            using var harness = new Harness("sweep");
            char rlm = (char)0x200F;

            var certificate = NewCertificate(harness.CalibrationRecordId);
            certificate.NuclideSummaries.Add(new CertificateNuclideSummary
            {
                SortOrder = 1,
                Radionuclide = "Cs-137"
            });
            certificate.UncertaintyComponents.Add(new CertificateUncertaintyComponent
            {
                SortOrder = 1,
                ComponentName = "Type A"
            });
            certificate.FunctionalChecks.Add(new CertificateFunctionalCheck
            {
                SortOrder = 1,
                CheckName = "Battery Check"
            });

            InjectMarkIntoEveryStringProperty(certificate, SystemGeneratedProperties, rlm);
            foreach (var n in certificate.NuclideSummaries)
                InjectMarkIntoEveryStringProperty(n, Array.Empty<string>(), rlm);
            foreach (var r in certificate.CalibrationResults)
                InjectMarkIntoEveryStringProperty(r, Array.Empty<string>(), rlm);
            foreach (var u in certificate.UncertaintyComponents)
                InjectMarkIntoEveryStringProperty(u, Array.Empty<string>(), rlm);
            foreach (var f in certificate.FunctionalChecks)
                InjectMarkIntoEveryStringProperty(f, Array.Empty<string>(), rlm);

            await harness.Repository.AddAsync(certificate);

            var stored = await harness.Repository.GetByIdAsync(certificate.Id);
            Assert.NotNull(stored);

            AssertNoStringPropertyContainsAHiddenMark(stored!, SystemGeneratedProperties);
            foreach (var n in stored!.NuclideSummaries)
                AssertNoStringPropertyContainsAHiddenMark(n, Array.Empty<string>());
            foreach (var r in stored.CalibrationResults)
                AssertNoStringPropertyContainsAHiddenMark(r, Array.Empty<string>());
            foreach (var u in stored.UncertaintyComponents)
                AssertNoStringPropertyContainsAHiddenMark(u, Array.Empty<string>());
            foreach (var f in stored.FunctionalChecks)
                AssertNoStringPropertyContainsAHiddenMark(f, Array.Empty<string>());
        }

        /// <summary>
        /// خانة لا تحوي إلّا RLM كانت تُحسب مملوءة (IsNullOrWhiteSpace = false)
        /// وتُطبع بياضاً، فيغطّي التوقيع محرفاً لم يُطبع قطّ. بعد المرحلة (أ)
        /// Clean تعيد null فتعود الخانة إلى رمز الخلوّ ~ في نصّ التوقيع.
        /// </summary>
        [Fact]
        public async Task ClientAddress_ContainingOnlyRlm_SignsAsTheEmptyMarker()
        {
            using var harness = new Harness("emptymarker");

            var certificate = NewCertificate(harness.CalibrationRecordId);
            certificate.ClientAddress = ((char)0x200F).ToString();

            await harness.Repository.AddAsync(certificate);
            var stored = await harness.Repository.GetByIdAsync(certificate.Id);
            Assert.NotNull(stored);

            var hmac = new HmacService(harness.Factory);
            hmac.Initialize();
            var signatureService = new CertificateSignatureService(hmac);

            string payload = signatureService.BuildSignaturePayload(stored!, stored!.SignaturePayloadVersion!);
            string? caLine = payload.Split('\n').SingleOrDefault(l => l.StartsWith("CA:", StringComparison.Ordinal));

            Assert.Equal("CA:~", caLine);
        }

        /// <summary>
        /// هذا الاختبار كان سيفشل قبل المرحلة (أ) — وهو التعبير المباشر عن
        /// الخاصّيّة المحميّة: وثيقتان متطابقتان على الورق (محرف اتّجاه خفيّ
        /// لصيق من Word لا يظهر في الطباعة ولا يغيّر معنى الحقل) يجب أن يحملا
        /// رمز تحقّق واحداً، لا رمزين.
        ///
        /// القراءة قبل الحقن عبر GetByCertificateNumberAsync عمداً: صفوف
        /// النتائج المقروءة تحمل Ids حقيقيّة، وهو ما تحتاجه SyncChildren
        /// للمطابقة. كائن مُعاد بناؤه من الصفر ناقص حقل واحد كان سيُنتج تدويراً
        /// لسبب لا علاقة له بالمحارف الخفيّة.
        ///
        /// الحقول المحقونة (ClientName · DeviceSerialNumber · Unit) لها قيم في
        /// NewCertificate قبل التعديل عمداً، فيكون الفرق الوحيد بين نصّي
        /// التوقيع القديم والجديد محرفاً خفيّاً لا محتوى. الحقن في حقل فارغ
        /// (كـClientAddress أو Notes) يضيف محتوى جديداً لا محرفاً على قيمة
        /// قائمة، فيدوّر الرمز بحقّ ويُسقط البرهان.
        /// </summary>
        [Fact]
        public async Task EditingOnlyWithHiddenMarksInjected_DoesNotRotateTheVerifyCode()
        {
            using var harness = new Harness("norotate");

            string number = await harness.Repository.AddAsync(NewCertificate(harness.CalibrationRecordId));
            var issued = await harness.Repository.GetByCertificateNumberAsync(number);
            Assert.NotNull(issued);
            string originalCode = issued!.VerifyCode!;

            char rlm = (char)0x200F;
            issued.ClientName += rlm;
            issued.DeviceSerialNumber += rlm;
            issued.CalibrationResults.Single().Unit += rlm;

            bool rotated = await harness.Repository.UpdateAsync(issued);

            Assert.False(rotated);

            var after = await harness.Repository.GetByCertificateNumberAsync(number);
            Assert.Equal(originalCode, after!.VerifyCode);

            using var context = new CalQrDbContext(harness.Options);
            Assert.Empty(context.CertificateVerifyCodeHistory);
        }
    }
}
