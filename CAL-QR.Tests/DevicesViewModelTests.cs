using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Repositories;
using CAL_QR.Services;
using CAL_QR.ViewModels;

namespace CAL_QR.Tests
{
    public class DevicesViewModelTests
    {
        // ─── Stubs ────────────────────────────────────────────────────────────

        private class TestDbContextFactory : IDbContextFactory<CalQrDbContext>
        {
            private readonly DbContextOptions<CalQrDbContext> _options;
            public TestDbContextFactory(DbContextOptions<CalQrDbContext> options) => _options = options;
            public CalQrDbContext CreateDbContext() => new CalQrDbContext(_options);
        }

        private class StubQrService : IQrService
        {
            public BitmapSource GenerateQrCodeImage(string content, int sizePx) => throw new NotSupportedException();
            public byte[] GenerateQrCodePngBytes(string content, int sizePx) => Array.Empty<byte>();
            public string GenerateVerificationText(string ownerName, string deviceType, string model,
                string serial, string certNo, string calDate, string expDate,
                string engineerName, string description, string result, string verifyCode) => string.Empty;
        }

        private class StubPrintService : IPrintService
        {
            public IEnumerable<string> GetAvailablePrinters() => Array.Empty<string>();
            public void PrintQrLabel(QrPrintJob job) { }
            public void PrintMultipleQrLabels(IEnumerable<QrPrintJob> jobs) { }
            public BitmapSource RenderLabelPreview(QrPrintJob job, Models.PaperTemplate template) => throw new NotSupportedException();
        }

        private class StubPaperTemplateRepository : IPaperTemplateRepository
        {
            public Task<IEnumerable<PaperTemplate>> GetAllAsync() => Task.FromResult<IEnumerable<PaperTemplate>>(Array.Empty<PaperTemplate>());
            public Task<PaperTemplate?> GetByIdAsync(int id) => Task.FromResult<PaperTemplate?>(null);
            public Task<PaperTemplate?> GetDefaultAsync() => Task.FromResult<PaperTemplate?>(null);
            public Task AddAsync(PaperTemplate template) => Task.CompletedTask;
            public Task UpdateAsync(PaperTemplate template) => Task.CompletedTask;
            public Task DeleteAsync(int id) => Task.CompletedTask;
            public Task SetDefaultAsync(int id) => Task.CompletedTask;
        }

        private class StubCertificateRepository : ICertificateRepository
        {
            public Task<Certificate?> GetByIdAsync(int id) => Task.FromResult<Certificate?>(null);
            public Task<Certificate?> GetByCertificateNumberAsync(string certificateNumber) => Task.FromResult<Certificate?>(null);
            public Task<string> AddAsync(Certificate certificate) => Task.FromResult("TNRC-0000-0000");
            public Task<bool> UpdateAsync(Certificate certificate) => Task.FromResult(false);
            public Task MarkPrintedAsync(int certificateId) => Task.CompletedTask;
            public Task<bool> SyncSignedCopyStateAsync(int certificateId) => Task.FromResult(false);
            public Task<CertificateVerificationResult> VerifyByCodeAsync(string verifyCode) =>
                Task.FromResult(new CertificateVerificationResult { Status = CertificateVerificationStatus.NotFound });
            public Task RevokeAsync(int certificateId, string revokedByName, string reason) => Task.CompletedTask;
        }

        private static DbContextOptions<CalQrDbContext> NewInMemoryOptions() =>
            new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase("DevicesVM_" + Guid.NewGuid())
                .Options;

        private DevicesViewModel BuildVm(DbContextOptions<CalQrDbContext> options, User? user)
        {
            var factory = new TestDbContextFactory(options);
            var currentUser = new TestCurrentUserService { CurrentUser = user };
            return new DevicesViewModel(
                new DeviceRepository(factory),
                factory,
                new OwnerRepository(factory),
                new DeviceTypeRepository(factory),
                new StubQrService(),
                new StubPrintService(),
                new StubPaperTemplateRepository(),
                () => throw new NotSupportedException("dialog not supported in tests"),
                () => throw new NotSupportedException("dialog not supported in tests"),
                () => throw new NotSupportedException("dialog not supported in tests"),
                () => throw new NotSupportedException("dialog not supported in tests"),
                new StubCertificateRepository(),
                currentUser);
        }

        // ─── Tests ────────────────────────────────────────────────────────────

        [Fact]
        public void CanEdit_AdminUser_ReturnsTrue()
        {
            var options = NewInMemoryOptions();
            var admin = new User { Username = "admin", Role = UserRole.Admin };

            using var vm = BuildVm(options, admin);

            Assert.True(vm.CanEdit);
        }

        [Fact]
        public void CanEdit_NullUser_ReturnsFalse()
        {
            var options = NewInMemoryOptions();

            using var vm = BuildVm(options, null);

            Assert.False(vm.CanEdit);
        }

        [Fact]
        public void CanEdit_EditorUser_ReturnsTrue()
        {
            var options = NewInMemoryOptions();
            var editor = new User { Username = "editor", Role = UserRole.Viewer, IsEditor = true };

            using var vm = BuildVm(options, editor);

            Assert.True(vm.CanEdit);
        }

        [Fact]
        public void ToggleAdvancedSearchCommand_TogglesIsAdvancedSearchVisible()
        {
            var options = NewInMemoryOptions();

            using var vm = BuildVm(options, null);

            Assert.False(vm.IsAdvancedSearchVisible);

            vm.ToggleAdvancedSearchCommand.Execute(null);
            Assert.True(vm.IsAdvancedSearchVisible);

            vm.ToggleAdvancedSearchCommand.Execute(null);
            Assert.False(vm.IsAdvancedSearchVisible);
        }

        [Fact]
        public async Task LoadDataAsync_ReturnsOnlyNonDeletedRecords()
        {
            var options = NewInMemoryOptions();

            using (var context = new CalQrDbContext(options))
            {
                context.AppSettings.Add(new AppSetting { Key = "AlertDaysThreshold", Value = "30" });
                var owner = new Owner { Name = "جهة الاختبار" };
                var type = new DeviceType { Name = "نوع الاختبار" };
                context.Owners.Add(owner);
                context.DeviceTypes.Add(type);
                await context.SaveChangesAsync();

                var device1 = new Device { Model = "A100", SerialNumber = "SN-A100", OwnerId = owner.Id, DeviceTypeId = type.Id };
                var device2 = new Device { Model = "B200", SerialNumber = "SN-B200", OwnerId = owner.Id, DeviceTypeId = type.Id };
                context.Devices.AddRange(device1, device2);
                await context.SaveChangesAsync();

                // سجل معايرة للجهاز الأوّل — غير محذوف
                context.CalibrationRecords.Add(new CalibrationRecord
                {
                    DeviceId = device1.Id, CertificateNumber = "TNRC-2024-0001",
                    CalibrationDate = DateTime.Today, ExpiryDate = DateTime.Today.AddYears(1),
                    EngineerName = "إدريس", Result = "Passed", HmacSignature = "H1", IsDeleted = false
                });
                // سجل معايرة للجهاز الثاني — محذوف
                context.CalibrationRecords.Add(new CalibrationRecord
                {
                    DeviceId = device2.Id, CertificateNumber = "TNRC-2024-0002",
                    CalibrationDate = DateTime.Today, ExpiryDate = DateTime.Today.AddYears(1),
                    EngineerName = "إدريس", Result = "Passed", HmacSignature = "H2", IsDeleted = true
                });
                await context.SaveChangesAsync();
            }

            using var vm = BuildVm(options, new User { Username = "admin", Role = UserRole.Admin });
            await vm.LoadDataAsync();

            // يجب أن تظهر السجلات غير المحذوفة فقط
            Assert.Equal(1, vm.Devices.Count);
            Assert.Equal("A100", vm.Devices[0].Model);
        }

        [Fact]
        public async Task ClearFiltersAsync_ResetsSearchAndFilters()
        {
            var options = NewInMemoryOptions();
            using (var context = new CalQrDbContext(options))
            {
                context.AppSettings.Add(new AppSetting { Key = "AlertDaysThreshold", Value = "30" });
                await context.SaveChangesAsync();
            }

            using var vm = BuildVm(options, null);
            vm.SearchText = "بحث تجريبي";
            vm.FilterModel = "نموذج";
            vm.IsAdvancedSearchVisible = true;

            vm.ClearFiltersCommand.Execute(null);

            await Task.Delay(50); // السماح للـ async بالانتهاء
            Assert.Equal(string.Empty, vm.SearchText);
            Assert.Equal(string.Empty, vm.FilterModel);
        }
    }
}
