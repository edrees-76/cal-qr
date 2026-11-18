using System;

namespace CAL_QR.Validation
{
    public enum IssuableDocument
    {
        None,
        Certificate,
        StatusReport
    }

    /// <summary>
    /// أيّ وثيقة يجوز إصدارها لسجلّ معايرة. نموذج الشهادة يقرأ السجلّ المحفوظ، فالقرار
    /// يتبع النتيجة المحفوظة لا المعروضة: نتيجة غُيّرت ولم تُحفظ تُوقف الزرّين حتّى الحفظ،
    /// وإلّا صدرت شهادة معايرة لسجلّ محفوظ «راسب» (أو العكس).
    /// </summary>
    public static class DocumentIssueRules
    {
        public const string ResultFailed = "Failed";

        public static IssuableDocument Issuable(bool recordSaved, bool hasCertificate, string? savedResult, string? shownResult)
        {
            if (!recordSaved || hasCertificate || HasUnsavedResult(savedResult, shownResult))
                return IssuableDocument.None;

            return savedResult == ResultFailed ? IssuableDocument.StatusReport : IssuableDocument.Certificate;
        }

        public static bool HasUnsavedResult(string? savedResult, string? shownResult) =>
            !string.Equals(savedResult ?? string.Empty, shownResult ?? string.Empty, StringComparison.Ordinal);
    }
}
