using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Data;
using CAL_QR.Models;

namespace CAL_QR.Repositories
{
    public class AuditLogRepository : IAuditLogRepository
    {
        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;

        public AuditLogRepository(IDbContextFactory<CalQrDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<IEnumerable<AuditLog>> GetAllAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.AuditLogs
                .AsNoTracking()
                .OrderByDescending(l => l.ActionAt)
                .ToListAsync();
        }

        public async Task AddAsync(AuditLog log)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            log.ActionAt = DateTime.UtcNow;
            context.AuditLogs.Add(log);
            await context.SaveChangesAsync();
        }

        public async Task LogAsync(string action, string entityName, string entityId, string details)
        {
            var log = new AuditLog
            {
                Action = action,
                EntityName = entityName,
                EntityId = entityId,
                Details = details,
                ActionAt = DateTime.UtcNow
            };
            await AddAsync(log);
        }
    }
}
