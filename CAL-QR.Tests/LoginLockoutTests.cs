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
    public class LoginLockoutTests
    {
        private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void RemainingLock_NullWhenNoLockOrExpired_ElseTimeLeft()
        {
            Assert.Null(LoginLockoutRules.RemainingLock(null, Now));
            Assert.Null(LoginLockoutRules.RemainingLock(Now, Now));
            Assert.Equal(TimeSpan.FromMinutes(2), LoginLockoutRules.RemainingLock(Now.AddMinutes(2), Now));
        }

        [Fact]
        public void RegisterFailure_CountsUntilMax_ThenLocksAndResetsCounter()
        {
            var (a1, l1) = LoginLockoutRules.RegisterFailure(0, null, Now);
            Assert.Equal(1, a1); Assert.Null(l1);
            var (a2, l2) = LoginLockoutRules.RegisterFailure(a1, l1, Now);
            Assert.Equal(2, a2); Assert.Null(l2);
            var (a3, l3) = LoginLockoutRules.RegisterFailure(a2, l2, Now);
            Assert.Equal(0, a3);
            Assert.Equal(Now + LoginLockoutRules.LockDuration, l3);
        }

        [Fact]
        public void RegisterFailure_DuringActiveLock_ChangesNothing()
        {
            var until = Now.AddMinutes(3);
            Assert.Equal((0, (DateTime?)until), LoginLockoutRules.RegisterFailure(0, until, Now));
        }

        [Fact]
        public void RegisterFailure_AfterExpiredLock_StartsFreshCount()
        {
            var (a, l) = LoginLockoutRules.RegisterFailure(0, Now.AddMinutes(-1), Now);
            Assert.Equal(1, a);
            Assert.Null(l);
        }

        [Fact]
        public async Task Repository_LockPersistsAcrossNewRepositoryInstance_AndResetClearsIt()
        {
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase("CalQrTestDb_LoginLock_" + Guid.NewGuid()).Options;
            var factory = new TestFactory(options);
            var repo = new UserRepository(factory);
            var user = new User { FullName = "u", Username = "u", PasswordHash = "x", Role = UserRole.User, IsActive = true, CreatedAt = Now };
            await repo.AddAsync(user);
            var id = (await repo.GetByUsernameAsync("u"))!.Id;

            DateTime? locked = null;
            for (int i = 0; i < LoginLockoutRules.MaxAttempts; i++)
                locked = await repo.RegisterFailedLoginAsync(id, Now);
            Assert.NotNull(locked);

            var restarted = new UserRepository(factory); // محاكاة إعادة التشغيل
            var stored = await restarted.GetByUsernameAsync("u");
            Assert.NotNull(LoginLockoutRules.RemainingLock(stored!.LockedUntil, Now));

            await restarted.ResetLoginFailuresAsync(id);
            var cleared = await restarted.GetByUsernameAsync("u");
            Assert.Null(cleared!.LockedUntil);
            Assert.Equal(0, cleared.FailedLoginAttempts);
        }

        [Fact]
        public async Task UpdateAsync_DoesNotClearAnActiveLock_FromAStaleUserObject()
        {
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase("CalQrTestDb_LoginLock2_" + Guid.NewGuid()).Options;
            var repo = new UserRepository(new TestFactory(options));
            await repo.AddAsync(new User { FullName = "u", Username = "u", PasswordHash = "x", Role = UserRole.User, IsActive = true, CreatedAt = Now });
            var stale = (await repo.GetByUsernameAsync("u"))!;

            for (int i = 0; i < LoginLockoutRules.MaxAttempts; i++)
                await repo.RegisterFailedLoginAsync(stale.Id, Now);

            stale.FullName = "renamed";
            await repo.UpdateAsync(stale);

            var after = (await repo.GetByUsernameAsync("u"))!;
            Assert.Equal("renamed", after.FullName);
            Assert.NotNull(after.LockedUntil);
        }

        private sealed class TestFactory : IDbContextFactory<CalQrDbContext>
        {
            private readonly DbContextOptions<CalQrDbContext> _options;
            public TestFactory(DbContextOptions<CalQrDbContext> options) => _options = options;
            public CalQrDbContext CreateDbContext() => new CalQrDbContext(_options);
        }
    }
}
