using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Data;
using CAL_QR.Repositories;

namespace CAL_QR.Services
{
    /// <summary>ما في AppSettings من حصيلة بذر أنواع الأجهزة لحظة القراءة.</summary>
    public sealed class DeviceTypeSeedNoticeSnapshot
    {
        public DeviceTypeSeedNoticeSnapshot(string? conflicts, string? seedFailure, string? repairFailure)
        {
            Conflicts = conflicts;
            SeedFailure = seedFailure;
            RepairFailure = repairFailure;
        }

        public string? Conflicts { get; }
        public string? SeedFailure { get; }
        public string? RepairFailure { get; }
    }

    public interface IDeviceTypeSeedNoticeService
    {
        Task<DeviceTypeSeedNoticeSnapshot> LoadAsync();

        /// <summary>
        /// يمحو حصيلة التعارض بإقرار المدير ويسجّل ذلك في سجلّ العمليّات. لا يمحو إلا النصّ الذي
        /// عُرض فعلاً: إن تغيّر المخزَّن بعد العرض يبقى ليُعرض في الدخول التالي.
        /// </summary>
        Task<bool> AcknowledgeConflictsAsync(string shownConflicts);
    }

    public sealed class DeviceTypeSeedNoticeService : IDeviceTypeSeedNoticeService
    {
        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;
        private readonly IAuditLogRepository _auditLogRepository;

        public DeviceTypeSeedNoticeService(IDbContextFactory<CalQrDbContext> contextFactory, IAuditLogRepository auditLogRepository)
        {
            _contextFactory = contextFactory;
            _auditLogRepository = auditLogRepository;
        }

        public async Task<DeviceTypeSeedNoticeSnapshot> LoadAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var keys = new[] { DeviceTypeSeeder.ConflictsKey, DeviceTypeSeeder.FailureKey, DeviceTypeSeeder.RepairFailureKey };
            var values = await context.AppSettings.AsNoTracking()
                .Where(s => keys.Contains(s.Key))
                .ToDictionaryAsync(s => s.Key, s => s.Value);

            values.TryGetValue(DeviceTypeSeeder.ConflictsKey, out var conflicts);
            values.TryGetValue(DeviceTypeSeeder.FailureKey, out var seedFailure);
            values.TryGetValue(DeviceTypeSeeder.RepairFailureKey, out var repairFailure);

            return new DeviceTypeSeedNoticeSnapshot(conflicts, seedFailure, repairFailure);
        }

        public async Task<bool> AcknowledgeConflictsAsync(string shownConflicts)
        {
            if (string.IsNullOrWhiteSpace(shownConflicts))
                throw new ArgumentException("لا حصيلة تعارض لإقرارها.", nameof(shownConflicts));

            using (var context = await _contextFactory.CreateDbContextAsync())
            {
                var setting = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == DeviceTypeSeeder.ConflictsKey);
                if (setting == null || setting.Value != shownConflicts)
                    return false;

                context.AppSettings.Remove(setting);
                await context.SaveChangesAsync();
            }

            await _auditLogRepository.LogAsync(
                "إقرار حصيلة فحص أنواع الأجهزة",
                "AppSettings",
                DeviceTypeSeeder.ConflictsKey,
                "اطّلع المدير على حصيلة فحص أنواع الأجهزة فمُحيت. نصّها:\n" + shownConflicts);

            return true;
        }
    }
}
