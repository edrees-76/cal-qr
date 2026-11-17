using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Repositories;
using CAL_QR.Services;
using CAL_QR.Validation;

namespace CAL_QR.Tests
{
    /// <summary>
    /// فلتر سجلّ العمليّات بيوم المستخدم المحلّيّ: ActionAt مخزَّن بتوقيت UTC، والمستخدم يختار يوماً
    /// محلّيّاً. منطقة ثابتة (UTC+2 كتوقيت ليبيا) ليعمل الاختبار على أيّ جهاز.
    /// </summary>
    public class AuditLogLocalDayFilterTests
    {
        private static readonly TimeZoneInfo Plus2 =
            TimeZoneInfo.CreateCustomTimeZone("Test+2", TimeSpan.FromHours(2), "Test+2", "Test+2");

        private static readonly TimeZoneInfo Minus5 =
            TimeZoneInfo.CreateCustomTimeZone("Test-5", TimeSpan.FromHours(-5), "Test-5", "Test-5");

        private static DateTime Utc(int y, int mo, int d, int h, int mi) => new DateTime(y, mo, d, h, mi, 0, DateTimeKind.Utc);

        // ── القاعدة النقيّة ──

        [Fact]
        public void ToUtcRange_Plus2_StartsTwoHoursBeforeUtcMidnight()
        {
            var (start, end) = AuditLogDateRangeRules.ToUtcRange(new DateTime(2026, 11, 17), new DateTime(2026, 11, 17), Plus2);

            Assert.Equal(Utc(2026, 11, 16, 22, 0), start);
            Assert.Equal(Utc(2026, 11, 17, 22, 0), end);
        }

        [Fact]
        public void ToUtcRange_Minus5_StartsFiveHoursAfterUtcMidnight()
        {
            var (start, end) = AuditLogDateRangeRules.ToUtcRange(new DateTime(2026, 11, 17), new DateTime(2026, 11, 18), Minus5);

            Assert.Equal(Utc(2026, 11, 17, 5, 0), start);
            Assert.Equal(Utc(2026, 11, 19, 5, 0), end);
        }

        [Fact]
        public void ToUtcRange_IgnoresTimeOfDayInPickedDates()
        {
            var (start, end) = AuditLogDateRangeRules.ToUtcRange(
                new DateTime(2026, 11, 17, 15, 45, 0), new DateTime(2026, 11, 17, 9, 10, 0), Plus2);

            Assert.Equal(Utc(2026, 11, 16, 22, 0), start);
            Assert.Equal(Utc(2026, 11, 17, 22, 0), end);
        }

        [Fact]
        public void ToUtcRange_MissingBounds_StayNull()
        {
            var (start, end) = AuditLogDateRangeRules.ToUtcRange(null, null, Plus2);

            Assert.Null(start);
            Assert.Null(end);
        }

        [Fact]
        public void ToUtcRange_NullZone_Throws() =>
            Assert.Throws<ArgumentNullException>(() => AuditLogDateRangeRules.ToUtcRange(DateTime.Today, null, null!));

        [Fact]
        public void ToLocal_Plus2_ShowsUtcTimeTwoHoursLater()
        {
            // القيمة المقروءة من القاعدة Kind=Unspecified؛ تُعامل على أنّها UTC.
            var stored = new DateTime(2026, 11, 16, 23, 30, 0, DateTimeKind.Unspecified);

            Assert.Equal(new DateTime(2026, 11, 17, 1, 30, 0), AuditLogDateRangeRules.ToLocal(stored, Plus2));
        }

        // ── المستودع ──

        private class TestDbContextFactory : IDbContextFactory<CalQrDbContext>
        {
            private readonly DbContextOptions<CalQrDbContext> _options;

            public TestDbContextFactory(DbContextOptions<CalQrDbContext> options)
            {
                _options = options;
            }

            public CalQrDbContext CreateDbContext() => new CalQrDbContext(_options);
        }

        private class NoUserService : ICurrentUserService
        {
            public User? CurrentUser => null;
            public void SetCurrentUser(User user) { }
            public void ClearCurrentUser() { }
        }

        private static async Task<AuditLogRepository> RepoWith(params (string action, DateTime actionAtUtc)[] logs)
        {
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase("AuditLocalDay_" + Guid.NewGuid())
                .Options;

            using (var context = new CalQrDbContext(options))
            {
                foreach (var (action, at) in logs)
                    context.AuditLogs.Add(new AuditLog { Action = action, ActionAt = at });
                await context.SaveChangesAsync();
            }

            return new AuditLogRepository(new TestDbContextFactory(options), new NoUserService());
        }

        [Fact]
        public async Task Filter_LocalDay_IncludesEarlyMorningAndExcludesNextLocalDay()
        {
            var repo = await RepoWith(
                ("early-17", Utc(2026, 11, 16, 22, 30)),   // 00:30 محلّيّاً يوم 17
                ("late-16", Utc(2026, 11, 16, 21, 59)),    // 23:59 محلّيّاً يوم 16
                ("late-17", Utc(2026, 11, 17, 21, 59)),    // 23:59 محلّيّاً يوم 17
                ("early-18", Utc(2026, 11, 17, 22, 0)));   // 00:00 محلّيّاً يوم 18

            var logs = await repo.GetFilteredAsync(new DateTime(2026, 11, 17), new DateTime(2026, 11, 17), null, Plus2);

            Assert.Equal(new[] { "early-17", "late-17" }, logs.Select(l => l.Action).OrderBy(a => a).ToArray());
        }

        [Fact]
        public async Task Filter_StartOnly_IncludesFromLocalMidnight()
        {
            var repo = await RepoWith(
                ("before", Utc(2026, 11, 16, 21, 59)),
                ("from", Utc(2026, 11, 16, 22, 0)));

            var logs = await repo.GetFilteredAsync(new DateTime(2026, 11, 17), null, null, Plus2);

            Assert.Equal("from", Assert.Single(logs).Action);
        }
    }
}
