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

                    // Assert Excel Detailed columns structure
                    using (var workbook = new ClosedXML.Excel.XLWorkbook(excelDetPath))
                    {
                        var ws = workbook.Worksheet(1);
                        Assert.Equal("ت", ws.Cell(6, 1).GetString());
                        Assert.Equal("رقم الشهادة", ws.Cell(6, 2).GetString());
                        Assert.Equal("الجهة المالكة", ws.Cell(6, 3).GetString());
                        Assert.Equal("نوع الجهاز", ws.Cell(6, 4).GetString());
                        Assert.Equal("الموديل", ws.Cell(6, 5).GetString());
                        Assert.Equal("الرقم التسلسلي", ws.Cell(6, 6).GetString());
                        Assert.Equal("تاريخ المعايرة", ws.Cell(6, 7).GetString());
                        Assert.Equal("تاريخ الانتهاء", ws.Cell(6, 8).GetString());
                        Assert.Equal("النتيجة", ws.Cell(6, 9).GetString());
                        Assert.Equal("المهندس المعايِر", ws.Cell(6, 10).GetString());
                        Assert.Equal("التفاصيل", ws.Cell(6, 11).GetString());
                        
                        // Check that HmacSignature ("التوقيع الرقمي") is completely missing
                        for (int col = 1; col <= 15; col++)
                        {
                            Assert.NotEqual("التوقيع الرقمي", ws.Cell(6, col).GetString());
                        }

                        // Check record index is populated
                        Assert.Equal("1", ws.Cell(7, 1).GetString());
                    }

                    // Assert Excel Summary columns structure
                    using (var workbook = new ClosedXML.Excel.XLWorkbook(excelSumPath))
                    {
                        var ws = workbook.Worksheet(1);
                        Assert.Equal("ت", ws.Cell(6, 1).GetString());
                        Assert.Equal("رقم الشهادة", ws.Cell(6, 2).GetString());
                        Assert.Equal("الجهة المالكة", ws.Cell(6, 3).GetString());
                        Assert.Equal("الموديل", ws.Cell(6, 4).GetString());
                        Assert.Equal("الرقم التسلسلي", ws.Cell(6, 5).GetString());
                        Assert.Equal("تاريخ الانتهاء", ws.Cell(6, 6).GetString());
                        Assert.Equal("النتيجة", ws.Cell(6, 7).GetString());
                        Assert.Equal("الحالة", ws.Cell(6, 8).GetString());
                        
                        // Check record index is populated
                        Assert.Equal("1", ws.Cell(7, 1).GetString());
                    }
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

        [Fact]
        public async Task ExportService_PerformanceExcelDetailedColumns_AreCorrect()
        {
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase(databaseName: "CalQrTestDb_Perf_" + Guid.NewGuid().ToString())
                .Options;

            var factory = new TestDbContextFactory(options);

            using (var context = new CalQrDbContext(options))
            {
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
                    CertificateNumber = "C-PERF-TEST",
                    CalibrationDate = DateTime.Today,
                    ExpiryDate = DateTime.Today.AddYears(1),
                    Result = "Passed",
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

                var reportData = new PerformanceReportData
                {
                    StartDate = DateTime.Today.AddMonths(-1),
                    EndDate = DateTime.Today,
                    TotalRecords = records.Count,
                    PassedCount = records.Count(r => r.Result == "Passed"),
                    FailedCount = 0,
                    ConditionalCount = 0,
                    PassedPercent = 100,
                    FailedPercent = 0,
                    ConditionalPercent = 0,
                    ByOwner = records.GroupBy(r => r.Device!.Owner!.Name).Select(g => new DistributionItem { Name = g.Key, Count = g.Count() }).ToList(),
                    ByDeviceType = records.GroupBy(r => r.Device!.DeviceType!.Name).Select(g => new DistributionItem { Name = g.Key, Count = g.Count() }).ToList(),
                    ByEngineer = records.GroupBy(r => r.EngineerName).Select(g => new DistributionItem { Name = g.Key, Count = g.Count() }).ToList(),
                    NewDevices = 1,
                    NewOwners = 1,
                    NewDeviceTypes = 1,
                    IsDetailed = true,
                    Records = records
                };

                string tempDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PerfTestsTemp");
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }

                string excelPerfPath = Path.Combine(tempDir, "test_perf.xlsx");

                try
                {
                    await service.ExportPerformanceReportToExcelAsync(reportData, excelPerfPath);

                    Assert.True(File.Exists(excelPerfPath), "Performance Excel file should be created");

                    using (var workbook = new ClosedXML.Excel.XLWorkbook(excelPerfPath))
                    {
                        var ws = workbook.Worksheet(1);
                        
                        // Dynamically locate the detailed table start by looking for cell with value "ت"
                        int headerRow = -1;
                        for (int r = 1; r <= 100; r++)
                        {
                            if (ws.Cell(r, 1).GetString() == "ت")
                            {
                                headerRow = r;
                                break;
                            }
                        }

                        Assert.True(headerRow > 0, "Detailed table header row containing 'ت' should be found");
                        
                        // Verify headers
                        Assert.Equal("ت", ws.Cell(headerRow, 1).GetString());
                        Assert.Equal("رقم الشهادة", ws.Cell(headerRow, 2).GetString());
                        Assert.Equal("الجهة المالكة", ws.Cell(headerRow, 3).GetString());
                        Assert.Equal("الموديل", ws.Cell(headerRow, 4).GetString());
                        Assert.Equal("الرقم التسلسلي", ws.Cell(headerRow, 5).GetString());
                        Assert.Equal("تاريخ المعايرة", ws.Cell(headerRow, 6).GetString());
                        Assert.Equal("تاريخ الانتهاء", ws.Cell(headerRow, 7).GetString());
                        Assert.Equal("النتيجة", ws.Cell(headerRow, 8).GetString());
                        Assert.Equal("المهندس المعايِر", ws.Cell(headerRow, 9).GetString());

                        // Verify values
                        Assert.Equal("1", ws.Cell(headerRow + 1, 1).GetString());
                        Assert.Equal("C-PERF-TEST", ws.Cell(headerRow + 1, 2).GetString());
                        Assert.Equal("جهة فحص تجريبية", ws.Cell(headerRow + 1, 3).GetString());
                    }
                }
                finally
                {
                    if (File.Exists(excelPerfPath)) File.Delete(excelPerfPath);
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
