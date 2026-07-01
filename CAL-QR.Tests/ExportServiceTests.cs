using Xunit;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Services;

namespace CAL_QR.Tests
{
    public class ExportServiceTests
    {
        [Fact]
        public async Task ExportService_ExportsExcelAndPdf_FilesExist()
        {
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase(databaseName: "CalQrTestDb_Export_" + Guid.NewGuid().ToString())
                .Options;

            var factory = new TestDbContextFactory(options);

            using (var context = new CalQrDbContext(options))
            {
                context.AppSettings.Add(new AppSetting { Key = "AlertDaysThreshold", Value = "30" });

                var owner = new Owner { Name = "جهة فحص تجريبية" };
                context.Owners.Add(owner);

                var type = new DeviceType { Name = "جهاز قياس إشعاع" };
                context.DeviceTypes.Add(type);
                await context.SaveChangesAsync();

                var device = new Device { Model = "X-Model", SerialNumber = "SN-999", OwnerId = owner.Id, DeviceTypeId = type.Id };
                context.Devices.Add(device);
                await context.SaveChangesAsync();

                context.CalibrationRecords.Add(new CalibrationRecord
                {
                    DeviceId = device.Id,
                    CertificateNumber = "C-EXPORT-TEST",
                    CalibrationDate = DateTime.Today,
                    ExpiryDate = DateTime.Today.AddYears(1),
                    Result = "سار",
                    EngineerName = "إدريس"
                });
                await context.SaveChangesAsync();
            }

            using (var context = new CalQrDbContext(options))
            {
                var records = await context.CalibrationRecords
                    .Include(r => r.Device)
                        .ThenInclude(d => d!.Owner)
                    .Include(r => r.Device)
                        .ThenInclude(d => d!.DeviceType)
                    .ToListAsync();

                var service = new ExportService(factory);

                string tempDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ExportTestsTemp");
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }

                string excelDetPath = Path.Combine(tempDir, "test_detailed.xlsx");
                string excelSumPath = Path.Combine(tempDir, "test_summary.xlsx");
                string pdfDetPath = Path.Combine(tempDir, "test_detailed.pdf");
                string pdfSumPath = Path.Combine(tempDir, "test_summary.pdf");

                try
                {
                    await service.ExportToExcelAsync(records, "Detailed", excelDetPath);
                    await service.ExportToExcelAsync(records, "Summary", excelSumPath);
                    await service.ExportToPdfAsync(records, "Detailed", pdfDetPath);
                    await service.ExportToPdfAsync(records, "Summary", pdfSumPath);

                    Assert.True(File.Exists(excelDetPath), "Excel detailed file should be created");
                    Assert.True(File.Exists(excelSumPath), "Excel summary file should be created");
                    Assert.True(File.Exists(pdfDetPath), "PDF detailed file should be created");
                    Assert.True(File.Exists(pdfSumPath), "PDF summary file should be created");
                }
                finally
                {
                    if (File.Exists(excelDetPath)) File.Delete(excelDetPath);
                    if (File.Exists(excelSumPath)) File.Delete(excelSumPath);
                    if (File.Exists(pdfDetPath)) File.Delete(pdfDetPath);
                    if (File.Exists(pdfSumPath)) File.Delete(pdfSumPath);
                    if (Directory.Exists(tempDir)) Directory.Delete(tempDir);
                }
            }
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
