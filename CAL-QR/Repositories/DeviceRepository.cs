using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Data;
using CAL_QR.Models;

namespace CAL_QR.Repositories
{
    public class DeviceRepository : IDeviceRepository
    {
        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;

        public DeviceRepository(IDbContextFactory<CalQrDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<IEnumerable<Device>> GetAllAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Devices
                .AsNoTracking()
                .Include(d => d.Owner)
                .Include(d => d.DeviceType)
                .Where(d => !d.IsDeleted)
                .ToListAsync();
        }

        public async Task<Device?> GetByIdAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Devices
                .AsNoTracking()
                .Include(d => d.Owner)
                .Include(d => d.DeviceType)
                .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted);
        }

        public async Task<Device?> GetBySerialNumberAsync(string serialNumber)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Devices
                .AsNoTracking()
                .Include(d => d.Owner)
                .Include(d => d.DeviceType)
                .FirstOrDefaultAsync(d => d.SerialNumber == serialNumber && !d.IsDeleted);
        }

        public async Task AddAsync(Device device)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            device.CreatedAt = DateTime.UtcNow;
            device.IsDeleted = false;
            context.Devices.Add(device);
            await context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Device device)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            context.Entry(device).State = EntityState.Modified;
            await context.SaveChangesAsync();
        }

        public async Task SoftDeleteAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var device = await context.Devices.FindAsync(id);
            if (device != null)
            {
                device.IsDeleted = true;
                await context.SaveChangesAsync();
            }
        }
    }
}
