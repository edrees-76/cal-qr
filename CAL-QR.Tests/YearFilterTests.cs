using Xunit;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Data;
using CAL_QR.Models;

namespace CAL_QR.Tests
{
    public class YearFilterTests
    {
        private class TestDbContextFactory : IDbContextFactory<CalQrDbContext>
        {
            private readonly DbContextOptions<CalQrDbContext> _options;
            public TestDbContextFactory(DbContextOptions<CalQrDbContext> options)
            {
                _options = options;
            }
            public CalQrDbContext CreateDbContext() => new CalQrDbContext(_options);
        }

        private static async Task<DbContextOptions<CalQrDbContext>> SeedAsync(string dbName)
        {
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase(databaseName: dbName + "_" + Guid.NewGuid().ToString())
                .Options;

            using (var context = new CalQrDbContext(options))
            {
                var owner = new Owner { Name = "Owner A" };
                var type = new DeviceType { Name = "Type A" };
                context.Owners.Add(owner);
                context.DeviceTypes.Add(type);
                await context.SaveChangesAsync();

                var device = new Device { Model = "Ludlum", SerialNumber = "SN-YEAR", OwnerId = owner.Id, DeviceTypeId = type.Id };
                context.Devices.Add(device);
                await context.SaveChangesAsync();

                // 2022: one record. 2024: two records (duplicate year). 2021: soft-deleted only.
                context.CalibrationRecords.AddRange(
                    new CalibrationRecord
                    {
                        DeviceId = device.Id,
                        CertificateNumber = "CERT-2022",
                        CalibrationDate = new DateTime(2022, 5, 10),
                        ExpiryDate = new DateTime(2023, 5, 10),
                        Result = "Passed",
                        HmacSignature = "H1"
                    },
                    new CalibrationRecord
                    {
                        DeviceId = device.Id,
                        CertificateNumber = "CERT-2024-A",
                        CalibrationDate = new DateTime(2024, 1, 20),
                        ExpiryDate = new DateTime(2025, 1, 20),
                        Result = "Passed",
                        HmacSignature = "H2"
                    },
                    new CalibrationRecord
                    {
                        DeviceId = device.Id,
                        CertificateNumber = "CERT-2024-B",
                        CalibrationDate = new DateTime(2024, 9, 3),
                        ExpiryDate = new DateTime(2025, 9, 3),
                        Result = "Failed",
                        HmacSignature = "H3"
                    },
                    new CalibrationRecord
                    {
                        DeviceId = device.Id,
                        CertificateNumber = "CERT-2021-DELETED",
                        CalibrationDate = new DateTime(2021, 3, 1),
                        ExpiryDate = new DateTime(2022, 3, 1),
                        Result = "Passed",
                        HmacSignature = "H4",
                        IsDeleted = true
                    });
                await context.SaveChangesAsync();
            }

            return options;
        }

        [Fact]
        public async Task AvailableYears_ReturnsDistinctExistingYearsDescending_ExcludingSoftDeleted()
        {
            var options = await SeedAsync("CalQrTestDb_YearFilter_Distinct");
            var factory = new TestDbContextFactory(options);

            using (var context = factory.CreateDbContext())
            {
                var years = await context.CalibrationRecords
                    .AsNoTracking()
                    .Where(r => !r.IsDeleted && !r.Device!.IsDeleted)
                    .Select(r => r.CalibrationDate.Year)
                    .Distinct()
                    .OrderByDescending(y => y)
                    .ToListAsync();

                Assert.Equal(new[] { 2024, 2022 }, years);
                Assert.DoesNotContain(2021, years);
            }
        }

        [Fact]
        public async Task YearFilter_ReturnsOnlyRecordsOfSelectedYear_WithCorrectTotalCount()
        {
            var options = await SeedAsync("CalQrTestDb_YearFilter_Filtering");
            var factory = new TestDbContextFactory(options);

            using (var context = factory.CreateDbContext())
            {
                int? selectedYear = 2024;

                IQueryable<CalibrationRecord> query = context.CalibrationRecords
                    .AsNoTracking()
                    .Include(r => r.Device)
                    .Where(r => !r.IsDeleted && !r.Device!.IsDeleted);

                if (selectedYear.HasValue)
                {
                    query = query.Where(r => r.CalibrationDate.Year == selectedYear.Value);
                }

                int totalCount = await query.CountAsync();
                var records = await query.ToListAsync();

                Assert.Equal(2, totalCount);
                Assert.All(records, r => Assert.Equal(2024, r.CalibrationDate.Year));
                Assert.Contains(records, r => r.CertificateNumber == "CERT-2024-A");
                Assert.Contains(records, r => r.CertificateNumber == "CERT-2024-B");
                Assert.DoesNotContain(records, r => r.CertificateNumber == "CERT-2022");
            }
        }

        [Fact]
        public void YearFilter_IsTranslatedToSql_NotEvaluatedClientSide()
        {
            // Real SQLite provider (not InMemory): only it produces the production SQL.
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options;

            using (var context = new CalQrDbContext(options))
            {
                int? selectedYear = 2024;

                IQueryable<CalibrationRecord> query = context.CalibrationRecords
                    .AsNoTracking()
                    .Include(r => r.Device)
                    .Where(r => !r.IsDeleted && !r.Device!.IsDeleted);

                query = query.Where(r => r.CalibrationDate.Year == selectedYear!.Value);

                string sql = query.ToQueryString();

                Assert.Contains("strftime", sql);
            }
        }

        // Mirrors DevicesViewModel.ComputeDefaultYear (private): current year when present,
        // otherwise the newest available year, otherwise null.
        private static int? ComputeDefaultYear(System.Collections.Generic.List<int> years)
        {
            int currentYear = DateTime.Today.Year;
            return years.Contains(currentYear)
                ? currentYear
                : (years.Count > 0 ? years[0] : (int?)null);
        }

        [Fact]
        public void ComputeDefaultYear_PrefersCurrentYear_ThenNewest_ThenNull()
        {
            int currentYear = DateTime.Today.Year;

            // 1. Current year present -> current year
            var withCurrent = new System.Collections.Generic.List<int> { currentYear, currentYear - 2, currentYear - 5 };
            Assert.Equal(currentYear, ComputeDefaultYear(withCurrent));

            // 2. Current year missing -> newest available (list is ordered descending)
            var withoutCurrent = new System.Collections.Generic.List<int> { currentYear - 2, currentYear - 5 };
            Assert.Equal(currentYear - 2, ComputeDefaultYear(withoutCurrent));

            // 3. Empty list -> null
            var empty = new System.Collections.Generic.List<int>();
            Assert.Null(ComputeDefaultYear(empty));
        }
    }
}
