using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Data;
using CAL_QR.Models;

namespace CAL_QR.Repositories
{
    public class OwnerRepository : IOwnerRepository
    {
        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;

        public OwnerRepository(IDbContextFactory<CalQrDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<IEnumerable<Owner>> GetAllAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Owners
                .AsNoTracking()
                .Where(o => !o.IsDeleted)
                .OrderBy(o => o.Name)
                .ToListAsync();
        }

        public async Task<Owner?> GetByIdAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Owners
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted);
        }

        public async Task AddAsync(Owner owner)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            owner.CreatedAt = DateTime.UtcNow;
            owner.IsDeleted = false;
            context.Owners.Add(owner);
            await context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Owner owner)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            context.Entry(owner).State = EntityState.Modified;
            await context.SaveChangesAsync();
        }

        public async Task SoftDeleteAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var owner = await context.Owners.FindAsync(id);
            if (owner != null)
            {
                owner.IsDeleted = true;
                await context.SaveChangesAsync();
            }
        }
    }
}
