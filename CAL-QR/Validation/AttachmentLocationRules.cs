using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CAL_QR.Validation
{
    /// <summary>
    /// أين يُبحث عن مرفق محفوظ. عمود Attachment.FilePath يخزّن مساراً مطلقاً وقت الحفظ، فإن
    /// نُقل مجلّد المرفقات أو استُعيدت نسخة على جهاز آخر صار المسار المخزّن ميّتاً بينما الملفّ
    /// موجود في المجلّد الحاليّ. يُجرَّب المسار المخزّن أوّلاً، ثمّ تخطيط المجلّد الحاليّ:
    /// &lt;الجذر&gt;\&lt;رقم السجلّ&gt;\&lt;الاسم&gt; ثمّ &lt;الجذر&gt;\&lt;رقم السجلّ&gt;\signed\&lt;الاسم&gt;
    /// (النسخة الموقّعة). القيمة المخزّنة لا تُعاد كتابتها.
    /// </summary>
    public static class AttachmentLocationRules
    {
        public const string SignedFolderName = "signed";

        public static IReadOnlyList<string> Candidates(string? storedPath, string? currentRoot, int recordId, string? fileName)
        {
            var result = new List<string>();
            if (!string.IsNullOrWhiteSpace(storedPath))
                result.Add(storedPath);

            if (!string.IsNullOrWhiteSpace(currentRoot) && recordId > 0 && IsPlainFileName(fileName))
            {
                string recordFolder = Path.Combine(currentRoot, recordId.ToString());
                result.Add(Path.Combine(recordFolder, fileName!));
                result.Add(Path.Combine(recordFolder, SignedFolderName, fileName!));
            }

            return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static string? FirstExisting(IEnumerable<string> candidates, Func<string, bool> fileExists)
            => candidates.FirstOrDefault(fileExists);

        /// <summary>
        /// اسم ملفّ مجرّد فقط: لا مسار ولا ".." ولا محارف ممنوعة، حتى لا يخرج البحث من
        /// مجلّد السجلّ بقيمة عبثت بها نسخة مستعادة.
        /// </summary>
        public static bool IsPlainFileName(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return false;
            if (fileName == "." || fileName == "..") return false;
            if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return false;
            return string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal);
        }

        public static string NotFoundMessage(IReadOnlyList<string> triedPaths)
        {
            string tried = triedPaths.Count == 0
                ? "(لا مسار مسجَّل)"
                : string.Join(Environment.NewLine, triedPaths);
            return "الملفّ المرفق غير موجود في مساره المسجَّل ولا في مجلّد المرفقات الحاليّ."
                + Environment.NewLine + Environment.NewLine
                + "المواضع التي بُحث فيها:" + Environment.NewLine + tried
                + Environment.NewLine + Environment.NewLine
                + "إن نقلت المرفقات يدوياً فضع مجلّد السجلّ داخل مجلّد المرفقات المحدّد في الإعدادات.";
        }
    }
}
