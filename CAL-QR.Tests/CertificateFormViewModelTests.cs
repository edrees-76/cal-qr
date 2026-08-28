using Xunit;
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Repositories;
using CAL_QR.Services;
using CAL_QR.ViewModels;

#pragma warning disable CS1998

namespace CAL_QR.Tests
{
    public class CertificateFormViewModelTests
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

        private async Task<(TestDbContextFactory factory, int recordId)> SeedRecordAsync(string result)
        {
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase(databaseName: "CalQrTestDb_CertFormVM_" + Guid.NewGuid().ToString())
                .Options;

            var factory = new TestDbContextFactory(options);

            CalibrationRecord record;
            using (var context = factory.CreateDbContext())
            {
                var owner = new Owner { Name = "Owner A", CreatedAt = DateTime.UtcNow };
                context.Owners.Add(owner);
                await context.SaveChangesAsync();

                var deviceType = new DeviceType
                {
                    Name = "Pancake Probe",
                    ComplianceVerdict = "APPROVED FOR OPERATIONAL USE",
                    CreatedAt = DateTime.UtcNow
                };
                context.DeviceTypes.Add(deviceType);
                await context.SaveChangesAsync();

                var device = new Device
                {
                    OwnerId = owner.Id,
                    DeviceTypeId = deviceType.Id,
                    Model = "Model A",
                    SerialNumber = "SN123",
                    CreatedAt = DateTime.UtcNow
                };
                context.Devices.Add(device);
                await context.SaveChangesAsync();

                record = new CalibrationRecord
                {
                    DeviceId = device.Id,
                    CertificateNumber = string.Empty,
                    CalibrationDate = DateTime.Today,
                    ExpiryDate = DateTime.Today.AddYears(1),
                    EngineerName = "Edrees",
                    Result = result,
                    HmacSignature = string.Empty,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                context.CalibrationRecords.Add(record);
                await context.SaveChangesAsync();
            }

            return (factory, record.Id);
        }

        [Fact]
        public async Task LoadForStatusReport_SeedsNotPerformedVerdict_NotTemplateApproval()
        {
            // Arrange
            var (factory, recordId) = await SeedRecordAsync("Failed");

            var typeRepo = new DeviceTypeRepository(factory);
            var draftBuilder = new CertificateDraftBuilder();
            var hmacService = new HmacService(factory);
            hmacService.Initialize();
            var signatureService = new CertificateSignatureService(hmacService);
            var certificateRepo = new CertificateRepository(
                factory,
                new CertificateNumberService(factory),
                signatureService);
            var authService = new TestCurrentUserService();
            var auditRepo = new AuditLogRepository(factory, authService);

            var vm = new CertificateFormViewModel(
                typeRepo,
                draftBuilder,
                certificateRepo,
                auditRepo,
                factory);

            // Act
            vm.LoadForStatusReport(recordId);

            // Assert
            Assert.False(string.IsNullOrWhiteSpace(vm.CertificateTemplateType));
            Assert.True(vm.IsStatusReport);
            Assert.Equal("NOT PERFORMED", vm.ComplianceVerdict);
            Assert.DoesNotContain("APPROVED", vm.ComplianceVerdict, StringComparison.OrdinalIgnoreCase);
        }

        // النظير العكسيّ للاختبار أعلاه. بدونه، أيّ "إصلاح" يُنقل إلى
        // CertificateDraftBuilder بدل الـViewModel كان سيُفرغ حكم كلّ شهادة
        // معايرة عاديّة وتبقى الاختبارات خضراء.
        [Fact]
        public async Task LoadForRecord_KeepsTemplateVerdict_ForNormalCertificate()
        {
            // Arrange
            var (factory, recordId) = await SeedRecordAsync("Passed");

            var typeRepo = new DeviceTypeRepository(factory);
            var draftBuilder = new CertificateDraftBuilder();
            var hmacService = new HmacService(factory);
            hmacService.Initialize();
            var signatureService = new CertificateSignatureService(hmacService);
            var certificateRepo = new CertificateRepository(
                factory,
                new CertificateNumberService(factory),
                signatureService);
            var authService = new TestCurrentUserService();
            var auditRepo = new AuditLogRepository(factory, authService);

            var vm = new CertificateFormViewModel(
                typeRepo,
                draftBuilder,
                certificateRepo,
                auditRepo,
                factory);

            // Act
            vm.LoadForRecord(recordId);

            // Assert
            Assert.False(string.IsNullOrWhiteSpace(vm.CertificateTemplateType));
            Assert.False(vm.IsStatusReport);
            Assert.Equal("APPROVED FOR OPERATIONAL USE", vm.ComplianceVerdict);
        }
    }
}
