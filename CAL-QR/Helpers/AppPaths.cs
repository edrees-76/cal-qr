using System;
using System.IO;

namespace CAL_QR.Helpers
{
    /// <summary>
    /// المصدر الوحيد لمسارات بيانات البرنامج (القاعدة، المؤشّر db_path.txt، المرفقات، مجلّد QR).
    ///
    /// لماذا: بعد التثبيت في Program Files لا يستطيع البرنامج الكتابة بجوار ملفّه التنفيذيّ، وكانت هذه
    /// المسارات تُركَّب في اثني عشر موضعاً من BaseDirectory. الآن البيانات الجديدة تُنشأ تحت
    /// %ProgramData%\CAL-QR (مشتركة بين حسابات ويندوز فلا ينقسم ترقيم الشهادات).
    ///
    /// قاعدة التوافق (بلا نقل بيانات): إن وُجد ملفّ أو مجلّد قديم بجوار البرنامج (كتشغيل bin\Debug)
    /// يبقى مستعملاً كما هو؛ لا يُنقل ولا تُعدَّل مساراته المخزَّنة في جدول المرفقات. التثبيت الجديد وحده
    /// (بلا أيّ أثر قديم بجوار البرنامج) يستعمل %ProgramData%\CAL-QR.
    /// كلّ الدوالّ تقبل مجلّد البرنامج ومجلّد البيانات وسيطين اختياريّين لتُختبر بلا لمس الجهاز.
    /// </summary>
    public static class AppPaths
    {
        public const string AppFolderName = "CAL-QR";
        public const string PointerFileName = "db_path.txt";
        public const string DefaultDbFileName = "cal-qr.db";
        // اسم القاعدة الافتراضيّة القديم بجوار البرنامج؛ يبقى معترفاً به للتوافق.
        public const string LegacyDefaultDbFileName = "cal-qr-simulation.db";
        public const string AttachmentsFolderName = "Attachments";
        public const string QrFolderName = "poster";

        /// <summary>مجلّد البرنامج (للقراءة فقط بعد التثبيت: الشعارات والخطوط).</summary>
        public static string BaseDirectory => AppDomain.CurrentDomain.BaseDirectory;

        /// <summary>مجلّد البيانات المشترك: %ProgramData%\CAL-QR.</summary>
        public static string DataRoot => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), AppFolderName);

        /// <summary>مكان كتابة ملفّ المؤشّر db_path.txt (لا يُكتب أبداً بجوار البرنامج).</summary>
        public static string PointerFileForWrite(string? dataRoot = null) =>
            Path.Combine(dataRoot ?? DataRoot, PointerFileName);

        /// <summary>
        /// ملفّ المؤشّر الواجب قراءته: الجديد في مجلّد البيانات أوّلاً، وإلا القديم بجوار البرنامج، وإلا null.
        /// </summary>
        public static string? FindPointerFile(string? baseDirectory = null, string? dataRoot = null)
        {
            string current = PointerFileForWrite(dataRoot);
            if (File.Exists(current)) return current;

            string legacy = Path.Combine(baseDirectory ?? BaseDirectory, PointerFileName);
            return File.Exists(legacy) ? legacy : null;
        }

        /// <summary>قاعدة البيانات الافتراضيّة (حين لا مؤشّر): القديمة بجوار البرنامج إن وُجدت، وإلا الجديدة.</summary>
        public static string DefaultDbPath(string? baseDirectory = null, string? dataRoot = null)
        {
            string legacy = Path.Combine(baseDirectory ?? BaseDirectory, LegacyDefaultDbFileName);
            return File.Exists(legacy)
                ? Path.GetFullPath(legacy)
                : Path.GetFullPath(Path.Combine(dataRoot ?? DataRoot, DefaultDbFileName));
        }

        /// <summary>مجلّد المرفقات الافتراضيّ (حين لا إعداد AttachmentsPath).</summary>
        public static string DefaultAttachmentsFolder(string? baseDirectory = null, string? dataRoot = null) =>
            DefaultFolder(AttachmentsFolderName, baseDirectory, dataRoot);

        /// <summary>مجلّد مخرجات QR الافتراضيّ (حين لا إعداد QrOutputPath).</summary>
        public static string DefaultQrFolder(string? baseDirectory = null, string? dataRoot = null) =>
            DefaultFolder(QrFolderName, baseDirectory, dataRoot);

        private static string DefaultFolder(string name, string? baseDirectory, string? dataRoot)
        {
            string legacy = Path.Combine(baseDirectory ?? BaseDirectory, name);
            return Directory.Exists(legacy)
                ? Path.GetFullPath(legacy)
                : Path.GetFullPath(Path.Combine(dataRoot ?? DataRoot, name));
        }

        /// <summary>
        /// ينشئ المجلّد إن لم يوجد. عند العجز عن الكتابة يُلقي رسالة عربيّة واضحة بدل انهيار غامض
        /// (مجلّد ProgramData يحتاج صلاحيّة كتابة تمنحها الحزمة المثبِّتة للمستخدمين).
        /// </summary>
        public static void EnsureDirectory(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                Directory.CreateDirectory(path);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
            {
                throw new InvalidOperationException(
                    $"تعذّر إنشاء أو فتح مجلّد بيانات المنظومة:\n{path}\n" +
                    "تأكّد من أنّ حسابك يملك صلاحيّة الكتابة عليه، أو أعد تثبيت المنظومة.", ex);
            }
        }
    }
}
