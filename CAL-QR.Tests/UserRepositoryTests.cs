using Xunit;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Repositories;
using CAL_QR.ViewModels;
using CAL_QR.Helpers;

namespace CAL_QR.Tests
{
    public class UserRepositoryTests
    {
        [Fact]
        public async Task UserRepository_AddAndGetUsers_WorksCorrectly()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase(databaseName: "CalQrTestDb_User_" + Guid.NewGuid().ToString())
                .Options;

            var factory = new TestDbContextFactory(options);
            var repo = new UserRepository(factory);

            var user = new User
            {
                FullName = "أحمد علي",
                Username = "ahmed_ali",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("MySecretPass123"),
                Role = UserRole.User,
                Permissions = SystemPermissions.Records | SystemPermissions.Reports,
                IsEditor = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            // Act
            await repo.AddAsync(user);

            // Assert
            var retrieved = await repo.GetByUsernameAsync("ahmed_ali");
            Assert.NotNull(retrieved);
            Assert.Equal("أحمد علي", retrieved.FullName);
            Assert.Equal(UserRole.User, retrieved.Role);
            Assert.True(retrieved.IsEditor);
            Assert.True(retrieved.IsActive);
            Assert.True(BCrypt.Net.BCrypt.Verify("MySecretPass123", retrieved.PasswordHash));
            
            var all = await repo.GetAllAsync();
            Assert.Single(all);
        }

        [Fact]
        public async Task UserRepository_UpdateUser_WorksCorrectly()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase(databaseName: "CalQrTestDb_UserUpdate_" + Guid.NewGuid().ToString())
                .Options;

            var factory = new TestDbContextFactory(options);
            var repo = new UserRepository(factory);

            var user = new User
            {
                FullName = "خالد محمود",
                Username = "khaled",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("KhaledPass"),
                Role = UserRole.Viewer,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            await repo.AddAsync(user);

            // Act
            var added = await repo.GetByUsernameAsync("khaled");
            added!.FullName = "خالد محمود المحدث";
            added.Role = UserRole.User;
            added.IsActive = false;
            added.PasswordHash = BCrypt.Net.BCrypt.HashPassword("NewPass");
            await repo.UpdateAsync(added);

            // Assert
            var updated = await repo.GetByIdAsync(added.Id);
            Assert.NotNull(updated);
            Assert.Equal("خالد محمود المحدث", updated.FullName);
            Assert.Equal(UserRole.User, updated.Role);
            Assert.False(updated.IsActive);
            Assert.True(BCrypt.Net.BCrypt.Verify("NewPass", updated.PasswordHash));
        }

        [Fact]
        public async Task UserRepository_GetActiveAdminsCount_WorksCorrectly()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase(databaseName: "CalQrTestDb_UserAdminCount_" + Guid.NewGuid().ToString())
                .Options;

            var factory = new TestDbContextFactory(options);
            var repo = new UserRepository(factory);

            // Seed active admin
            await repo.AddAsync(new User { Username = "admin1", FullName = "Admin One", Role = UserRole.Admin, IsActive = true, CreatedAt = DateTime.UtcNow });
            // Seed inactive admin
            await repo.AddAsync(new User { Username = "admin2", FullName = "Admin Two", Role = UserRole.Admin, IsActive = false, CreatedAt = DateTime.UtcNow });
            // Seed active normal user
            await repo.AddAsync(new User { Username = "user1", FullName = "User One", Role = UserRole.User, IsActive = true, CreatedAt = DateTime.UtcNow });

            // Act
            int adminCount = await repo.GetActiveAdminsCountAsync();

            // Assert
            Assert.Equal(1, adminCount);
        }

        [Fact]
        public void UsersViewModel_CanFreezeUser_GuardRules_WorkCorrectly()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase(databaseName: "CalQrTestDb_UserVM_" + Guid.NewGuid().ToString())
                .Options;
            var factory = new TestDbContextFactory(options);
            
            // ViewModel instantiation requires repository dependencies, but we can pass mocks or dummy repos for testing the pure method.
            // Since UserRepository just uses EF factory, we can supply standard repos.
            var userRepo = new UserRepository(factory);
            var auditRepo = new AuditLogRepository(factory, new TestCurrentUserService());
            var vm = new UsersViewModel(userRepo, auditRepo, () => null!, new TestCurrentUserService());

            var adminUser = new User { Id = 1, Username = "admin", Role = UserRole.Admin, IsActive = true };
            var normalUser = new User { Id = 2, Username = "user", Role = UserRole.User, IsActive = true };

            // Act & Assert 1: Self-freeze rule
            // Current User is User 2, trying to freeze User 2 -> should fail
            bool canSelfFreeze = vm.CanFreezeUser(normalUser, currentUserId: 2, activeAdminsCount: 2);
            Assert.False(canSelfFreeze, "User should not be able to freeze their own active account");

            // Act & Assert 2: Last Active Admin rule
            // Target is admin (User 1), activeAdminsCount is 1 -> should fail
            bool canFreezeLastAdmin = vm.CanFreezeUser(adminUser, currentUserId: 2, activeAdminsCount: 1);
            Assert.False(canFreezeLastAdmin, "Should not be able to freeze the last active admin account");

            // Act & Assert 3: Safe freeze cases
            // Target is admin (User 1), but activeAdminsCount is 2 -> should pass
            bool canFreezeAdminWithBackup = vm.CanFreezeUser(adminUser, currentUserId: 2, activeAdminsCount: 2);
            Assert.True(canFreezeAdminWithBackup, "Should be able to freeze admin if there is another active admin");

            // Target is normal user (User 2), current user is User 1 -> should pass
            bool canFreezeNormal = vm.CanFreezeUser(normalUser, currentUserId: 1, activeAdminsCount: 1);
            Assert.True(canFreezeNormal, "Should be able to freeze normal user");
        }

        [Fact]
        public async Task AuditLogRepository_GetFilteredAsync_FiltersCorrectly()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase(databaseName: "CalQrTestDb_AuditFilter_" + Guid.NewGuid().ToString())
                .Options;

            var factory = new TestDbContextFactory(options);
            var repo = new AuditLogRepository(factory, new TestCurrentUserService());

            // Add mock users to database
            using (var context = new CalQrDbContext(options))
            {
                context.Users.Add(new User { Id = 10, Username = "ali", FullName = "علي" });
                context.Users.Add(new User { Id = 20, Username = "omar", FullName = "عمر" });
                
                context.AuditLogs.Add(new AuditLog { Action = "Action1", ActionAt = DateTime.Today.AddDays(-5), UserId = 10, Username = "ali" });
                context.AuditLogs.Add(new AuditLog { Action = "Action2", ActionAt = DateTime.Today.AddDays(-2), UserId = 20, Username = "omar" });
                context.AuditLogs.Add(new AuditLog { Action = "Action3", ActionAt = DateTime.Today, UserId = 10, Username = "ali" });
                
                await context.SaveChangesAsync();
            }

            // Act & Assert 1: Filter by UserId
            var user10Logs = await repo.GetFilteredAsync(null, null, 10);
            Assert.Equal(2, user10Logs.Count);
            Assert.All(user10Logs, l => Assert.Equal(10, l.UserId));

            // Act & Assert 2: Filter by date range
            var rangeLogs = await repo.GetFilteredAsync(DateTime.Today.AddDays(-4), DateTime.Today.AddDays(-1), null);
            Assert.Single(rangeLogs);
            Assert.Equal("Action2", rangeLogs[0].Action);

            // Act & Assert 3: Combine Date and User Filters
            var combinedLogs = await repo.GetFilteredAsync(DateTime.Today.AddDays(-1), DateTime.Today, 10);
            Assert.Single(combinedLogs);
            Assert.Equal("Action3", combinedLogs[0].Action);
        }

        [Fact]
        public async Task UserRepository_AddDuplicateUsername_ThrowsDbUpdateException()
        {
            // Arrange
            var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
            connection.Open();
            try
            {
                var options = new DbContextOptionsBuilder<CalQrDbContext>()
                    .UseSqlite(connection)
                    .Options;

                using (var context = new CalQrDbContext(options))
                {
                    context.Database.EnsureCreated();
                }

                var factory = new TestDbContextFactory(options);
                var repo = new UserRepository(factory);

                var user1 = new User { Username = "dup_user", FullName = "User 1", PasswordHash = "Hash1" };
                var user2 = new User { Username = "dup_user", FullName = "User 2", PasswordHash = "Hash2" };

                // Act
                await repo.AddAsync(user1);

                // Assert: second add should throw DbUpdateException due to unique constraint index
                await Assert.ThrowsAsync<DbUpdateException>(async () => await repo.AddAsync(user2));
            }
            finally
            {
                connection.Close();
            }
        }

        [Fact]
        public void DatabaseMigrator_SeedAdminUser_IsIdempotent()
        {
            // Arrange
            var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
            connection.Open();
            try
            {
                var options = new DbContextOptionsBuilder<CalQrDbContext>()
                    .UseSqlite(connection)
                    .Options;

                using (var context = new CalQrDbContext(options))
                {
                    context.Database.EnsureCreated();

                    // Simulate upgrade by writing FirstRunCompleted setting
                    context.AppSettings.Add(new AppSetting { Key = "FirstRunCompleted", Value = "true" });
                    context.SaveChanges();
                    
                    // First seed run
                    DatabaseMigrator.RunMigrations(context);
                    
                    var firstAdmin = context.Users.FirstOrDefault(u => u.Username == "admin");
                    Assert.NotNull(firstAdmin);
                    string firstPasswordHash = firstAdmin.PasswordHash;

                    // Alter user info to verify it won't be overwritten
                    firstAdmin.FullName = "Modified Admin Name";
                    context.SaveChanges();

                    // Second seed run
                    DatabaseMigrator.RunMigrations(context);

                    // Assert user info is untouched
                    var secondAdmin = context.Users.FirstOrDefault(u => u.Username == "admin");
                    Assert.NotNull(secondAdmin);
                    Assert.Equal("Modified Admin Name", secondAdmin.FullName);
                    Assert.Equal(firstPasswordHash, secondAdmin.PasswordHash);
                    
                    // Verify no duplicate users were seeded
                    Assert.Equal(1, context.Users.Count());
                }
            }
            finally
            {
                connection.Close();
            }
        }

        [Fact]
        public void DatabaseMigrator_FirstRunCompletedEmpty_DoesNotSeedAdmin()
        {
            // Arrange
            var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
            connection.Open();
            try
            {
                var options = new DbContextOptionsBuilder<CalQrDbContext>()
                    .UseSqlite(connection)
                    .Options;

                using (var context = new CalQrDbContext(options))
                {
                    context.Database.EnsureCreated();
                    
                    // Act
                    DatabaseMigrator.RunMigrations(context);
                    
                    // Assert
                    var admin = context.Users.FirstOrDefault(u => u.Username == "admin");
                    Assert.Null(admin);
                    Assert.Empty(context.Users);
                }
            }
            finally
            {
                connection.Close();
            }
        }

        [Fact]
        public void LoginWindow_BridgeAuthentication_SucceedsAndFailsCorrectly()
        {
            var thread = new System.Threading.Thread(() =>
            {
                // Arrange
                var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
                connection.Open();
                try
                {
                    var options = new DbContextOptionsBuilder<CalQrDbContext>()
                        .UseSqlite(connection)
                        .Options;

                    var factory = new TestDbContextFactory(options);
                    var currentUserService = new TestCurrentUserService();
                    var userRepo = new UserRepository(factory);

                    using (var context = new CalQrDbContext(options))
                    {
                        context.Database.EnsureCreated();
                        
                        // Seed legacy password hash (SHA256)
                        context.AppSettings.Add(new AppSetting { Key = "PasswordHash", Value = PasswordHelper.HashPassword("legacyPass") });
                        context.AppSettings.Add(new AppSetting { Key = "FirstRunCompleted", Value = "true" });
                        
                        // Seed the default admin user with BCrypt hashed password
                        context.Users.Add(new User
                        {
                            Username = "admin",
                            PasswordHash = BCrypt.Net.BCrypt.HashPassword("bcryptPass"),
                            FullName = "Default Admin",
                            Role = UserRole.Admin,
                            Permissions = SystemPermissions.UserManagement,
                            IsEditor = true,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        });
                        
                        context.SaveChanges();
                    }

                    if (System.Windows.Application.Current == null)
                    {
                        var app = new System.Windows.Application();
                        app.ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
                    }

                    try
                    {
                        var appResources = new System.Windows.ResourceDictionary
                        {
                            Source = new Uri("pack://application:,,,/CAL-QR;component/App.xaml", UriKind.Absolute)
                        };
                        System.Windows.Application.Current.Resources.MergedDictionaries.Add(appResources);
                    }
                    catch (Exception)
                    {
                        // Fallback in case package URI resolution fails in testing environment
                        try
                        {
                            System.Windows.Application.Current.Resources.MergedDictionaries.Add(
                                new System.Windows.ResourceDictionary { Source = new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesign2.Defaults.xaml", UriKind.Absolute) }
                            );
                        }
                        catch { }
                        System.Windows.Application.Current.Resources["CairoFont"] = new System.Windows.Media.FontFamily("Cairo");
                        System.Windows.Application.Current.Resources["MaterialDesignFlatButton"] = new System.Windows.Style(typeof(System.Windows.Controls.Button));
                    }

                    var loginWindow = new CAL_QR.Views.LoginWindow(factory, currentUserService, userRepo);
                    var loginMethod = typeof(CAL_QR.Views.LoginWindow).GetMethod("BtnLogin_Click", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    Assert.NotNull(loginMethod);

                    // Test 1: Wrong legacy password, no username -> should fail
                    loginWindow.TxtUsername.Text = "";
                    loginWindow.TxtPassword.Password = "wrongLegacyPass";
                    loginMethod.Invoke(loginWindow, new object[] { null!, null! });
                    Assert.Null(currentUserService.CurrentUser);

                    // Test 2: Correct legacy password, no username -> should succeed via bridge
                    loginWindow.TxtUsername.Text = "";
                    loginWindow.TxtPassword.Password = "legacyPass";
                    loginMethod.Invoke(loginWindow, new object[] { null!, null! });
                    Assert.NotNull(currentUserService.CurrentUser);
                    Assert.Equal("admin", currentUserService.CurrentUser.Username);

                    // Clear session
                    currentUserService.ClearCurrentUser();

                    // Test 3: Correct BCrypt password, with username -> should succeed via primary path
                    loginWindow.TxtUsername.Text = "admin";
                    loginWindow.TxtPassword.Password = "bcryptPass";
                    loginMethod.Invoke(loginWindow, new object[] { null!, null! });
                    Assert.NotNull(currentUserService.CurrentUser);
                    Assert.Equal("admin", currentUserService.CurrentUser.Username);
                }
                finally
                {
                    connection.Close();
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();
        }

        [Fact]
        public void LoginWindow_InactiveUser_ReturnsSpecificErrorMessage()
        {
            var thread = new System.Threading.Thread(() =>
            {
                // Arrange
                var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
                connection.Open();
                try
                {
                    var options = new DbContextOptionsBuilder<CalQrDbContext>()
                        .UseSqlite(connection)
                        .Options;

                    var factory = new TestDbContextFactory(options);
                    var currentUserService = new TestCurrentUserService();
                    var userRepo = new UserRepository(factory);

                    using (var context = new CalQrDbContext(options))
                    {
                        context.Database.EnsureCreated();
                        
                        // Seed an inactive user
                        context.Users.Add(new User
                        {
                            Username = "inactiveuser",
                            PasswordHash = BCrypt.Net.BCrypt.HashPassword("somepass"),
                            FullName = "Inactive User",
                            Role = UserRole.User,
                            Permissions = SystemPermissions.Reports,
                            IsEditor = false,
                            IsActive = false, // Frozen
                            CreatedAt = DateTime.UtcNow
                        });
                        
                        context.SaveChanges();
                    }

                    if (System.Windows.Application.Current == null)
                    {
                        var app = new System.Windows.Application();
                        app.ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
                    }

                    try
                    {
                        var appResources = new System.Windows.ResourceDictionary
                        {
                            Source = new Uri("pack://application:,,,/CAL-QR;component/App.xaml", UriKind.Absolute)
                        };
                        System.Windows.Application.Current.Resources.MergedDictionaries.Add(appResources);
                    }
                    catch (Exception)
                    {
                        try
                        {
                            System.Windows.Application.Current.Resources.MergedDictionaries.Add(
                                new System.Windows.ResourceDictionary { Source = new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesign2.Defaults.xaml", UriKind.Absolute) }
                            );
                        }
                        catch { }
                        System.Windows.Application.Current.Resources["CairoFont"] = new System.Windows.Media.FontFamily("Cairo");
                        System.Windows.Application.Current.Resources["MaterialDesignFlatButton"] = new System.Windows.Style(typeof(System.Windows.Controls.Button));
                    }

                    var loginWindow = new CAL_QR.Views.LoginWindow(factory, currentUserService, userRepo);
                    var loginMethod = typeof(CAL_QR.Views.LoginWindow).GetMethod("BtnLogin_Click", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    Assert.NotNull(loginMethod);

                    // Test: Login as inactive user
                    loginWindow.TxtUsername.Text = "inactiveuser";
                    loginWindow.TxtPassword.Password = "somepass";
                    loginMethod.Invoke(loginWindow, new object[] { null!, null! });

                    // Assert: session is still null, but error message is specific
                    Assert.Null(currentUserService.CurrentUser);
                    Assert.Equal("هذا الحساب موقوف حالياً. يرجى مراجعة مدير النظام.", loginWindow.TxtError.Text);
                    
                    // Verify failed attempts count remains 0 (i.e. did not increment)
                    var failedAttemptsField = typeof(CAL_QR.Views.LoginWindow).GetField("_failedAttempts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    Assert.NotNull(failedAttemptsField);
                    int failedAttempts = (int)failedAttemptsField.GetValue(loginWindow)!;
                    Assert.Equal(0, failedAttempts);
                }
                finally
                {
                    connection.Close();
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();
        }

        [Fact]
        public void HasPermission_Admin_AlwaysReturnsTrue()
        {
            // Arrange
            var adminUser = new User
            {
                Role = UserRole.Admin,
                Permissions = SystemPermissions.None // No permissions flagged
            };

            // Act & Assert
            Assert.True(adminUser.HasPermission(SystemPermissions.Records));
            Assert.True(adminUser.HasPermission(SystemPermissions.Reports));
            Assert.True(adminUser.HasPermission(SystemPermissions.Settings));
            Assert.True(adminUser.HasPermission(SystemPermissions.BackupRestore));
            Assert.True(adminUser.HasPermission(SystemPermissions.UserManagement));
            Assert.True(adminUser.HasPermission(SystemPermissions.Verification));
            Assert.True(adminUser.HasPermission(SystemPermissions.Owners));
            Assert.True(adminUser.HasPermission(SystemPermissions.DeviceTypes));
        }

        [Fact]
        public void HasPermission_UserWithSpecificPermission_OnlyHasThatPermission()
        {
            // Arrange
            var user = new User
            {
                Role = UserRole.User,
                Permissions = SystemPermissions.Reports
            };

            // Act & Assert
            Assert.True(user.HasPermission(SystemPermissions.Reports));
            Assert.False(user.HasPermission(SystemPermissions.Records));
            Assert.False(user.HasPermission(SystemPermissions.Settings));
        }

        [Fact]
        public void HasPermission_UserWithOnlyVerification_DoesNotHaveRecordsOrOthers()
        {
            // Arrange
            var user = new User
            {
                Role = UserRole.User,
                Permissions = SystemPermissions.Verification
            };

            // Act & Assert
            Assert.True(user.HasPermission(SystemPermissions.Verification));
            Assert.False(user.HasPermission(SystemPermissions.Records));
            Assert.False(user.HasPermission(SystemPermissions.Owners));
            Assert.False(user.HasPermission(SystemPermissions.DeviceTypes));
            Assert.False(user.HasPermission(SystemPermissions.Reports));
        }

        [Fact]
        public void CanEdit_ReturnsFalse_WhenIsEditorIsFalse()
        {
            // Arrange
            var user = new User
            {
                Role = UserRole.User,
                Permissions = SystemPermissions.Records,
                IsEditor = false
            };
            var mockUserService = new TestCurrentUserService();
            mockUserService.SetCurrentUser(user);

            var dbContextOptions = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase(databaseName: "CalQrTestDb_UserVM_CanEdit_" + Guid.NewGuid().ToString())
                .Options;
            var factory = new TestDbContextFactory(dbContextOptions);

            var userRepo = new UserRepository(factory);
            var auditRepo = new AuditLogRepository(factory, mockUserService);

            var usersVm = new UsersViewModel(userRepo, auditRepo, () => null!, mockUserService);

            // Act & Assert
            Assert.False(usersVm.CanEdit);
        }

        [Fact]
        public void PermissionsSplit_OneTimeMigration_SucceedsAndDoesNotRepeat()
        {
            // Arrange
            var dbContextOptions = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase(databaseName: "CalQrTestDb_PermissionsSplit_" + Guid.NewGuid().ToString())
                .Options;

            using (var context = new CalQrDbContext(dbContextOptions))
            {
                context.Database.EnsureCreated();

                // Create legacy user with only Records permission (representing legacy CertificateManagement)
                var legacyUser = new User
                {
                    Username = "legacy_user",
                    FullName = "Legacy User",
                    PasswordHash = "hash",
                    Role = UserRole.User,
                    Permissions = SystemPermissions.Records,
                    IsEditor = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                context.Users.Add(legacyUser);
                context.SaveChanges();
            }

            // Run database migration (first time)
            using (var context = new CalQrDbContext(dbContextOptions))
            {
                DatabaseMigrator.RunMigrations(context);
            }

            // Verify first migration granted the additional permissions
            using (var context = new CalQrDbContext(dbContextOptions))
            {
                var migratedUser = context.Users.First(u => u.Username == "legacy_user");
                Assert.True(migratedUser.Permissions.HasFlag(SystemPermissions.Records));
                Assert.True(migratedUser.Permissions.HasFlag(SystemPermissions.Verification));
                Assert.True(migratedUser.Permissions.HasFlag(SystemPermissions.Owners));
                Assert.True(migratedUser.Permissions.HasFlag(SystemPermissions.DeviceTypes));

                // Now simulate an admin manually removing "Verification" permission from this user
                migratedUser.Permissions &= ~SystemPermissions.Verification;
                context.SaveChanges();
            }

            // Run database migration (second time)
            using (var context = new CalQrDbContext(dbContextOptions))
            {
                DatabaseMigrator.RunMigrations(context);
            }

            // Verify the manual change is NOT overwritten (idempotence)
            using (var context = new CalQrDbContext(dbContextOptions))
            {
                var migratedUser = context.Users.First(u => u.Username == "legacy_user");
                Assert.True(migratedUser.Permissions.HasFlag(SystemPermissions.Records));
                Assert.False(migratedUser.Permissions.HasFlag(SystemPermissions.Verification)); // Should remain removed
                Assert.True(migratedUser.Permissions.HasFlag(SystemPermissions.Owners));
                Assert.True(migratedUser.Permissions.HasFlag(SystemPermissions.DeviceTypes));
            }
        }

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
