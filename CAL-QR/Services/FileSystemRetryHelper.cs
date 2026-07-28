using System;
using System.IO;

namespace CAL_QR.Services
{
    /// <summary>
    /// حذف ملفات ومجلدات بإعادة محاولة، لعمليات التنظيف على القرص.
    /// أقفال الملفات العابرة على Windows (مؤشر ملف لم يُغلق، مكافح فيروسات، مفهرس النظام)
    /// تُفشل الحذف لأجزاء من الثانية. حذف المجلد التكراري أكثر عرضة لها من حذف الملف
    /// لأنه عملية مركّبة، ولذلك السقف خمس محاولات بتراجع تصاعدي لا ثلاث بفواصل ثابتة.
    /// </summary>
    public static class FileSystemRetryHelper
    {
        // فواصل الانتظار بين المحاولات بالمللي ثانية: خمس محاولات وأربعة فواصل،
        // فلا انتظار بعد المحاولة الأخيرة.
        private static readonly int[] RetryDelaysMs = { 50, 100, 200, 400 };

        /// <summary>
        /// يحذف ملفاً. يُرجع true إن لم يعد الملف موجوداً بعد المحاولات،
        /// و false إن كان غير موجود أصلاً أو تعذّر حذفه.
        /// </summary>
        public static bool TryDeleteFile(string path) =>
            TryDelete(path, () => File.Exists(path), () => File.Delete(path), "file");

        /// <summary>
        /// يحذف مجلداً بمحتوياته. يُرجع true إن لم يعد المجلد موجوداً بعد المحاولات،
        /// و false إن كان غير موجود أصلاً أو تعذّر حذفه.
        /// </summary>
        public static bool TryDeleteDirectory(string path) =>
            TryDelete(path, () => Directory.Exists(path), () => Directory.Delete(path, recursive: true), "directory");

        private static bool TryDelete(string path, Func<bool> exists, Action delete, string kind)
        {
            // غير موجود أصلاً: لا شيء حُذف
            if (!exists())
            {
                return false;
            }

            string? lastError = null;

            for (int attempt = 0; attempt < RetryDelaysMs.Length + 1; attempt++)
            {
                try
                {
                    delete();
                    return true;
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                }

                // قد يكون طرف آخر حذف المسار بين محاولتين
                if (!exists())
                {
                    return true;
                }

                if (attempt < RetryDelaysMs.Length)
                {
                    System.Threading.Thread.Sleep(RetryDelaysMs[attempt]);
                }
            }

            if (!exists())
            {
                return true;
            }

            Console.WriteLine($"[Warning] Failed to delete {kind} {path}: {lastError}");
            return false;
        }
    }
}
