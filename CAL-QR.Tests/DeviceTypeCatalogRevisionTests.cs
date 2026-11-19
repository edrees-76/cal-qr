using System;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CAL_QR.Data;
using CAL_QR.Models;

namespace CAL_QR.Tests
{
    /// <summary>
    /// مراجعة نصوص الفحوص الوظيفيّة إلى قوالب رضا الجديدة (Dose Rate Meter · Gamma · PED)
    /// على القواعد القائمة: تُستبدل الصفوف المطابقة لبصمة المراجعة السابقة وحدها، وما عدّله
    /// المستخدم يُترك ويُبلَّغ عنه. SQLite حقيقي كنظيرتها DeviceTypeTemplateSeedingTests.
    /// </summary>
    public class DeviceTypeCatalogRevisionTests
    {
        private const string DoseRateMeter = "Dose Rate Meter";

        private static string NewDbPath(string tag) =>
            Path.Combine(Path.GetTempPath(), $"cal_qr_devtype_revision_{tag}_{Guid.NewGuid():N}.db");

        private static DbContextOptions<CalQrDbContext> OptionsFor(string dbPath) =>
            new DbContextOptionsBuilder<CalQrDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

        private static void CleanUp(string dbPath)
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                try { File.Delete(dbPath); } catch (IOException) { }
            }
        }

        private static DeviceTypeDefinition Def(string name) =>
            DeviceTypeCatalog.All.Single(d => d.Name == name);

        /// <summary>قاعدة مهاجَرة ثمّ معادة إلى حال ما قبل المراجعة: فحوص النوع بالنصوص القديمة وعلم المراجعة ممحوّ.</summary>
        private static void PrepareOldDatabase(DbContextOptions<CalQrDbContext> options, string typeName,
            Func<DeviceTypeDefinition, CatalogFunctionalCheck[]> rowsToPlant)
        {
            using var context = new CalQrDbContext(options);
            DatabaseMigrator.RunMigrations(context);

            var type = context.DeviceTypes.Single(t => t.Name == typeName);
            context.DeviceTypeFunctionalCheckTemplates.RemoveRange(
                context.DeviceTypeFunctionalCheckTemplates.Where(t => t.DeviceTypeId == type.Id).ToList());

            foreach (var c in rowsToPlant(Def(typeName)))
            {
                context.DeviceTypeFunctionalCheckTemplates.Add(new DeviceTypeFunctionalCheckTemplate
                {
                    DeviceTypeId = type.Id,
                    SortOrder = c.SortOrder,
                    CheckName = c.CheckName,
                    Requirement = c.Requirement,
                    DefaultResult = c.DefaultResult
                });
            }

            context.AppSettings.RemoveRange(context.AppSettings.Where(s =>
                s.Key == DeviceTypeSeeder.RevisionFlagKey || s.Key == DeviceTypeSeeder.ConflictsKey).ToList());
            context.SaveChanges();
        }

        private static string[] CheckNames(CalQrDbContext context, string typeName)
        {
            var type = context.DeviceTypes.Single(t => t.Name == typeName);
            return context.DeviceTypeFunctionalCheckTemplates
                .Where(t => t.DeviceTypeId == type.Id)
                .OrderBy(t => t.SortOrder)
                .Select(t => t.CheckName)
                .ToArray();
        }

        [Fact]
        public void Catalog_RevisedTypes_HaveDistinctPreviousFingerprint()
        {
            var revised = DeviceTypeCatalog.All.Where(d => d.PreviousFunctionalChecks.Length > 0)
                .Select(d => d.Name).OrderBy(n => n).ToArray();

            Assert.Equal(new[] { DoseRateMeter, "Gamma Scintillation Probe", "Personal Electronic Dosimeter (PED)" }, revised);

            foreach (var name in revised)
            {
                var d = Def(name);
                Assert.Equal(5, d.FunctionalChecks.Length);
                Assert.Equal(5, d.PreviousFunctionalChecks.Length);
                Assert.False(DeviceTypeSeeder.FunctionalChecksMatch(
                    d.PreviousFunctionalChecks.Select(c => new DeviceTypeFunctionalCheckTemplate
                    {
                        SortOrder = c.SortOrder, CheckName = c.CheckName, Requirement = c.Requirement, DefaultResult = c.DefaultResult
                    }).ToList(),
                    d.FunctionalChecks));
            }
        }

        [Fact]
        public void FreshDatabase_SeedsNewTexts_SetsFlag_NoNotice()
        {
            string dbPath = NewDbPath("fresh");
            var options = OptionsFor(dbPath);
            try
            {
                using (var context = new CalQrDbContext(options))
                {
                    DatabaseMigrator.RunMigrations(context);
                }

                using (var context = new CalQrDbContext(options))
                {
                    Assert.Equal(Def(DoseRateMeter).FunctionalChecks.Select(c => c.CheckName).ToArray(),
                        CheckNames(context, DoseRateMeter));
                    Assert.Equal("true", context.AppSettings.Single(s => s.Key == DeviceTypeSeeder.RevisionFlagKey).Value);
                    Assert.Null(context.AppSettings.SingleOrDefault(s => s.Key == DeviceTypeSeeder.ConflictsKey));
                }
            }
            finally { CleanUp(dbPath); }
        }

        [Fact]
        public void OldUntouchedChecks_AreReplaced_AndAdminIsNotified()
        {
            string dbPath = NewDbPath("old");
            var options = OptionsFor(dbPath);
            try
            {
                PrepareOldDatabase(options, DoseRateMeter, d => d.PreviousFunctionalChecks);

                using (var context = new CalQrDbContext(options))
                {
                    var result = DeviceTypeSeeder.ReviseCatalogFunctionalChecksIfNeeded(context);
                    Assert.Null(result.Failure);
                    Assert.Equal(new[] { DoseRateMeter }, result.RevisedFunctionalChecks.ToArray());
                    Assert.Empty(result.RevisionSkippedTypes);
                }

                using (var context = new CalQrDbContext(options))
                {
                    var type = context.DeviceTypes.Single(t => t.Name == DoseRateMeter);
                    var rows = context.DeviceTypeFunctionalCheckTemplates
                        .Where(t => t.DeviceTypeId == type.Id).OrderBy(t => t.SortOrder).ToList();
                    Assert.True(DeviceTypeSeeder.FunctionalChecksMatch(rows, Def(DoseRateMeter).FunctionalChecks));

                    var notice = context.AppSettings.Single(s => s.Key == DeviceTypeSeeder.ConflictsKey).Value;
                    Assert.Contains($"«{DoseRateMeter}»", notice);
                    Assert.Equal("true", context.AppSettings.Single(s => s.Key == DeviceTypeSeeder.RevisionFlagKey).Value);
                }
            }
            finally { CleanUp(dbPath); }
        }

        [Fact]
        public void UserEditedChecks_AreKept_AndReportedAsSkipped()
        {
            string dbPath = NewDbPath("edited");
            var options = OptionsFor(dbPath);
            try
            {
                // المستخدم غيّر متطلّب الفحص الأوّل في النسخة القديمة.
                PrepareOldDatabase(options, DoseRateMeter, d => d.PreviousFunctionalChecks
                    .Select((c, i) => i == 0
                        ? new CatalogFunctionalCheck { SortOrder = c.SortOrder, CheckName = c.CheckName, Requirement = "Lab-specific limit", DefaultResult = c.DefaultResult }
                        : c)
                    .ToArray());

                using (var context = new CalQrDbContext(options))
                {
                    var result = DeviceTypeSeeder.ReviseCatalogFunctionalChecksIfNeeded(context);
                    Assert.Empty(result.RevisedFunctionalChecks);
                    Assert.Equal(new[] { DoseRateMeter }, result.RevisionSkippedTypes.ToArray());
                }

                using (var context = new CalQrDbContext(options))
                {
                    var type = context.DeviceTypes.Single(t => t.Name == DoseRateMeter);
                    Assert.Equal("Lab-specific limit", context.DeviceTypeFunctionalCheckTemplates
                        .Where(t => t.DeviceTypeId == type.Id).OrderBy(t => t.SortOrder).First().Requirement);
                    Assert.Contains("معدَّلة يدويّاً",
                        context.AppSettings.Single(s => s.Key == DeviceTypeSeeder.ConflictsKey).Value);
                }
            }
            finally { CleanUp(dbPath); }
        }

        [Fact]
        public void ExistingUnacknowledgedNotice_IsPreserved_RevisionIsAppended()
        {
            string dbPath = NewDbPath("append");
            var options = OptionsFor(dbPath);
            try
            {
                PrepareOldDatabase(options, DoseRateMeter, d => d.PreviousFunctionalChecks);

                using (var context = new CalQrDbContext(options))
                {
                    context.AppSettings.Add(new AppSetting { Key = DeviceTypeSeeder.ConflictsKey, Value = "تعارض سابق", UpdatedAt = DateTime.UtcNow });
                    context.SaveChanges();
                }

                using (var context = new CalQrDbContext(options))
                {
                    DeviceTypeSeeder.ReviseCatalogFunctionalChecksIfNeeded(context);
                }

                using (var context = new CalQrDbContext(options))
                {
                    var notice = context.AppSettings.Single(s => s.Key == DeviceTypeSeeder.ConflictsKey).Value;
                    Assert.StartsWith("تعارض سابق\n", notice);
                    Assert.Contains($"«{DoseRateMeter}»", notice);
                }
            }
            finally { CleanUp(dbPath); }
        }

        [Fact]
        public void SecondRun_IsNoOp_EvenIfFlagRemoved()
        {
            string dbPath = NewDbPath("idempotent");
            var options = OptionsFor(dbPath);
            try
            {
                PrepareOldDatabase(options, DoseRateMeter, d => d.PreviousFunctionalChecks);

                using (var context = new CalQrDbContext(options))
                {
                    DeviceTypeSeeder.ReviseCatalogFunctionalChecksIfNeeded(context);
                    context.AppSettings.RemoveRange(context.AppSettings.Where(s =>
                        s.Key == DeviceTypeSeeder.RevisionFlagKey || s.Key == DeviceTypeSeeder.ConflictsKey).ToList());
                    context.SaveChanges();
                }

                using (var context = new CalQrDbContext(options))
                {
                    var result = DeviceTypeSeeder.ReviseCatalogFunctionalChecksIfNeeded(context);
                    Assert.False(result.HasDiagnostics);
                    Assert.Equal(5, CheckNames(context, DoseRateMeter).Length);
                }
            }
            finally { CleanUp(dbPath); }
        }

        [Fact]
        public void TypeWithoutChecks_IsLeftEmpty()
        {
            string dbPath = NewDbPath("empty");
            var options = OptionsFor(dbPath);
            try
            {
                PrepareOldDatabase(options, DoseRateMeter, _ => Array.Empty<CatalogFunctionalCheck>());

                using (var context = new CalQrDbContext(options))
                {
                    var result = DeviceTypeSeeder.ReviseCatalogFunctionalChecksIfNeeded(context);
                    Assert.False(result.HasDiagnostics);
                    Assert.Empty(CheckNames(context, DoseRateMeter));
                }
            }
            finally { CleanUp(dbPath); }
        }
    }
}
