using System;
using System.IO;
using System.Threading.Tasks;
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
    /// مسار الإخراج (تصدير PDF وطباعة الملصق) يجد الشهادة بالرابط الموثوق CalibrationRecordId،
    /// لا بالعمود القديم CalibrationRecord.CertificateNumber القابل للتحرير: رقم مكتوب فيه قد
    /// يكون خُصِّص لاحقاً لشهادة سجلّ آخر.
    /// </summary>
    public class CertificateLookupByRecordTests
    {
        private class TestDbContextFactory : IDbContextFactory<CalQrDbContext>
        {
            private readonly DbContextOptions<CalQrDbContext> _options;
            public TestDbContextFactory(DbContextOptions<CalQrDbContext> options) => _options = options;
            public CalQrDbContext CreateDbContext() => new CalQrDbContext(_options);
        }

        private sealed class Harness : IDisposable
        {
            public string DbPath { get; }
            public DbContextOptions<CalQrDbContext> Options { get; }
            public CertificateRepository Repository { get; }
            public int RecordA { get; }
            public int RecordB { get; }

            public Harness(string tag)
            {
                DbPath = Path.Combine(Path.GetTempPath(), $"cal_qr_lookup_{tag}_{Guid.NewGuid():N}.db");
                Options = new DbContextOptionsBuilder<CalQrDbContext>()
                    .UseSqlite($"Data Source={DbPath}")
                    .Options;
                var factory = new TestDbContextFactory(Options);

                using (var context = new CalQrDbContext(Options))
                {
                    DatabaseMigrator.RunMigrations(context);

                    var owner = new Owner { Name = "مختبر الاختبار" };
                    var deviceType = new DeviceType { Name = "Pancake Probe" };
                    context.Owners.Add(owner);
                    context.DeviceTypes.Add(deviceType);
                    context.SaveChanges();

                    var deviceA = new Device { Model = "Ludlum 44-9", SerialNumber = "SN-A-" + tag, OwnerId = owner.Id, DeviceTypeId = deviceType.Id };
                    var deviceB = new Device { Model = "Ludlum 44-9", SerialNumber = "SN-B-" + tag, OwnerId = owner.Id, DeviceTypeId = deviceType.Id };
                    context.Devices.AddRange(deviceA, deviceB);
                    context.SaveChanges();

                    var recordA = NewRecord(deviceA.Id, "A");
                    var recordB = NewRecord(deviceB.Id, "B");
                    context.CalibrationRecords.AddRange(recordA, recordB);
                    context.SaveChanges();
                    RecordA = recordA.Id;
                    RecordB = recordB.Id;
                }

                var hmac = new HmacService(factory);
                hmac.Initialize();
                Repository = new CertificateRepository(
                    factory,
                    new CertificateNumberService(factory),
                    new CertificateSignatureService(hmac));
            }

            private static CalibrationRecord NewRecord(int deviceId, string tag) => new CalibrationRecord
            {
                DeviceId = deviceId,
                CalibrationDate = new DateTime(2026, 1, 15),
                ExpiryDate = new DateTime(2027, 1, 15),
                EngineerName = "م. أحمد الشريف",
                Result = "Passed",
                HmacSignature = "SIG-" + tag
            };

            public void Dispose()
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                if (File.Exists(DbPath)) File.Delete(DbPath);
            }
        }

        private static Certificate NewCertificate(int calibrationRecordId, string serial) => new Certificate
        {
            CalibrationRecordId = calibrationRecordId,
            CertificateTemplateType = "Pancake Probe",
            ClientName = "مختبر الاختبار",
            DeviceModel = "Ludlum 44-9",
            DeviceSerialNumber = serial,
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

        [Fact]
        public async Task GetByCalibrationRecordId_ReturnsThatRecordsLiveCertificate()
        {
            using var harness = new Harness("own");

            string number = await harness.Repository.AddAsync(NewCertificate(harness.RecordA, "SN-A"));

            var found = await harness.Repository.GetByCalibrationRecordIdAsync(harness.RecordA);

            Assert.NotNull(found);
            Assert.Equal(number, found!.CertificateNumber);
            Assert.Equal(harness.RecordA, found.CalibrationRecordId);
            Assert.NotEmpty(found.CalibrationResults);
            Assert.Null(await harness.Repository.GetByCalibrationRecordIdAsync(harness.RecordB));
        }

        [Fact]
        public async Task GetByCalibrationRecordId_IgnoresRevokedCertificate()
        {
            using var harness = new Harness("revoked");

            await harness.Repository.AddAsync(NewCertificate(harness.RecordA, "SN-A"));
            var issued = await harness.Repository.GetByCalibrationRecordIdAsync(harness.RecordA);
            await harness.Repository.RevokeAsync(issued!.Id, "م. خالد العتيبي", "اختبار");

            Assert.Null(await harness.Repository.GetByCalibrationRecordIdAsync(harness.RecordA));
        }

        /// <summary>
        /// سيناريو NV-7: سجلّ A أُبطلت شهادته وكُتب في عموده القديم رقمٌ خُصِّص لاحقاً لشهادة السجلّ B.
        /// البحث بالرقم (المسار القديم) يُعيد شهادة B؛ البحث بالسجلّ لا يُعيد لـA شيئاً ولـB شهادته.
        /// </summary>
        [Fact]
        public async Task LegacyNumberPointingAtAnotherRecordsCertificate_DoesNotLeakIntoLookupByRecord()
        {
            using var harness = new Harness("collision");

            await harness.Repository.AddAsync(NewCertificate(harness.RecordA, "SN-A"));
            var certA = await harness.Repository.GetByCalibrationRecordIdAsync(harness.RecordA);
            await harness.Repository.RevokeAsync(certA!.Id, "م. خالد العتيبي", "اختبار");

            string numberB = await harness.Repository.AddAsync(NewCertificate(harness.RecordB, "SN-B"));

            using (var context = new CalQrDbContext(harness.Options))
            {
                var recordA = await context.CalibrationRecords.FindAsync(harness.RecordA);
                recordA!.CertificateNumber = numberB;
                await context.SaveChangesAsync();
            }

            // المسار القديم كان يُخرج شهادة جهاز آخر لسجلّ A.
            var byLegacyNumber = await harness.Repository.GetByCertificateNumberAsync(numberB);
            Assert.Equal(harness.RecordB, byLegacyNumber!.CalibrationRecordId);

            Assert.Null(await harness.Repository.GetByCalibrationRecordIdAsync(harness.RecordA));
            var forB = await harness.Repository.GetByCalibrationRecordIdAsync(harness.RecordB);
            Assert.Equal(numberB, forB!.CertificateNumber);
            Assert.Equal("SN-B", forB.DeviceSerialNumber);
        }

        [Theory]
        [InlineData("TNRC-SSDL-2026-0001", true)]
        [InlineData("  TNRC-SSDL-2026-0001  ", true)]
        [InlineData(CertificateNumberDisplayRules.None, false)]
        [InlineData("", false)]
        [InlineData("   ", false)]
        [InlineData(null, false)]
        public void HasIssuedCertificate_FollowsDisplayValue(string? display, bool expected)
        {
            Assert.Equal(expected, CertificateNumberDisplayRules.HasIssuedCertificate(display));
        }

        [Fact]
        public void HasIssuedCertificate_RoundTripsWithDisplay()
        {
            Assert.False(CertificateNumberDisplayRules.HasIssuedCertificate(CertificateNumberDisplayRules.Display(null)));
            Assert.True(CertificateNumberDisplayRules.HasIssuedCertificate(CertificateNumberDisplayRules.Display("TNRC-SSDL-2026-0007")));
        }
    }
}
