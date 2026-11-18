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
    /// <summary>
    /// إصلاحات الجولة الثانية من مراجعة 18 نوفمبر 2026 (Codex وGemini).
    /// </summary>
    public class ReviewRound2FixesTests
    {
        private sealed class Factory : IDbContextFactory<CalQrDbContext>
        {
            private readonly DbContextOptions<CalQrDbContext> _options;
            public Factory(string name) =>
                _options = new DbContextOptionsBuilder<CalQrDbContext>().UseInMemoryDatabase(name).Options;
            public CalQrDbContext CreateDbContext() => new CalQrDbContext(_options);
        }

        // ── تعديل الجهة لا يمسّ تاريخ الإنشاء ولا علم البيانات التجريبيّة ──

        [Fact]
        public async Task OwnerUpdate_ChangesEditableFields_KeepsCreatedAtAndSeedFlag()
        {
            var factory = new Factory("Round2_Owner_" + Guid.NewGuid().ToString("N"));
            var createdAt = new DateTime(2025, 3, 1, 8, 0, 0, DateTimeKind.Utc);
            int id;
            using (var db = factory.CreateDbContext())
            {
                var owner = new Owner { Name = "Old", Address = "A", CreatedAt = createdAt, IsSeedTestData = true };
                db.Owners.Add(owner);
                await db.SaveChangesAsync();
                id = owner.Id;
            }

            // ما يبنيه نموذج الجهة: كائن جديد بالحقول القابلة للتحرير فقط.
            await new OwnerRepository(factory).UpdateAsync(new Owner
            {
                Id = id, Name = "New", Address = "B", ContactPhone = "091", ContactPerson = "P"
            });

            using (var db = factory.CreateDbContext())
            {
                var stored = await db.Owners.SingleAsync(o => o.Id == id);
                Assert.Equal("New", stored.Name);
                Assert.Equal("B", stored.Address);
                Assert.Equal("091", stored.ContactPhone);
                Assert.Equal("P", stored.ContactPerson);
                Assert.Equal(createdAt, stored.CreatedAt);
                Assert.True(stored.IsSeedTestData);
                Assert.False(stored.IsDeleted);
            }
        }

        [Fact]
        public async Task OwnerUpdate_MissingOwner_Throws()
        {
            var factory = new Factory("Round2_OwnerMissing_" + Guid.NewGuid().ToString("N"));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new OwnerRepository(factory).UpdateAsync(new Owner { Id = 999, Name = "X" }));
        }

        // ── المحاولات المتبقية تتبع عدّاد الحساب ──

        [Theory]
        [InlineData(1, 2)]
        [InlineData(2, 1)]
        [InlineData(0, 0)] // العدّاد صُفّر لأنّ الحساب قُفل للتوّ
        public void AttemptsLeft_FollowsAccountCounter(int attemptsAfter, int expected)
        {
            Assert.Equal(expected, LoginLockoutRules.AttemptsLeft(attemptsAfter));
        }

        [Fact]
        public void AttemptsLeft_EachAccountCountsAlone()
        {
            var now = new DateTime(2026, 11, 18, 9, 0, 0, DateTimeKind.Utc);
            // خطأ واحد سابق للحساب A لا يؤثّر في B: B عند أوّل خطأ له يبقى له محاولتان.
            var (afterA, _) = LoginLockoutRules.RegisterFailure(1, null, now);
            var (afterB, _) = LoginLockoutRules.RegisterFailure(0, null, now);
            Assert.Equal(1, LoginLockoutRules.AttemptsLeft(afterA));
            Assert.Equal(2, LoginLockoutRules.AttemptsLeft(afterB));
        }

        // ── لاحقة النسبة: لا "٪ %" ──

        [Theory]
        [InlineData("2.00", "2.00 %")]
        [InlineData("2.00 %", "2.00 %")]
        [InlineData("2.00%", "2.00%")]
        [InlineData("2.00٪", "2.00٪")]
        [InlineData(" 3.99 ٪ ", "3.99 ٪")]
        [InlineData(null, "")]
        [InlineData("  ", "")]
        public void WithPercent_DoesNotDoubleArabicOrLatinPercent(string? value, string expected)
        {
            Assert.Equal(expected, PercentDisplayRules.WithPercent(value));
        }

        // ── العرض المبسّط: تحذير عند أكثر من صفّ مطبوع ──

        [Fact]
        public void SimplifiedWarning_MoreThanOnePrintedRow_Warns()
        {
            var rows = new[]
            {
                new CertificateCalibrationResult { CorrectionFactor = "0.95", AbsoluteRelativeError = "5.5" },
                new CertificateCalibrationResult { CorrectionFactor = "0.99" },
                new CertificateCalibrationResult() // صفّ فارغ لا يُطبع
            };
            string? warning = SimplifiedResultsRules.Warning(true, rows);
            Assert.NotNull(warning);
            Assert.Contains("2", warning);
        }

        [Fact]
        public void SimplifiedWarning_OneRowOrTableLayout_Silent()
        {
            var one = new[] { new CertificateCalibrationResult { CorrectionFactor = "0.95" }, new CertificateCalibrationResult() };
            var many = new[] { new CertificateCalibrationResult { CorrectionFactor = "1" }, new CertificateCalibrationResult { CorrectionFactor = "2" } };
            Assert.Null(SimplifiedResultsRules.Warning(true, one));
            Assert.Null(SimplifiedResultsRules.Warning(false, many));
            Assert.Null(SimplifiedResultsRules.Warning(true, null));
        }
    }
}
