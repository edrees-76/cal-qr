using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Data;
using CAL_QR.Models;

namespace CAL_QR.Repositories
{
    public class PaperTemplateRepository : IPaperTemplateRepository
    {
        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;

        public PaperTemplateRepository(IDbContextFactory<CalQrDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<IEnumerable<PaperTemplate>> GetAllAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.PaperTemplates
                .AsNoTracking()
                .OrderBy(t => t.TemplateName)
                .ToListAsync();
        }

        public async Task<PaperTemplate?> GetByIdAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.PaperTemplates
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<PaperTemplate?> GetDefaultAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.PaperTemplates
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.IsDefault);
        }

        public async Task AddAsync(PaperTemplate template)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            template.CreatedAt = DateTime.UtcNow;
            if (template.IsDefault)
            {
                var defaults = await context.PaperTemplates.Where(t => t.IsDefault).ToListAsync();
                foreach (var d in defaults)
                {
                    d.IsDefault = false;
                }
            }
            context.PaperTemplates.Add(template);
            await context.SaveChangesAsync();
        }

        public async Task UpdateAsync(PaperTemplate template)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            if (template.IsDefault)
            {
                var defaults = await context.PaperTemplates.Where(t => t.IsDefault && t.Id != template.Id).ToListAsync();
                foreach (var d in defaults)
                {
                    d.IsDefault = false;
                }
            }
            context.Entry(template).State = EntityState.Modified;
            await context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var template = await context.PaperTemplates.FindAsync(id);
            if (template != null)
            {
                context.PaperTemplates.Remove(template);
                await context.SaveChangesAsync();
            }
        }

        public async Task SetDefaultAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var templates = await context.PaperTemplates.ToListAsync();
            foreach (var t in templates)
            {
                t.IsDefault = (t.Id == id);
            }
            await context.SaveChangesAsync();
        }
    }
}
