using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
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
    public class CalibrationFormViewModelTests
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
                .UseInMemoryDatabase(databaseName: "CalQrTestDb_FormVM_" + Guid.NewGuid().ToString())
                .Options;

            var factory = new TestDbContextFactory(options);

            CalibrationRecord record;
            using (var context = factory.CreateDbContext())
            {
                var owner = new Owner { Name = "Owner A", CreatedAt = DateTime.UtcNow };
                context.Owners.Add(owner);
                await context.SaveChangesAsync();

                var deviceType = new DeviceType { Name = "Pancake Probe", CreatedAt = DateTime.UtcNow };
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

        private static CalibrationFormViewModel BuildViewModel(TestDbContextFactory factory)
        {
            var ownerRepo = new OwnerRepository(factory);
            var typeRepo = new DeviceTypeRepository(factory);
            var deviceRepo = new DeviceRepository(factory);
            var calRepo = new CalibrationRepository(factory);
            var attachmentRepo = new AttachmentRepository(factory);
            var authService = new TestCurrentUserService();
            var auditRepo = new AuditLogRepository(factory, authService);

            // لا HmacService ولا QrService: نُزعا من مُنشئ الـViewModel بعد إيقاف
            // التوقيع ورمز الـQR على مستوى سجل المعايرة — صارا مسؤولية الشهادة.
            return new CalibrationFormViewModel(
                factory,
                ownerRepo,
                typeRepo,
                deviceRepo,
                calRepo,
                attachmentRepo,
                auditRepo,
                // مصنع حوار الشهادة: لا يُستدعى في هذا الاختبار (لا مسار إصدار هنا)،
                // وإنشاء نافذة WPF في خيط اختبار غير STA كان سيفشل أصلاً.
                () => throw new NotSupportedException("لا يُنشأ حوار الشهادة في هذا الاختبار.")
            );
        }

        [Fact]
        public async Task SaveCommand_CanExecute_ReturnsFalse_WhenSavingInProgress()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase(databaseName: "CalQrTestDb_FormVM_SaveLock_" + Guid.NewGuid().ToString())
                .Options;

            var factory = new TestDbContextFactory(options);
            var vm = BuildViewModel(factory);

            // Populate required fields to make CanSave return true normally
            vm.OwnerText = "Owner A";
            vm.DeviceTypeText = "Type A";
            vm.Model = "Model A";
            vm.SerialNumber = "SN123";
            vm.CertificateNumber = "CERT-XYZ";
            vm.EngineerName = "Edrees";

            // Verify it can save initially
            Assert.True(vm.SaveCommand.CanExecute(null));

            // Set _isSaving field using reflection since it is private
            var fieldInfo = typeof(CalibrationFormViewModel).GetField("_isSaving", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(fieldInfo);
            fieldInfo.SetValue(vm, true);

            // Verify SaveCommand.CanExecute returns false when _isSaving is true (logical lock preventing double clicks)
            Assert.False(vm.SaveCommand.CanExecute(null));
        }

        // TEST-02 دفعة 2: القوائم تُحمَّل بمهمّة تهيئة لا تُنتظر في الـconstructor،
        // فيجب أن تنتظرها دوالّ التحميل قبل البحث في القوائم، وإلّا بقي المالك والنوع فارغين.
        [Fact]
        public async Task LoadForDeviceOnly_AwaitsInitialization_AndSelectsOwnerAndType()
        {
            var (factory, _) = await SeedRecordAsync("Passed");
            int deviceId;
            using (var context = factory.CreateDbContext())
            {
                deviceId = context.Devices.Select(d => d.Id).Single();
            }
            var vm = BuildViewModel(factory);

            await vm.LoadForDeviceOnly(deviceId);
            await vm.Initialization;

            Assert.NotNull(vm.SelectedOwner);
            Assert.Equal("Owner A", vm.SelectedOwner!.Name);
            Assert.NotNull(vm.SelectedDeviceType);
            Assert.Equal("Pancake Probe", vm.SelectedDeviceType!.Name);
            Assert.Equal("SN123", vm.SerialNumber);
            Assert.False(vm.IsEditMode);
        }

        [Fact]
        public async Task PersistCertificateNumberAsync_SavesNumberToDatabase()
        {
            // Arrange
            var (factory, recordId) = await SeedRecordAsync("Passed");
            var vm = BuildViewModel(factory);

            // Act
            await vm.PersistCertificateNumberAsync(recordId, "TNRC-SSDL-2026-TEST");

            // Assert
            using (var context = factory.CreateDbContext())
            {
                var reloaded = await context.CalibrationRecords.FindAsync(recordId);
                Assert.NotNull(reloaded);
                Assert.Equal("TNRC-SSDL-2026-TEST", reloaded!.CertificateNumber);
            }
        }

        // الشرطان كانا متطابقين فيُعرض الزرّان معًا مهما كانت النتيجة. الحالات
        // الثلاث تُفحص هنا، ومنها Conditional التي لم تُخزَّن في أيّ قاعدة بعد
        // ولا يغطّيها اختبار يدويّ.
        [Fact]
        public async Task IssueButtons_FollowCalibrationResult()
        {
            var (failedFactory, failedId) = await SeedRecordAsync("Failed");
            var failedVm = BuildViewModel(failedFactory);
            await failedVm.LoadForEdit(failedId);

            Assert.True(failedVm.IsEditMode);
            Assert.False(failedVm.CanIssueCertificate);
            Assert.True(failedVm.CanIssueStatusReport);

            var (passedFactory, passedId) = await SeedRecordAsync("Passed");
            var passedVm = BuildViewModel(passedFactory);
            await passedVm.LoadForEdit(passedId);

            Assert.True(passedVm.CanIssueCertificate);
            Assert.False(passedVm.CanIssueStatusReport);

            var (condFactory, condId) = await SeedRecordAsync("Conditional");
            var condVm = BuildViewModel(condFactory);
            await condVm.LoadForEdit(condId);

            Assert.True(condVm.CanIssueCertificate);
            Assert.False(condVm.CanIssueStatusReport);
        }

        // جوهر الإصلاح الأوّل كان الإشعار: SetProperty وحدها كانت تترك الزرّين
        // جامدين حتى إعادة فتح النافذة. والإصلاح الثاني (مراجعة 18 نوفمبر 2026):
        // نموذج الشهادة يقرأ السجلّ المحفوظ، فنتيجة غُيّرت ولم تُحفظ كانت تُتيح
        // إصدار تقرير حالة لسجلّ محفوظ «ناجح» (أو شهادة لسجلّ «راسب»). الآن
        // يتوقّف الزرّان ويظهر تلميح «احفظ أوّلاً»، والإشعارات ما زالت تُطلق.
        [Fact]
        public async Task ChangingResult_WithoutSaving_DisablesBothButtonsAndRaisesNotifications()
        {
            var (factory, recordId) = await SeedRecordAsync("Passed");
            var vm = BuildViewModel(factory);
            await vm.LoadForEdit(recordId);

            Assert.True(vm.CanIssueCertificate);

            var raised = new List<string>();
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != null) raised.Add(e.PropertyName);
            };

            vm.SelectedResult = "Failed";

            Assert.False(vm.CanIssueCertificate);
            Assert.False(vm.CanIssueStatusReport);
            Assert.True(vm.ShowIssueHint);
            Assert.Contains("احفظ", vm.IssueHintText);

            Assert.Contains(nameof(CalibrationFormViewModel.CanIssueCertificate), raised);
            Assert.Contains(nameof(CalibrationFormViewModel.CanIssueStatusReport), raised);
            Assert.Contains(nameof(CalibrationFormViewModel.IssueHintText), raised);
            Assert.Contains(nameof(CalibrationFormViewModel.ShowIssueHint), raised);

            // العودة إلى النتيجة المحفوظة تُعيد الزرّ الصحيح.
            vm.SelectedResult = "Passed";
            Assert.True(vm.CanIssueCertificate);
            Assert.False(vm.CanIssueStatusReport);
        }

        // مراجعة 18 نوفمبر 2026: بعد إغلاق نموذج الشهادة كانت الحالة تُحدَّث فقط إن بقيت
        // الشهادة حيّة. الإلغاء يضع IsDeleted، فيبقى HasCertificate صحيحاً ومعرّف الملغاة محفوظاً.
        [Fact]
        public async Task RefreshAfterEdit_RevokedCertificate_ClearsHasCertificateAndReopensIssuing()
        {
            var (factory, recordId) = await SeedRecordAsync("Passed");
            int certificateId;
            using (var context = factory.CreateDbContext())
            {
                var certificate = new Certificate
                {
                    CalibrationRecordId = recordId,
                    CertificateNumber = "TNRC-SSDL-2026-0001",
                    CertificateTemplateType = "Pancake Probe",
                    ClientName = "Owner A",
                    DeviceModel = "Model A",
                    DeviceSerialNumber = "SN123",
                    CalibrationDate = DateTime.Today,
                    IssueDate = DateTime.Today
                };
                context.Certificates.Add(certificate);
                await context.SaveChangesAsync();
                certificateId = certificate.Id;
            }

            var vm = BuildViewModel(factory);
            await vm.LoadForEdit(recordId);
            Assert.True(vm.HasCertificate);
            Assert.True(vm.EditCertificateCommand.CanExecute(null));
            Assert.False(vm.CanIssueCertificate);

            // ما يفعله RevokeAsync في المستودع: IsRevoked + IsDeleted.
            using (var context = factory.CreateDbContext())
            {
                var stored = await context.Certificates.FindAsync(certificateId);
                stored!.IsRevoked = true;
                stored.IsDeleted = true;
                await context.SaveChangesAsync();
            }

            vm.RefreshCertificateStateAfterEdit();

            Assert.False(vm.HasCertificate);
            Assert.False(vm.EditCertificateCommand.CanExecute(null));
            Assert.True(vm.CanIssueCertificate);
        }

        [Fact]
        public async Task RefreshAfterEdit_LiveCertificate_KeepsStateAndUpdatesNumber()
        {
            var (factory, recordId) = await SeedRecordAsync("Passed");
            int certificateId;
            using (var context = factory.CreateDbContext())
            {
                var certificate = new Certificate
                {
                    CalibrationRecordId = recordId,
                    CertificateNumber = "TNRC-SSDL-2026-0001",
                    CertificateTemplateType = "Pancake Probe",
                    ClientName = "Owner A",
                    DeviceModel = "Model A",
                    DeviceSerialNumber = "SN123",
                    CalibrationDate = DateTime.Today,
                    IssueDate = DateTime.Today
                };
                context.Certificates.Add(certificate);
                await context.SaveChangesAsync();
                certificateId = certificate.Id;
            }

            var vm = BuildViewModel(factory);
            await vm.LoadForEdit(recordId);

            using (var context = factory.CreateDbContext())
            {
                var stored = await context.Certificates.FindAsync(certificateId);
                stored!.CertificateNumber = "TNRC-SSDL-2026-0002";
                await context.SaveChangesAsync();
            }

            vm.RefreshCertificateStateAfterEdit();

            Assert.True(vm.HasCertificate);
            Assert.Equal("TNRC-SSDL-2026-0002", vm.CertificateNumber);
            Assert.True(vm.EditCertificateCommand.CanExecute(null));
        }
    }
}
