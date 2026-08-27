using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Data;

namespace CAL_QR.Helpers
{
    /// <summary>
    /// مجلّد كلّ سجلّ معايرة يُسمّى باسم CalibrationRecordId (سبب bb90ecc)، والنسخة
    /// الموقّعة تقيم في مجلّد "signed" فرعيّ منه لتبقى منفصلة بصريًّا على القرص
    /// كما هي منفصلة منطقيًّا عبر Attachment.CertificateId.
    /// </summary>
    public static class AttachmentPaths
    {
        public static async Task<string> ResolveRootAsync(IDbContextFactory<CalQrDbContext> contextFactory)
        {
            string attachmentsRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Attachments");
            using (var context = await contextFactory.CreateDbContextAsync())
            {
                var setting = await context.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "AttachmentsPath");
                if (setting != null && !string.IsNullOrWhiteSpace(setting.Value))
                {
                    attachmentsRoot = setting.Value;
                }
            }
            return attachmentsRoot;
        }

        public static string RecordFolder(string root, int recordId)
            => Path.Combine(root, recordId.ToString());

        public static string SignedFolder(string root, int recordId)
            => Path.Combine(RecordFolder(root, recordId), "signed");
    }
}
