using Xunit;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Repositories;
using CAL_QR.ViewModels;
using CAL_QR.Services;
using CAL_QR.ViewModels.Base;

#pragma warning disable CS1998

namespace CAL_QR.Tests
{
    [Collection("MessageBoxMock")]
    public class DeviceDetailViewModelTests : IDisposable
    {
        public DeviceDetailViewModelTests()
        {
            // Intercept message box to prevent blocking headless test runners
            DeviceDetailViewModel.MessageBoxShowMock = (msg, caption, btn, img) => MessageBoxResult.Yes;
        }

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
        public async Task LoadDeviceDetails_WithPreferredRecordId_SetsSelectedRecordToPreferred()
        {
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase(databaseName: "CalQrTestDb_DetailsVM_PreferredRecord_" + Guid.NewGuid().ToString())
                .Options;

            var factory = new TestDbContextFactory(options);
            var calRepo = new CalibrationRepository(factory);
            var devRepo = new DeviceRepository(factory);
            var authService = new TestCurrentUserService();

            int deviceId;
            CalibrationRecord recordOlder, recordNewer;

            using (var context = new CalQrDbContext(options))
            {
                var owner = new Owner { Name = "Owner A" };
                var type = new DeviceType { Name = "Type A" };
                context.Owners.Add(owner);
                context.DeviceTypes.Add(type);
                await context.SaveChangesAsync();

                var device = new Device { Model = "Ludlum", SerialNumber = "SN100", OwnerId = owner.Id, DeviceTypeId = type.Id };
                context.Devices.Add(device);
                await context.SaveChangesAsync();
                deviceId = device.Id;

                recordOlder = new CalibrationRecord
                {
                    DeviceId = deviceId,
                    CertificateNumber = "CERT-OLDER",
                    CalibrationDate = DateTime.Today.AddMonths(-12),
                    ExpiryDate = DateTime.Today,
                    Result = "Passed",
                    HmacSignature = "HMAC1"
                };
                recordNewer = new CalibrationRecord
                {
                    DeviceId = deviceId,
                    CertificateNumber = "CERT-NEWER",
                    CalibrationDate = DateTime.Today,
                    ExpiryDate = DateTime.Today.AddYears(1),
                    Result = "Passed",
                    HmacSignature = "HMAC2"
                };
                context.CalibrationRecords.AddRange(recordOlder, recordNewer);
                await context.SaveChangesAsync();
            }

            var vm = new DeviceDetailViewModel(factory, calRepo, devRepo, authService, null!, null!, null!);

            // 1. Without preferredRecordId -> defaults to FirstOrDefault (latest)
            await vm.LoadDeviceDetailsAsync(deviceId);
            Assert.Equal("CERT-NEWER", vm.SelectedRecord?.CertificateNumber);

            // 2. With preferredRecordId -> sets SelectedRecord to the specific older record requested
            await vm.LoadDeviceDetailsAsync(deviceId, recordOlder.Id);
            Assert.Equal("CERT-OLDER", vm.SelectedRecord?.CertificateNumber);
        }

        // ───────── حذف سجلّ لم تصدر له شهادة ─────────

        private static (DbContextOptions<CalQrDbContext> options, TestDbContextFactory factory, TestCurrentUserService auth) NewDb(UserRole role = UserRole.Admin)
        {
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase("CalQrTestDb_DeleteRecord_" + Guid.NewGuid())
                .Options;
            var auth = new TestCurrentUserService();
            auth.SetCurrentUser(new User { Id = 1, Username = "tester", Role = role, IsActive = true });
            return (options, new TestDbContextFactory(options), auth);
        }

        private static async Task<(int deviceId, int[] recordIds)> SeedDeviceAsync(
            DbContextOptions<CalQrDbContext> options, params (string legacyNumber, string signature)[] records)
        {
            using var context = new CalQrDbContext(options);
            var owner = new Owner { Name = "Owner" };
            var type = new DeviceType { Name = "Type" };
            context.Owners.Add(owner);
            context.DeviceTypes.Add(type);
            await context.SaveChangesAsync();
            var device = new Device { Model = "Ludlum", SerialNumber = "SN-DEL", OwnerId = owner.Id, DeviceTypeId = type.Id };
            context.Devices.Add(device);
            await context.SaveChangesAsync();

            var list = records.Select((r, i) => new CalibrationRecord
            {
                DeviceId = device.Id,
                CertificateNumber = r.legacyNumber,
                HmacSignature = r.signature,
                CalibrationDate = DateTime.Today.AddDays(-i),
                ExpiryDate = DateTime.Today.AddYears(1),
                Result = "Passed"
            }).ToList();
            context.CalibrationRecords.AddRange(list);
            await context.SaveChangesAsync();
            return (device.Id, list.Select(r => r.Id).ToArray());
        }

        private static async Task AddCertificateAsync(DbContextOptions<CalQrDbContext> options, int recordId, bool revoked)
        {
            using var context = new CalQrDbContext(options);
            context.Certificates.Add(new Certificate
            {
                CalibrationRecordId = recordId,
                CertificateNumber = "TNRC-SSDL-2026-" + recordId.ToString("D4"),
                ClientName = "Client",
                DeviceModel = "Ludlum",
                DeviceSerialNumber = "SN-DEL",
                IsRevoked = revoked,
                IsDeleted = revoked
            });
            await context.SaveChangesAsync();
        }

        private static DeviceDetailViewModel NewVm(TestDbContextFactory factory, TestCurrentUserService auth)
            => new DeviceDetailViewModel(factory, new CalibrationRepository(factory), new DeviceRepository(factory),
                auth, new AuditLogRepository(factory, auth), null!, null!);

        [Fact]
        public async Task DeleteRecord_AdminRecordWithoutCertificate_SoftDeletesAuditsAndRaisesSaved()
        {
            var (options, factory, auth) = NewDb();
            var (deviceId, ids) = await SeedDeviceAsync(options, ("", ""), ("", ""));
            var vm = NewVm(factory, auth);
            await vm.LoadDeviceDetailsAsync(deviceId, ids[0]);
            bool saved = false;
            vm.Saved += (s, e) => saved = true;

            Assert.True(vm.ShowDeleteRecord);
            await ((AsyncRelayCommand)vm.DeleteRecordCommand).ExecuteAsync(null);

            using var context = new CalQrDbContext(options);
            Assert.True(context.CalibrationRecords.Single(r => r.Id == ids[0]).IsDeleted);
            Assert.False(context.CalibrationRecords.Single(r => r.Id == ids[1]).IsDeleted);
            Assert.Single(context.AuditLogs.Where(a => a.Action == "حذف سجلّ معايرة" && a.EntityId == ids[0].ToString()));
            Assert.True(saved);
            Assert.DoesNotContain(vm.Calibrations, r => r.Id == ids[0]);
        }

        [Fact]
        public async Task DeleteRecord_OnlyRecordOfDevice_WithoutCertificate_IsAllowed()
        {
            // قاعدة «آخر سجلّ للجهاز» لا تُطبَّق على سجلّ بلا شهادة (لا طريق مسدود).
            var (options, factory, auth) = NewDb();
            var (deviceId, ids) = await SeedDeviceAsync(options, ("", ""));
            var vm = NewVm(factory, auth);
            await vm.LoadDeviceDetailsAsync(deviceId);

            await ((AsyncRelayCommand)vm.DeleteRecordCommand).ExecuteAsync(null);

            using var context = new CalQrDbContext(options);
            Assert.True(context.CalibrationRecords.Single(r => r.Id == ids[0]).IsDeleted);
        }

        [Theory]
        [InlineData(false)] // شهادة سارية
        [InlineData(true)]  // شهادة مبطَلة (IsDeleted)
        public async Task DeleteRecord_RecordWithAnyCertificate_ButtonHiddenAndNotDeleted(bool revoked)
        {
            var (options, factory, auth) = NewDb();
            var (deviceId, ids) = await SeedDeviceAsync(options, ("", ""), ("", ""));
            await AddCertificateAsync(options, ids[0], revoked);
            var vm = NewVm(factory, auth);
            await vm.LoadDeviceDetailsAsync(deviceId, ids[0]);

            Assert.False(vm.ShowDeleteRecord);
            Assert.False(vm.DeleteRecordCommand.CanExecute(null));
            await ((AsyncRelayCommand)vm.DeleteRecordCommand).ExecuteAsync(null);

            using var context = new CalQrDbContext(options);
            Assert.False(context.CalibrationRecords.Single(r => r.Id == ids[0]).IsDeleted);
        }

        [Fact]
        public async Task DeleteRecord_LegacyIssuedRecord_ButtonHidden()
        {
            var (options, factory, auth) = NewDb();
            var (deviceId, ids) = await SeedDeviceAsync(options, ("CERT-OLD", "HMAC1"));
            var vm = NewVm(factory, auth);
            await vm.LoadDeviceDetailsAsync(deviceId);

            Assert.False(vm.ShowDeleteRecord);
        }

        [Theory]
        [InlineData(UserRole.User)]
        [InlineData(UserRole.Viewer)]
        public async Task DeleteRecord_NonAdmin_ButtonHiddenAndNotDeleted(UserRole role)
        {
            var (options, factory, auth) = NewDb(role);
            var (deviceId, ids) = await SeedDeviceAsync(options, ("", ""));
            var vm = NewVm(factory, auth);
            await vm.LoadDeviceDetailsAsync(deviceId);

            Assert.False(vm.ShowDeleteRecord);
            await ((AsyncRelayCommand)vm.DeleteRecordCommand).ExecuteAsync(null);

            using var context = new CalQrDbContext(options);
            Assert.False(context.CalibrationRecords.Single(r => r.Id == ids[0]).IsDeleted);
        }

        [Fact]
        public async Task DeleteRecord_UserDeclinesConfirmation_NothingDeleted()
        {
            var (options, factory, auth) = NewDb();
            var (deviceId, ids) = await SeedDeviceAsync(options, ("", ""));
            var vm = NewVm(factory, auth);
            await vm.LoadDeviceDetailsAsync(deviceId);
            DeviceDetailViewModel.MessageBoxShowMock = (msg, caption, btn, img) => MessageBoxResult.No;

            await ((AsyncRelayCommand)vm.DeleteRecordCommand).ExecuteAsync(null);

            using var context = new CalQrDbContext(options);
            Assert.False(context.CalibrationRecords.Single(r => r.Id == ids[0]).IsDeleted);
            Assert.Empty(context.AuditLogs);
        }

        [Fact]
        public async Task DeleteRecord_CertificateIssuedAfterDialogOpened_IsReCheckedAndBlocked()
        {
            // الفحص يُعاد على قاعدة البيانات لحظة التنفيذ: النافذة فُتحت قبل صدور الشهادة.
            var (options, factory, auth) = NewDb();
            var (deviceId, ids) = await SeedDeviceAsync(options, ("", ""), ("", ""));
            var vm = NewVm(factory, auth);
            await vm.LoadDeviceDetailsAsync(deviceId, ids[0]);
            Assert.True(vm.ShowDeleteRecord);

            await AddCertificateAsync(options, ids[0], revoked: false);
            await ((AsyncRelayCommand)vm.DeleteRecordCommand).ExecuteAsync(null);

            using var context = new CalQrDbContext(options);
            Assert.False(context.CalibrationRecords.Single(r => r.Id == ids[0]).IsDeleted);
            Assert.Empty(context.AuditLogs);
        }

        public void Dispose()
        {
            DeviceDetailViewModel.MessageBoxShowMock = null;
        }
    }
}
