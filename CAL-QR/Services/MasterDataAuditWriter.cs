using System;
using System.Threading.Tasks;
using CAL_QR.Repositories;
using CAL_QR.Validation;

namespace CAL_QR.Services
{
    /// <summary>
    /// يكتب حدث تدقيق لعمليّة جهة/نوع جهاز بعد نجاحها. فشل التدقيق يُسجَّل في
    /// <see cref="AppLog"/> ولا يُلغي العمليّة (تمّت فعلاً) ولا يُخفى.
    /// </summary>
    public static class MasterDataAuditWriter
    {
        public static async Task TryLogAsync(
            IAuditLogRepository auditLogRepository,
            string action,
            string entityName,
            int entityId,
            string? name,
            string? note = null)
        {
            try
            {
                await auditLogRepository.LogAsync(
                    action,
                    entityName,
                    entityId > 0 ? entityId.ToString() : string.Empty,
                    MasterDataAuditRules.Describe(action, name, entityId, note));
            }
            catch (Exception ex)
            {
                AppLog.Error($"MasterData: audit log failed after '{action}'", ex);
            }
        }
    }
}
