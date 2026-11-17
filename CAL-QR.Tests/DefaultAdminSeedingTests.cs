using System;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CAL_QR.Data;
using CAL_QR.Helpers;
using CAL_QR.Models;

namespace CAL_QR.Tests
{
    /// <summary>
    /// حساب المدير الاحتياطيّ: كلمته المؤقّتة تُرجَع ليعرضها الإقلاع على الشاشة، بدل أن تُكتب
    /// في Debug/Console اللذين لا يظهران في نسخة Release.
    /// </summary>
    public class DefaultAdminSeedingTests
    {
        private static void WithContext(Action<CalQrDbContext> test)
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            try
            {
                var options = new DbContextOptionsBuilder<CalQrDbContext>().UseSqlite(connection).Options;
                using var context = new CalQrDbContext(options);
                context.Database.EnsureCreated();
                test(context);
            }
            finally
            {
                connection.Close();
            }
        }

        private static void MarkFirstRunCompleted(CalQrDbContext context)
        {
            context.AppSettings.Add(new AppSetting { Key = "FirstRunCompleted", Value = "true" });
            context.SaveChanges();
        }

        [Fact]
        public void Seed_FirstRunCompletedAndNoUsers_ReturnsPasswordMatchingStoredHash()
        {
            WithContext(context =>
            {
                MarkFirstRunCompleted(context);

                string? password = DatabaseMigrator.SeedDefaultAdminUser(context);

                Assert.False(string.IsNullOrWhiteSpace(password));
                var admin = Assert.Single(context.Users);
                Assert.Equal(DatabaseMigrator.DefaultAdminUsername, admin.Username);
                Assert.Equal(UserRole.Admin, admin.Role);
                Assert.True(admin.IsActive);
                Assert.True(BCrypt.Net.BCrypt.Verify(password, admin.PasswordHash));
                Assert.NotEqual(password, admin.PasswordHash);
            });
        }

        [Fact]
        public void Seed_FirstRunNotCompleted_ReturnsNullAndCreatesNoUser()
        {
            WithContext(context =>
            {
                string? password = DatabaseMigrator.SeedDefaultAdminUser(context);

                Assert.Null(password);
                Assert.Empty(context.Users);
            });
        }

        [Fact]
        public void Seed_UsersAlreadyExist_ReturnsNullAndAddsNoUser()
        {
            WithContext(context =>
            {
                MarkFirstRunCompleted(context);
                context.Users.Add(new User
                {
                    FullName = "مدير قائم",
                    Username = "owner",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("ExistingPass123"),
                    Role = UserRole.Admin,
                    IsEditor = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
                context.SaveChanges();

                string? password = DatabaseMigrator.SeedDefaultAdminUser(context);

                Assert.Null(password);
                var only = Assert.Single(context.Users);
                Assert.Equal("owner", only.Username);
            });
        }

        [Fact]
        public void RunMigrations_ReportsSeededPasswordOnceOnly()
        {
            WithContext(context =>
            {
                MarkFirstRunCompleted(context);

                var first = DatabaseMigrator.RunMigrations(context);
                var second = DatabaseMigrator.RunMigrations(context);

                Assert.NotNull(first.SeededAdminTemporaryPassword);
                var admin = Assert.Single(context.Users);
                Assert.True(BCrypt.Net.BCrypt.Verify(first.SeededAdminTemporaryPassword, admin.PasswordHash));
                Assert.Null(second.SeededAdminTemporaryPassword);
            });
        }

        [Fact]
        public void RunMigrations_FreshDatabase_ReportsNoSeededPassword()
        {
            WithContext(context =>
            {
                var result = DatabaseMigrator.RunMigrations(context);

                Assert.Null(result.SeededAdminTemporaryPassword);
                Assert.Empty(context.Users);
            });
        }

        [Fact]
        public void Notice_ContainsUsernameAndPassword()
        {
            string message = SeededAdminNotice.BuildMessage("admin", "a1b2c3d4e5");

            Assert.Contains("admin", message);
            Assert.Contains("a1b2c3d4e5", message);
            Assert.False(string.IsNullOrWhiteSpace(SeededAdminNotice.Title));
        }
    }
}
