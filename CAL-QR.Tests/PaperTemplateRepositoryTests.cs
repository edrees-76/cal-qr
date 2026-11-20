using Xunit;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Repositories;

namespace CAL_QR.Tests
{
    public class PaperTemplateRepositoryTests
    {
        [Fact]
        public async Task TemplateRepository_CRUD_WorksCorrectly()
        {
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase(databaseName: "CalQrTestDb_Templates_" + System.Guid.NewGuid().ToString())
                .Options;

            var factory = new TestDbContextFactory(options);
            var repo = new PaperTemplateRepository(factory);

            var template = new PaperTemplate
            {
                TemplateName = "Test Template",
                PaperType = "A4",
                PaperWidthMm = 210,
                PaperHeightMm = 297,
                Columns = 3,
                Rows = 8,
                LabelWidthMm = 70,
                LabelHeightMm = 35,
                IsDefault = false
            };

            await repo.AddAsync(template);
            var all = await repo.GetAllAsync();
            Assert.Single(all);
            Assert.Equal("Test Template", all.First().TemplateName);

            template.TemplateName = "Updated Name";
            await repo.UpdateAsync(template);
            var updated = await repo.GetByIdAsync(template.Id);
            Assert.Equal("Updated Name", updated?.TemplateName);

            await repo.DeleteAsync(template.Id);
            var empty = await repo.GetAllAsync();
            Assert.Empty(empty);
        }

        [Fact]
        public async Task TemplateRepository_Update_WithDetachedCopy_KeepsCreatedAt()
        {
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase(databaseName: "CalQrTestDb_Templates_CreatedAt_" + System.Guid.NewGuid().ToString())
                .Options;
            var repo = new PaperTemplateRepository(new TestDbContextFactory(options));

            var original = new PaperTemplate { TemplateName = "T", PaperType = "A4", Columns = 2, Rows = 4 };
            await repo.AddAsync(original);
            var createdAt = (await repo.GetByIdAsync(original.Id))!.CreatedAt;

            // كما يفعل PaperTemplateViewModel: كائن جديد بالمعرّف نفسه، CreatedAt الافتراضيّ.
            var detached = new PaperTemplate
            {
                Id = original.Id,
                TemplateName = "Renamed",
                PaperType = "A4",
                Columns = 3,
                Rows = 5,
                LabelWidthMm = 60
            };
            await repo.UpdateAsync(detached);

            var after = await repo.GetByIdAsync(original.Id);
            Assert.Equal("Renamed", after!.TemplateName);
            Assert.Equal(3, after.Columns);
            Assert.Equal(60m, after.LabelWidthMm);
            Assert.Equal(createdAt, after.CreatedAt);
        }

        [Fact]
        public async Task TemplateRepository_Update_UnknownId_Throws()
        {
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase(databaseName: "CalQrTestDb_Templates_Unknown_" + System.Guid.NewGuid().ToString())
                .Options;
            var repo = new PaperTemplateRepository(new TestDbContextFactory(options));

            await Assert.ThrowsAsync<System.InvalidOperationException>(
                () => repo.UpdateAsync(new PaperTemplate { Id = 999, TemplateName = "X" }));
        }

        [Fact]
        public async Task TemplateRepository_SetDefault_UnsetsOtherDefaults()
        {
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase(databaseName: "CalQrTestDb_Templates_Default_" + System.Guid.NewGuid().ToString())
                .Options;

            var factory = new TestDbContextFactory(options);
            var repo = new PaperTemplateRepository(factory);

            var template1 = new PaperTemplate
            {
                TemplateName = "Template 1",
                PaperType = "A4",
                PaperWidthMm = 210,
                PaperHeightMm = 297,
                Columns = 3,
                Rows = 8,
                LabelWidthMm = 70,
                LabelHeightMm = 35,
                IsDefault = true
            };

            var template2 = new PaperTemplate
            {
                TemplateName = "Template 2",
                PaperType = "A4",
                PaperWidthMm = 210,
                PaperHeightMm = 297,
                Columns = 3,
                Rows = 8,
                LabelWidthMm = 70,
                LabelHeightMm = 35,
                IsDefault = false
            };

            await repo.AddAsync(template1);
            await repo.AddAsync(template2);

            var currentDefault = await repo.GetDefaultAsync();
            Assert.Equal("Template 1", currentDefault?.TemplateName);

            await repo.SetDefaultAsync(template2.Id);

            var newDefault = await repo.GetDefaultAsync();
            Assert.Equal("Template 2", newDefault?.TemplateName);

            var t1 = await repo.GetByIdAsync(template1.Id);
            Assert.False(t1?.IsDefault);
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
