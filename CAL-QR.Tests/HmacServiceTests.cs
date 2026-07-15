using Xunit;
using System;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Services;

namespace CAL_QR.Tests
{
    public class HmacServiceTests
    {
        private readonly IHmacService _hmacService;

        public HmacServiceTests()
        {
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase(databaseName: "CalQrTestDb_HmacServiceTests_" + Guid.NewGuid().ToString())
                .Options;
            var factory = new TestDbContextFactory(options);
            _hmacService = new HmacService(factory);
            _hmacService.Initialize();
        }

        [Fact]
        public void HmacService_SameData_ProducesSameSignature()
        {
            string signature1 = _hmacService.ComputeSignature(
                certNo: "CERT-100",
                model: "Geiger-A",
                serial: "SN-9999",
                ownerName: "T.N.R.C",
                calDate: "2026-06-30",
                expDate: "2027-06-30",
                result: "Passed",
                engineerName: "Edrees"
            );

            string signature2 = _hmacService.ComputeSignature(
                certNo: "CERT-100",
                model: "Geiger-A",
                serial: "SN-9999",
                ownerName: "T.N.R.C",
                calDate: "2026-06-30",
                expDate: "2027-06-30",
                result: "Passed",
                engineerName: "Edrees"
            );

            Assert.Equal(signature1, signature2);
        }

        [Fact]
        public void HmacService_DifferentData_ProducesDifferentSignature()
        {
            string signature1 = _hmacService.ComputeSignature(
                certNo: "CERT-100",
                model: "Geiger-A",
                serial: "SN-9999",
                ownerName: "T.N.R.C",
                calDate: "2026-06-30",
                expDate: "2027-06-30",
                result: "Passed",
                engineerName: "Edrees"
            );

            string signature2 = _hmacService.ComputeSignature(
                certNo: "CERT-101",
                model: "Geiger-A",
                serial: "SN-9999",
                ownerName: "T.N.R.C",
                calDate: "2026-06-30",
                expDate: "2027-06-30",
                result: "Passed",
                engineerName: "Edrees"
            );

            Assert.NotEqual(signature1, signature2);
        }

        [Fact]
        public void HmacService_ModifiedData_VerificationFails()
        {
            string certNo = "CERT-100";
            string model = "Geiger-A";
            string serial = "SN-9999";
            string ownerName = "T.N.R.C";
            string calDate = "2026-06-30";
            string expDate = "2027-06-30";
            string result = "Passed";
            string engineerName = "Edrees";

            string signature = _hmacService.ComputeSignature(
                certNo: certNo,
                model: model,
                serial: serial,
                ownerName: ownerName,
                calDate: calDate,
                expDate: expDate,
                result: result,
                engineerName: engineerName
            );

            bool verified = _hmacService.VerifySignature(
                certNo: certNo,
                model: model,
                serial: serial,
                ownerName: ownerName,
                calDate: calDate,
                expDate: expDate,
                result: result,
                engineerName: engineerName,
                signature: signature,
                recordCreatedAt: DateTime.UtcNow
            );

            Assert.True(verified);

            // Modify serial
            bool verifiedModified = _hmacService.VerifySignature(
                certNo: certNo,
                model: model,
                serial: "SN-9999-MODIFIED",
                ownerName: ownerName,
                calDate: calDate,
                expDate: expDate,
                result: result,
                engineerName: engineerName,
                signature: signature,
                recordCreatedAt: DateTime.UtcNow
            );

            Assert.False(verifiedModified);
        }

        [Fact]
        public void VerifySignature_LegacyRecordWithLegacyKey_Succeeds()
        {
            // Record created before CutoverDate (July 14, 2026) using legacy key and legacy 8-char signature
            string certNo = "CERT-LEGACY";
            string model = "Geiger-A";
            string serial = "SN-9999";
            string ownerName = "T.N.R.C";
            string calDate = "2026-07-10";
            string expDate = "2027-07-10";
            string result = "Passed";
            string engineerName = "Edrees";

            // Compute legacy 16-character signature using the LegacySecretKey: "CalQR-Nuclear-Center-2026-SecretKey"
            string rawData = $"{certNo}|{model}|{serial}|{ownerName}|{calDate}|{expDate}|{result}|{engineerName}";
            using var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes("CalQR-Nuclear-Center-2026-SecretKey"));
            byte[] hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawData));
            string computedLegacy = Convert.ToHexString(hashBytes).Substring(0, 16).ToUpper();
            string signature8 = computedLegacy.Substring(0, 8);

            // Verify using HmacService (with pre-cutover record date)
            bool verified = _hmacService.VerifySignature(
                certNo: certNo,
                model: model,
                serial: serial,
                ownerName: ownerName,
                calDate: calDate,
                expDate: expDate,
                result: result,
                engineerName: engineerName,
                signature: signature8,
                recordCreatedAt: new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc)
            );

            Assert.True(verified);
        }

        [Fact]
        public void VerifySignature_NewRecordWithNewKey_Succeeds()
        {
            // Record created on/after CutoverDate (July 14, 2026) using new active key
            string certNo = "CERT-NEW";
            string model = "Geiger-A";
            string serial = "SN-9999";
            string ownerName = "T.N.R.C";
            string calDate = "2026-07-15";
            string expDate = "2027-07-15";
            string result = "Passed";
            string engineerName = "Edrees";

            string signature16 = _hmacService.ComputeSignature(certNo, model, serial, ownerName, calDate, expDate, result, engineerName);

            bool verified = _hmacService.VerifySignature(
                certNo: certNo,
                model: model,
                serial: serial,
                ownerName: ownerName,
                calDate: calDate,
                expDate: expDate,
                result: result,
                engineerName: engineerName,
                signature: signature16,
                recordCreatedAt: new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc)
            );

            Assert.True(verified);
        }

        [Fact]
        public void VerifySignature_NewRecordWithLegacyKey_Fails()
        {
            // Record created on/after CutoverDate (July 14, 2026) but signed with legacy key
            string certNo = "CERT-NEW-LEAKED";
            string model = "Geiger-A";
            string serial = "SN-9999";
            string ownerName = "T.N.R.C";
            string calDate = "2026-07-15";
            string expDate = "2027-07-15";
            string result = "Passed";
            string engineerName = "Edrees";

            // Compute legacy 16-character signature using the LegacySecretKey
            string rawData = $"{certNo}|{model}|{serial}|{ownerName}|{calDate}|{expDate}|{result}|{engineerName}";
            using var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes("CalQR-Nuclear-Center-2026-SecretKey"));
            byte[] hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawData));
            string computedLegacy = Convert.ToHexString(hashBytes).Substring(0, 16).ToUpper();

            // Verify using HmacService (with post-cutover record date) -> must fail!
            bool verified = _hmacService.VerifySignature(
                certNo: certNo,
                model: model,
                serial: serial,
                ownerName: ownerName,
                calDate: calDate,
                expDate: expDate,
                result: result,
                engineerName: engineerName,
                signature: computedLegacy,
                recordCreatedAt: new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc)
            );

            Assert.False(verified);
        }

        [Fact]
        public void HmacService_DIResolution_IsSingletonAndSharesState()
        {
            // Arrange
            var services = new ServiceCollection();
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase(databaseName: "CalQrTestDb_DI_" + Guid.NewGuid().ToString())
                .Options;
            
            services.AddSingleton<IDbContextFactory<CalQrDbContext>>(new TestDbContextFactory(options));
            services.AddSingleton<IHmacService, HmacService>();
            
            var provider = services.BuildServiceProvider();

            // Act
            var hmacService1 = provider.GetRequiredService<IHmacService>();
            var hmacService2 = provider.GetRequiredService<IHmacService>();

            hmacService1.Initialize();

            // Assert
            Assert.Same(hmacService1, hmacService2); // Verifies Singleton lifetime in DI
            
            // Both compute the same signature using the generated key
            string sig1 = hmacService1.ComputeSignature("123", "M", "S", "O", "2026-07-15", "2027-07-15", "Passed", "E");
            string sig2 = hmacService2.ComputeSignature("123", "M", "S", "O", "2026-07-15", "2027-07-15", "Passed", "E");
            Assert.Equal(sig1, sig2);
        }

        [Fact]
        public void VerifySignature_NullRecordCreatedAt_LegacyCalDate_With8CharSignature_Succeeds()
        {
            string certNo = "CERT-OFFLINE-OLD";
            string model = "Geiger-A";
            string serial = "SN-9999";
            string ownerName = "T.N.R.C";
            // CalibrationDate is before Cutover Date
            string calDate = "2026-07-13";
            string expDate = "2027-07-13";
            string result = "Passed";
            string engineerName = "Edrees";

            // Force legacy signature manually computed
            string rawData = $"{certNo}|{model}|{serial}|{ownerName}|{calDate}|{expDate}|{result}|{engineerName}";
            using var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes("CalQR-Nuclear-Center-2026-SecretKey"));
            byte[] hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawData));
            string computedLegacy = Convert.ToHexString(hashBytes).Substring(0, 16).ToUpper();
            string signature8 = computedLegacy.Substring(0, 8);

            bool verified = _hmacService.VerifySignature(
                certNo: certNo,
                model: model,
                serial: serial,
                ownerName: ownerName,
                calDate: calDate,
                expDate: expDate,
                result: result,
                engineerName: engineerName,
                signature: signature8,
                recordCreatedAt: null // Offline validation fallback to calDate
            );

            Assert.True(verified);
        }

        [Fact]
        public void VerifySignature_NullRecordCreatedAt_NewCalDate_With8CharSignature_Fails()
        {
            string certNo = "CERT-OFFLINE-NEW";
            string model = "Geiger-A";
            string serial = "SN-9999";
            string ownerName = "T.N.R.C";
            // CalibrationDate is on or after Cutover Date
            string calDate = "2026-07-14";
            string expDate = "2027-07-14";
            string result = "Passed";
            string engineerName = "Edrees";

            string signature16 = _hmacService.ComputeSignature(certNo, model, serial, ownerName, calDate, expDate, result, engineerName);
            string signature8 = signature16.Substring(0, 8);

            bool verified = _hmacService.VerifySignature(
                certNo: certNo,
                model: model,
                serial: serial,
                ownerName: ownerName,
                calDate: calDate,
                expDate: expDate,
                result: result,
                engineerName: engineerName,
                signature: signature8,
                recordCreatedAt: null // Offline validation fallback to calDate
            );

            Assert.False(verified); // Must fail since signature is only 8 chars for new cal date
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
