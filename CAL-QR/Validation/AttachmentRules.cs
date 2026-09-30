using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CAL_QR.Validation
{
    /// <summary>
    /// قائمة الامتدادات المسموحة للمرفقات. المرفقات تُفتح بـ ShellExecute، فملفّ تنفيذيّ أو
    /// سكربت أو اختصار (.exe .bat .lnk .js …) مرفقاً يُنفَّذ بنقرة. الامتدادات ذات الماكرو
    /// (.docm .xlsm) غير مسموحة عمداً.
    /// </summary>
    public static class AttachmentRules
    {
        public static readonly IReadOnlyList<string> Documents = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".txt", ".csv" };
        public static readonly IReadOnlyList<string> Images = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff" };

        public static IEnumerable<string> AllowedExtensions => Documents.Concat(Images);

        public static bool IsAllowed(string? fileName)
        {
            var ext = Path.GetExtension(fileName ?? string.Empty);
            return !string.IsNullOrEmpty(ext) &&
                   AllowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>مرشّح OpenFileDialog: الأنواع المسموحة فقط.</summary>
        public static string DialogFilter =>
            "المرفقات المسموحة|" + string.Join(";", AllowedExtensions.Select(e => "*" + e));

        public const string RejectedMessage =
            "نوع الملفّ غير مسموح كمرفق. المسموح: PDF والصور ومستندات Office النصّيّة (بلا ماكرو) وTXT وCSV.";
    }
}
