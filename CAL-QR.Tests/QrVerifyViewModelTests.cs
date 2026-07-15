using Xunit;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Services;
using CAL_QR.ViewModels;

namespace CAL_QR.Tests
{
    public class QrVerifyViewModelTests
    {
        [Fact]
        public async Task VerifyPastedText_LegacyBilingualFormat_ParsesFieldsCorrectlyAndVerifiesSignature()
        {
            // 1. Arrange
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase(databaseName: "CalQrTestDb_Verify_Bilingual_" + Guid.NewGuid().ToString())
                .Options;

            var factory = new TestDbContextFactory(options);
            var hmacService = new HmacService(factory);
            hmacService.Initialize();

            string ownerName = "Nuclear Research Center";
            string deviceTypeName = "Geiger Counter";
            string model = "Model-99";
            string serial = "SN-12345";
            string certNo = "CERT-999-OLD";
            string calDate = "2026-06-30";
            string expDate = "2027-06-30";
            string engineer = "Edrees";
            string description = "Annual Calibration";
            string result = "Passed";

            // Compute legacy 8-char signature (manually using legacy key for Arrange phase)
            string rawData = $"{certNo}|{model}|{serial}|{ownerName}|{calDate}|{expDate}|{result}|{engineer}";
            using var legacyHmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes("CalQR-Nuclear-Center-2026-SecretKey"));
            byte[] hashBytes = legacyHmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(rawData));
            string computedLegacy = Convert.ToHexString(hashBytes).Substring(0, 16).ToUpper();
            string verifyCode = computedLegacy.Substring(0, 8); // legacy is 8 chars

            using (var context = new CalQrDbContext(options))
            {
                var owner = new Owner { Name = ownerName };
                context.Owners.Add(owner);

                var deviceType = new DeviceType { Name = deviceTypeName };
                context.DeviceTypes.Add(deviceType);
                await context.SaveChangesAsync();

                var device = new Device
                {
                    Model = model,
                    SerialNumber = serial,
                    OwnerId = owner.Id,
                    DeviceTypeId = deviceType.Id,
                    IsDeleted = false
                };
                context.Devices.Add(device);
                await context.SaveChangesAsync();

                var record = new CalibrationRecord
                {
                    DeviceId = device.Id,
                    CertificateNumber = certNo,
                    CalibrationDate = DateTime.Parse(calDate),
                    ExpiryDate = DateTime.Parse(expDate),
                    Result = "Passed",
                    EngineerName = engineer,
                    HmacSignature = verifyCode,
                    CreatedAt = new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc),
                    IsDeleted = false
                };
                context.CalibrationRecords.Add(record);
                await context.SaveChangesAsync();
            }

            string qrText = "=== شهادة معايرة | Calibration Certificate ===\n" +
                            $"الجهة / Owner: {ownerName}\n" +
                            $"النوع / Type: {deviceTypeName}\n" +
                            $"الموديل / Model: {model}\n" +
                            $"الرقم التسلسلي / S/N: {serial}\n" +
                            $"رقم الشهادة / Cert No: {certNo}\n" +
                            $"تاريخ المعايرة / Cal. Date: {calDate}\n" +
                            $"تاريخ الانتهاء / Exp. Date: {expDate}\n" +
                            $"المهندس / Engineer: {engineer}\n" +
                            $"نوع المعايرة / Cal. Type: {description}\n" +
                            "النتيجة / Result: ✅ ناجح Passed\n" +
                            $"كود التحقق / Verify Code: {verifyCode}\n" +
                            "─────────────────────────────────\n" +
                            "الجهة المعايِرة / Calibrated by:\n" +
                            "مركز البحوث النووية | Nuclear Research Center\n" +
                            "وحدة المعايرة | Calibration Unit";

            var viewModel = new QrVerifyViewModel(hmacService, factory);
            viewModel.ConcatenatedText = qrText;

            // 2. Act
            await viewModel.VerifyPastedTextAsync();

            // 3. Assert
            Assert.Equal(ownerName, viewModel.Owner);
            Assert.Equal(deviceTypeName, viewModel.DeviceType);
            Assert.Equal(model, viewModel.Model);
            Assert.Equal(serial, viewModel.Serial);
            Assert.Equal(certNo, viewModel.CertNo);
            Assert.Equal(calDate, viewModel.CalDate);
            Assert.Equal(expDate, viewModel.ExpDate);
            Assert.Equal(engineer, viewModel.Engineer);
            Assert.Equal(description, viewModel.CalType);
            Assert.Equal("Passed", viewModel.Result);
            Assert.Equal(verifyCode, viewModel.ReadVerifyCode);
            Assert.True(viewModel.IsSuccess);
        }

        [Fact]
        public async Task VerifyPastedText_SimplifiedArabicFormat_ParsesFieldsCorrectlyAndVerifiesSignature()
        {
            // 1. Arrange
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase(databaseName: "CalQrTestDb_Verify_Simplified_" + Guid.NewGuid().ToString())
                .Options;

            var factory = new TestDbContextFactory(options);
            var hmacService = new HmacService(factory);
            hmacService.Initialize();

            string ownerName = "المركز الوطني";
            string deviceTypeName = "كاشف إشعاعي";
            string model = "Model-ABC";
            string serial = "SN-8888";
            string certNo = "CERT-2026-NEW";
            string calDate = "2026-07-15";
            string expDate = "2027-07-15";
            string engineer = "علي";
            string result = "Passed";

            string verifyCode = hmacService.ComputeSignature(
                certNo: certNo,
                model: model,
                serial: serial,
                ownerName: ownerName,
                calDate: calDate,
                expDate: expDate,
                result: result,
                engineerName: engineer
            );

            using (var context = new CalQrDbContext(options))
            {
                var owner = new Owner { Name = ownerName };
                context.Owners.Add(owner);

                var deviceType = new DeviceType { Name = deviceTypeName };
                context.DeviceTypes.Add(deviceType);
                await context.SaveChangesAsync();

                var device = new Device
                {
                    Model = model,
                    SerialNumber = serial,
                    OwnerId = owner.Id,
                    DeviceTypeId = deviceType.Id,
                    IsDeleted = false
                };
                context.Devices.Add(device);
                await context.SaveChangesAsync();

                var record = new CalibrationRecord
                {
                    DeviceId = device.Id,
                    CertificateNumber = certNo,
                    CalibrationDate = DateTime.Parse(calDate),
                    ExpiryDate = DateTime.Parse(expDate),
                    Result = "Passed",
                    EngineerName = engineer,
                    HmacSignature = verifyCode,
                    CreatedAt = new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc),
                    IsDeleted = false
                };
                context.CalibrationRecords.Add(record);
                await context.SaveChangesAsync();
            }

            string qrText = "=== شهادة معايرة ===\n" +
                            $"الجهة: {ownerName}\n" +
                            $"النوع / Type: {deviceTypeName}\n" +
                            $"الموديل / Model: {model}\n" +
                            $"الرقم التسلسلي / S/N: {serial}\n" +
                            $"رقم الشهادة: {certNo}\n" +
                            $"تاريخ المعايرة: {calDate}\n" +
                            $"تاريخ انتهاء المعايرة / Exp. Date: {expDate}\n" +
                            $"المهندس: {engineer}\n" +
                            "النتيجة / Result: ✅ ناجح Passed\n" +
                            $"كود التحقق / Verify Code: {verifyCode}\n" +
                            "─────────────────────────────────\n" +
                            "الجهة المعايِرة / Calibrated by:\n" +
                            "مركز البحوث النووية\n" +
                            "إدارة الوقاية من الاشعاع\n" +
                            "قسم قياس وتقدير الجرعات الشخصية والمعايرة\n" +
                            "وحدة المعايرة";

            var viewModel = new QrVerifyViewModel(hmacService, factory);
            viewModel.ConcatenatedText = qrText;

            // 2. Act
            await viewModel.VerifyPastedTextAsync();

            // 3. Assert
            Assert.Equal(ownerName, viewModel.Owner);
            Assert.Equal(deviceTypeName, viewModel.DeviceType);
            Assert.Equal(model, viewModel.Model);
            Assert.Equal(serial, viewModel.Serial);
            Assert.Equal(certNo, viewModel.CertNo);
            Assert.Equal(calDate, viewModel.CalDate);
            Assert.Equal(expDate, viewModel.ExpDate);
            Assert.Equal(engineer, viewModel.Engineer);
            Assert.Equal("Passed", viewModel.Result);
            Assert.Equal(verifyCode, viewModel.ReadVerifyCode);
            Assert.True(viewModel.IsSuccess);
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
