using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CAL_QR.Data;
using CAL_QR.Helpers;
using CAL_QR.Models;
using CAL_QR.Services;

namespace CAL_QR.Tests
{
    public class RecoveryAnswerServiceTests : IDisposable
    {
        private class TestDbContextFactory : IDbContextFactory<CalQrDbContext>
        {
            private readonly DbContextOptions<CalQrDbContext> _options;
            public TestDbContextFactory(DbContextOptions<CalQrDbContext> options) => _options = options;
            public CalQrDbContext CreateDbContext() => new CalQrDbContext(_options);
        }

        private readonly string _dbPath =
            Path.Combine(Path.GetTempPath(), $"cal_qr_recovery_{Guid.NewGuid():N}.db");
        private readonly TestDbContextFactory _factory;
        private DateTime _now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        public RecoveryAnswerServiceTests()
        {
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseSqlite($"Data Source={_dbPath}").Options;
            _factory = new TestDbContextFactory(options);
            using var db = _factory.CreateDbContext();
            db.Database.EnsureCreated();
        }

        public void Dispose()
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(_dbPath))
            {
                try { File.Delete(_dbPath); }
                catch (IOException) { /* temp file still locked; harmless leftover */ }
            }
        }

        private RecoveryAnswerService NewService() => new(_factory, () => _now);

        private void SeedAnswer(string storedValue)
        {
            using var db = _factory.CreateDbContext();
            db.AppSettings.Add(new AppSetting { Key = "SecurityAnswer", Value = storedValue });
            db.SaveChanges();
        }

        private string? Get(string key)
        {
            using var db = _factory.CreateDbContext();
            return db.AppSettings.AsNoTracking().FirstOrDefault(s => s.Key == key)?.Value;
        }

        [Fact]
        public void HashAnswer_ProducesBcryptWithSalt()
        {
            var svc = NewService();
            var a = svc.HashAnswer("Cairo");
            var b = svc.HashAnswer("Cairo");
            Assert.StartsWith("$2", a);
            Assert.NotEqual(a, b);
        }

        [Fact]
        public void HashAnswer_EmptyThrows()
        {
            Assert.Throws<ArgumentException>(() => NewService().HashAnswer("   "));
        }

        [Fact]
        public void NotConfigured_WhenMissingOrEmpty()
        {
            var svc = NewService();
            Assert.Equal(RecoveryAnswerStatus.NotConfigured, svc.Verify("x").Status);
            SeedAnswer("");
            Assert.Equal(RecoveryAnswerStatus.NotConfigured, svc.Verify("x").Status);
            Assert.Null(Get("RecoveryFailedAttempts"));
        }

        [Theory]
        [InlineData("القاهرة", "  القاهرة   ")]
        [InlineData("cairo", "Cairo")]
        [InlineData("new york", "  New    York ")]
        public void Bcrypt_NormalizesCaseAndWhitespace(string original, string attempt)
        {
            var svc = NewService();
            SeedAnswer(svc.HashAnswer(original));
            Assert.Equal(RecoveryAnswerStatus.Success, svc.Verify(attempt).Status);
        }

        [Fact]
        public void WrongAnswer_DecrementsRemainingAttempts()
        {
            var svc = NewService();
            SeedAnswer(svc.HashAnswer("cairo"));
            var r1 = svc.Verify("no");
            var r2 = svc.Verify("no");
            Assert.Equal(RecoveryAnswerStatus.WrongAnswer, r1.Status);
            Assert.Equal(4, r1.RemainingAttempts);
            Assert.Equal(3, r2.RemainingAttempts);
        }

        [Fact]
        public void FifthWrong_Locks_AndCorrectAnswerDuringLockStillLocked_ThenSucceedsAfterExpiry()
        {
            var svc = NewService();
            SeedAnswer(svc.HashAnswer("cairo"));
            for (int i = 0; i < 4; i++)
                Assert.Equal(RecoveryAnswerStatus.WrongAnswer, svc.Verify("no").Status);

            var locked = svc.Verify("no");
            Assert.Equal(RecoveryAnswerStatus.LockedOut, locked.Status);
            Assert.Equal(TimeSpan.FromMinutes(15), locked.RemainingLock);

            _now = _now.AddMinutes(5);
            var during = svc.Verify("cairo");
            Assert.Equal(RecoveryAnswerStatus.LockedOut, during.Status);
            Assert.Equal(TimeSpan.FromMinutes(10), during.RemainingLock);

            _now = _now.AddMinutes(11);
            Assert.Equal(RecoveryAnswerStatus.Success, svc.Verify("cairo").Status);
            Assert.Equal("0", Get("RecoveryFailedAttempts"));
            Assert.True(string.IsNullOrEmpty(Get("RecoveryLockedUntilUtc")));
            Assert.Equal(4, svc.Verify("no").RemainingAttempts);
        }

        [Fact]
        public void Lock_SurvivesNewServiceInstance()
        {
            var svc = NewService();
            SeedAnswer(svc.HashAnswer("cairo"));
            for (int i = 0; i < 5; i++) svc.Verify("no");

            var fresh = NewService();
            Assert.Equal(RecoveryAnswerStatus.LockedOut, fresh.Verify("cairo").Status);
        }

        [Fact]
        public void Lock_RoundTripsUnderArabicCulture()
        {
            var original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("ar-SA");
                var svc = NewService();
                SeedAnswer(svc.HashAnswer("cairo"));
                for (int i = 0; i < 5; i++) svc.Verify("no");

                _now = _now.AddMinutes(1);
                var r = svc.Verify("cairo");
                Assert.Equal(RecoveryAnswerStatus.LockedOut, r.Status);
                Assert.Equal(TimeSpan.FromMinutes(14), r.RemainingLock);
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }

        [Fact]
        public void Legacy_VerifiesUpgradesToBcryptAndStillVerifies()
        {
            SeedAnswer(PasswordHelper.HashPassword("Cairo"));
            var svc = NewService();

            Assert.Equal(RecoveryAnswerStatus.Success, svc.Verify(" Cairo ").Status);
            Assert.StartsWith("$2", Get("SecurityAnswer"));
            Assert.Equal(RecoveryAnswerStatus.Success, svc.Verify("Cairo").Status);
        }

        [Fact]
        public void Legacy_IsCaseSensitive_WrongCountsAsFailure()
        {
            SeedAnswer(PasswordHelper.HashPassword("Cairo"));
            var r = NewService().Verify("cairo");
            Assert.Equal(RecoveryAnswerStatus.WrongAnswer, r.Status);
            Assert.Equal(4, r.RemainingAttempts);
            Assert.Equal(PasswordHelper.HashPassword("Cairo"), Get("SecurityAnswer"));
        }

        [Fact]
        public void MalformedBcryptHash_IsWrongAnswer()
        {
            SeedAnswer("$2a$10$broken");
            var r = NewService().Verify("x");
            Assert.Equal(RecoveryAnswerStatus.WrongAnswer, r.Status);
        }
    }
}
