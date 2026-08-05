using Xunit;
using System;
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

        [Fact]
        public async Task SaveCommand_CanExecute_ReturnsFalse_WhenSavingInProgress()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase(databaseName: "CalQrTestDb_FormVM_SaveLock_" + Guid.NewGuid().ToString())
                .Options;

            var factory = new TestDbContextFactory(options);
            var ownerRepo = new OwnerRepository(factory);
            var typeRepo = new DeviceTypeRepository(factory);
            var deviceRepo = new DeviceRepository(factory);
            var calRepo = new CalibrationRepository(factory);
            var attachmentRepo = new AttachmentRepository(factory);
            var authService = new TestCurrentUserService();
            var auditRepo = new AuditLogRepository(factory, authService);

            // لا HmacService ولا QrService: نُزعا من مُنشئ الـViewModel بعد إيقاف
            // التوقيع ورمز الـQR على مستوى سجل المعايرة — صارا مسؤولية الشهادة.
            var vm = new CalibrationFormViewModel(
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
    }
}
