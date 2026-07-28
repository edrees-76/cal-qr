using System;
using System.Collections.Generic;
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
    /// اختبارات عدّاد أرقام الشهادات. SQLite حقيقي لا InMemory: مزوّد InMemory
    /// لا يطبّق المفاتيح الأساسية بسلوك ON CONFLICT ولا يدعم RETURNING،
    /// فينتج نجاحاً كاذباً في كل اختبار ذرّية.
    /// </summary>
    public class CertificateNumberServiceTests
    {
        private class TestDbContextFactory : IDbContextFactory<CalQrDbContext>
        {
            private readonly DbContextOptions<CalQrDbContext> _options;
            public TestDbContextFactory(DbContextOptions<CalQrDbContext> options) => _options = options;
            public CalQrDbContext CreateDbContext() => new CalQrDbContext(_options);
        }

        private static string NewDbPath(string tag) =>
            Path.Combine(Path.GetTempPath(), $"cal_qr_cert_number_{tag}_{Guid.NewGuid():N}.db");

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

        /// <summary>تخصيص واحد داخل معاملة يملكها الاختبار، كما يفعل المستودع.</summary>
        private static async Task<string> AllocateOnceAsync(
            CertificateNumberService service, CalQrDbContext context, DateTime issueDate)
        {
            using var transaction = await context.Database.BeginTransactionAsync();
            string number = await service.AllocateAsync(context, issueDate);
            await transaction.CommitAsync();
            return number;
        }

        [Fact]
        public async Task Allocate_FirstNumberOfYear_IsZeroPaddedToFourDigits()
        {
            string dbPath = NewDbPath("first");
            var options = OptionsFor(dbPath);
            try
            {
                using var context = new CalQrDbContext(options);
                DatabaseMigrator.RunMigrations(context);
                var service = new CertificateNumberService(new TestDbContextFactory(options));

                string number = await AllocateOnceAsync(service, context, new DateTime(2026, 5, 4));

                Assert.Equal("TNRC-SSDL-2026-0001", number);
            }
            finally { CleanUp(dbPath); }
        }

        [Fact]
        public async Task Allocate_UsesIssueDateYear_NotCalibrationDateYear()
        {
            // معايرة في ٣٠ ديسمبر ٢٠٢٥، إصدار في ٢ يناير ٢٠٢٦.
            // الاختبار الحاسم للبند: السنة تُشتق من IssueDate.
            string dbPath = NewDbPath("issueyear");
            var options = OptionsFor(dbPath);
            try
            {
                using var context = new CalQrDbContext(options);
                DatabaseMigrator.RunMigrations(context);
                var service = new CertificateNumberService(new TestDbContextFactory(options));

                string number = await AllocateOnceAsync(service, context, new DateTime(2026, 1, 2));

                Assert.StartsWith("TNRC-SSDL-2026-", number, StringComparison.Ordinal);
                Assert.DoesNotContain("2025", number);
            }
            finally { CleanUp(dbPath); }
        }

        [Fact]
        public async Task Allocate_ResetsSequenceAtYearBoundary()
        {
            string dbPath = NewDbPath("reset");
            var options = OptionsFor(dbPath);
            try
            {
                using var context = new CalQrDbContext(options);
                DatabaseMigrator.RunMigrations(context);
                var service = new CertificateNumberService(new TestDbContextFactory(options));

                for (int i = 0; i < 42; i++)
                {
                    await AllocateOnceAsync(service, context, new DateTime(2026, 6, 1));
                }

                string last2026 = await AllocateOnceAsync(service, context, new DateTime(2026, 12, 31));
                string first2027 = await AllocateOnceAsync(service, context, new DateTime(2027, 1, 1));

                Assert.Equal("TNRC-SSDL-2026-0043", last2026);
                Assert.Equal("TNRC-SSDL-2027-0001", first2027);
            }
            finally { CleanUp(dbPath); }
        }

        [Fact]
        public async Task Allocate_UnderConcurrency_ProducesNoDuplicatesAndNoGaps()
        {
            string dbPath = NewDbPath("race");
            var options = OptionsFor(dbPath);
            const int concurrentAllocations = 50;

            try
            {
                using (var context = new CalQrDbContext(options))
                {
                    DatabaseMigrator.RunMigrations(context);
                }

                var factory = new TestDbContextFactory(options);
                var service = new CertificateNumberService(factory);
                var issueDate = new DateTime(2026, 3, 10);

                var tasks = Enumerable.Range(0, concurrentAllocations).Select(async _ =>
                {
                    // كل مهمة بسياقها ومعاملتها، كما يقع فعلاً عند مستخدمين متزامنين
                    using var context = factory.CreateDbContext();
                    return await AllocateOnceAsync(service, context, issueDate);
                });

                string[] numbers = await Task.WhenAll(tasks);

                Assert.Equal(concurrentAllocations, numbers.Distinct().Count());

                var sequences = numbers
                    .Select(n => int.Parse(n.Split('-')[3]))
                    .OrderBy(n => n)
                    .ToArray();

                Assert.Equal(Enumerable.Range(1, concurrentAllocations).ToArray(), sequences);
            }
            finally { CleanUp(dbPath); }
        }

        [Fact]
        public async Task Allocate_OutsideTransaction_Throws()
        {
            string dbPath = NewDbPath("notx");
            var options = OptionsFor(dbPath);
            try
            {
                using var context = new CalQrDbContext(options);
                DatabaseMigrator.RunMigrations(context);
                var service = new CertificateNumberService(new TestDbContextFactory(options));

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => service.AllocateAsync(context, new DateTime(2026, 1, 1)));
            }
            finally { CleanUp(dbPath); }
        }

        [Fact]
        public async Task Allocate_AfterRollback_ReturnsTheNumberToThePool_BecauseNoCertificateEverExisted()
        {
            // رقم خُصّص ثم تراجعت المعاملة: العدّاد يتراجع معها، فالرقم يعود.
            //
            // هذا سلوك **معتمد وصحيح**، لا عيب ولا تنازل: التراجع يعني أن الشهادة
            // لم توجد قط — لم تُطبع، ولم تدخل سجل الصادر، ولم يرها أحد.
            // قاعدة «الرقم لا يعود للاستخدام أبداً» تخص رقماً **ثبت** فصار وثيقة،
            // وتحرسه SoftDelete مع الفهرس الفريد غير المشروط على CertificateNumber
            // — لا هذا المسار.
            //
            // اختبار SoftDeletedCertificate_DoesNotReleaseItsNumber في
            // CertificateAmendmentTests هو الذي يحرس القاعدة الحقيقية.
            string dbPath = NewDbPath("rollback");
            var options = OptionsFor(dbPath);
            try
            {
                using var context = new CalQrDbContext(options);
                DatabaseMigrator.RunMigrations(context);
                var service = new CertificateNumberService(new TestDbContextFactory(options));

                await AllocateOnceAsync(service, context, new DateTime(2026, 1, 5));

                using (var transaction = await context.Database.BeginTransactionAsync())
                {
                    await service.AllocateAsync(context, new DateTime(2026, 1, 5));
                    await transaction.RollbackAsync();
                }

                string next = await AllocateOnceAsync(service, context, new DateTime(2026, 1, 5));

                // العدّاد داخل المعاملة، فتراجعها يُعيد الزيادة: التالي 0002 لا 0003.
                // (بخلاف SEQUENCE في محركات أخرى، وهي لا تتراجع.)
                Assert.Equal("TNRC-SSDL-2026-0002", next);
            }
            finally { CleanUp(dbPath); }
        }

        [Fact]
        public async Task Sync_RaisesCounterToLargestExistingNumber()
        {
            string dbPath = NewDbPath("syncup");
            var options = OptionsFor(dbPath);
            try
            {
                using (var context = new CalQrDbContext(options))
                {
                    DatabaseMigrator.RunMigrations(context);
                    SeedCertificateWithNumber(context, "TNRC-SSDL-2026-0009");
                    context.CertificateSequence.Add(new CertificateSequence { Year = 2026, LastNumber = 3 });
                    context.SaveChanges();
                }

                var service = new CertificateNumberService(new TestDbContextFactory(options));
                int changed = await service.SyncCounterWithExistingAsync();

                Assert.Equal(1, changed);

                using (var context = new CalQrDbContext(options))
                {
                    Assert.Equal(9, context.CertificateSequence.Single(s => s.Year == 2026).LastNumber);
                    // لا صفّ طفيلي بسنة مشوّهة — هذا وحده كان سيكشف انزياح substr فوراً
                    Assert.Single(context.CertificateSequence);
                }
            }
            finally { CleanUp(dbPath); }
        }

        [Fact]
        public async Task Sync_NeverLowersCounter_AndProvesItEvaluatedTheYear()
        {
            // الفرق بين دالّة مزامنة آمنة وثغرة إعادة استخدام رقم.
            //
            // ⚠ النسخة السابقة من هذا الاختبار نجحت نجاحاً كاذباً: انزياح substr
            // كان يُرسل الإدراج إلى صفّ Year = 26، فلا يُمَسّ Year = 2026 أصلاً
            // وتبقى 20 صدفةً. أي أن قاعدة «لا تُنقص» لم تُفحص قط.
            //
            // العلاج: سنتان في نفس الاستدعاء — واحدة يجب أن تبقى، وأخرى يجب أن
            // تُرفع. نجاح الثانية يُثبت أن الحلقة دارت وقيّمت الأولى فعلاً
            // واختارت عدم إنقاصها، بدل أن تكون تخطّتها بصمت.
            string dbPath = NewDbPath("syncdown");
            var options = OptionsFor(dbPath);
            try
            {
                using (var context = new CalQrDbContext(options))
                {
                    DatabaseMigrator.RunMigrations(context);
                    SeedCertificateWithNumber(context, "TNRC-SSDL-2026-0009");
                    SeedCertificateWithNumber(context, "TNRC-SSDL-2027-0005");
                    context.CertificateSequence.Add(new CertificateSequence { Year = 2026, LastNumber = 20 });
                    context.SaveChanges();
                }

                var service = new CertificateNumberService(new TestDbContextFactory(options));
                int changed = await service.SyncCounterWithExistingAsync();

                // 2027 وحدها تغيّرت؛ 2026 قُيّمت ورُفض إنقاصها
                Assert.Equal(1, changed);

                using (var context = new CalQrDbContext(options))
                {
                    Assert.Equal(20, context.CertificateSequence.Single(s => s.Year == 2026).LastNumber);
                    Assert.Equal(5, context.CertificateSequence.Single(s => s.Year == 2027).LastNumber);
                    Assert.Equal(2, context.CertificateSequence.Count());
                }
            }
            finally { CleanUp(dbPath); }
        }

        [Fact]
        public async Task Sync_NeverProducesAMalformedYearRow()
        {
            // الحارس الذي كان سيكشف انزياح substr فوراً: أي صفّ بسنة خارج نطاق
            // معقول دليلٌ على أن الرقم قُرئ من موضع خاطئ.
            string dbPath = NewDbPath("syncyearsanity");
            var options = OptionsFor(dbPath);
            try
            {
                using (var context = new CalQrDbContext(options))
                {
                    DatabaseMigrator.RunMigrations(context);
                    SeedCertificateWithNumber(context, "TNRC-SSDL-2026-0009");
                    SeedCertificateWithNumber(context, "TNRC-SSDL-2027-0005");
                    context.SaveChanges();
                }

                var service = new CertificateNumberService(new TestDbContextFactory(options));
                await service.SyncCounterWithExistingAsync();

                using (var context = new CalQrDbContext(options))
                {
                    var years = context.CertificateSequence.Select(s => s.Year).ToList();

                    Assert.Equal(new[] { 2026, 2027 }, years.OrderBy(y => y).ToArray());
                    Assert.All(years, y => Assert.InRange(y, 2000, 2999));
                }
            }
            finally { CleanUp(dbPath); }
        }

        [Fact]
        public async Task Sync_IgnoresNumbersOfForeignShape()
        {
            // رقم ورقي مرحَّل بصيغة أخرى لا يدخل المزامنة ولا يُنتج صفّاً.
            string dbPath = NewDbPath("syncforeign");
            var options = OptionsFor(dbPath);
            try
            {
                using (var context = new CalQrDbContext(options))
                {
                    DatabaseMigrator.RunMigrations(context);
                    SeedCertificateWithNumber(context, "SSDL/441/2025");
                    SeedCertificateWithNumber(context, "TNRC-SSDL-26-0009");
                    context.SaveChanges();
                }

                var service = new CertificateNumberService(new TestDbContextFactory(options));
                int changed = await service.SyncCounterWithExistingAsync();

                Assert.Equal(0, changed);

                using (var context = new CalQrDbContext(options))
                {
                    Assert.Empty(context.CertificateSequence);
                }
            }
            finally { CleanUp(dbPath); }
        }

        [Fact]
        public void Format_ProducesTheFrozenShape()
        {
            Assert.Equal("TNRC-SSDL-2026-0018", CertificateNumberService.Format(2026, 18));
            Assert.Equal("TNRC-SSDL-2026-0001", CertificateNumberService.Format(2026, 1));
            Assert.Equal("TNRC-SSDL-2026-9999", CertificateNumberService.Format(2026, 9999));
        }

        private static void SeedCertificateWithNumber(CalQrDbContext context, string certificateNumber)
        {
            var owner = new Owner { Name = "جهة " + certificateNumber };
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

            context.Certificates.Add(new Certificate
            {
                CalibrationRecordId = record.Id,
                CertificateNumber = certificateNumber,
                ClientName = "مستشفى بنغازي الطبي",
                DeviceModel = "Ludlum 44-9",
                DeviceSerialNumber = "SN-" + certificateNumber,
                CalibrationDate = new DateTime(2026, 1, 15),
                IssueDate = new DateTime(2026, 1, 20),
                DueDate = new DateTime(2027, 1, 15)
            });
            context.SaveChanges();
        }
    }
}
