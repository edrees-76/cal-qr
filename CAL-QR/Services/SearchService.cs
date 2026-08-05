using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Validation;

namespace CAL_QR.Services
{
    public class SearchService : ISearchService
    {
        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;

        public SearchService(IDbContextFactory<CalQrDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<List<SearchResultItem>> SearchAsync(string query, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return new List<SearchResultItem>();
            }

            var normalizedQuery = query.Trim().ToLower();

            // Task 1: Search Devices (Model & SerialNumber)
            var devicesTask = Task.Run(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var context = _contextFactory.CreateDbContext();
                var items = await context.Devices
                    .AsNoTracking()
                    .Where(d => !d.IsDeleted && 
                               (d.Model.ToLower().Contains(normalizedQuery) || 
                                d.SerialNumber.ToLower().Contains(normalizedQuery)))
                    .Select(d => new SearchResultItem
                    {
                        Id = d.Id,
                        EntityType = SearchEntityType.Device,
                        DisplayTitle = d.Model,
                        DisplaySubtitle = "رقم تسلسلي: " + d.SerialNumber
                    })
                    .ToListAsync(cancellationToken);
                return items;
            }, cancellationToken);

            // Task 2: Search Owners (Name)
            var ownersTask = Task.Run(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var context = _contextFactory.CreateDbContext();
                var items = await context.Owners
                    .AsNoTracking()
                    .Where(o => !o.IsDeleted && o.Name.ToLower().Contains(normalizedQuery))
                    .Select(o => new SearchResultItem
                    {
                        Id = o.Id,
                        EntityType = SearchEntityType.Owner,
                        DisplayTitle = o.Name,
                        DisplaySubtitle = "جهة مالكة"
                    })
                    .ToListAsync(cancellationToken);
                return items;
            }, cancellationToken);

            // Task 3: Search Device Types (Name)
            var deviceTypesTask = Task.Run(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var context = _contextFactory.CreateDbContext();
                var items = await context.DeviceTypes
                    .AsNoTracking()
                    .Where(t => !t.IsDeleted && t.Name.ToLower().Contains(normalizedQuery))
                    .Select(t => new SearchResultItem
                    {
                        Id = t.Id,
                        EntityType = SearchEntityType.DeviceType,
                        DisplayTitle = t.Name,
                        DisplaySubtitle = "نوع جهاز"
                    })
                    .ToListAsync(cancellationToken);
                return items;
            }, cancellationToken);

            // Task 4: Search Calibration Records (CertificateNumber)
            var calibrationsTask = Task.Run(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var context = _contextFactory.CreateDbContext();
                // البحث والعرض على الشهادة لا على العمود المهجور: المطابقة عبر
                // استعلام فرعيّ، والعنوان من الرقم المُصدَر. لولا الاثنين معاً لظهرت
                // كل نتيجة بعنوان فارغ ولما وُجدت شهادة بالبحث عن رقمها.
                var rawItems = await context.CalibrationRecords
                    .AsNoTracking()
                    .Include(r => r.Device)
                        .ThenInclude(d => d!.Owner)
                    .Where(r => !r.IsDeleted && context.Certificates.Any(c =>
                        c.CalibrationRecordId == r.Id &&
                        !c.IsDeleted &&
                        c.CertificateNumber.ToLower().Contains(normalizedQuery)))
                    .Select(r => new
                    {
                        r.Id,
                        IssuedNumber = context.Certificates
                            .Where(c => c.CalibrationRecordId == r.Id && !c.IsDeleted)
                            .Select(c => c.CertificateNumber)
                            .FirstOrDefault(),
                        DeviceModel = r.Device != null ? r.Device.Model : string.Empty,
                        OwnerName = (r.Device != null && r.Device.Owner != null) ? r.Device.Owner.Name : string.Empty
                    })
                    .ToListAsync(cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                var results = rawItems.Select(r => new SearchResultItem
                {
                    Id = r.Id,
                    EntityType = SearchEntityType.CalibrationRecord,
                    DisplayTitle = CertificateNumberDisplayRules.Display(r.IssuedNumber),
                    DisplaySubtitle = string.IsNullOrWhiteSpace(r.DeviceModel) 
                        ? "شهادة معايرة" 
                        : $"{r.DeviceModel} — {r.OwnerName}"
                }).ToList();

                return results;
            }, cancellationToken);

            // Wait for all search tasks to complete
            await Task.WhenAll(devicesTask, ownersTask, deviceTypesTask, calibrationsTask);

            // Collect all results
            var allResults = new List<SearchResultItem>();
            allResults.AddRange(await devicesTask);
            allResults.AddRange(await ownersTask);
            allResults.AddRange(await deviceTypesTask);
            allResults.AddRange(await calibrationsTask);

            return allResults;
        }
    }
}
