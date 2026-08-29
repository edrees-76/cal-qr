using Xunit;
using System;
using System.Collections.Generic;
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

        private async Task<(TestDbContextFactory factory, int recordId)> SeedRecordAsync(
            string result, string defaultCheckResult = "Acceptable")
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

                context.DeviceTypeFunctionalCheckTemplates.AddRange(
                    new DeviceTypeFunctionalCheckTemplate { DeviceTypeId = deviceType.Id, SortOrder = 1, CheckName = "Background Check", Requirement = "within limits", DefaultResult = defaultCheckResult },
                    new DeviceTypeFunctionalCheckTemplate { DeviceTypeId = deviceType.Id, SortOrder = 2, CheckName = "Visual Inspection", Requirement = "no damage", DefaultResult = defaultCheckResult });
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

        private static CertificateFormViewModel BuildViewModel(TestDbContextFactory factory)
        {
            var hmacService = new HmacService(factory);
            hmacService.Initialize();

            return new CertificateFormViewModel(
                new DeviceTypeRepository(factory),
                new CertificateDraftBuilder(),
                new CertificateRepository(
                    factory,
                    new CertificateNumberService(factory),
                    new CertificateSignatureService(hmacService)),
                new AuditLogRepository(factory, new TestCurrentUserService()),
                factory);
        }

        [Fact]
        public async Task LoadForStatusReport_SeedsNotPerformedVerdict_NotTemplateApproval()
        {
            // Arrange
            var (factory, recordId) = await SeedRecordAsync("Failed");
            var vm = BuildViewModel(factory);

            // Act
            vm.LoadForStatusReport(recordId);

            // Assert
            Assert.False(string.IsNullOrWhiteSpace(vm.CertificateTemplateType));
            Assert.True(vm.IsStatusReport);
            Assert.Equal("NOT PERFORMED", vm.ComplianceVerdict);
            Assert.DoesNotContain("APPROVED", vm.ComplianceVerdict, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task LoadForStatusReport_DoesNotInheritTemplateCheckResults()
        {
            // Arrange
            var (factory, recordId) = await SeedRecordAsync("Failed");
            var vm = BuildViewModel(factory);

            // Act
            vm.LoadForStatusReport(recordId);

            // Assert
            Assert.Equal(2, vm.FunctionalChecks.Count);
            Assert.Contains(vm.FunctionalChecks, c => c.CheckName == "Background Check");
            Assert.Contains(vm.FunctionalChecks, c => c.CheckName == "Visual Inspection");
            Assert.All(vm.FunctionalChecks, c => Assert.True(string.IsNullOrWhiteSpace(c.Result)));
        }

        // النظير العكسيّ لاختبار الفحوص أعلاه. بدونه، أيّ إصلاح يُنقل إلى
        // CertificateDraftBuilder يُفرغ فحوص كلّ شهادة والاختبارات خضراء.
        [Fact]
        public async Task LoadForRecord_KeepsTemplateCheckResults_ForNormalCertificate()
        {
            // Arrange
            var (factory, recordId) = await SeedRecordAsync("Passed");
            var vm = BuildViewModel(factory);

            // Act
            vm.LoadForRecord(recordId);

            // Assert
            Assert.Equal(2, vm.FunctionalChecks.Count);
            Assert.All(vm.FunctionalChecks, c => Assert.Equal("Acceptable", c.Result));
        }

        // النظير العكسيّ للاختبار أعلاه. بدونه، أيّ "إصلاح" يُنقل إلى
        // CertificateDraftBuilder بدل الـViewModel كان سيُفرغ حكم كلّ شهادة
        // معايرة عاديّة وتبقى الاختبارات خضراء.
        [Fact]
        public async Task LoadForRecord_KeepsTemplateVerdict_ForNormalCertificate()
        {
            // Arrange
            var (factory, recordId) = await SeedRecordAsync("Passed");
            var vm = BuildViewModel(factory);

            // Act
            vm.LoadForRecord(recordId);

            // Assert
            Assert.False(string.IsNullOrWhiteSpace(vm.CertificateTemplateType));
            Assert.False(vm.IsStatusReport);
            Assert.Equal("APPROVED FOR OPERATIONAL USE", vm.ComplianceVerdict);
        }

        [Fact]
        public async Task StatusReport_WarnsWhenNoCheckFailed()
        {
            // Arrange
            var (factory, recordId) = await SeedRecordAsync("Failed");
            var vm = BuildViewModel(factory);
            vm.LoadForStatusReport(recordId);

            foreach (var check in vm.FunctionalChecks)
                check.Result = "Acceptable";

            // Act
            vm.RunTemplateConsistencyChecks();

            // Assert
            // التحذير المقصود هو تحذير «لا رسوب»، لا تحذير «بلا نتيجة»: الثاني
            // يُطلقه أيضًا فشلٌ في ضبط النتائج داخل الاختبار نفسه، فيمرّ الاختبار
            // لسبب غير الذي كُتب له.
            Assert.Contains(vm.TemplateWarnings, w => w.Message.Contains("لا يُظهر أيّ Failed"));
        }

        // العنوان ونصّ الزرّ هما أوّل ما يقرؤه المعايِر، وكانا يقولان "شهادة" على
        // تقرير حالة. الحالة الرابعة (تعديل تقرير حالة) تُغطّى بضبط DocumentType
        // مباشرةً مع _isEditMode عبر LoadForEdit في اختبار مستقلّ مستقبلاً — هنا
        // نحرس الحالات الثلاث التي يبلغها المستخدم من شاشة سجلّ المعايرة.
        [Fact]
        public async Task FormTitleAndSaveButtonText_FollowDocumentType()
        {
            var (factory, recordId) = await SeedRecordAsync("Failed");

            var certificateVm = BuildViewModel(factory);
            certificateVm.LoadForRecord(recordId);

            Assert.False(certificateVm.IsStatusReport);
            Assert.Equal("إصدار شهادة جديدة", certificateVm.FormTitle);
            Assert.Equal("حفظ وإصدار الشهادة", certificateVm.SaveButtonText);

            var reportVm = BuildViewModel(factory);
            reportVm.LoadForStatusReport(recordId);

            Assert.True(reportVm.IsStatusReport);
            Assert.Equal("إصدار تقرير حالة جديد", reportVm.FormTitle);
            Assert.Equal("حفظ وإصدار تقرير الحالة", reportVm.SaveButtonText);
        }

        // بلا هذا الحارس، نقل الإشعارين خارج setter الـDocumentType كان سيُبقي
        // العنوان والزرّ على قيمتهما القديمة في الواجهة رغم صحّة الخاصيّتين.
        [Fact]
        public async Task DocumentTypeChange_RaisesFormTitleAndSaveButtonTextNotifications()
        {
            var (factory, recordId) = await SeedRecordAsync("Failed");
            var vm = BuildViewModel(factory);
            vm.LoadForRecord(recordId);

            var raised = new List<string>();
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != null) raised.Add(e.PropertyName);
            };

            vm.DocumentType = CAL_QR.Enums.CertificateDocumentType.CalibrationStatusReport;

            Assert.Contains(nameof(CertificateFormViewModel.FormTitle), raised);
            Assert.Contains(nameof(CertificateFormViewModel.SaveButtonText), raised);
            Assert.Contains(nameof(CertificateFormViewModel.IsStatusReport), raised);
        }

        // ملاحظة على النطاق: هذان يحرسان منطق ResultOptions في الـViewModel.
        // ربطُ العمود نفسه في XAML خارج ما يبلغه xUnit ولا اختبار آليّ يحرسه.
        [Fact]
        public async Task ResultOptions_KeepAValueThatIsNotInTheApprovedList()
        {
            // "Yes" كانت مزروعة في قوالب Pancake و Beta وصدرت بها شهادات
            // حقيقيّة، والتجميد التاريخيّ يمنع تصحيحها. وComboBox مغلق قيمته
            // خارج قائمته يعرض فراغًا ويكتب null عند الحفظ — فتُمحى نتائج
            // وثيقة صادرة بمجرّد فتحها. القائمة تحرس الجديد ولا تعدم القديم.
            var (factory, recordId) = await SeedRecordAsync("Passed", "Yes");
            var vm = BuildViewModel(factory);

            vm.LoadForRecord(recordId);

            Assert.All(vm.FunctionalChecks, c => Assert.Equal("Yes", c.Result));
            Assert.Contains("Yes", vm.ResultOptions);
            Assert.All(vm.FunctionalChecks, c => Assert.Contains(c.Result, vm.ResultOptions));
        }

        [Fact]
        public async Task ResultOptions_HoldOnlyTheApprovedWordsWhenNothingIsOutOfList()
        {
            // النظير العكسيّ: بلا هذا، حارسٌ يضيف كلّ قيمة بلا شرط كان يمرّ
            // ويُنتج قائمة تكبر بالتكرار عند كلّ فتح.
            var (factory, recordId) = await SeedRecordAsync("Passed");
            var vm = BuildViewModel(factory);

            vm.LoadForRecord(recordId);

            Assert.Equal(4, vm.ResultOptions.Count);
            Assert.Null(vm.ResultOptions[0]);
            Assert.Contains("Acceptable", vm.ResultOptions);
            Assert.Contains("Failed", vm.ResultOptions);
            Assert.Contains("Not Performed", vm.ResultOptions);
            Assert.DoesNotContain("Yes", vm.ResultOptions);
        }
    }
}
