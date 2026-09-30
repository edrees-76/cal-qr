using System;
using System.Globalization;
using System.IO;

namespace CAL_QR.Services
{
    /// <summary>
    /// سجلّ أخطاء ملفّيّ بسيط: %LocalAppData%\CAL-QR\logs\app-yyyyMMdd.log. لا يُلقي أبداً
    /// (فشل التسجيل لا يجوز أن يُسقط التطبيق) لكنّه يكتب فشله في Debug.
    /// </summary>
    public static class AppLog
    {
        private static readonly object Gate = new();

        public static string LogDirectory { get; set; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CAL-QR", "logs");

        public static void Error(string context, Exception ex) =>
            Write("ERROR", $"{context}: {ex}");

        public static void Warn(string message) => Write("WARN", message);

        private static void Write(string level, string message)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(LogDirectory);
                    var file = Path.Combine(LogDirectory,
                        $"app-{DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.log");
                    var line = $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} [{level}] {message}{Environment.NewLine}";
                    File.AppendAllText(file, line);
                }
            }
            catch (Exception logEx)
            {
                System.Diagnostics.Debug.WriteLine($"[AppLog] failed to write log: {logEx.Message}");
            }
        }
    }
}
