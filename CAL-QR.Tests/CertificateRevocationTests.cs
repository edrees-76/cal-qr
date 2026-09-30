using System;
using System.IO;
using System.Linq;
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
    /// التحقق من سلوك إلغاء الشهادة: قاعدة البيانات، مسار التحقق، والقيود.
    /// SQLite حقيقي لضمان سلوك الفهارس والمعاملات.
    /// </summary>
    public class CertificateRevocationTests
    {
        private class TestDbContextFactory : IDbContextFactory<CalQrDbContext>
        {
            private readonly DbContextOptions<CalQrDbContext> _options;
            public TestDbContextFactory(DbContextOptions<CalQrDbContext> options) => _options = options;
            public CalQrDbContext CreateDbContext() => new CalQrDbContext(_options);
        }

        private static string NewDbPath(string tag) =>
            Path.Combine(Path.GetTempPath(), $"cal_qr_revoke_{tag}_{Guid.NewGuid():N}.db");

        private static DbContextOptions<CalQrDbContext> OptionsFor(string dbPath) =>
            new DbContextOptionsBuilder<CalQrDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

        private static void CleanUp(string dbPath)
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) { try { File.Delete(dbPath); } catch { } }
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
            var owner = new Owner { Name = "مختبر الاختبار" };
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
            ClientName = "مختبر الاختبار",
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

        [Fact]
        public async Task RevokeAsync_SetsIsRevokedAndIsDeleted()
        {
            using var harness = new Harness("flags");

            string number = await harness.Repository.AddAsync(NewCertificate(harness.CalibrationRecordId));
            var issued = await harness.Repository.GetByCertificateNumberAsync(number);

            await harness.Repository.RevokeAsync(issued!.Id, "م. خالد العتيبي", "جهاز معطوب وتم استبداله");

            using var context = new CalQrDbContext(harness.Options);
            var stored = context.Certificates.Single(c => c.Id == issued.Id);

            Assert.True(stored.IsRevoked);
            Assert.True(stored.IsDeleted);
            Assert.NotNull(stored.RevokedAt);
            Assert.Equal("م. خالد العتيبي", stored.RevokedByName);
            Assert.Equal("جهاز معطوب وتم استبداله", stored.RevocationReason);
        }

        [Fact]
        public async Task RevokeAsync_VerifyCode_ReturnsRevokedStatus()
        {
            using var harness = new Harness("verify");

            string number = await harness.Repository.AddAsync(NewCertificate(harness.CalibrationRecordId));
            var issued = await harness.Repository.GetByCertificateNumberAsync(number);
            string code = issued!.VerifyCode!;

            await harness.Repository.RevokeAsync(issued.Id, "م. فاطمة الزهراء", "انتهت صلاحية الجهاز");

            var result = await harness.Repository.VerifyByCodeAsync(code);

            Assert.Equal(CertificateVerificationStatus.Revoked, result.Status);
            Assert.NotNull(result.Certificate);
            Assert.NotNull(result.RevokedAt);
            Assert.Equal("م. فاطمة الزهراء", result.RevokedByName);
            Assert.Equal("انتهت صلاحية الجهاز", result.RevocationReason);
        }

        [Fact]
        public async Task RevokeAsync_AllowsNewCertificateForSameCalibrationRecord()
        {
            // الفهرس الفريد المشروط (IsDeleted=0) يسمح بشهادة بديلة بعد الإلغاء.
            using var harness = new Harness("replacement");

            string first = await harness.Repository.AddAsync(NewCertificate(harness.CalibrationRecordId));
            var issued = await harness.Repository.GetByCertificateNumberAsync(first);

            await harness.Repository.RevokeAsync(issued!.Id, "م. علي الغرياني", "بيانات خاطئة");

            string second = await harness.Repository.AddAsync(NewCertificate(harness.CalibrationRecordId));

            Assert.NotEqual(first, second);
            Assert.Equal("TNRC-SSDL-2026-0001", first);
            Assert.Equal("TNRC-SSDL-2026-0002", second);
        }

        [Fact]
        public async Task RevokeAsync_DoubleRevoke_ThrowsInvalidOperationException()
        {
            using var harness = new Harness("double");

            string number = await harness.Repository.AddAsync(NewCertificate(harness.CalibrationRecordId));
            var issued = await harness.Repository.GetByCertificateNumberAsync(number);

            await harness.Repository.RevokeAsync(issued!.Id, "م. خالد", "سبب أول");

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => harness.Repository.RevokeAsync(issued.Id, "م. سامي", "سبب ثانٍ"));
        }

        [Fact]
        public async Task RevokeAsync_MissingId_ThrowsInvalidOperationException()
        {
            using var harness = new Harness("notfound");

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => harness.Repository.RevokeAsync(9999, "م. أحمد", "اختبار"));
        }

        [Fact]
        public async Task RevokeAsync_EmptyRevokedBy_ThrowsArgumentException()
        {
            using var harness = new Harness("emptyname");

            string number = await harness.Repository.AddAsync(NewCertificate(harness.CalibrationRecordId));
            var issued = await harness.Repository.GetByCertificateNumberAsync(number);

            await Assert.ThrowsAsync<ArgumentException>(
                () => harness.Repository.RevokeAsync(issued!.Id, "   ", "سبب صالح"));
        }

        [Fact]
        public async Task RevokeAsync_EmptyReason_ThrowsArgumentException()
        {
            using var harness = new Harness("emptyreason");

            string number = await harness.Repository.AddAsync(NewCertificate(harness.CalibrationRecordId));
            var issued = await harness.Repository.GetByCertificateNumberAsync(number);

            await Assert.ThrowsAsync<ArgumentException>(
                () => harness.Repository.RevokeAsync(issued!.Id, "م. أحمد", ""));
        }

        [Fact]
        public async Task RevokedCertificate_DoesNotAppearAsAuthentic()
        {
            using var harness = new Harness("notauthentic");

            string number = await harness.Repository.AddAsync(NewCertificate(harness.CalibrationRecordId));
            var issued = await harness.Repository.GetByCertificateNumberAsync(number);
            string code = issued!.VerifyCode!;

            await harness.Repository.RevokeAsync(issued.Id, "م. محمد", "اختبار");

            var result = await harness.Repository.VerifyByCodeAsync(code);

            Assert.NotEqual(CertificateVerificationStatus.Authentic, result.Status);
            Assert.NotEqual(CertificateVerificationStatus.AuthenticAmended, result.Status);
        }

        [Fact]
        public async Task RevokeAsync_StoredTimestamp_IsUtcAndRecent()
        {
            using var harness = new Harness("timestamp");

            var before = DateTime.UtcNow.AddSeconds(-2);
            string number = await harness.Repository.AddAsync(NewCertificate(harness.CalibrationRecordId));
            var issued = await harness.Repository.GetByCertificateNumberAsync(number);

            await harness.Repository.RevokeAsync(issued!.Id, "م. رضا", "اختبار التوقيت");
            var after = DateTime.UtcNow.AddSeconds(2);

            using var context = new CalQrDbContext(harness.Options);
            var stored = context.Certificates.Single(c => c.Id == issued.Id);

            Assert.NotNull(stored.RevokedAt);
            Assert.InRange(stored.RevokedAt!.Value, before, after);
        }

        [Fact]
        public async Task RevokedAfterAmendment_BothTheOldAndTheCurrentCode_ReportRevoked()
        {
            using var harness = new Harness("chain");

            string number = await harness.Repository.AddAsync(NewCertificate(harness.CalibrationRecordId));
            var issued = await harness.Repository.GetByCertificateNumberAsync(number);
            string oldCode = issued!.VerifyCode!;

            issued.CalibrationResults.Single().MeasuredReading = "5.25";
            Assert.True(await harness.Repository.UpdateAsync(issued));

            var amended = await harness.Repository.GetByCertificateNumberAsync(number);
            string currentCode = amended!.VerifyCode!;
            Assert.NotEqual(oldCode, currentCode);

            await harness.Repository.RevokeAsync(amended.Id, "م. خالد العتيبي", "خطأ في القراءة");

            var viaCurrent = await harness.Repository.VerifyByCodeAsync(currentCode);
            var viaOld = await harness.Repository.VerifyByCodeAsync(oldCode);

            Assert.Equal(CertificateVerificationStatus.Revoked, viaCurrent.Status);
            Assert.Equal(CertificateVerificationStatus.Revoked, viaOld.Status);
            Assert.Equal("خطأ في القراءة", viaOld.RevocationReason);
            Assert.Equal("م. خالد العتيبي", viaOld.RevokedByName);
            Assert.NotNull(viaOld.RevokedAt);
        }

        [Fact]
        public async Task AmendedButNotRevoked_OldCode_StillReportsAuthenticAmended()
        {
            using var harness = new Harness("amendonly");

            string number = await harness.Repository.AddAsync(NewCertificate(harness.CalibrationRecordId));
            var issued = await harness.Repository.GetByCertificateNumberAsync(number);
            string oldCode = issued!.VerifyCode!;

            issued.CalibrationResults.Single().MeasuredReading = "5.25";
            Assert.True(await harness.Repository.UpdateAsync(issued));

            var viaOld = await harness.Repository.VerifyByCodeAsync(oldCode);

            Assert.Equal(CertificateVerificationStatus.AuthenticAmended, viaOld.Status);
        }

        [Fact]
        public async Task UpdateAsync_OnACertificateRevokedAfterTheObjectWasLoaded_ThrowsAndKeepsItRevoked()
        {
            using var harness = new Harness("stale");

            string number = await harness.Repository.AddAsync(NewCertificate(harness.CalibrationRecordId));
            var staleCopy = await harness.Repository.GetByCertificateNumberAsync(number);

            // شهادة تُلغى بعد أن حُمّلت هذه النسخة (نافذة تعديل مفتوحة مثلاً).
            await harness.Repository.RevokeAsync(staleCopy!.Id, "م. سامي", "استبدال الجهاز");

            staleCopy.CalibrationResults.Single().MeasuredReading = "5.25";
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => harness.Repository.UpdateAsync(staleCopy));

            using var context = new CalQrDbContext(harness.Options);
            var stored = context.Certificates.Single(c => c.Id == staleCopy.Id);
            Assert.True(stored.IsRevoked);
            Assert.True(stored.IsDeleted);
            Assert.NotNull(stored.RevokedAt);
            Assert.Equal("م. سامي", stored.RevokedByName);
            Assert.Equal("استبدال الجهاز", stored.RevocationReason);
        }
    }
}
