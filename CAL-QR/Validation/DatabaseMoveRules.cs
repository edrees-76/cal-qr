using System;
using System.IO;

namespace CAL_QR.Validation
{
    /// <summary>
    /// حارس نقل قاعدة البيانات من الإعدادات. النقل ينسخ القاعدة الحاليّة إلى المسار الجديد
    /// بـ BackupDatabase، وهي تستبدل محتوى الوجهة كاملاً؛ فوجهة موجودة تعني محو قاعدة أخرى.
    /// </summary>
    public static class DatabaseMoveRules
    {
        /// <summary>يُرجع رسالة الرفض، أو null إن جاز النقل إلى المسار الجديد.</summary>
        public static string? Validate(string currentDatabasePath, string newDatabasePath, bool targetFileExists)
        {
            if (string.IsNullOrWhiteSpace(newDatabasePath))
                return "مسار قاعدة البيانات الجديد غير صالح.";

            if (string.Equals(Path.GetFullPath(currentDatabasePath), Path.GetFullPath(newDatabasePath),
                    StringComparison.OrdinalIgnoreCase))
                return "المسار الجديد هو مسار قاعدة البيانات الحاليّة نفسه.";

            if (targetFileExists)
                return "يوجد ملفّ بهذا الاسم في المسار الجديد، والنقل سيكتب فوقه ويمحو محتواه.\n" +
                       "اختر اسم ملفّ جديداً أو مجلّداً آخر.";

            return null;
        }
    }
}
