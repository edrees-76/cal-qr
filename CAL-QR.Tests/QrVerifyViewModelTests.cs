using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Repositories;
using CAL_QR.Services;
using CAL_QR.ViewModels;

namespace CAL_QR.Tests
{
    /// <summary>
    /// يحرس مسار التحقق الحالي: QrVerifyViewModel → ICertificateRepository.VerifyByCodeAsync.
    /// SQLite حقيقي عبر DatabaseMigrator، لأن CertificateNumberService.AllocateAsync
    /// ينفّذ SQL خام (INSERT ... ON CONFLICT ... RETURNING) لا يدعمه مزوّد InMemory.
    /// </summary>
    public class QrVerifyViewModelTests
    {
        private class TestDbContextFactory : IDbContextFactory<CalQrDbContext>
        {
            private readonly DbContextOptions<CalQrDbContext> _options;
            public TestDbContextFactory(DbContextOptions<CalQrDbContext> options) => _options = options;
            public CalQrDbContext CreateDbContext() => new CalQrDbContext(_options);
        }

        private static string NewDbPath(string tag) =>
            Path.Combine(Path.GetTempPath(), $"cal_qr_verify_vm_{tag}_{Guid.NewGuid():N}.db");

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
            public CertificateSignatureService SignatureService { get; }
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
                SignatureService = new CertificateSignatureService(hmac);

                Repository = new CertificateRepository(
                    Factory,
                    new CertificateNumberService(Factory),
                    SignatureService);
            }

            public void Dispose() => CleanUp(DbPath);
        }

        private static int SeedCalibrationRecord(CalQrDbContext context, string tag)
        {
            var owner = new Owner { Name = "مركز البحوث النووية" };
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
            ClientName = "مركز البحوث النووية",
            DeviceModel = "Ludlum 44-9",
            DeviceSerialNumber = "PR-777",
            CalibrationDate = new DateTime(2026, 1, 15),
            IssueDate = new DateTime(2026, 1, 20)
        };

        [Fact]
        public async Task QuickVerify_AuthenticCertificate_ReturnsAuthenticAndDisplaysItsData()
        {
            using var harness = new Harness("quick");

            string number = await harness.Repository.AddAsync(NewCertificate(harness.CalibrationRecordId));
            var issued = await harness.Repository.GetByCertificateNumberAsync(number);

            var viewModel = new QrVerifyViewModel(harness.Repository);
            viewModel.QuickVerifyCode = issued!.VerifyCode!;

            await viewModel.QuickVerifyAsync();

            Assert.True(viewModel.IsValidated);
            Assert.True(viewModel.IsAuthentic);
            Assert.True(viewModel.IsSuccess);
            Assert.False(viewModel.IsAmended);
            Assert.False(viewModel.IsNotFound);
            Assert.False(viewModel.IsUnverifiable);

            Assert.Equal(issued.ClientName, viewModel.Owner);
            Assert.Equal(issued.CertificateNumber, viewModel.CertNo);
            Assert.Equal(issued.DeviceModel, viewModel.Model);
            Assert.Equal(issued.DeviceSerialNumber, viewModel.Serial);
        }

        [Fact]
        public async Task VerifyPastedText_ExtractsVLineFromQrPayload_ReturnsAuthentic()
        {
            using var harness = new Harness("paste");

            string number = await harness.Repository.AddAsync(NewCertificate(harness.CalibrationRecordId));
            var issued = await harness.Repository.GetByCertificateNumberAsync(number);
            Assert.NotNull(issued);

            string qrText = harness.SignatureService.BuildQrPayload(issued);

            var viewModel = new QrVerifyViewModel(harness.Repository);
            viewModel.ConcatenatedText = qrText;

            await viewModel.VerifyPastedTextAsync();

            Assert.True(viewModel.IsValidated);
            Assert.True(viewModel.IsAuthentic);
            Assert.Equal(issued.CertificateNumber, viewModel.CertNo);
        }

        [Fact]
        public async Task QuickVerify_UnknownCode_ReturnsNotFoundNotAuthentic()
        {
            using var harness = new Harness("unknown");

            await harness.Repository.AddAsync(NewCertificate(harness.CalibrationRecordId));

            var viewModel = new QrVerifyViewModel(harness.Repository);
            viewModel.QuickVerifyCode = "ZZZZZZZZ";

            await viewModel.QuickVerifyAsync();

            Assert.True(viewModel.IsValidated);
            Assert.True(viewModel.IsNotFound);
            Assert.False(viewModel.IsSuccess);
            Assert.False(viewModel.IsAuthentic);
        }

        [Fact]
        public async Task VerifyPastedText_NoVLine_ReportsUnverifiableNotSuccess()
        {
            using var harness = new Harness("noVline");

            var viewModel = new QrVerifyViewModel(harness.Repository);
            viewModel.ConcatenatedText = "L:TNRC-SSDL\nC:مركز البحوث النووية\nSN:PR-777";

            await viewModel.VerifyPastedTextAsync();

            Assert.True(viewModel.IsValidated);
            Assert.True(viewModel.IsUnverifiable);
            Assert.False(viewModel.IsSuccess);
        }
    }
}
