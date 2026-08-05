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

        public async Task<DeviceType?> GetByIdWithTemplatesAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.DeviceTypes
                .AsNoTracking()
                .Include(t => t.FunctionalCheckTemplates)
                .Include(t => t.UncertaintyComponentTemplates)
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

        /// <summary>
        /// تُحمَّل الصفّ من السياق ويُعدَّل اسمه وحده، فلا يُكتب عمود آخر.
        ///
        /// ⚠ لا EntityState.Modified ولا كائن مفصول: كلاهما يكتب كل الأعمدة، وهو
        /// ما كان يمسح القالب كاملاً عند كل إعادة تسمية.
        ///
        /// الاسم يُكتب كما ورد بلا Trim ولا فحص تفرّد — تطبيع أو تشديد هنا يغيّر
        /// سلوك نموذج أنواع الأجهزة القائم، وهو خارج نطاق هذا الإصلاح.
        ///
        /// نوع غير موجود ⇒ لا شيء، على نمط SoftDeleteAsync أدناه.
        /// </summary>
        public async Task RenameAsync(int id, string name)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var type = await context.DeviceTypes.FirstOrDefaultAsync(t => t.Id == id);
            if (type != null)
            {
                type.Name = name;
                await context.SaveChangesAsync();
            }
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
