using System.Collections.Generic;

namespace CalQr.AutoRun.Logic;

/// <summary>نصوص الواجهة بالعربية والإنجليزية. اختبار الوحدة يضمن تطابق المفاتيح في اللغتين.</summary>
public static class LauncherStrings
{
    public const string WindowTitle = nameof(WindowTitle);
    public const string Subtitle = nameof(Subtitle);
    public const string LanguageToggle = nameof(LanguageToggle);
    public const string InstallTitle = nameof(InstallTitle);
    public const string InstallDescription = nameof(InstallDescription);
    public const string InstallButton = nameof(InstallButton);
    public const string GuideTitle = nameof(GuideTitle);
    public const string GuideDescription = nameof(GuideDescription);
    public const string GuideButton = nameof(GuideButton);
    public const string VersionLabel = nameof(VersionLabel);
    public const string Verifying = nameof(Verifying);
    public const string InstallerStarted = nameof(InstallerStarted);
    public const string InstallCancelled = nameof(InstallCancelled);
    public const string ErrorTitle = nameof(ErrorTitle);
    public const string ErrInstallerMissing = nameof(ErrInstallerMissing);
    public const string ErrChecksumMissing = nameof(ErrChecksumMissing);
    public const string ErrChecksumInvalid = nameof(ErrChecksumInvalid);
    public const string ErrChecksumMismatch = nameof(ErrChecksumMismatch);
    public const string ErrInstallerUnreadable = nameof(ErrInstallerUnreadable);
    public const string ErrInstallerLaunch = nameof(ErrInstallerLaunch);
    public const string ErrGuideMissing = nameof(ErrGuideMissing);
    public const string ErrGuideOpen = nameof(ErrGuideOpen);

    private static readonly Dictionary<string, string> Arabic = new Dictionary<string, string>
    {
        [WindowTitle] = "CAL-QR — التشغيل",
        [Subtitle] = "نظام معايرة أجهزة المسح الإشعاعي",
        [LanguageToggle] = "English",
        [InstallTitle] = "تثبيت المنظومة",
        [InstallDescription] = "يتحقق البرنامج من سلامة ملف التثبيت ثم يشغّله.",
        [InstallButton] = "تثبيت",
        [GuideTitle] = "دليل الاستخدام",
        [GuideDescription] = "يفتح دليل استخدام المنظومة (متوفر بالعربية).",
        [GuideButton] = "فتح الدليل",
        [VersionLabel] = "الإصدار {0}",
        [Verifying] = "جارٍ التحقق من ملف التثبيت…",
        [InstallerStarted] = "تم تشغيل المثبِّت. أكمل خطوات التثبيت في نافذته.",
        [InstallCancelled] = "أُلغي تشغيل المثبِّت.",
        [ErrorTitle] = "CAL-QR",
        [ErrInstallerMissing] = "ملف التثبيت غير موجود بجوار هذا البرنامج.\nتأكد من نسخ مجلد التوزيع كاملاً.",
        [ErrChecksumMissing] = "ملف البصمة (.sha256) الخاص بالمثبِّت غير موجود، فلا يمكن التحقق من سلامته.\nتأكد من نسخ مجلد التوزيع كاملاً.",
        [ErrChecksumInvalid] = "ملف البصمة (.sha256) تالف أو بصيغة غير صحيحة، فلا يمكن التحقق من سلامة المثبِّت.",
        [ErrChecksumMismatch] = "ملف التثبيت تالف أو معدَّل: بصمته لا تطابق البصمة المرفقة.\nلن يتم تشغيله. أعد نسخ مجلد التوزيع من مصدره.",
        [ErrInstallerUnreadable] = "تعذّرت قراءة ملف التثبيت للتحقق منه. أغلق أي برنامج يستخدمه وأعد المحاولة.",
        [ErrInstallerLaunch] = "تعذّر تشغيل المثبِّت:\n{0}",
        [ErrGuideMissing] = "ملف دليل الاستخدام غير موجود بجوار هذا البرنامج.",
        [ErrGuideOpen] = "تعذّر فتح دليل الاستخدام. تأكد من وجود برنامج لقراءة ملفات PDF على الجهاز."
    };

    private static readonly Dictionary<string, string> English = new Dictionary<string, string>
    {
        [WindowTitle] = "CAL-QR — Launcher",
        [Subtitle] = "Radiation Survey Meter Calibration System",
        [LanguageToggle] = "العربية",
        [InstallTitle] = "Install the system",
        [InstallDescription] = "Verifies the installer file's integrity, then runs it.",
        [InstallButton] = "Install",
        [GuideTitle] = "User guide",
        [GuideDescription] = "Opens the system user guide (available in Arabic).",
        [GuideButton] = "Open guide",
        [VersionLabel] = "Version {0}",
        [Verifying] = "Verifying the installer file…",
        [InstallerStarted] = "The installer has started. Continue with the steps in its window.",
        [InstallCancelled] = "Launching the installer was cancelled.",
        [ErrorTitle] = "CAL-QR",
        [ErrInstallerMissing] = "The installer file was not found next to this program.\nMake sure the whole distribution folder was copied.",
        [ErrChecksumMissing] = "The installer's checksum file (.sha256) is missing, so its integrity cannot be verified.\nMake sure the whole distribution folder was copied.",
        [ErrChecksumInvalid] = "The checksum file (.sha256) is corrupt or malformed, so the installer cannot be verified.",
        [ErrChecksumMismatch] = "The installer is corrupt or modified: its checksum does not match the supplied one.\nIt will not be run. Copy the distribution folder again from its source.",
        [ErrInstallerUnreadable] = "The installer file could not be read for verification. Close any program using it and try again.",
        [ErrInstallerLaunch] = "Could not start the installer:\n{0}",
        [ErrGuideMissing] = "The user guide file was not found next to this program.",
        [ErrGuideOpen] = "Could not open the user guide. Make sure a PDF reader is installed on this computer."
    };

    public static string Get(LauncherLanguage language, string key) =>
        (language == LauncherLanguage.Arabic ? Arabic : English)[key];

    public static string Format(LauncherLanguage language, string key, params object[] args) =>
        string.Format(Get(language, key), args);

    public static IReadOnlyCollection<string> Keys(LauncherLanguage language) =>
        (language == LauncherLanguage.Arabic ? Arabic : English).Keys;
}
