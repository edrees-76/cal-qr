using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Repositories;
using CAL_QR.Services;
using CAL_QR.Validation;

namespace CAL_QR.Tests
{
    /// <summary>
    /// تنبيه المدير بحصيلة بذر أنواع الأجهزة (التبعية الحاجزة رقم 7): تُقرأ من AppSettings،
    /// وتُعرض للمدير وحده، وحصيلة التعارض تُمحى بإقراره وتُسجَّل.
    /// </summary>
    public class DeviceTypeSeedNoticeTests
    {
        private static User MakeUser(UserRole role, bool isActive = true) => new User
        {
            FullName = "مستخدم",
            Username = "u",
            PasswordHash = "x",
            Role = role,
            Permissions = SystemPermissions.Settings,
            IsEditor = true,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow
        };

        // ── القواعد النقيّة ──

        [Fact]
        public void CanSeeNotice_ActiveAdmin_True() =>
            Assert.True(DeviceTypeSeedNoticeRules.CanSeeNotice(MakeUser(UserRole.Admin)));

        [Fact]
        public void CanSeeNotice_NonAdminEvenWithSettings_False() =>
            Assert.False(DeviceTypeSeedNoticeRules.CanSeeNotice(MakeUser(UserRole.User)));

        [Fact]
        public void CanSeeNotice_InactiveAdmin_False() =>
            Assert.False(DeviceTypeSeedNoticeRules.CanSeeNotice(MakeUser(UserRole.Admin, isActive: false)));

        [Fact]
        public void CanSeeNotice_NoUser_False() =>
            Assert.False(DeviceTypeSeedNoticeRules.CanSeeNotice(null));

        [Fact]
        public void BuildMessage_NothingStored_ReturnsNull()
        {
            Assert.Null(DeviceTypeSeedNoticeRules.BuildMessage(null, null, null));
            Assert.Null(DeviceTypeSeedNoticeRules.BuildMessage("  ", "", "\n"));
        }

        [Fact]
        public void BuildMessage_ConflictsOnly_ContainsTextAndAcknowledgeQuestion()
        {
            string? message = DeviceTypeSeedNoticeRules.BuildMessage("تعارض تسمية: «أ» و«ب»", null, null);

            Assert.NotNull(message);
            Assert.Contains("تعارض تسمية: «أ» و«ب»", message);
            Assert.Contains("تريد إخفاءها", message);
        }

        [Fact]
        public void BuildMessage_FailuresOnly_ContainsBothWithoutAcknowledgeQuestion()
        {
            string? message = DeviceTypeSeedNoticeRules.BuildMessage(null, "فشل البذر", "فشلت الترقية");

            Assert.NotNull(message);
            Assert.Contains("فشل البذر", message);
            Assert.Contains("فشلت الترقية", message);
            Assert.DoesNotContain("تريد إخفاءها", message);
        }

        [Fact]
        public void HasAcknowledgeableConflicts_OnlyForNonBlankConflicts()
        {
            Assert.True(DeviceTypeSeedNoticeRules.HasAcknowledgeableConflicts("تعارض"));
            Assert.False(DeviceTypeSeedNoticeRules.HasAcknowledgeableConflicts(null));
            Assert.False(DeviceTypeSeedNoticeRules.HasAcknowledgeableConflicts("   "));
        }

        // ── الخدمة ──

        private class TestDbContextFactory : IDbContextFactory<CalQrDbContext>
        {
            private readonly DbContextOptions<CalQrDbContext> _options;

            public TestDbContextFactory(DbContextOptions<CalQrDbContext> options)
            {
                _options = options;
            }

            public CalQrDbContext CreateDbContext() => new CalQrDbContext(_options);
        }

        private static (DeviceTypeSeedNoticeService service, Mock<IAuditLogRepository> audit, DbContextOptions<CalQrDbContext> options) CreateService()
        {
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase("SeedNotice_" + Guid.NewGuid())
                .Options;
            var audit = new Mock<IAuditLogRepository>();
            var service = new DeviceTypeSeedNoticeService(new TestDbContextFactory(options), audit.Object);
            return (service, audit, options);
        }

        private static void Store(DbContextOptions<CalQrDbContext> options, string key, string value)
        {
            using var context = new CalQrDbContext(options);
            context.AppSettings.Add(new AppSetting { Key = key, Value = value, UpdatedAt = DateTime.UtcNow });
            context.SaveChanges();
        }

        [Fact]
        public async Task LoadAsync_ReadsAllThreeKeys()
        {
            var (service, _, options) = CreateService();
            Store(options, DeviceTypeSeeder.ConflictsKey, "تعارض");
            Store(options, DeviceTypeSeeder.FailureKey, "فشل البذر");
            Store(options, DeviceTypeSeeder.RepairFailureKey, "فشلت الترقية");

            var snapshot = await service.LoadAsync();

            Assert.Equal("تعارض", snapshot.Conflicts);
            Assert.Equal("فشل البذر", snapshot.SeedFailure);
            Assert.Equal("فشلت الترقية", snapshot.RepairFailure);
        }

        [Fact]
        public async Task LoadAsync_NothingStored_AllNull()
        {
            var (service, _, _) = CreateService();

            var snapshot = await service.LoadAsync();

            Assert.Null(snapshot.Conflicts);
            Assert.Null(snapshot.SeedFailure);
            Assert.Null(snapshot.RepairFailure);
        }

        [Fact]
        public async Task Acknowledge_ShownText_RemovesConflictsOnlyAndLogs()
        {
            var (service, audit, options) = CreateService();
            Store(options, DeviceTypeSeeder.ConflictsKey, "تعارض");
            Store(options, DeviceTypeSeeder.FailureKey, "فشل البذر");

            bool removed = await service.AcknowledgeConflictsAsync("تعارض");

            Assert.True(removed);
            using (var context = new CalQrDbContext(options))
            {
                Assert.False(context.AppSettings.Any(s => s.Key == DeviceTypeSeeder.ConflictsKey));
                Assert.True(context.AppSettings.Any(s => s.Key == DeviceTypeSeeder.FailureKey));
            }
            audit.Verify(a => a.LogAsync(
                It.IsAny<string>(), "AppSettings", DeviceTypeSeeder.ConflictsKey,
                It.Is<string>(d => d.Contains("تعارض")), null, null), Times.Once);
        }

        [Fact]
        public async Task Acknowledge_StoredTextChangedSinceShown_KeepsItAndDoesNotLog()
        {
            var (service, audit, options) = CreateService();
            Store(options, DeviceTypeSeeder.ConflictsKey, "تعارض أحدث");

            bool removed = await service.AcknowledgeConflictsAsync("تعارض قديم");

            Assert.False(removed);
            using (var context = new CalQrDbContext(options))
            {
                Assert.True(context.AppSettings.Any(s => s.Key == DeviceTypeSeeder.ConflictsKey));
            }
            audit.Verify(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string?>()), Times.Never);
        }

        [Fact]
        public async Task Acknowledge_BlankText_Throws()
        {
            var (service, _, _) = CreateService();

            await Assert.ThrowsAsync<ArgumentException>(() => service.AcknowledgeConflictsAsync("  "));
        }
    }
}
