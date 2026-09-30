using System;
using System.IO;

namespace CAL_QR.Helpers
{
    /// <summary>
    /// المجلّد الافتراضيّ الوحيد لمخرجات QR حين لا يُضبط QrOutputPath. كان في ثلاثة مواضع بثلاثة أسماء
    /// (poster / QR_Output / QR) فتنظّف إعادة الضبط أو تنسخ النسخةُ الاحتياطيّة مجلّداً غير الذي تُكتب فيه الملفّات.
    /// </summary>
    public static class QrPaths
    {
        public const string DefaultFolderName = "poster";

        public static string DefaultFolder() => AppPaths.DefaultQrFolder();
    }
}
