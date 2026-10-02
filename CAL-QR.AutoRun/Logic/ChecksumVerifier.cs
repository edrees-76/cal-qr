using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CalQr.AutoRun.Logic;

public enum ChecksumStatus
{
    Ok,
    FileMissing,
    ChecksumFileMissing,
    ChecksumFileInvalid,
    Unreadable,
    Mismatch
}

/// <summary>
/// يتحقق من سلامة ملف عبر ملف <c>&lt;الملف&gt;.sha256</c> بجواره (صيغة <c>HASH  FILENAME</c> كما ينتجها سكربت البناء).
/// أي نتيجة غير <see cref="ChecksumStatus.Ok"/> تعني عدم تشغيل الملف (fail-closed).
/// </summary>
public static class ChecksumVerifier
{
    public static ChecksumStatus Verify(string filePath)
    {
        if (!File.Exists(filePath)) return ChecksumStatus.FileMissing;

        var checksumPath = filePath + ".sha256";
        if (!File.Exists(checksumPath)) return ChecksumStatus.ChecksumFileMissing;

        try
        {
            var expected = ParseExpected(File.ReadAllText(checksumPath, Encoding.UTF8));
            if (expected == null) return ChecksumStatus.ChecksumFileInvalid;

            var actual = ComputeSha256(filePath);
            return string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase)
                ? ChecksumStatus.Ok
                : ChecksumStatus.Mismatch;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            return ChecksumStatus.Unreadable;
        }
    }

    /// <summary>أول رمز في الملف إن كان 64 خانة سداسية عشرية، وإلا <c>null</c>.</summary>
    public static string? ParseExpected(string? text)
    {
        var trimmed = (text ?? string.Empty).Trim().TrimStart('﻿');
        var tokens = trimmed.Split(new[] { ' ', '\t', '\r', '\n', '*' }, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) return null;

        var candidate = tokens[0];
        if (candidate.Length != 64) return null;
        foreach (var c in candidate)
        {
            var isHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
            if (!isHex) return null;
        }
        return candidate;
    }

    public static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
    }
}
