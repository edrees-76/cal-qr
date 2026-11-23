using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Repositories;
using CAL_QR.ViewModels;

namespace CAL_QR.Tests
{
    /// <summary>
    /// جولة 1.1: مواضع الإطلاق بلا انتظار في نموذج المعايرة — حذف المرفق المحفوظ،
    /// وفحص تكرار الرقم التسلسليّ (سباق الترتيب وفشل الاستعلام ومعرّف الجهاز القديم).
    /// </summary>
    public class FireAndForgetFormFixesTests
    {
        private sealed class Factory : IDbContextFactory<CalQrDbContext>
        {
            private readonly DbContextOptions<CalQrDbContext> _options;
            public Factory(DbContextOptions<CalQrDbContext> options) => _options = options;
            public CalQrDbContext CreateDbContext() => new CalQrDbContext(_options);
        }

        private static async Task<CalibrationFormViewModel> BuildAsync(
            Mock<IDeviceRepository> deviceRepo, Mock<IAttachmentRepository> attachmentRepo)
        {
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase("FireAndForgetForm_" + Guid.NewGuid())
                .Options;
            var factory = new Factory(options);

            var vm = new CalibrationFormViewModel(
                factory,
                new OwnerRepository(factory),
                new DeviceTypeRepository(factory),
                deviceRepo.Object,
                new CalibrationRepository(factory),
                attachmentRepo.Object,
                new AuditLogRepository(factory, new TestCurrentUserService()),
                () => throw new NotSupportedException());

            await vm.Initialization;
            return vm;
        }

        private static int DeviceIdOf(CalibrationFormViewModel vm) =>
            (int)typeof(CalibrationFormViewModel)
                .GetField("_deviceId", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(vm)!;

        private static void SetDeviceId(CalibrationFormViewModel vm, int id) =>
            typeof(CalibrationFormViewModel)
                .GetField("_deviceId", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(vm, id);

        // ── حذف المرفق ───────────────────────────────────────────────

        [Fact]
        public async Task TryRemoveAttachment_Saved_DeletesFromRepositoryThenRemovesFromList()
        {
            var attachments = new Mock<IAttachmentRepository>();
            attachments.Setup(r => r.DeleteAsync(7)).Returns(Task.CompletedTask);
            var vm = await BuildAsync(new Mock<IDeviceRepository>(), attachments);
            var item = new AttachmentItem { Id = 7, FileName = "a.pdf", IsSaved = true };
            vm.Attachments.Add(item);

            var removed = await vm.TryRemoveAttachmentAsync(item);

            Assert.True(removed);
            Assert.DoesNotContain(item, vm.Attachments);
            attachments.Verify(r => r.DeleteAsync(7), Times.Once);
        }

        [Fact]
        public async Task TryRemoveAttachment_SavedAndDeleteFails_KeepsItemAndReturnsFalse()
        {
            var attachments = new Mock<IAttachmentRepository>();
            attachments.Setup(r => r.DeleteAsync(7)).ThrowsAsync(new InvalidOperationException("db locked"));
            var vm = await BuildAsync(new Mock<IDeviceRepository>(), attachments);
            var item = new AttachmentItem { Id = 7, FileName = "a.pdf", IsSaved = true };
            vm.Attachments.Add(item);

            var removed = await vm.TryRemoveAttachmentAsync(item);

            Assert.False(removed);
            Assert.Contains(item, vm.Attachments);
        }

        [Fact]
        public async Task TryRemoveAttachment_NotSaved_RemovesWithoutTouchingRepository()
        {
            var attachments = new Mock<IAttachmentRepository>(MockBehavior.Strict);
            var vm = await BuildAsync(new Mock<IDeviceRepository>(), attachments);
            var item = new AttachmentItem { FileName = "new.pdf", IsSaved = false };
            vm.Attachments.Add(item);

            var removed = await vm.TryRemoveAttachmentAsync(item);

            Assert.True(removed);
            Assert.DoesNotContain(item, vm.Attachments);
        }

        // ── فحص تكرار الرقم التسلسليّ ────────────────────────────────

        [Fact]
        public async Task SerialCheck_Match_LinksDeviceAndWarns()
        {
            var devices = new Mock<IDeviceRepository>();
            devices.Setup(r => r.GetBySerialNumberAsync("SN1"))
                .ReturnsAsync(new Device { Id = 5, Model = "M-5", SerialNumber = "SN1" });
            var vm = await BuildAsync(devices, new Mock<IAttachmentRepository>());

            vm.SerialNumber = "SN1";
            await vm.SerialCheckTask;

            Assert.Equal(5, DeviceIdOf(vm));
            Assert.Equal("M-5", vm.Model);
            Assert.Contains("مسجل مسبقاً", vm.SerialNumberWarning);
        }

        [Fact]
        public async Task SerialCheck_StaleResultArrivingLate_IsIgnored()
        {
            var staleTcs = new TaskCompletionSource<Device?>();
            var currentTcs = new TaskCompletionSource<Device?>();
            var devices = new Mock<IDeviceRepository>();
            devices.Setup(r => r.GetBySerialNumberAsync("AB")).Returns(staleTcs.Task);
            devices.Setup(r => r.GetBySerialNumberAsync("AB1")).Returns(currentTcs.Task);
            var vm = await BuildAsync(devices, new Mock<IAttachmentRepository>());

            vm.SerialNumber = "AB";
            var staleCheck = vm.SerialCheckTask;
            vm.SerialNumber = "AB1";
            var currentCheck = vm.SerialCheckTask;

            // الرقم الحاليّ بلا جهاز، ثمّ تصل نتيجة «AB» القديمة متأخّرة بجهاز موجود.
            currentTcs.SetResult(null);
            await currentCheck;
            staleTcs.SetResult(new Device { Id = 99, Model = "OTHER", SerialNumber = "AB" });
            await staleCheck;

            Assert.Equal(0, DeviceIdOf(vm));
            Assert.Equal(string.Empty, vm.Model);
            Assert.Equal(string.Empty, vm.SerialNumberWarning);
        }

        [Fact]
        public async Task SerialCheck_ChangeResetsDeviceIdBeforeNewLookupReturns()
        {
            var pending = new TaskCompletionSource<Device?>();
            var devices = new Mock<IDeviceRepository>();
            devices.Setup(r => r.GetBySerialNumberAsync("NEW")).Returns(pending.Task);
            var vm = await BuildAsync(devices, new Mock<IAttachmentRepository>());
            SetDeviceId(vm, 5);

            vm.SerialNumber = "NEW";

            Assert.Equal(0, DeviceIdOf(vm));

            pending.SetResult(null);
            await vm.SerialCheckTask;
            Assert.Equal(0, DeviceIdOf(vm));
        }

        [Fact]
        public async Task SerialCheck_RepositoryFails_DoesNotThrowAndWarns()
        {
            var devices = new Mock<IDeviceRepository>();
            devices.Setup(r => r.GetBySerialNumberAsync("SN1")).ThrowsAsync(new InvalidOperationException("db locked"));
            var vm = await BuildAsync(devices, new Mock<IAttachmentRepository>());
            SetDeviceId(vm, 5);

            vm.SerialNumber = "SN1";
            await vm.SerialCheckTask;

            Assert.Equal(0, DeviceIdOf(vm));
            Assert.Contains("تعذّر فحص", vm.SerialNumberWarning);
        }
    }
}
