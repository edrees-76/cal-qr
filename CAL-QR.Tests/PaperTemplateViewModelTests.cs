using Xunit;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Repositories;
using CAL_QR.ViewModels;
using System.Linq;

namespace CAL_QR.Tests
{
    public class PaperTemplateViewModelTests
    {
        public PaperTemplateViewModelTests()
        {
            // Inject a mock message box handler to prevent blocking UI dialogs in headless test execution
            PaperTemplateViewModel.MessageBoxShowMock = (message, caption, button, image) => { };
        }

        [Fact]
        public async Task TemplateViewModel_Validation_Fails_WhenWidthExceeded()
        {
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase(databaseName: "CalQrTestDb_VM_Width_" + System.Guid.NewGuid().ToString())
                .Options;

            var factory = new TestDbContextFactory(options);
            var repo = new PaperTemplateRepository(factory);
            var vm = new PaperTemplateViewModel(repo);

            vm.TemplateName = "Test Template";
            vm.PaperType = "Custom";
            vm.PaperWidth = 200; // 200 mm
            vm.PaperHeight = 300;
            vm.Columns = 3;
            vm.Rows = 5;
            vm.LabelWidth = 60; // 3 * 60 = 180
            vm.LabelHeight = 40;
            vm.MarginLeft = 10;
            vm.MarginRight = 20; // 10 + 180 + 20 = 210 > 200
            vm.GapHorizontal = 5; // 2 * 5 = 10 -> Total 10 + 180 + 10 + 20 = 220 > 200

            await vm.SaveAsync();

            var all = await repo.GetAllAsync();
            Assert.Empty(all);
        }

        [Fact]
        public async Task TemplateViewModel_Validation_Fails_WhenHeightExceeded()
        {
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase(databaseName: "CalQrTestDb_VM_Height_" + System.Guid.NewGuid().ToString())
                .Options;

            var factory = new TestDbContextFactory(options);
            var repo = new PaperTemplateRepository(factory);
            var vm = new PaperTemplateViewModel(repo);

            vm.TemplateName = "Test Template";
            vm.PaperType = "Custom";
            vm.PaperWidth = 200;
            vm.PaperHeight = 200; // 200 mm
            vm.Columns = 1;
            vm.Rows = 3;
            vm.LabelWidth = 100;
            vm.LabelHeight = 60; // 3 * 60 = 180
            vm.MarginTop = 15;
            vm.MarginBottom = 15; // 15 + 180 + 15 = 210 > 200

            await vm.SaveAsync();

            var all = await repo.GetAllAsync();
            Assert.Empty(all);
        }

        [Fact]
        public async Task TemplateViewModel_Save_Succeeds_WhenValid()
        {
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase(databaseName: "CalQrTestDb_VM_Success_" + System.Guid.NewGuid().ToString())
                .Options;

            var factory = new TestDbContextFactory(options);
            var repo = new PaperTemplateRepository(factory);
            var vm = new PaperTemplateViewModel(repo);

            vm.TemplateName = "Valid Template";
            vm.PaperType = "Custom";
            vm.PaperWidth = 210;
            vm.PaperHeight = 297;
            vm.Columns = 2;
            vm.Rows = 5;
            vm.LabelWidth = 90; // 180
            vm.LabelHeight = 50; // 250
            vm.MarginLeft = 10;
            vm.MarginRight = 10; // 10 + 180 + 10 = 200 <= 210
            vm.MarginTop = 15;
            vm.MarginBottom = 15; // 15 + 250 + 15 = 280 <= 297
            vm.GapHorizontal = 5; // 1 * 5 = 5 -> Total 10 + 180 + 5 + 10 = 205 <= 210

            await vm.SaveAsync();

            var all = await repo.GetAllAsync();
            Assert.Single(all);
            var saved = all.Single();
            Assert.Equal("Valid Template", saved.TemplateName);
            Assert.Equal(10, (double)saved.MarginRightMm);
            Assert.Equal(15, (double)saved.MarginBottomMm);
        }

        [Fact]
        public async Task TemplateViewModel_BackwardCompatibility_LoadsCorrectly()
        {
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase(databaseName: "CalQrTestDb_VM_Compat_" + System.Guid.NewGuid().ToString())
                .Options;

            var factory = new TestDbContextFactory(options);
            var repo = new PaperTemplateRepository(factory);

            // Seed template without MarginRightMm / MarginBottomMm (defaulting to 0)
            var oldTemplate = new PaperTemplate
            {
                TemplateName = "Old Template",
                PaperType = "A4",
                PaperWidthMm = 210,
                PaperHeightMm = 297,
                Columns = 3,
                Rows = 8,
                LabelWidthMm = 70,
                LabelHeightMm = 35,
                MarginLeftMm = 0,
                MarginTopMm = 0,
                IsDefault = false
            };
            await repo.AddAsync(oldTemplate);

            var vm = new PaperTemplateViewModel(repo);
            vm.LoadTemplate(oldTemplate.Id);

            Assert.Equal("Old Template", vm.TemplateName);
            Assert.Equal(0, vm.MarginRight);
            Assert.Equal(0, vm.MarginBottom);
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
