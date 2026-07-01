using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Data;
using CAL_QR.Models;

namespace CAL_QR.Repositories
{
    public class DeviceTypeRepository : IDeviceTypeRepository
    {
        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;

        public DeviceTypeRepository(IDbContextFactory<CalQrDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<IEnumerable<DeviceType>> GetAllAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.DeviceTypes
                .AsNoTracking()
                .Where(t => !t.IsDeleted)
                .OrderBy(t => t.Name)
                .ToListAsync();
        }

        public async Task<DeviceType?> GetByIdAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.DeviceTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
        }

        public async Task AddAsync(DeviceType type)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            type.CreatedAt = DateTime.UtcNow;
            type.IsDeleted = false;
            context.DeviceTypes.Add(type);
            await context.SaveChangesAsync();
        }

        public async Task UpdateAsync(DeviceType type)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            context.Entry(type).State = EntityState.Modified;
            await context.SaveChangesAsync();
        }

        public async Task SoftDeleteAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var type = await context.DeviceTypes.FindAsync(id);
            if (type != null)
            {
                type.IsDeleted = true;
                await context.SaveChangesAsync();
            }
        }
    }
}
