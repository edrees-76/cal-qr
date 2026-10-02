using System;
using System.IO;
using System.Linq;

namespace CalQr.AutoRun.Logic;

/// <summary>يحدد أماكن ملفات الحزمة بالنسبة لمجلد التوزيع (بجوار البرنامج).</summary>
public static class PackageLocator
{
    public const string InstallerPattern = "CAL-QRSetup_v*.exe";
    public const string DocsFolder = "Docs";
    public const string GuideFileName = "UserGuide.ar.pdf";

    /// <summary>أعلى إصدار من المثبِّت في المجلد، أو <c>null</c> إن لم يوجد.</summary>
    public static string? FindInstaller(string baseDirectory)
    {
        if (!Directory.Exists(baseDirectory)) return null;

        return Directory.GetFiles(baseDirectory, InstallerPattern)
            .OrderByDescending(p => ParseVersion(p) ?? new Version(0, 0))
            .ThenByDescending(p => p, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    /// <summary>الإصدار من اسم المثبِّت <c>CAL-QRSetup_v1.0.0.exe</c> → <c>1.0.0</c>.</summary>
    public static Version? ParseVersion(string installerPath)
    {
        var name = Path.GetFileNameWithoutExtension(installerPath);
        var marker = name.LastIndexOf("_v", StringComparison.OrdinalIgnoreCase);
        if (marker < 0) return null;
        return Version.TryParse(name.Substring(marker + 2), out var version) ? version : null;
    }

    /// <summary>مسار دليل الاستخدام: <c>Docs\UserGuide.ar.pdf</c> (الدليل عربي فقط ويُفتح في لغتي الواجهة).</summary>
    public static string GuidePath(string baseDirectory) =>
        Path.Combine(baseDirectory, DocsFolder, GuideFileName);
}
