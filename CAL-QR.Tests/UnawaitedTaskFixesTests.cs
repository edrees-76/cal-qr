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
using CAL_QR.ViewModels;

namespace CAL_QR.Tests
{
    /// <summary>
    /// بقايا TEST-01: حفظ آخر طابعة/قالب صار مُنتظَراً وفشله لا يُلقى، وفشل تدقيق رفع عدّاد
    /// الشهادات منفصل عن فشل تطبيق الحدّ نفسه.
    /// </summary>
    public class UnawaitedTaskFixesTests
    {
        [Fact]
        public async Task TrySaveLastPrintSettings_UpdatesPrinterAndTemplate()
        {
            var factory = NewFactoryWithSettings();

            await PrintPreviewViewModel.TrySaveLastPrintSettingsAsync(factory, "Zebra ZD420", 7);

            using var context = factory.CreateDbContext();
            Assert.Equal("Zebra ZD420", context.AppSettings.Single(s => s.Key == "LastPrinterName").Value);
            Assert.Equal("7", context.AppSettings.Single(s => s.Key == "LastTemplateId").Value);
        }

        [Fact]
        public async Task TrySaveLastPrintSettings_NoTemplate_KeepsStoredTemplate()
        {
            var factory = NewFactoryWithSettings();

            await PrintPreviewViewModel.TrySaveLastPrintSettingsAsync(factory, "P1", null);

            using var context = factory.CreateDbContext();
            Assert.Equal("P1", context.AppSettings.Single(s => s.Key == "LastPrinterName").Value);
            Assert.Equal("3", context.AppSettings.Single(s => s.Key == "LastTemplateId").Value);
        }

        [Fact]
        public async Task TrySaveLastPrintSettings_DatabaseFailure_DoesNotPropagate()
        {
            var ex = await Record.ExceptionAsync(() =>
                PrintPreviewViewModel.TrySaveLastPrintSettingsAsync(new ThrowingFactory(), "P1", 1));

            Assert.Null(ex);
        }

        [Fact]
        public void TryLogRaise_WritesOneEntryAndReturnsTrue()
        {
            var audit = new Mock<IAuditLogRepository>();

            bool logged = CertificateCounterFloor.TryLogRaise(audit.Object, "سنة 2026: من 5 إلى 9");

            Assert.True(logged);
            audit.Verify(a => a.LogAsync(
                "رفع عدّاد الشهادات", "نظام", "CertificateSequence", "سنة 2026: من 5 إلى 9",
                It.IsAny<int?>(), It.IsAny<string?>()), Times.Once);
        }

        [Fact]
        public void TryLogRaise_AuditFailure_ReturnsFalseWithoutThrowing()
        {
            var audit = new Mock<IAuditLogRepository>();
            audit.Setup(a => a.LogAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<int?>(), It.IsAny<string?>()))
                .ThrowsAsync(new InvalidOperationException("db down"));

            bool logged = true;
            var ex = Record.Exception(() => logged = CertificateCounterFloor.TryLogRaise(audit.Object, "x"));

            Assert.Null(ex);
            Assert.False(logged);
        }

        private static IDbContextFactory<CalQrDbContext> NewFactoryWithSettings()
        {
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase("CalQrTestDb_PrintSettings_" + Guid.NewGuid())
                .Options;
            var factory = new InMemoryFactory(options);
            using (var context = factory.CreateDbContext())
            {
                context.AppSettings.Add(new AppSetting { Key = "LastPrinterName", Value = "" });
                context.AppSettings.Add(new AppSetting { Key = "LastTemplateId", Value = "3" });
                context.SaveChanges();
            }
            return factory;
        }

        private sealed class InMemoryFactory : IDbContextFactory<CalQrDbContext>
        {
            private readonly DbContextOptions<CalQrDbContext> _options;
            public InMemoryFactory(DbContextOptions<CalQrDbContext> options) => _options = options;
            public CalQrDbContext CreateDbContext() => new CalQrDbContext(_options);
        }

        private sealed class ThrowingFactory : IDbContextFactory<CalQrDbContext>
        {
            public CalQrDbContext CreateDbContext() => throw new InvalidOperationException("database is locked");
        }
    }
}
