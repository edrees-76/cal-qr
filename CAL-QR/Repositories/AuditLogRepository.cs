using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Services;

namespace CAL_QR.Repositories
{
    public class AuditLogRepository : IAuditLogRepository
    {
        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;
        private readonly ICurrentUserService _currentUserService;

        public AuditLogRepository(IDbContextFactory<CalQrDbContext> contextFactory, ICurrentUserService currentUserService)
        {
            _contextFactory = contextFactory;
            _currentUserService = currentUserService;
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

        public async Task LogAsync(string action, string entityName, string entityId, string details, int? userId = null, string? username = null)
        {
            var log = new AuditLog
            {
                Action = action,
                EntityName = entityName,
                EntityId = entityId,
                Details = details,
                UserId = userId ?? _currentUserService.CurrentUser?.Id,
                Username = username ?? _currentUserService.CurrentUser?.Username,
                ActionAt = DateTime.UtcNow
            };
            await AddAsync(log);
        }

        public async Task<List<AuditLog>> GetFilteredAsync(DateTime? startDate, DateTime? endDate, int? userId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var query = context.AuditLogs
                .AsNoTracking()
                .AsQueryable();

            if (startDate.HasValue)
            {
                var start = startDate.Value.Date;
                query = query.Where(l => l.ActionAt >= start);
            }

            if (endDate.HasValue)
            {
                var end = endDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(l => l.ActionAt <= end);
            }

            if (userId.HasValue)
            {
                query = query.Where(l => l.UserId == userId.Value);
            }

            return await query
                .OrderByDescending(l => l.ActionAt)
                .ToListAsync();
        }
    }
}
