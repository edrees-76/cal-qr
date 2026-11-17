using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Repositories;
using CAL_QR.Validation;

namespace CAL_QR.Tests
{
    /// <summary>آخر تسجيل دخول (LastLoginAt) وقيد سجلّ العمليّات عند انتهاء الجلسة.</summary>
    public class LoginSessionRecordTests
    {
        private static readonly DateTime Now = new(2026, 11, 18, 10, 30, 0, DateTimeKind.Utc);

        private static async Task<(UserRepository repo, int id)> NewUserAsync()
        {
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase("CalQrTestDb_LoginSession_" + Guid.NewGuid()).Options;
            var repo = new UserRepository(new TestFactory(options));
            await repo.AddAsync(new User { FullName = "u", Username = "u", PasswordHash = "x", Role = UserRole.User, IsActive = true, CreatedAt = Now });
            return (repo, (await repo.GetByUsernameAsync("u"))!.Id);
        }

        [Fact]
        public async Task RecordSuccessfulLogin_SetsLastLoginAt_AndClearsFailures()
        {
            var (repo, id) = await NewUserAsync();
            await repo.RegisterFailedLoginAsync(id, Now);

            await repo.RecordSuccessfulLoginAsync(id, Now);

            var after = (await repo.GetByUsernameAsync("u"))!;
            Assert.Equal(Now, after.LastLoginAt);
            Assert.Equal(0, after.FailedLoginAttempts);
            Assert.Null(after.LockedUntil);
        }

        [Fact]
        public async Task RecordSuccessfulLogin_UnknownUser_DoesNothing()
        {
            var (repo, _) = await NewUserAsync();
            await repo.RecordSuccessfulLoginAsync(999_999, Now);
            Assert.Null((await repo.GetByUsernameAsync("u"))!.LastLoginAt);
        }

        [Fact]
        public async Task UpdateAsync_FromStaleUserObject_DoesNotRollBackLastLoginAt()
        {
            var (repo, id) = await NewUserAsync();
            var stale = (await repo.GetByUsernameAsync("u"))!; // LastLoginAt == null, loaded before the login

            await repo.RecordSuccessfulLoginAsync(id, Now);
            stale.FullName = "renamed";
            await repo.UpdateAsync(stale);

            var after = (await repo.GetByUsernameAsync("u"))!;
            Assert.Equal("renamed", after.FullName);
            Assert.Equal(Now, after.LastLoginAt);
        }

        [Theory]
        [InlineData(SessionEndReason.LogoutButton, "بزرّ الخروج")]
        [InlineData(SessionEndReason.WindowClosed, "بإغلاق النافذة الرئيسيّة")]
        [InlineData(SessionEndReason.IdleLock, "بالقفل التلقائيّ بعد الخمول")]
        public void SessionEndDetails_NameTheAccountAndTheWay(SessionEndReason reason, string how)
        {
            Assert.Equal($"انتهت جلسة الحساب admin {how}.", SessionEndAuditRules.Details("admin", reason));
        }

        [Fact]
        public void SessionEndAction_IsOneActionForEveryWay()
        {
            Assert.Equal("تسجيل خروج", SessionEndAuditRules.Action);
        }

        private sealed class TestFactory : IDbContextFactory<CalQrDbContext>
        {
            private readonly DbContextOptions<CalQrDbContext> _options;
            public TestFactory(DbContextOptions<CalQrDbContext> options) => _options = options;
            public CalQrDbContext CreateDbContext() => new CalQrDbContext(_options);
        }
    }
}
