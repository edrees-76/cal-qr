using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Repositories;
using CAL_QR.Services;
using CAL_QR.Validation;

namespace CAL_QR.Tests
{
    /// <summary>
    /// حدّ عدّاد الشهادات خارج القاعدة: استعادة نسخة أقدم لا تُعيد إعطاء رقم شهادة صدرت بعدها.
    /// كلّ اختبار يستخدم مجلّداً مؤقّتاً للمخزن، فلا كتابة في ProgramData.
    /// </summary>
    public class CertificateCounterFloorTests
    {
        private class TestDbContextFactory : IDbContextFactory<CalQrDbContext>
        {
            private readonly DbContextOptions<CalQrDbContext> _options;
            public TestDbContextFactory(DbContextOptions<CalQrDbContext> options) => _options = options;
            public CalQrDbContext CreateDbContext() => new CalQrDbContext(_options);
        }

        private static string NewTempDir(string tag)
        {
            string dir = Path.Combine(Path.GetTempPath(), $"cal_qr_counter_{tag}_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void DeleteDir(string dir)
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        // ── القواعد ────────────────────────────────────────────────────────────

        [Fact]
        public void LedgerId_IsStableShortAndKeyDependent()
        {
            Assert.Null(CertificateCounterFloorRules.LedgerId(null));
            Assert.Null(CertificateCounterFloorRules.LedgerId("  "));

            string? a1 = CertificateCounterFloorRules.LedgerId("key-A");
            string? a2 = CertificateCounterFloorRules.LedgerId("key-A");
            string? b = CertificateCounterFloorRules.LedgerId("key-B");

            Assert.Equal(a1, a2);
            Assert.NotEqual(a1, b);
            Assert.Equal(16, a1!.Length);
            Assert.DoesNotContain("key-A", a1);
        }

        [Fact]
        public void Merge_TakesPerYearMaximum_AndIgnoresNonPositive()
        {
            var merged = CertificateCounterFloorRules.Merge(
                new Dictionary<int, int> { [2025] = 80, [2026] = 10 },
                null,
                new Dictionary<int, int> { [2026] = 45, [2027] = 0, [2024] = -3 });

            Assert.Equal(2, merged.Count);
            Assert.Equal(80, merged[2025]);
            Assert.Equal(45, merged[2026]);
        }

        [Fact]
        public void Raises_OnlyWhereFloorIsHigher_IncludingMissingYears()
        {
            var raises = CertificateCounterFloorRules.Raises(
                new Dictionary<int, int> { [2025] = 90, [2026] = 10 },
                new Dictionary<int, int> { [2025] = 80, [2026] = 45, [2027] = 3 });

            Assert.Equal(new[] { (2026, 10, 45), (2027, 0, 3) }, raises);
        }

        [Theory]
        [InlineData("45", 45, 45)]
        [InlineData(" 60 ", 45, 60)]
        [InlineData("9999", 45, 9999)]
        public void ParseConfirmed_AcceptsProposedOrHigher(string input, int proposed, int expected)
        {
            var (value, error) = CertificateCounterFloorRules.ParseConfirmed(input, proposed);
            Assert.Equal(expected, value);
            Assert.Null(error);
        }

        [Theory]
        [InlineData("44")]
        [InlineData("10000")]
        [InlineData("abc")]
        [InlineData("")]
        [InlineData("-50")]
        [InlineData("4.5")]
        public void ParseConfirmed_RejectsLowerOutOfRangeOrNonNumbers(string input)
        {
            var (value, error) = CertificateCounterFloorRules.ParseConfirmed(input, 45);
            Assert.Null(value);
            Assert.False(string.IsNullOrWhiteSpace(error));
        }

        // ── المخزن ─────────────────────────────────────────────────────────────

        [Fact]
        public void Store_RaisesNeverLowers_SeparatesLedgers_AndPersists()
        {
            string dir = NewTempDir("store");
            try
            {
                var store = new CertificateCounterFloorStore(dir);
                store.Raise("ledger-a", new Dictionary<int, int> { [2026] = 45 });
                store.Raise("ledger-a", new Dictionary<int, int> { [2026] = 30, [2025] = 7 });
                store.Raise("ledger-b", new Dictionary<int, int> { [2026] = 3 });

                var reopened = new CertificateCounterFloorStore(dir);
                var a = reopened.Read("ledger-a");
                Assert.Equal(45, a[2026]);
                Assert.Equal(7, a[2025]);
                Assert.Equal(3, reopened.Read("ledger-b")[2026]);
                Assert.Empty(reopened.Read("ledger-unknown"));
            }
            finally
            {
                DeleteDir(dir);
            }
        }

        [Fact]
        public void Store_CorruptFile_IsMovedAsideNotOverwrittenSilently()
        {
            string dir = NewTempDir("corrupt");
            try
            {
                string file = Path.Combine(dir, CertificateCounterFloorStore.FileName);
                File.WriteAllText(file, "{ this is not json");

                var store = new CertificateCounterFloorStore(dir);
                Assert.Empty(store.Read("ledger-a"));

                Assert.False(File.Exists(file));
                string aside = Assert.Single(Directory.GetFiles(dir, CertificateCounterFloorStore.FileName + ".corrupt-*"));
                Assert.Equal("{ this is not json", File.ReadAllText(aside));

                store.Raise("ledger-a", new Dictionary<int, int> { [2026] = 5 });
                Assert.Equal(5, store.Read("ledger-a")[2026]);
            }
            finally
            {
                DeleteDir(dir);
            }
        }

        // ── التطبيق عند الإقلاع ────────────────────────────────────────────────

        private static DbContextOptions<CalQrDbContext> InMemoryOptions() =>
            new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase("counter_floor_" + Guid.NewGuid().ToString("N"))
                .Options;

        [Fact]
        public void Apply_RaisesDatabaseToFloor_AndFloorToDatabase()
        {
            string dir = NewTempDir("apply");
            try
            {
                var options = InMemoryOptions();
                using (var context = new CalQrDbContext(options))
                {
                    context.AppSettings.Add(new AppSetting { Key = CertificateCounterFloor.HmacKeySetting, Value = "key-A" });
                    context.CertificateSequence.Add(new CertificateSequence { Year = 2025, LastNumber = 90 });
                    context.CertificateSequence.Add(new CertificateSequence { Year = 2026, LastNumber = 10 });
                    context.SaveChanges();
                }

                var store = new CertificateCounterFloorStore(dir);
                string ledger = CertificateCounterFloorRules.LedgerId("key-A")!;
                store.Raise(ledger, new Dictionary<int, int> { [2025] = 80, [2026] = 45 });

                List<(int Year, int From, int To)> raises;
                using (var context = new CalQrDbContext(options))
                {
                    raises = CertificateCounterFloor.Apply(context, store);
                }

                Assert.Equal(new[] { (2026, 10, 45) }, raises);
                using (var context = new CalQrDbContext(options))
                {
                    Assert.Equal(90, context.CertificateSequence.Single(s => s.Year == 2025).LastNumber);
                    Assert.Equal(45, context.CertificateSequence.Single(s => s.Year == 2026).LastNumber);
                }
                Assert.Equal(90, store.Read(ledger)[2025]);
                Assert.Equal(45, store.Read(ledger)[2026]);
            }
            finally
            {
                DeleteDir(dir);
            }
        }

        [Fact]
        public void Apply_OtherLedgerFloor_DoesNotTouchThisDatabase()
        {
            string dir = NewTempDir("ledger");
            try
            {
                var options = InMemoryOptions();
                using (var context = new CalQrDbContext(options))
                {
                    context.AppSettings.Add(new AppSetting { Key = CertificateCounterFloor.HmacKeySetting, Value = "demo-key" });
                    context.CertificateSequence.Add(new CertificateSequence { Year = 2026, LastNumber = 4 });
                    context.SaveChanges();
                }

                var store = new CertificateCounterFloorStore(dir);
                store.Raise(CertificateCounterFloorRules.LedgerId("real-key")!, new Dictionary<int, int> { [2026] = 300 });

                using (var context = new CalQrDbContext(options))
                {
                    Assert.Empty(CertificateCounterFloor.Apply(context, store));
                }
                using (var context = new CalQrDbContext(options))
                {
                    Assert.Equal(4, context.CertificateSequence.Single().LastNumber);
                }
            }
            finally
            {
                DeleteDir(dir);
            }
        }

        // ── الإصدار يسجّل الحدّ ────────────────────────────────────────────────

        [Fact]
        public async Task AddAsync_RecordsIssuedNumberInFloorStore()
        {
            string dir = NewTempDir("issue");
            string dbPath = Path.Combine(dir, "issue.db");
            try
            {
                var options = new DbContextOptionsBuilder<CalQrDbContext>().UseSqlite($"Data Source={dbPath}").Options;
                var factory = new TestDbContextFactory(options);
                int recordId;
                using (var context = new CalQrDbContext(options))
                {
                    DatabaseMigrator.RunMigrations(context);
                    var owner = new Owner { Name = "مختبر الاختبار" };
                    var deviceType = new DeviceType { Name = "Pancake Probe" };
                    context.Owners.Add(owner);
                    context.DeviceTypes.Add(deviceType);
                    context.SaveChanges();
                    var device = new Device { Model = "Ludlum 44-9", SerialNumber = "SN-FLOOR", OwnerId = owner.Id, DeviceTypeId = deviceType.Id };
                    context.Devices.Add(device);
                    context.SaveChanges();
                    var record = new CalibrationRecord
                    {
                        DeviceId = device.Id,
                        CalibrationDate = new DateTime(2026, 1, 15),
                        ExpiryDate = new DateTime(2027, 1, 15),
                        EngineerName = "م. أحمد الشريف",
                        Result = "Passed",
                        HmacSignature = "SIG"
                    };
                    context.CalibrationRecords.Add(record);
                    context.SaveChanges();
                    recordId = record.Id;
                }

                var hmac = new HmacService(factory);
                hmac.Initialize();
                var store = new CertificateCounterFloorStore(dir);
                var repository = new CertificateRepository(
                    factory, new CertificateNumberService(factory), new CertificateSignatureService(hmac), store);

                string number = await repository.AddAsync(new Certificate
                {
                    CalibrationRecordId = recordId,
                    CertificateTemplateType = "Pancake Probe",
                    ClientName = "مختبر الاختبار",
                    DeviceModel = "Ludlum 44-9",
                    DeviceSerialNumber = "SN-FLOOR",
                    CalibrationDate = new DateTime(2026, 1, 15),
                    IssueDate = new DateTime(2026, 1, 20)
                });

                string ledger;
                using (var context = new CalQrDbContext(options))
                {
                    ledger = CertificateCounterFloor.LedgerIdOf(context)!;
                }
                Assert.Equal(CertificateDateRules.ExtractSequence(number), store.Read(ledger)[2026]);
            }
            finally
            {
                DeleteDir(dir);
            }
        }

        // ── الاستعادة ──────────────────────────────────────────────────────────

        private static void SetCounter(DbContextOptions<CalQrDbContext> options, int year, int lastNumber)
        {
            using var context = new CalQrDbContext(options);
            var row = context.CertificateSequence.SingleOrDefault(s => s.Year == year);
            if (row == null) context.CertificateSequence.Add(new CertificateSequence { Year = year, LastNumber = lastNumber });
            else row.LastNumber = lastNumber;
            context.SaveChanges();
        }

        private static int GetCounter(DbContextOptions<CalQrDbContext> options, int year)
        {
            using var context = new CalQrDbContext(options);
            return context.CertificateSequence.Single(s => s.Year == year).LastNumber;
        }

        [Fact]
        public async Task Restore_OlderBackup_ProposesLiveCounter_AndStartupRaisesRestoredDatabase()
        {
            string dir = NewTempDir("restore");
            int year = DateTime.Today.Year;
            try
            {
                string dbPath = Path.Combine(dir, "live.db");
                string backups = Path.Combine(dir, "Backups");
                string attachments = Path.Combine(dir, "Attachments");
                string qr = Path.Combine(dir, "QR_Output");
                string floorDir = Path.Combine(dir, "Floor");
                Directory.CreateDirectory(backups);
                Directory.CreateDirectory(attachments);
                Directory.CreateDirectory(qr);

                var options = new DbContextOptionsBuilder<CalQrDbContext>().UseSqlite($"Data Source={dbPath}").Options;
                var factory = new TestDbContextFactory(options);
                using (var context = new CalQrDbContext(options))
                {
                    context.Database.EnsureCreated();
                    context.AppSettings.Add(new AppSetting { Key = CertificateCounterFloor.HmacKeySetting, Value = "lab-key" });
                    context.AppSettings.Add(new AppSetting { Key = "BackupPath", Value = backups });
                    context.AppSettings.Add(new AppSetting { Key = "BackupSchedule", Value = "None" });
                    context.AppSettings.Add(new AppSetting { Key = "AttachmentsPath", Value = attachments });
                    context.AppSettings.Add(new AppSetting { Key = "QrOutputPath", Value = qr });
                    context.CertificateSequence.Add(new CertificateSequence { Year = year, LastNumber = 5 });
                    await context.SaveChangesAsync();
                }

                var store = new CertificateCounterFloorStore(floorDir);
                var service = new BackupService(
                    factory, new AuditLogRepository(factory, new TestCurrentUserService()), new TestBackupPasswordStore(), store);

                // نسخة يوم الاثنين (آخر رقم 5)، ثمّ صدرت 6..9 يوم الثلاثاء.
                await service.BackupNowAsync(backups);
                string backup = Assert.Single(Directory.GetFiles(backups, "CalQR_Backup_*.cqbak"));
                SetCounter(options, year, 9);

                var report = (await service.RestoreAsync(backup)).CounterReport;

                Assert.NotNull(report);
                Assert.Equal(year, report!.Year);
                Assert.Equal(5, report.RestoredLastNumber);
                Assert.Equal(9, report.ProposedLastNumber);
                Assert.Null(report.Warning);
                Assert.Equal(CertificateCounterFloorRules.LedgerId("lab-key"), report.LedgerId);

                // القاعدة المستعادة تحمل 5 حتّى الإقلاع؛ الإقلاع يرفعها إلى 9 ولو أُغلقت النافذة.
                Assert.Equal(5, GetCounter(options, year));
                using (var context = new CalQrDbContext(options))
                {
                    Assert.Equal(new[] { (year, 5, 9) }, CertificateCounterFloor.Apply(context, store));
                }
                Assert.Equal(9, GetCounter(options, year));
            }
            finally
            {
                DeleteDir(dir);
            }
        }

        [Fact]
        public async Task ConfirmCertificateCounter_RaisesDatabaseAndFloor_RejectsLowerThanProposed()
        {
            string dir = NewTempDir("confirm");
            int year = DateTime.Today.Year;
            try
            {
                string dbPath = Path.Combine(dir, "live.db");
                var options = new DbContextOptionsBuilder<CalQrDbContext>().UseSqlite($"Data Source={dbPath}").Options;
                var factory = new TestDbContextFactory(options);
                using (var context = new CalQrDbContext(options))
                {
                    context.Database.EnsureCreated();
                    context.CertificateSequence.Add(new CertificateSequence { Year = year, LastNumber = 5 });
                    await context.SaveChangesAsync();
                }

                var store = new CertificateCounterFloorStore(Path.Combine(dir, "Floor"));
                var service = new BackupService(
                    factory, new AuditLogRepository(factory, new TestCurrentUserService()), new TestBackupPasswordStore(), store);
                var report = new CertificateCounterRestoreReport("ledger-x", year, 5, 9, null);

                Assert.Throws<ArgumentOutOfRangeException>(() => service.ConfirmCertificateCounter(report, 8));
                Assert.Equal(5, GetCounter(options, year));

                service.ConfirmCertificateCounter(report, 12);

                Assert.Equal(12, GetCounter(options, year));
                Assert.Equal(12, store.Read("ledger-x")[year]);
            }
            finally
            {
                DeleteDir(dir);
            }
        }
    }
}
