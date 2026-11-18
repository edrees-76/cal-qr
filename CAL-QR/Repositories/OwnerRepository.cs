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
            // تحديث الحقول القابلة للتحرير فقط على السجلّ المخزَّن. وسم كائن النموذج الجديد
            // بـ Modified كان يكتب كلّ أعمدته: CreatedAt بوقت التعديل، وIsSeedTestData وIsDeleted بـ false.
            using var context = await _contextFactory.CreateDbContextAsync();
            var stored = await context.Owners.FindAsync(owner.Id)
                ?? throw new InvalidOperationException($"الجهة رقم {owner.Id} غير موجودة.");

            stored.Name = owner.Name;
            stored.Address = owner.Address;
            stored.ContactPhone = owner.ContactPhone;
            stored.ContactPerson = owner.ContactPerson;
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
