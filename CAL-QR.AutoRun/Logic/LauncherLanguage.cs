using System;

namespace CalQr.AutoRun.Logic;

public enum LauncherLanguage
{
    Arabic,
    English
}

public static class LauncherLanguageExtensions
{
    public static LauncherLanguage Other(this LauncherLanguage language) =>
        language == LauncherLanguage.Arabic ? LauncherLanguage.English : LauncherLanguage.Arabic;

    /// <summary>رمز اللغة المستخدم في أسماء الملفات والتخزين: ar / en.</summary>
    public static string Code(this LauncherLanguage language) =>
        language == LauncherLanguage.Arabic ? "ar" : "en";

    /// <summary>
    /// اللغة الابتدائية: آخر اختيار محفوظ إن كان صالحاً، وإلا لغة واجهة ويندوز (ar → عربي، غير ذلك → إنجليزي).
    /// </summary>
    public static LauncherLanguage Resolve(string? savedCode, string? windowsUiLanguageTwoLetter)
    {
        var saved = savedCode?.Trim();
        if (string.Equals(saved, "ar", StringComparison.OrdinalIgnoreCase)) return LauncherLanguage.Arabic;
        if (string.Equals(saved, "en", StringComparison.OrdinalIgnoreCase)) return LauncherLanguage.English;

        return string.Equals(windowsUiLanguageTwoLetter, "ar", StringComparison.OrdinalIgnoreCase)
            ? LauncherLanguage.Arabic
            : LauncherLanguage.English;
    }
}
