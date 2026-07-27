using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Services;

namespace CAL_QR.Tests
{
    /// <summary>
    /// اختبارات مخطط الشهادات.
    /// ملاحظة إلزامية: كل الاختبارات هنا تستخدم مزوّد SQLite الحقيقي لا InMemory،
    /// لأن مزوّد InMemory لا يطبّق الفهارس الفريدة ولا قيود المفاتيح الأجنبية ولا سلوك Cascade،
    /// فيُنتج نجاحاً كاذباً في كل اختبار يتعلق بقيود المخطط.
    /// </summary>
    public class CertificateSchemaTests
    {
        private class TestDbContextFactory : IDbContextFactory<CalQrDbContext>
        {
            private readonly DbContextOptions<CalQrDbContext> _options;
            public TestDbContextFactory(DbContextOptions<CalQrDbContext> options)
            {
                _options = options;
            }
            public CalQrDbContext CreateDbContext() => new CalQrDbContext(_options);
        }

        private static string NewDbPath(string tag) =>
            Path.Combine(Path.GetTempPath(), $"cal_qr_cert_schema_{tag}_{Guid.NewGuid():N}.db");

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

        /// <summary>يُنشئ جهة/نوع/جهاز/سجل معايرة ويعيد معرّف سجل المعايرة.</summary>
        private static int SeedCalibrationRecord(CalQrDbContext context, string certificateNumber)
        {
            var owner = new Owner { Name = "الجهة المالكة " + certificateNumber };
            var deviceType = new DeviceType { Name = "Pancake Probe " + certificateNumber };
            context.Owners.Add(owner);
            context.DeviceTypes.Add(deviceType);
            context.SaveChanges();

            var device = new Device
            {
                Model = "Ludlum 44-9",
                SerialNumber = "SN-" + certificateNumber,
                OwnerId = owner.Id,
                DeviceTypeId = deviceType.Id
            };
            context.Devices.Add(device);
            context.SaveChanges();

            var record = new CalibrationRecord
            {
                DeviceId = device.Id,
                CertificateNumber = certificateNumber,
                CalibrationDate = new DateTime(2026, 1, 15),
                ExpiryDate = new DateTime(2027, 1, 15),
                EngineerName = "م. أحمد الشريف",
                Result = "Passed",
                HmacSignature = "SIG-" + certificateNumber
            };
            context.CalibrationRecords.Add(record);
            context.SaveChanges();

            return record.Id;
        }

        private static Certificate NewCertificate(int calibrationRecordId, string certificateNumber) => new Certificate
        {
            CalibrationRecordId = calibrationRecordId,
            CertificateNumber = certificateNumber,
            ClientName = "مستشفى بنغازي الطبي",
            DeviceModel = "Ludlum 44-9",
            DeviceSerialNumber = "SN-" + certificateNumber
        };

        [Fact]
        public void Certificate_CanBeCreatedAndRetrieved_WithChildRows()
        {
            string dbPath = NewDbPath("create");
            var options = OptionsFor(dbPath);

            try
            {
                int recordId;
                using (var context = new CalQrDbContext(options))
                {
                    DatabaseMigrator.RunMigrations(context);
                    recordId = SeedCalibrationRecord(context, "CERT-CREATE-001");

                    var certificate = NewCertificate(recordId, "CERT-CREATE-001");
                    certificate.CalibrationResults.Add(new CertificateCalibrationResult
                    {
                        SortOrder = 1,
                        Radionuclide = "Cs-137",
                        ReferenceValue = "5.40",
                        MeasuredReading = "5.20",
                        CorrectionFactor = "1.038",
                        Unit = "µSv/h"
                    });
                    certificate.UncertaintyComponents.Add(new CertificateUncertaintyComponent
                    {
                        SortOrder = 1,
                        ComponentName = "Reference source activity",
                        EvaluationType = "B",
                        StandardUncertainty = "1.2"
                    });
                    certificate.FunctionalChecks.Add(new CertificateFunctionalCheck
                    {
                        SortOrder = 1,
                        CheckName = "Battery check",
                        Requirement = "> 90%",
                        Result = "Pass"
                    });

                    context.Certificates.Add(certificate);
                    context.SaveChanges();
                }

                using (var context = new CalQrDbContext(options))
                {
                    var stored = context.Certificates
                        .Include(c => c.CalibrationResults)
                        .Include(c => c.UncertaintyComponents)
                        .Include(c => c.FunctionalChecks)
                        .Single();

                    Assert.Equal("CERT-CREATE-001", stored.CertificateNumber);
                    Assert.Equal(recordId, stored.CalibrationRecordId);
                    Assert.False(stored.IsDeleted);
                    Assert.Single(stored.CalibrationResults);
                    Assert.Single(stored.UncertaintyComponents);
                    Assert.Single(stored.FunctionalChecks);
                    // القيم الرقمية محفوظة نصياً بأرقامها المعنوية كما أُدخلت
                    Assert.Equal("5.40", stored.CalibrationResults.First().ReferenceValue);
                }
            }
            finally
            {
                CleanUp(dbPath);
            }
        }

        [Fact]
        public void Certificate_DuplicateCalibrationRecordId_Throws()
        {
            string dbPath = NewDbPath("dup");
            var options = OptionsFor(dbPath);

            try
            {
                int recordId;
                using (var context = new CalQrDbContext(options))
                {
                    DatabaseMigrator.RunMigrations(context);
                    recordId = SeedCalibrationRecord(context, "CERT-DUP-001");

                    context.Certificates.Add(NewCertificate(recordId, "CERT-DUP-001"));
                    context.SaveChanges();
                }

                // شهادة ثانية لنفس سجل المعايرة، وكلتاهما غير محذوفة.
                // تُضاف من سياق جديد لا يتتبّع الأولى، فيقع الرفض على قيد قاعدة البيانات
                // لا على مُصحّح العلاقات في EF.
                using (var context = new CalQrDbContext(options))
                {
                    context.Certificates.Add(NewCertificate(recordId, "CERT-DUP-002"));

                    Assert.Throws<DbUpdateException>(() => context.SaveChanges());
                }

                using (var context = new CalQrDbContext(options))
                {
                    Assert.Single(context.Certificates);
                }
            }
            finally
            {
                CleanUp(dbPath);
            }
        }

        [Fact]
        public void Certificate_SoftDeleted_AllowsNewCertificateForSameRecord()
        {
            string dbPath = NewDbPath("softdelete");
            var options = OptionsFor(dbPath);

            try
            {
                int recordId;
                using (var context = new CalQrDbContext(options))
                {
                    DatabaseMigrator.RunMigrations(context);
                    recordId = SeedCalibrationRecord(context, "CERT-SOFT-001");

                    var first = NewCertificate(recordId, "CERT-SOFT-001");
                    context.Certificates.Add(first);
                    context.SaveChanges();

                    // حذف ناعم للشهادة الأولى: الصف يبقى في الجدول
                    first.IsDeleted = true;
                    context.SaveChanges();
                }

                // الفهرس المشروط يسمح بإصدار شهادة بديلة لنفس سجل المعايرة
                using (var context = new CalQrDbContext(options))
                {
                    context.Certificates.Add(NewCertificate(recordId, "CERT-SOFT-002"));
                    context.SaveChanges();
                }

                using (var context = new CalQrDbContext(options))
                {
                    Assert.Equal(2, context.Certificates.Count());
                    Assert.Single(context.Certificates.Where(c => !c.IsDeleted));
                    Assert.Equal("CERT-SOFT-002", context.Certificates.Single(c => !c.IsDeleted).CertificateNumber);
                }
            }
            finally
            {
                CleanUp(dbPath);
            }
        }

        [Fact]
        public void Certificate_Delete_CascadesToAllChildTables()
        {
            string dbPath = NewDbPath("cascade");
            var options = OptionsFor(dbPath);

            try
            {
                int certificateId;
                using (var context = new CalQrDbContext(options))
                {
                    DatabaseMigrator.RunMigrations(context);
                    int recordId = SeedCalibrationRecord(context, "CERT-CASC-001");

                    var certificate = NewCertificate(recordId, "CERT-CASC-001");
                    certificate.CalibrationResults.Add(new CertificateCalibrationResult { SortOrder = 1, Radionuclide = "Cs-137" });
                    certificate.CalibrationResults.Add(new CertificateCalibrationResult { SortOrder = 2, Radionuclide = "Co-60" });
                    certificate.UncertaintyComponents.Add(new CertificateUncertaintyComponent { SortOrder = 1, ComponentName = "Repeatability" });
                    certificate.FunctionalChecks.Add(new CertificateFunctionalCheck { SortOrder = 1, CheckName = "Audio alarm" });

                    context.Certificates.Add(certificate);
                    context.SaveChanges();
                    certificateId = certificate.Id;
                }

                // سياق جديد لا يتتبّع الصفوف الأبناء، فيقع الحذف المتسلسل على قاعدة البيانات نفسها
                using (var context = new CalQrDbContext(options))
                {
                    var certificate = context.Certificates.Single(c => c.Id == certificateId);
                    context.Certificates.Remove(certificate);
                    context.SaveChanges();
                }

                using (var context = new CalQrDbContext(options))
                {
                    Assert.Empty(context.Certificates);
                    Assert.Empty(context.CertificateCalibrationResults);
                    Assert.Empty(context.CertificateUncertaintyComponents);
                    Assert.Empty(context.CertificateFunctionalChecks);
                }
            }
            finally
            {
                CleanUp(dbPath);
            }
        }

        [Fact]
        public void CalibrationRecord_WithCertificate_CannotBeHardDeleted()
        {
            string dbPath = NewDbPath("restrict");
            var options = OptionsFor(dbPath);

            try
            {
                int recordId;
                using (var context = new CalQrDbContext(options))
                {
                    DatabaseMigrator.RunMigrations(context);
                    recordId = SeedCalibrationRecord(context, "CERT-RESTRICT-001");
                    context.Certificates.Add(NewCertificate(recordId, "CERT-RESTRICT-001"));
                    context.SaveChanges();
                }

                // سياق جديد لا يتتبّع الشهادة، فيصطدم الحذف بقيد المفتاح الأجنبي Restrict
                using (var context = new CalQrDbContext(options))
                {
                    var record = context.CalibrationRecords.Single(r => r.Id == recordId);
                    context.CalibrationRecords.Remove(record);

                    Assert.Throws<DbUpdateException>(() => context.SaveChanges());
                }

                using (var context = new CalQrDbContext(options))
                {
                    Assert.Single(context.CalibrationRecords);
                    Assert.Single(context.Certificates);
                }
            }
            finally
            {
                CleanUp(dbPath);
            }
        }

        [Fact]
        public void Certificate_ChildRows_PreserveSortOrder()
        {
            string dbPath = NewDbPath("sortorder");
            var options = OptionsFor(dbPath);

            try
            {
                using (var context = new CalQrDbContext(options))
                {
                    DatabaseMigrator.RunMigrations(context);
                    int recordId = SeedCalibrationRecord(context, "CERT-SORT-001");

                    var certificate = NewCertificate(recordId, "CERT-SORT-001");
                    // تُضاف بترتيب مبعثر عمداً للتأكد من أن الترتيب المحفوظ هو SortOrder لا ترتيب الإدراج
                    certificate.CalibrationResults.Add(new CertificateCalibrationResult { SortOrder = 3, Radionuclide = "Am-241" });
                    certificate.CalibrationResults.Add(new CertificateCalibrationResult { SortOrder = 1, Radionuclide = "Cs-137" });
                    certificate.CalibrationResults.Add(new CertificateCalibrationResult { SortOrder = 2, Radionuclide = "Co-60" });

                    certificate.UncertaintyComponents.Add(new CertificateUncertaintyComponent { SortOrder = 2, ComponentName = "Positioning" });
                    certificate.UncertaintyComponents.Add(new CertificateUncertaintyComponent { SortOrder = 1, ComponentName = "Repeatability" });

                    certificate.FunctionalChecks.Add(new CertificateFunctionalCheck { SortOrder = 2, CheckName = "Audio alarm" });
                    certificate.FunctionalChecks.Add(new CertificateFunctionalCheck { SortOrder = 1, CheckName = "Battery check" });

                    context.Certificates.Add(certificate);
                    context.SaveChanges();
                }

                using (var context = new CalQrDbContext(options))
                {
                    var results = context.CertificateCalibrationResults.OrderBy(r => r.SortOrder).ToList();
                    Assert.Equal(new[] { 1, 2, 3 }, results.Select(r => r.SortOrder).ToArray());
                    Assert.Equal(new[] { "Cs-137", "Co-60", "Am-241" }, results.Select(r => r.Radionuclide).ToArray());

                    var components = context.CertificateUncertaintyComponents.OrderBy(c => c.SortOrder).ToList();
                    Assert.Equal(new[] { 1, 2 }, components.Select(c => c.SortOrder).ToArray());
                    Assert.Equal(new[] { "Repeatability", "Positioning" }, components.Select(c => c.ComponentName).ToArray());

                    var checks = context.CertificateFunctionalChecks.OrderBy(c => c.SortOrder).ToList();
                    Assert.Equal(new[] { 1, 2 }, checks.Select(c => c.SortOrder).ToArray());
                    Assert.Equal(new[] { "Battery check", "Audio alarm" }, checks.Select(c => c.CheckName).ToArray());
                }
            }
            finally
            {
                CleanUp(dbPath);
            }
        }

        [Fact]
        public void Migrator_CreatesCertificateTables_OnExistingDatabase()
        {
            string dbPath = NewDbPath("migrator");
            var options = OptionsFor(dbPath);
            var certificateTables = new[]
            {
                "Certificates",
                "CertificateCalibrationResults",
                "CertificateUncertaintyComponents",
                "CertificateFunctionalChecks"
            };

            try
            {
                // 1. محاكاة قاعدة بيانات قائمة قديمة: مخطط كامل ثم إسقاط الجداول الأربعة.
                // إسقاط الجدول في SQLite يُسقط فهارسه معه.
                using (var context = new CalQrDbContext(options))
                {
                    DatabaseMigrator.RunMigrations(context);
                    foreach (var table in certificateTables.Reverse())
                    {
                        DropTable(context, table);
                    }
                }

                using (var context = new CalQrDbContext(options))
                {
                    foreach (var table in certificateTables)
                    {
                        Assert.False(TableExists(context, table), $"من المفترض أن الجدول {table} غير موجود قبل الهجرة.");
                    }
                }

                // 2. تشغيل منطق الهجرة على قاعدة البيانات القائمة
                using (var context = new CalQrDbContext(options))
                {
                    DatabaseMigrator.RunMigrations(context);
                }

                // 3. إثبات وجود الجداول الأربعة عبر sqlite_master
                using (var context = new CalQrDbContext(options))
                {
                    foreach (var table in certificateTables)
                    {
                        Assert.True(TableExists(context, table), $"الجدول {table} لم يُنشأ بواسطة الهجرة.");
                    }

                    // ولإثبات أنها جداول عاملة لا هياكل فارغة: كتابة وقراءة فعلية
                    int recordId = SeedCalibrationRecord(context, "CERT-MIGRATED-001");
                    var certificate = NewCertificate(recordId, "CERT-MIGRATED-001");
                    certificate.CalibrationResults.Add(new CertificateCalibrationResult { SortOrder = 1, Radionuclide = "Cs-137" });
                    certificate.UncertaintyComponents.Add(new CertificateUncertaintyComponent { SortOrder = 1, ComponentName = "Repeatability" });
                    certificate.FunctionalChecks.Add(new CertificateFunctionalCheck { SortOrder = 1, CheckName = "Battery check" });
                    context.Certificates.Add(certificate);
                    context.SaveChanges();

                    Assert.Single(context.Certificates);
                    Assert.Single(context.CertificateCalibrationResults);
                    Assert.Single(context.CertificateUncertaintyComponents);
                    Assert.Single(context.CertificateFunctionalChecks);
                }
            }
            finally
            {
                CleanUp(dbPath);
            }
        }

        [Fact]
        public async Task FactoryReset_WithIssuedCertificate_DeletesCertificateBeforeRecord()
        {
            string dbPath = NewDbPath("cleanup");
            var options = OptionsFor(dbPath);
            var factory = new TestDbContextFactory(options);

            // عزل مسارات القرص عن المجلدات المشتركة تحت BaseDirectory، حتى لا يتداخل
            // تنظيف الملفات في هذا الاختبار مع اختبارات التصفير الأخرى المتوازية
            string qrFolder = Path.Combine(Path.GetTempPath(), $"cal_qr_cert_cleanup_qr_{Guid.NewGuid():N}");
            string attachmentsFolder = Path.Combine(Path.GetTempPath(), $"cal_qr_cert_cleanup_att_{Guid.NewGuid():N}");
            Directory.CreateDirectory(qrFolder);
            Directory.CreateDirectory(attachmentsFolder);

            try
            {
                using (var context = new CalQrDbContext(options))
                {
                    DatabaseMigrator.RunMigrations(context);

                    context.AppSettings.Single(s => s.Key == "QrOutputPath").Value = qrFolder;
                    context.AppSettings.Single(s => s.Key == "AttachmentsPath").Value = attachmentsFolder;
                    context.SaveChanges();

                    int recordId = SeedCalibrationRecord(context, "CERT-CLEANUP-001");

                    var certificate = NewCertificate(recordId, "CERT-CLEANUP-001");
                    certificate.CalibrationResults.Add(new CertificateCalibrationResult { SortOrder = 1, Radionuclide = "Cs-137" });
                    certificate.UncertaintyComponents.Add(new CertificateUncertaintyComponent { SortOrder = 1, ComponentName = "Repeatability" });
                    certificate.FunctionalChecks.Add(new CertificateFunctionalCheck { SortOrder = 1, CheckName = "Battery check" });
                    context.Certificates.Add(certificate);
                    context.SaveChanges();
                }

                // لولا حذف الشهادة قبل سجل المعايرة لفشلت العملية بقيد Restrict
                var result = await SystemResetService.FactoryResetAsync(factory, SystemResetService.RequiredConfirmationPhrase);

                Assert.True(result.Success, result.Message);

                using (var context = new CalQrDbContext(options))
                {
                    Assert.Empty(context.CalibrationRecords);
                    Assert.Empty(context.Certificates);
                    // الجداول الأبناء تتبع الشهادة بسلوك Cascade
                    Assert.Empty(context.CertificateCalibrationResults);
                    Assert.Empty(context.CertificateUncertaintyComponents);
                    Assert.Empty(context.CertificateFunctionalChecks);
                }
            }
            finally
            {
                CleanUp(dbPath);
                try { Directory.Delete(qrFolder, recursive: true); } catch { }
                try { Directory.Delete(attachmentsFolder, recursive: true); } catch { }
            }
        }

        // مخطط الجدولين كما كان قبل إضافة الأعمدة الستة المكتشفة من نماذج
        // Beta Scintillation Probe و PED و Dose Rate Meter. نصوص ثابتة لا مستوفاة،
        // تفادياً لتحذير EF1002.
        private const string OldSchemaCertificatesSql = @"
            CREATE TABLE ""Certificates"" (
                ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_Certificates"" PRIMARY KEY AUTOINCREMENT,
                ""CalibrationRecordId"" INTEGER NOT NULL,
                ""CertificateNumber"" TEXT NOT NULL,
                ""ReferenceNo"" TEXT NULL,
                ""ClientName"" TEXT NOT NULL,
                ""ClientAddress"" TEXT NULL,
                ""DeviceModel"" TEXT NOT NULL,
                ""DeviceSerialNumber"" TEXT NOT NULL,
                ""DeviceManufacturer"" TEXT NULL,
                ""SurveyMeterModel"" TEXT NULL,
                ""SurveyMeterSerialNumber"" TEXT NULL,
                ""Temperature"" TEXT NULL,
                ""RelativeHumidity"" TEXT NULL,
                ""AtmosphericPressure"" TEXT NULL,
                ""AverageCorrectionFactor"" TEXT NULL,
                ""CorrectedReadingFormula"" TEXT NULL,
                ""ComplianceVerdict"" TEXT NULL,
                ""MethodologyEnabled"" INTEGER NOT NULL,
                ""RadiationSource"" TEXT NULL,
                ""ReferenceGeometry"" TEXT NULL,
                ""MethodologyText"" TEXT NULL,
                ""TraceabilityReference"" TEXT NULL,
                ""UncertaintyEnabled"" INTEGER NOT NULL,
                ""CombinedUncertainty"" TEXT NULL,
                ""ExpandedUncertainty"" TEXT NULL,
                ""CoverageFactor"" TEXT NULL,
                ""AdditionalInformation"" TEXT NULL,
                ""Notes"" TEXT NULL,
                ""QrPayload"" TEXT NULL,
                ""QrPayloadVersion"" TEXT NULL,
                ""CalibratedByName"" TEXT NULL,
                ""CalibratedByTitle"" TEXT NULL,
                ""CalibratedByDate"" TEXT NULL,
                ""ReviewedByName"" TEXT NULL,
                ""ReviewedByTitle"" TEXT NULL,
                ""ReviewedByDate"" TEXT NULL,
                ""ApprovedByName"" TEXT NULL,
                ""ApprovedByTitle"" TEXT NULL,
                ""ApprovedByDate"" TEXT NULL,
                ""AuthorizedByName"" TEXT NULL,
                ""AuthorizedByTitle"" TEXT NULL,
                ""AuthorizedByDate"" TEXT NULL,
                ""IssuedAt"" TEXT NOT NULL,
                ""IsDeleted"" INTEGER NOT NULL,
                ""CreatedAt"" TEXT NOT NULL,
                ""UpdatedAt"" TEXT NOT NULL,
                CONSTRAINT ""FK_Certificates_CalibrationRecords_CalibrationRecordId"" FOREIGN KEY (""CalibrationRecordId"") REFERENCES ""CalibrationRecords"" (""Id"") ON DELETE RESTRICT
            );";

        private const string OldSchemaCalibrationResultsSql = @"
            CREATE TABLE ""CertificateCalibrationResults"" (
                ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_CertificateCalibrationResults"" PRIMARY KEY AUTOINCREMENT,
                ""CertificateId"" INTEGER NOT NULL,
                ""SortOrder"" INTEGER NOT NULL,
                ""SourceId"" TEXT NULL,
                ""Radionuclide"" TEXT NULL,
                ""Scale"" TEXT NULL,
                ""ReferenceValue"" TEXT NULL,
                ""MeasuredReading"" TEXT NULL,
                ""CorrectionFactor"" TEXT NULL,
                ""AbsoluteRelativeError"" TEXT NULL,
                ""Unit"" TEXT NULL,
                ""Remarks"" TEXT NULL,
                CONSTRAINT ""FK_CertificateCalibrationResults_Certificates_CertificateId"" FOREIGN KEY (""CertificateId"") REFERENCES ""Certificates"" (""Id"") ON DELETE CASCADE
            );";

        [Fact]
        public void Migrator_AddsNewCertificateColumns_OnDatabaseWithOldSchema()
        {
            string dbPath = NewDbPath("oldschema");
            var options = OptionsFor(dbPath);

            try
            {
                // 1. قاعدة بيانات حقيقية بالمخطط الكامل الحالي
                using (var context = new CalQrDbContext(options))
                {
                    DatabaseMigrator.RunMigrations(context);
                }

                // 2. محاكاة نسخة مثبَّتة قديمة: إسقاط الجدولين وإعادة إنشائهما بلا الأعمدة الستة
                using (var context = new CalQrDbContext(options))
                {
                    DropTable(context, "CertificateCalibrationResults");
                    DropTable(context, "Certificates");
                    context.Database.ExecuteSqlRaw(OldSchemaCertificatesSql);
                    context.Database.ExecuteSqlRaw(OldSchemaCalibrationResultsSql);
                }

                using (var context = new CalQrDbContext(options))
                {
                    Assert.False(ColumnExists(context, "Certificates", "MeasurementType"));
                    Assert.False(ColumnExists(context, "Certificates", "Distance"));
                    Assert.False(ColumnExists(context, "Certificates", "CountingTime"));
                    Assert.False(ColumnExists(context, "Certificates", "CountingUnit"));
                    Assert.False(ColumnExists(context, "Certificates", "CalibrationStandard"));
                    Assert.False(ColumnExists(context, "CertificateCalibrationResults", "ReferenceDoseLevel"));
                }

                // 3. تشغيل الهجرة على القاعدة ذات المخطط القديم
                using (var context = new CalQrDbContext(options))
                {
                    DatabaseMigrator.RunMigrations(context);
                }

                // 4. إثبات وجود الأعمدة الستة عبر PRAGMA table_info
                using (var context = new CalQrDbContext(options))
                {
                    Assert.True(ColumnExists(context, "Certificates", "MeasurementType"));
                    Assert.True(ColumnExists(context, "Certificates", "Distance"));
                    Assert.True(ColumnExists(context, "Certificates", "CountingTime"));
                    Assert.True(ColumnExists(context, "Certificates", "CountingUnit"));
                    Assert.True(ColumnExists(context, "Certificates", "CalibrationStandard"));
                    Assert.True(ColumnExists(context, "CertificateCalibrationResults", "ReferenceDoseLevel"));
                }

                // 5. كتابة وقراءة فعلية تملأ الحقول الستة لإثبات أنها عاملة
                using (var context = new CalQrDbContext(options))
                {
                    int recordId = SeedCalibrationRecord(context, "CERT-OLDSCHEMA-001");
                    var certificate = NewCertificate(recordId, "CERT-OLDSCHEMA-001");
                    certificate.MeasurementType = "Personal Dose Measurement (mSv)";
                    certificate.Distance = "1.0 meter";
                    certificate.CountingTime = "60 Sec";
                    certificate.CountingUnit = "kCPM";
                    certificate.CalibrationStandard = "SSDL-TNRC Internal Calibration Procedure Ref.: SSDL-CP-01 Implemented in accordance with ISO/IEC 17025:2017 requirements.";
                    certificate.CalibrationResults.Add(new CertificateCalibrationResult
                    {
                        SortOrder = 1,
                        Radionuclide = "Cs-137",
                        ReferenceDoseLevel = "1.0 mSv",
                        ReferenceValue = "1.00"
                    });
                    context.Certificates.Add(certificate);
                    context.SaveChanges();
                }

                using (var context = new CalQrDbContext(options))
                {
                    var stored = context.Certificates.Include(c => c.CalibrationResults).Single();
                    Assert.Equal("Personal Dose Measurement (mSv)", stored.MeasurementType);
                    Assert.Equal("1.0 meter", stored.Distance);
                    Assert.Equal("60 Sec", stored.CountingTime);
                    Assert.Equal("kCPM", stored.CountingUnit);
                    Assert.StartsWith("SSDL-TNRC Internal Calibration Procedure", stored.CalibrationStandard);
                    Assert.Equal("1.0 mSv", stored.CalibrationResults.Single().ReferenceDoseLevel);
                }
            }
            finally
            {
                CleanUp(dbPath);
            }
        }

        private static bool ColumnExists(CalQrDbContext context, string tableName, string columnName)
        {
            var connection = context.Database.GetDbConnection();
            bool alreadyOpen = connection.State == System.Data.ConnectionState.Open;
            try
            {
                if (!alreadyOpen) connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = $"PRAGMA table_info({tableName});";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    if (string.Equals(reader["name"]?.ToString(), columnName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                return false;
            }
            finally
            {
                if (!alreadyOpen) connection.Close();
            }
        }

        private static void DropTable(CalQrDbContext context, string tableName)
        {
            var connection = context.Database.GetDbConnection();
            bool alreadyOpen = connection.State == System.Data.ConnectionState.Open;
            try
            {
                if (!alreadyOpen) connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "DROP TABLE IF EXISTS \"" + tableName + "\";";
                command.ExecuteNonQuery();
            }
            finally
            {
                if (!alreadyOpen) connection.Close();
            }
        }

        private static bool TableExists(CalQrDbContext context, string tableName)
        {
            var connection = context.Database.GetDbConnection();
            bool alreadyOpen = connection.State == System.Data.ConnectionState.Open;
            try
            {
                if (!alreadyOpen) connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{tableName}';";
                return Convert.ToInt32(command.ExecuteScalar() ?? 0) > 0;
            }
            finally
            {
                if (!alreadyOpen) connection.Close();
            }
        }
    }
}
