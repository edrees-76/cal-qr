using System;
using System.IO;
using System.Threading;
using Microsoft.Data.Sqlite;

namespace CAL_QR.Tests
{
    /// <summary>
    /// حذف مجلّدات الاختبارات المؤقّتة بأمان. السبب الجذريّ لتراكم مئات المجلّدات (E2E_Scenario*، PerfTest_*):
    /// مجمّع اتّصالات SQLite يُبقي ملفّ القاعدة مقفلاً بعد انتهاء الاختبار، فيفشل Directory.Delete، وكان
    /// الاستدعاء القديم `try { Directory.Delete(...) } catch {}` يبتلع الفشل، ولا يُنفَّذ أصلاً إن فشل الاختبار قبله.
    /// </summary>
    internal static class TestDirectory
    {
        public static string Create(string prefix)
        {
            string path = Path.Combine(AppContext.BaseDirectory, prefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        /// <summary>يحرّر مجمّع SQLite ثمّ يحذف المجلّد بإعادة محاولة قصيرة؛ فشله الأخير يُكتب في Debug لا يُبتلع.</summary>
        public static void Delete(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            for (int attempt = 1; attempt <= 5; attempt++)
            {
                SqliteConnection.ClearAllPools();
                try
                {
                    if (Directory.Exists(path)) Directory.Delete(path, true);
                    return;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    if (attempt == 5)
                    {
                        System.Diagnostics.Debug.WriteLine($"[TestDirectory] could not delete '{path}': {ex.Message}");
                        return;
                    }
                    Thread.Sleep(100 * attempt);
                }
            }
        }
    }
}
