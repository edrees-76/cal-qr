using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Data;
using CAL_QR.Models;

namespace CAL_QR.Repositories
{
    public class AttachmentRepository : IAttachmentRepository
    {
        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;

        public AttachmentRepository(IDbContextFactory<CalQrDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<IEnumerable<Attachment>> GetByRecordIdAsync(int recordId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Attachments
                .AsNoTracking()
                .Where(a => a.CalibrationRecordId == recordId)
                .ToListAsync();
        }

        public async Task AddAsync(Attachment attachment)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            attachment.UploadedAt = DateTime.UtcNow;
            context.Attachments.Add(attachment);
            await context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var attachment = await context.Attachments.FindAsync(id);
            if (attachment != null)
            {
                context.Attachments.Remove(attachment);
                await context.SaveChangesAsync();
            }
        }
    }
}
