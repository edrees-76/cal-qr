using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CAL_QR.Models;

namespace CAL_QR.Repositories
{
    public interface IAuditLogRepository
    {
        Task<IEnumerable<AuditLog>> GetAllAsync();
        Task AddAsync(AuditLog log);
        Task LogAsync(string action, string entityName, string entityId, string details, int? userId = null, string? username = null);
        Task<List<AuditLog>> GetFilteredAsync(DateTime? startDate, DateTime? endDate, int? userId);
    }
}
