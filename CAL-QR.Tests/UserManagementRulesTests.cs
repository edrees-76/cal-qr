using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Repositories;
using CAL_QR.Validation;
using CAL_QR.ViewModels;

namespace CAL_QR.Tests
{
    public class UserManagementRulesTests
    {
        [Theory]
        [InlineData(UserRole.Viewer)]
        [InlineData(UserRole.User)]
        [InlineData(UserRole.Admin)]
        public void Admin_CanAssignAnyRole_AndModifyAnyUser(UserRole target)
        {
            Assert.Null(UserManagementRules.CanAssignRole(UserRole.Admin, target));
            Assert.Null(UserManagementRules.CanModifyUser(UserRole.Admin, target));
        }

        [Theory]
        [InlineData(UserRole.Viewer)]
        [InlineData(UserRole.User)]
        public void NonAdmin_CannotAssignAdminRole(UserRole actor)
        {
            Assert.False(string.IsNullOrWhiteSpace(UserManagementRules.CanAssignRole(actor, UserRole.Admin)));
        }

        [Theory]
        [InlineData(UserRole.Viewer)]
        [InlineData(UserRole.User)]
        public void NonAdmin_CannotModifyExistingAdmin(UserRole actor)
        {
            Assert.False(string.IsNullOrWhiteSpace(UserManagementRules.CanModifyUser(actor, UserRole.Admin)));
        }

        [Fact]
        public void Editor_CanManageNonAdminUsers()
        {
            Assert.Null(UserManagementRules.CanAssignRole(UserRole.User, UserRole.Viewer));
            Assert.Null(UserManagementRules.CanAssignRole(UserRole.User, UserRole.User));
            Assert.Null(UserManagementRules.CanModifyUser(UserRole.User, UserRole.User));
            Assert.Null(UserManagementRules.CanModifyUser(UserRole.User, UserRole.Viewer));
        }

        [Theory]
        [InlineData(true, false, 1, true)]   // آخر مدير نشط يُخفَّض/يُجمَّد
        [InlineData(true, false, 2, false)]  // يوجد مدير نشط آخر
        [InlineData(true, true, 1, false)]   // يبقى مديراً نشطاً
        [InlineData(false, false, 1, false)] // لم يكن مديراً نشطاً أصلاً
        [InlineData(false, true, 1, false)]  // ترقية
        public void WouldLeaveNoActiveAdmin_Cases(bool was, bool will, int count, bool expected)
        {
            Assert.Equal(expected, UserManagementRules.WouldLeaveNoActiveAdmin(was, will, count));
        }

        [Fact]
        public async Task GetFirstActiveAdminAsync_SkipsInactiveAndNonAdmin_ReturnsLowestId_NullWhenNone()
        {
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase("CalQrTestDb_FirstAdmin_" + Guid.NewGuid())
                .Options;
            var repo = new UserRepository(new TestDbContextFactory(options));

            Assert.Null(await repo.GetFirstActiveAdminAsync());

            await repo.AddAsync(NewUser("normal", UserRole.User, true));
            await repo.AddAsync(NewUser("frozen_admin", UserRole.Admin, false));
            Assert.Null(await repo.GetFirstActiveAdminAsync());

            await repo.AddAsync(NewUser("boss1", UserRole.Admin, true));
            await repo.AddAsync(NewUser("boss2", UserRole.Admin, true));

            var first = await repo.GetFirstActiveAdminAsync();
            Assert.NotNull(first);
            Assert.Equal("boss1", first!.Username);
        }

        [Fact]
        public async Task UserForm_EditorCannotSaveAdminRole_NoWrite()
        {
            var (repo, _) = CreateRepo();
            await repo.AddAsync(NewUser("target", UserRole.User, true));
            var target = (await repo.GetByUsernameAsync("target"))!;

            var actor = new TestCurrentUserService { CurrentUser = new User { Id = 99, Username = "ed", Role = UserRole.User, IsEditor = true, IsActive = true } };
            var messages = new List<string>();
            var vm = new UserFormViewModel(repo, actor) { ShowMessage = (t, _) => messages.Add(t) };
            vm.LoadForEdit(target);
            vm.Role = UserRole.Admin;

            vm.SaveCommand.Execute(null);
            await WaitUntilAsync(() => messages.Count > 0);

            Assert.Single(messages);
            Assert.Equal(UserRole.User, (await repo.GetByUsernameAsync("target"))!.Role);
        }

        [Fact]
        public async Task UserForm_LastActiveAdminCannotBeDemoted_NoWrite()
        {
            var (repo, _) = CreateRepo();
            await repo.AddAsync(NewUser("boss", UserRole.Admin, true));
            var boss = (await repo.GetByUsernameAsync("boss"))!;

            var actor = new TestCurrentUserService { CurrentUser = boss };
            var messages = new List<string>();
            var vm = new UserFormViewModel(repo, actor) { ShowMessage = (t, _) => messages.Add(t) };
            vm.LoadForEdit(boss);
            vm.Role = UserRole.User;

            vm.SaveCommand.Execute(null);
            await WaitUntilAsync(() => messages.Count > 0);

            Assert.Equal(new[] { UserManagementRules.LastActiveAdminMessage }, messages);
            Assert.Equal(UserRole.Admin, (await repo.GetByUsernameAsync("boss"))!.Role);
        }

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            for (int i = 0; i < 100 && !condition(); i++)
            {
                await Task.Delay(50);
            }
        }

        private static (UserRepository repo, DbContextOptions<CalQrDbContext> options) CreateRepo()
        {
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase("CalQrTestDb_UserMgmt_" + Guid.NewGuid())
                .Options;
            return (new UserRepository(new TestDbContextFactory(options)), options);
        }

        private static User NewUser(string username, UserRole role, bool active) => new User
        {
            FullName = username,
            Username = username,
            PasswordHash = "x",
            Role = role,
            IsActive = active,
            CreatedAt = DateTime.UtcNow
        };

        private class TestDbContextFactory : IDbContextFactory<CalQrDbContext>
        {
            private readonly DbContextOptions<CalQrDbContext> _options;

            public TestDbContextFactory(DbContextOptions<CalQrDbContext> options)
            {
                _options = options;
            }

            public CalQrDbContext CreateDbContext()
            {
                return new CalQrDbContext(_options);
            }
        }
    }
}
