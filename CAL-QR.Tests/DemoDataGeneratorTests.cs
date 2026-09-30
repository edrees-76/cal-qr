using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CAL_QR.Data;
using CAL_QR.DemoData;
using CAL_QR.Repositories;
using CAL_QR.Services;

namespace CAL_QR.Tests
{
    public class DemoDataGeneratorTests : IDisposable
    {
        private sealed class Factory : IDbContextFactory<CalQrDbContext>
        {
            private readonly DbContextOptions<CalQrDbContext> _options;
            public Factory(string path) =>
                _options = new DbContextOptionsBuilder<CalQrDbContext>().UseSqlite($"Data Source={path}").Options;
            public CalQrDbContext CreateDbContext() => new CalQrDbContext(_options);
        }

        private readonly string _dir = Path.Combine(Path.GetTempPath(), "calqr_demo_" + Guid.NewGuid().ToString("N"));

        public DemoDataGeneratorTests() => Directory.CreateDirectory(_dir);

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(_dir, true); }
            catch (IOException) { /* ملفّ مؤقّت ما زال مقفلاً؛ لا يضرّ */ }
        }

        private static DemoDataOptions Small() => new()
        {
            Owners = 4, Devices = 6, Certificates = 12, FailedCertificates = 3
        };

        [Fact]
        public async Task Generate_CreatesExactCounts_UniqueNumbers_AndEveryCertificateVerifies()
        {
            string path = Path.Combine(_dir, "cal-qr-DEMO.db");

            var summary = await DemoDataGenerator.GenerateAsync(path, Small());

            Assert.Equal(12, summary.Certificates);
            Assert.Equal(3, summary.Failed);

            var factory = new Factory(path);
            using var db = factory.CreateDbContext();

            Assert.Equal(4, await db.Owners.CountAsync(o => o.IsSeedTestData));
            Assert.Equal(6, await db.Devices.CountAsync(d => d.IsSeedTestData));
            Assert.Equal(12, await db.CalibrationRecords.CountAsync());
            Assert.Equal(3, await db.CalibrationRecords.CountAsync(r => r.Result == "Failed"));
            Assert.Equal(9, await db.CalibrationRecords.CountAsync(r => r.Result == "Passed"));
            Assert.Equal(12, await db.Certificates.CountAsync(c => !c.IsDeleted));

            // كلّ سجلّ مرتبط برقم شهادته، والأرقام فريدة بصيغة TNRC-SSDL-YYYY-XXXX
            var numbers = await db.Certificates.Select(c => c.CertificateNumber).ToListAsync();
            Assert.Equal(12, numbers.Distinct().Count());
            Assert.All(numbers, n => Assert.StartsWith("TNRC-SSDL-", n));
            Assert.Equal(0, await db.CalibrationRecords.CountAsync(r => r.CertificateNumber == string.Empty));

            // كلّ شهادة تتحقّق بتوقيعها الحقيقيّ (المفتاح مخزَّن في القاعدة نفسها)
            var hmac = new HmacService(factory);
            hmac.Initialize();
            var repo = new CertificateRepository(factory, new CertificateNumberService(factory), new CertificateSignatureService(hmac));
            foreach (var code in await db.Certificates.Select(c => c.VerifyCode).ToListAsync())
            {
                Assert.False(string.IsNullOrEmpty(code));
                var result = await repo.VerifyByCodeAsync(code!);
                Assert.Equal(CertificateVerificationStatus.Authentic, result.Status);
            }

            // مستخدم المدير التجريبيّ موجود
            Assert.True(await db.Users.AnyAsync(u => u.Username == "admin" && u.IsActive));
        }

        [Fact]
        public async Task Generate_RefusesToOverwriteExistingFile_WithoutForce()
        {
            string path = Path.Combine(_dir, "cal-qr-DEMO.db");
            await DemoDataGenerator.GenerateAsync(path, Small());

            await Assert.ThrowsAsync<InvalidOperationException>(() => DemoDataGenerator.GenerateAsync(path, Small()));

            var again = await DemoDataGenerator.GenerateAsync(path, new DemoDataOptions
            {
                Owners = 4, Devices = 6, Certificates = 12, FailedCertificates = 3, OverwriteExisting = true
            });
            Assert.Equal(12, again.Certificates);
        }

        [Fact]
        public async Task Generate_RejectsInconsistentCounts()
        {
            string path = Path.Combine(_dir, "x.db");
            await Assert.ThrowsAsync<ArgumentException>(() => DemoDataGenerator.GenerateAsync(path,
                new DemoDataOptions { Owners = 2, Devices = 5, Certificates = 3, FailedCertificates = 1 }));
            await Assert.ThrowsAsync<ArgumentException>(() => DemoDataGenerator.GenerateAsync(path,
                new DemoDataOptions { Owners = 2, Devices = 2, Certificates = 4, FailedCertificates = 9 }));
            Assert.False(File.Exists(path));
        }
    }
}
