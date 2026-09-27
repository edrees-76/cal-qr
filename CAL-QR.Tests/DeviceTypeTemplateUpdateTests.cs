using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Repositories;

namespace CAL_QR.Tests
{
    /// <summary>
    /// اختبارات UpdateTemplateAsync في DeviceTypeRepository.
    /// SQLite حقيقي — المخطّط يُنشأ من DatabaseMigrator.
    /// </summary>
    public class DeviceTypeTemplateUpdateTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly DbContextOptions<CalQrDbContext> _opts;

        public DeviceTypeTemplateUpdateTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(),
                $"cal_qr_dtupdate_{Guid.NewGuid():N}.db");
            _opts = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseSqlite($"Data Source={_dbPath}")
                .Options;
            using var ctx = new CalQrDbContext(_opts);
            DatabaseMigrator.Migrate(ctx);
        }

        public void Dispose()
        {
            try { File.Delete(_dbPath); } catch { }
        }

        private IDbContextFactory<CalQrDbContext> Factory()
            => new TestDbContextFactory(_opts);

        private class TestDbContextFactory : IDbContextFactory<CalQrDbContext>
        {
            private readonly DbContextOptions<CalQrDbContext> _options;
            public TestDbContextFactory(DbContextOptions<CalQrDbContext> options) => _options = options;
            public CalQrDbContext CreateDbContext() => new CalQrDbContext(_options);
            public Task<CalQrDbContext> CreateDbContextAsync(
                System.Threading.CancellationToken cancellationToken = default)
                => Task.FromResult(new CalQrDbContext(_options));
        }

        private async Task<int> SeedTypeAsync(string name = "Test Probe")
        {
            using var ctx = new CalQrDbContext(_opts);
            var type = new DeviceType { Name = name };
            ctx.DeviceTypes.Add(type);
            await ctx.SaveChangesAsync();
            return type.Id;
        }

        // ────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task UpdateTemplate_ScalarFields_ArePersisted()
        {
            var id = await SeedTypeAsync();
            var repo = new DeviceTypeRepository(Factory());

            var updated = new DeviceType
            {
                Id                    = id,
                Name                  = "Updated Probe",
                ProcedureNo           = "PROC-001",
                ComplianceVerdict     = "APPROVED",
                CountingTime          = "60 Sec",
                CountingUnit          = "kCPM",
                UncertaintyEnabled    = true,
                MethodologyEnabled    = false,
                CorrectedReadingFormula = "Reading × CF"
            };

            await repo.UpdateTemplateAsync(updated);

            var result = await repo.GetByIdAsync(id);
            Assert.NotNull(result);
            Assert.Equal("Updated Probe",  result!.Name);
            Assert.Equal("PROC-001",       result.ProcedureNo);
            Assert.Equal("APPROVED",       result.ComplianceVerdict);
            Assert.Equal("60 Sec",         result.CountingTime);
            Assert.True(result.UncertaintyEnabled);
            Assert.False(result.MethodologyEnabled);
            Assert.Equal("Reading × CF",   result.CorrectedReadingFormula);
        }

        [Fact]
        public async Task UpdateTemplate_FunctionalChecks_ReplaceOldOnes()
        {
            var id = await SeedTypeAsync();
            using (var ctx = new CalQrDbContext(_opts))
            {
                ctx.DeviceTypeFunctionalCheckTemplates.Add(new DeviceTypeFunctionalCheckTemplate
                    { DeviceTypeId = id, SortOrder = 1, CheckName = "Old Check" });
                await ctx.SaveChangesAsync();
            }

            var repo = new DeviceTypeRepository(Factory());
            var updated = new DeviceType { Id = id, Name = "Test Probe" };
            updated.FunctionalCheckTemplates.Add(new DeviceTypeFunctionalCheckTemplate
                { SortOrder = 1, CheckName = "New Check A", DefaultResult = "Acceptable" });
            updated.FunctionalCheckTemplates.Add(new DeviceTypeFunctionalCheckTemplate
                { SortOrder = 2, CheckName = "New Check B" });

            await repo.UpdateTemplateAsync(updated);

            var result = await repo.GetByIdWithTemplatesAsync(id);
            Assert.NotNull(result);
            var checks = result!.FunctionalCheckTemplates.OrderBy(c => c.SortOrder).ToList();
            Assert.Equal(2, checks.Count);
            Assert.Equal("New Check A", checks[0].CheckName);
            Assert.Equal("New Check B", checks[1].CheckName);
            Assert.DoesNotContain(checks, c => c.CheckName == "Old Check");
        }

        [Fact]
        public async Task UpdateTemplate_UncertaintyComponents_ReplaceOldOnes()
        {
            var id = await SeedTypeAsync();
            using (var ctx = new CalQrDbContext(_opts))
            {
                ctx.DeviceTypeUncertaintyComponentTemplates.Add(new DeviceTypeUncertaintyComponentTemplate
                    { DeviceTypeId = id, SortOrder = 1, ComponentName = "Old Component" });
                await ctx.SaveChangesAsync();
            }

            var repo = new DeviceTypeRepository(Factory());
            var updated = new DeviceType { Id = id, Name = "Test Probe" };
            updated.UncertaintyComponentTemplates.Add(new DeviceTypeUncertaintyComponentTemplate
            {
                SortOrder = 1, ComponentName = "Source", EvaluationType = "Type B", Distribution = "Normal"
            });

            await repo.UpdateTemplateAsync(updated);

            var result = await repo.GetByIdWithTemplatesAsync(id);
            Assert.NotNull(result);
            var comps = result!.UncertaintyComponentTemplates.ToList();
            Assert.Single(comps);
            Assert.Equal("Source", comps[0].ComponentName);
            Assert.Equal("Type B", comps[0].EvaluationType);
            Assert.Equal("Normal", comps[0].Distribution);
        }

        [Fact]
        public async Task UpdateTemplate_EmptyChildren_ClearsExistingTemplates()
        {
            var id = await SeedTypeAsync();
            using (var ctx = new CalQrDbContext(_opts))
            {
                ctx.DeviceTypeFunctionalCheckTemplates.Add(new DeviceTypeFunctionalCheckTemplate
                    { DeviceTypeId = id, SortOrder = 1, CheckName = "Check" });
                ctx.DeviceTypeUncertaintyComponentTemplates.Add(new DeviceTypeUncertaintyComponentTemplate
                    { DeviceTypeId = id, SortOrder = 1, ComponentName = "Component" });
                await ctx.SaveChangesAsync();
            }

            var repo = new DeviceTypeRepository(Factory());
            // تحديث بلا أبناء — يجب أن يمحو ما كان موجوداً
            await repo.UpdateTemplateAsync(new DeviceType { Id = id, Name = "Test Probe" });

            var result = await repo.GetByIdWithTemplatesAsync(id);
            Assert.NotNull(result);
            Assert.Empty(result!.FunctionalCheckTemplates);
            Assert.Empty(result.UncertaintyComponentTemplates);
        }

        [Fact]
        public async Task UpdateTemplate_NonExistentId_DoesNotThrow()
        {
            var repo = new DeviceTypeRepository(Factory());
            var ex = await Record.ExceptionAsync(() =>
                repo.UpdateTemplateAsync(new DeviceType { Id = 9999, Name = "Ghost" }));
            Assert.Null(ex);
        }

        [Fact]
        public async Task UpdateTemplate_NullableFields_SetToNull_WhenEmpty()
        {
            var id = await SeedTypeAsync();
            using (var ctx = new CalQrDbContext(_opts))
            {
                var t = await ctx.DeviceTypes.FindAsync(id);
                t!.ProcedureNo = "OLD";
                t.Notes        = "Old notes";
                await ctx.SaveChangesAsync();
            }

            var repo = new DeviceTypeRepository(Factory());
            // حقول null تُكتب كـnull
            await repo.UpdateTemplateAsync(new DeviceType { Id = id, Name = "Test Probe", ProcedureNo = null, Notes = null });

            var result = await repo.GetByIdAsync(id);
            Assert.Null(result!.ProcedureNo);
            Assert.Null(result.Notes);
        }
    }
}
