using System;
using System.Diagnostics;
using System.IO;
using QuestPDF.Drawing;
using QuestPDF.Infrastructure;

namespace CAL_QR.Services.Documents
{
    /// <summary>
    /// تهيئة عامّة لمرة واحدة لبيئة QuestPDF: الترخيص وتسجيل خطوط Cairo الثابتة.
    ///
    /// CheckIfAllTextGlyphsAreAvailable مفعَّل افتراضياً، فنصّ عربي بخط غير
    /// مسجَّل يرمي استثناءً وقت التوليد.
    ///
    /// ⚠ نُسجِّل الملفين الثابتين (Regular/ExtraBold) عبر RegisterFontWithCustomName
    /// تحت اسم موحَّد واحد — لا RegisterFont العادي. السبب: تحقّق مباشر من جدول
    /// name الداخلي للملفين أظهر اسمي عائلة كلاسيكيَّين (NameID 1) مختلفين تماماً:
    /// "Cairo" لملف Regular مقابل "Cairo ExtraBold" لملف ExtraBold. لو سُجِّلا
    /// بـ RegisterFont العادي معتمدَين على الاكتشاف التلقائي، فقد يقرأ QuestPDF
    /// عائلتين منفصلتين لا عائلة واحدة بوزنين، فيسقط .Bold() إلى تعريض اصطناعي
    /// (faux bold) على ملف Regular — نفس العطل المُبلَّغ عنه. RegisterWithCustomName
    /// يفرض اسم العائلة "Cairo" صراحةً على كلا الملفين فيربط QuestPDF الوزنين
    /// تحتها بثقة، بصرف النظر عمّا يحمله جدول name الداخلي.
    ///
    /// Cairo.ttf (المتغيّر) لا يُسجَّل هنا ولا يُلمس — الواجهة تعتمده عبر pack URI.
    /// </summary>
    public static class CertificatePdfEnvironment
    {
        public const string ArabicFontFamily = "Cairo";

        private static readonly object _lock = new object();
        private static bool _initialized;

        public static void EnsureInitialized()
        {
            lock (_lock)
            {
                if (_initialized) return;

                QuestPDF.Settings.License = LicenseType.Community;

                RegisterFontSafe("Cairo-Regular.ttf");
                RegisterFontSafe("Cairo-ExtraBold.ttf");

                _initialized = true;
            }
        }

        private static void RegisterFontSafe(string fileName)
        {
            try
            {
                string fontPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Fonts", fileName);
                using var stream = File.OpenRead(fontPath);
                FontManager.RegisterFontWithCustomName(ArabicFontFamily, stream);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CertificatePdfEnvironment] Error registering font '{fileName}': {ex.Message}");
            }
        }
    }
}
