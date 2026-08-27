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
    /// إعادة حساب التوقيع عند التعديل، وأرشفة الرمز السابق، ومسار التحقق
    /// بالرمز القديم. SQLite حقيقي: الاختبارات تعتمد على المعاملات والفهارس
    /// وسلوك Cascade التي لا يطبّقها مزوّد InMemory.
    /// </summary>
    public class CertificateAmendmentTests
    {
        private class TestDbContextFactory : IDbContextFactory<CalQrDbContext>
        {
            private readonly DbContextOptions<CalQrDbContext> _options;
            public TestDbContextFactory(DbContextOptions<CalQrDbContext> options) => _options = options;
            public CalQrDbContext CreateDbContext() => new CalQrDbContext(_options);
        }

        private static string NewDbPath(string tag) =>
            Path.Combine(Path.GetTempPath(), $"cal_qr_cert_amend_{tag}_{Guid.NewGuid():N}.db");

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

        /// <summary>
        /// تُصدِر الشهادة فعليًّا كما يفعل حوار النسخة الموقّعة: صفّ Attachment
        /// حقيقيّ ثمّ مزامنة. لا تضبط العمود مباشرةً — المسار الإنتاجيّ هو
        /// المقصود بالاختبار.
        /// </summary>
        private static async Task AttachSignedCopyAsync(Harness harness, int certificateId, string fileName = "signed.pdf")
        {
            using (var context = new CalQrDbContext(harness.Options))
            {
                context.Attachments.Add(new Attachment
                {
                    CalibrationRecordId = harness.CalibrationRecordId,
                    CertificateId = certificateId,
                    FileName = fileName,
                    FilePath = $@"X:\signed\{fileName}",
                    FileExtension = ".pdf"
                });
                await context.SaveChangesAsync();
            }

            await harness.Repository.SyncSignedCopyStateAsync(certificateId);
        }

        [Fact]
        public async Task Issue_AssignsNumberVerifyCodeAndVersion()
        {
            using var harness = new Harness("issue");

            string number = await harness.Repository.AddAsync(NewCertificate(harness.CalibrationRecordId));

            Assert.Equal("TNRC-SSDL-2026-0001", number);

            var stored = await harness.Repository.GetByCertificateNumberAsync(number);
            Assert.NotNull(stored);
            Assert.Equal(16, stored!.VerifyCode!.Length);
            Assert.Equal("SIG1", stored.SignaturePayloadVersion);
            Assert.Equal(new DateTime(2027, 1, 15), stored.DueDate);
            Assert.Null(stored.AmendedAt);
            Assert.Null(stored.FirstPrintedAt);
            // AE مشتقّ نصياً من RE عند الحفظ
            Assert.Equal("3.70", stored.CalibrationResults.Single().AbsoluteRelativeError);
        }

        [Fact]
        public async Task StoredVerifyCode_MatchesRecomputationFromACleanRead()
        {
            // إثبات أن الحساب وقع بعد SaveChanges الأولى: لو وقع قبلها لاختلف
            // ترتيب الأبناء بين لحظة الإصدار ولحظة القراءة.
            using var harness = new Harness("recompute");

            var certificate = NewCertificate(harness.CalibrationRecordId);
            certificate.CalibrationResults.Add(new CertificateCalibrationResult
            {
                SortOrder = 1,
                Radionuclide = "Co-60",
                ReferenceValue = "8.00"
            });
            certificate.CalibrationResults.Add(new CertificateCalibrationResult
            {
                SortOrder = 1,
                Radionuclide = "Am-241",
                ReferenceValue = "2.00"
            });

            string number = await harness.Repository.AddAsync(certificate);

            var reread = await harness.Repository.GetByCertificateNumberAsync(number);

            var hmac = new HmacService(harness.Factory);
            hmac.Initialize();
            var signatureService = new CertificateSignatureService(hmac);

            Assert.Equal(
                reread!.VerifyCode,
                signatureService.ComputeVerifyCode(reread, reread.SignaturePayloadVersion!));
        }

        [Fact]
        public async Task EditingNotes_DoesNotRotateTheCodeNorFlagAnAmendment()
        {
            using var harness = new Harness("notes");

            string number = await harness.Repository.AddAsync(NewCertificate(harness.CalibrationRecordId));
            var issued = await harness.Repository.GetByCertificateNumberAsync(number);
            string originalCode = issued!.VerifyCode!;

            await AttachSignedCopyAsync(harness, issued.Id);

            issued.Notes = "ملاحظة إدارية جديدة";
            issued.ReferenceNo = "OUT-2026-999";
            bool rotated = await harness.Repository.UpdateAsync(issued);

            Assert.False(rotated);

            var after = await harness.Repository.GetByCertificateNumberAsync(number);
            Assert.Equal(originalCode, after!.VerifyCode);
            Assert.Null(after.AmendedAt);

            using var context = new CalQrDbContext(harness.Options);
            Assert.Empty(context.CertificateVerifyCodeHistory);
        }

        [Fact]
        public async Task EditingBeforeTheSignedCopyArrives_RotatesTheCodeButLeavesAmendedAtNull()
        {
            using var harness = new Harness("beforeprint");

            string number = await harness.Repository.AddAsync(NewCertificate(harness.CalibrationRecordId));
            var issued = await harness.Repository.GetByCertificateNumberAsync(number);

            issued!.CalibrationResults.Single().MeasuredReading = "5.25";
            bool rotated = await harness.Repository.UpdateAsync(issued);

            Assert.True(rotated);

            var after = await harness.Repository.GetByCertificateNumberAsync(number);
            Assert.Null(after!.AmendedAt);
        }

        [Fact]
        public async Task OldCodeFromAPrintedCopy_ResolvesToTheRightCertificate_AsAmendedNotForged()
        {
            // الاختبار الذي يمنع ظهور وثيقة أصلية كأنها مزوَّرة.
            using var harness = new Harness("oldcode");

            string number = await harness.Repository.AddAsync(NewCertificate(harness.CalibrationRecordId));
            var issued = await harness.Repository.GetByCertificateNumberAsync(number);
            string codeOnThePrintedPaper = issued!.VerifyCode!;

            await AttachSignedCopyAsync(harness, issued.Id);

            var toEdit = await harness.Repository.GetByCertificateNumberAsync(number);
            toEdit!.CalibrationResults.Single().MeasuredReading = "5.25";
            Assert.True(await harness.Repository.UpdateAsync(toEdit));

            var result = await harness.Repository.VerifyByCodeAsync(codeOnThePrintedPaper);

            Assert.Equal(CertificateVerificationStatus.AuthenticAmended, result.Status);
            Assert.NotNull(result.Certificate);
            Assert.Equal(number, result.Certificate!.CertificateNumber);
            Assert.NotNull(result.AmendedAt);

            // NotNull أعلاه يرضيه البديل ?? historical.ReplacedAt في
            // VerifyByCodeAsync. هذا التوكيد يفحص العمود ذاته، فلا يمرّ
            // الاختبار لسبب غير الذي كُتب له.
            var stored = await harness.Repository.GetByCertificateNumberAsync(number);
            Assert.NotNull(stored!.AmendedAt);
        }

        [Fact]
        public async Task CurrentCodeAfterAmendment_StillResolvesAsAuthentic()
        {
            using var harness = new Harness("currentcode");

            string number = await harness.Repository.AddAsync(NewCertificate(harness.CalibrationRecordId));
            var issued = await harness.Repository.GetByCertificateNumberAsync(number);
            await AttachSignedCopyAsync(harness, issued!.Id);

            issued.CalibrationResults.Single().MeasuredReading = "5.25";
            await harness.Repository.UpdateAsync(issued);

            var after = await harness.Repository.GetByCertificateNumberAsync(number);
            var result = await harness.Repository.VerifyByCodeAsync(after!.VerifyCode!);

            Assert.Equal(CertificateVerificationStatus.Authentic, result.Status);
        }

        [Fact]
        public async Task UnknownCode_IsNotFound_NeverMisreportedAsAmended()
        {
            using var harness = new Harness("unknown");

            await harness.Repository.AddAsync(NewCertificate(harness.CalibrationRecordId));

            var result = await harness.Repository.VerifyByCodeAsync("0123456789ABCDEF");

            Assert.Equal(CertificateVerificationStatus.NotFound, result.Status);
            Assert.Null(result.Certificate);
        }

        [Fact]
        public async Task TwoAmendments_ArchiveBothCodes_AndAmendedAtKeepsTheFirstDate()
        {
            using var harness = new Harness("twice");

            string number = await harness.Repository.AddAsync(NewCertificate(harness.CalibrationRecordId));
            var issued = await harness.Repository.GetByCertificateNumberAsync(number);
            string firstCode = issued!.VerifyCode!;
            await AttachSignedCopyAsync(harness, issued.Id);

            var edit1 = await harness.Repository.GetByCertificateNumberAsync(number);
            edit1!.CalibrationResults.Single().MeasuredReading = "5.25";
            await harness.Repository.UpdateAsync(edit1);

            var afterFirst = await harness.Repository.GetByCertificateNumberAsync(number);
            string secondCode = afterFirst!.VerifyCode!;
            DateTime firstAmendedAt = afterFirst.AmendedAt!.Value;

            var edit2 = await harness.Repository.GetByCertificateNumberAsync(number);
            edit2!.CalibrationResults.Single().MeasuredReading = "5.30";
            await harness.Repository.UpdateAsync(edit2);

            var afterSecond = await harness.Repository.GetByCertificateNumberAsync(number);

            // تاريخ **أول** تعديل، لا آخره
            Assert.Equal(firstAmendedAt, afterSecond!.AmendedAt);

            using var context = new CalQrDbContext(harness.Options);
            Assert.Equal(2, context.CertificateVerifyCodeHistory.Count());

            Assert.Equal(CertificateVerificationStatus.AuthenticAmended,
                (await harness.Repository.VerifyByCodeAsync(firstCode)).Status);
            Assert.Equal(CertificateVerificationStatus.AuthenticAmended,
                (await harness.Repository.VerifyByCodeAsync(secondCode)).Status);
        }

        [Fact]
        public async Task IssueDate_InTheFuture_IsRejectedAtTheRepository_NotOnlyInTheViewModel()
        {
            using var harness = new Harness("futuredate");

            var certificate = NewCertificate(harness.CalibrationRecordId);
            certificate.IssueDate = DateTime.Today.AddDays(1);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => harness.Repository.AddAsync(certificate));

            using var context = new CalQrDbContext(harness.Options);
            Assert.Empty(context.Certificates);
        }

        [Fact]
        public async Task InvalidatedCertificate_DoesNotReleaseItsNumber()
        {
            // القاعدة الحقيقية التي يحرسها هذا الاختبار: رقم **ثبت** لا يعود
            // للاستخدام أبداً، ولو أُبطلت شهادته.
            // الإبطال هنا يقع على السياق مباشرة لا عبر دالّة مستودع: فحص تصريح
            // الإبطال شأن طبقة التطبيق، والدالّة تُضاف عند وجود مستدعٍ حقيقي
            // (البند 5 في جدول التبعيات الحاجزة).
            using var harness = new Harness("noreuse");

            string first = await harness.Repository.AddAsync(NewCertificate(harness.CalibrationRecordId));

            using (var context = new CalQrDbContext(harness.Options))
            {
                var issued = context.Certificates.Single(c => c.CertificateNumber == first);
                issued.IsDeleted = true;
                context.SaveChanges();
            }

            // الفهرس الفريد المشروط على CalibrationRecordId يسمح بشهادة بديلة
            // بعد الإبطال، والفهرس غير المشروط على CertificateNumber يمنع
            // إعادة استعمال الرقم.
            string second = await harness.Repository.AddAsync(NewCertificate(harness.CalibrationRecordId));

            Assert.Equal("TNRC-SSDL-2026-0001", first);
            Assert.Equal("TNRC-SSDL-2026-0002", second);
        }

        [Fact]
        public async Task EditingAnIssuedCertificate_KeepsItsSignedCopyState()
        {
            // UpdateAsync ينسخ الكائن الوارد فوق المخزَّن عبر SetValues، ونموذج
            // الشهادة لا يعرف حقلَي النسخة الموقّعة. بلا إنقاذهما صراحةً كانت
            // كلّ عمليّة تعديل تُعيد شهادةً نسختها على القرص إلى «بانتظار».
            using var harness = new Harness("keepsigned");

            string number = await harness.Repository.AddAsync(NewCertificate(harness.CalibrationRecordId));
            var issued = await harness.Repository.GetByCertificateNumberAsync(number);
            await AttachSignedCopyAsync(harness, issued!.Id);

            var before = await harness.Repository.GetByCertificateNumberAsync(number);
            DateTime confirmedAt = before!.SignedCopyConfirmedAt!.Value;

            var toEdit = await harness.Repository.GetByCertificateNumberAsync(number);
            toEdit!.CalibrationResults.Single().MeasuredReading = "5.25";
            await harness.Repository.UpdateAsync(toEdit);

            var after = await harness.Repository.GetByCertificateNumberAsync(number);
            Assert.True(after!.IsSignedCopyAttached);
            Assert.Equal(confirmedAt, after.SignedCopyConfirmedAt);
        }

        [Fact]
        public async Task SyncSignedCopyState_RaisesTheFlag_WhenAnAttachmentExists()
        {
            using var harness = new Harness("syncraise");

            string number = await harness.Repository.AddAsync(NewCertificate(harness.CalibrationRecordId));
            var issued = await harness.Repository.GetByCertificateNumberAsync(number);
            await AttachSignedCopyAsync(harness, issued!.Id);

            var after = await harness.Repository.GetByCertificateNumberAsync(number);
            Assert.True(after!.IsSignedCopyAttached);
            Assert.NotNull(after.SignedCopyConfirmedAt);
        }

        [Fact]
        public async Task SyncSignedCopyState_KeepsTheFirstDate_WhenASecondFileIsAdded()
        {
            using var harness = new Harness("synckeepdate");

            string number = await harness.Repository.AddAsync(NewCertificate(harness.CalibrationRecordId));
            var issued = await harness.Repository.GetByCertificateNumberAsync(number);
            await AttachSignedCopyAsync(harness, issued!.Id, "signed-1.pdf");

            var afterFirst = await harness.Repository.GetByCertificateNumberAsync(number);
            DateTime firstConfirmedAt = afterFirst!.SignedCopyConfirmedAt!.Value;

            await AttachSignedCopyAsync(harness, issued.Id, "signed-2.pdf");

            var afterSecond = await harness.Repository.GetByCertificateNumberAsync(number);
            Assert.Equal(firstConfirmedAt, afterSecond!.SignedCopyConfirmedAt);
        }

        [Fact]
        public async Task SyncSignedCopyState_LowersTheFlag_WhenTheLastAttachmentIsRemoved()
        {
            using var harness = new Harness("synclower");

            string number = await harness.Repository.AddAsync(NewCertificate(harness.CalibrationRecordId));
            var issued = await harness.Repository.GetByCertificateNumberAsync(number);
            await AttachSignedCopyAsync(harness, issued!.Id);

            using (var context = new CalQrDbContext(harness.Options))
            {
                var attachment = context.Attachments.Single(a => a.CertificateId == issued.Id);
                context.Attachments.Remove(attachment);
                context.SaveChanges();
            }

            await harness.Repository.SyncSignedCopyStateAsync(issued.Id);

            var after = await harness.Repository.GetByCertificateNumberAsync(number);
            Assert.False(after!.IsSignedCopyAttached);
            Assert.Null(after.SignedCopyConfirmedAt);
        }
    }
}
