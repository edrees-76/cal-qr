using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Data;
using CAL_QR.Models;

namespace CAL_QR.Repositories
{
    public class CalibrationRepository : ICalibrationRepository
    {
        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;

        public CalibrationRepository(IDbContextFactory<CalQrDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<IEnumerable<CalibrationRecord>> GetAllAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.CalibrationRecords
                .AsNoTracking()
                .Include(r => r.Device)
                    .ThenInclude(d => d!.Owner)
                .Include(r => r.Device)
                    .ThenInclude(d => d!.DeviceType)
                .Where(r => !r.IsDeleted)
                .ToListAsync();
        }

        public async Task<CalibrationRecord?> GetByIdAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.CalibrationRecords
                .AsNoTracking()
                .Include(r => r.Device)
                    .ThenInclude(d => d!.Owner)
                .Include(r => r.Device)
                    .ThenInclude(d => d!.DeviceType)
                .Include(r => r.Attachments)
                .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
        }

        public async Task<CalibrationRecord?> GetByCertificateNumberAsync(string certificateNumber)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.CalibrationRecords
                .AsNoTracking()
                .Include(r => r.Device)
                    .ThenInclude(d => d!.Owner)
                .Include(r => r.Device)
                    .ThenInclude(d => d!.DeviceType)
                .FirstOrDefaultAsync(r => r.CertificateNumber == certificateNumber && !r.IsDeleted);
        }

        public async Task<IEnumerable<CalibrationRecord>> GetByDeviceIdAsync(int deviceId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.CalibrationRecords
                .AsNoTracking()
                .Include(r => r.Attachments)
                .Where(r => r.DeviceId == deviceId && !r.IsDeleted)
                .OrderByDescending(r => r.CalibrationDate)
                .ToListAsync();
        }

        public async Task AddAsync(CalibrationRecord record)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            record.CreatedAt = DateTime.UtcNow;
            record.UpdatedAt = DateTime.UtcNow;
            record.IsDeleted = false;
            context.CalibrationRecords.Add(record);
            await context.SaveChangesAsync();
        }

        public async Task UpdateAsync(CalibrationRecord record)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            record.UpdatedAt = DateTime.UtcNow;
            context.Entry(record).State = EntityState.Modified;
            await context.SaveChangesAsync();
        }

        public async Task SoftDeleteAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var record = await context.CalibrationRecords.FindAsync(id);
            if (record != null)
            {
                record.IsDeleted = true;
                await context.SaveChangesAsync();
            }
        }
    }
}
