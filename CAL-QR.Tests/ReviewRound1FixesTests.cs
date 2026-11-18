using System.IO;
using Xunit;
using CAL_QR.Models;
using CAL_QR.Validation;

namespace CAL_QR.Tests
{
    /// <summary>
    /// إصلاحات الجولة الأولى من مراجعة 18 نوفمبر 2026 (Codex وGemini): قواعد نقيّة.
    /// </summary>
    public class ReviewRound1FixesTests
    {
        // ── نقل قاعدة البيانات: لا كتابة فوق ملفّ موجود ──

        [Fact]
        public void DatabaseMove_ExistingTarget_IsRejected()
        {
            string? error = DatabaseMoveRules.Validate(@"C:\data\cal-qr.db", @"D:\other\lab.db", targetFileExists: true);
            Assert.NotNull(error);
            Assert.Contains("يكتب فوقه", error);
        }

        [Fact]
        public void DatabaseMove_NewFile_IsAllowed()
        {
            Assert.Null(DatabaseMoveRules.Validate(@"C:\data\cal-qr.db", @"D:\new\cal-qr.db", targetFileExists: false));
        }

        [Fact]
        public void DatabaseMove_SamePath_IsRejectedRegardlessOfCase()
        {
            Assert.NotNull(DatabaseMoveRules.Validate(@"C:\Data\cal-qr.db", @"c:\data\CAL-QR.db", targetFileExists: true));
        }

        [Fact]
        public void DatabaseMove_BlankPath_IsRejected()
        {
            Assert.NotNull(DatabaseMoveRules.Validate(@"C:\data\cal-qr.db", "  ", targetFileExists: false));
        }

        // ── الملصق: لا بادئة CF مكرّرة ──

        [Fact]
        public void LabelText_KeepsEachLineLabel_WithoutExtraPrefix()
        {
            string text = CorrectionFactorLabelRules.LabelText(new[] { "CFavg Cs-137 = 1.02", "CF Co-60 = 0.98", " " });
            Assert.Equal("CFavg Cs-137 = 1.02\nCF Co-60 = 0.98", text);
            Assert.DoesNotContain("CF CF", text);
        }

        [Theory]
        [InlineData("CFavg", "CFavg: see certificate")]
        [InlineData("CF / CFavg", "CF / CFavg: see certificate")]
        [InlineData(null, "CF: see certificate")]
        [InlineData("", "CF: see certificate")]
        public void LabelFallback_UsesShortLabel(string? label, string expected)
        {
            Assert.Equal(expected, CorrectionFactorLabelRules.LabelFallback(label));
        }

        // ── الإصدار يتبع النتيجة المحفوظة ──

        [Theory]
        [InlineData("Passed", "Passed", IssuableDocument.Certificate)]
        [InlineData("Conditional", "Conditional", IssuableDocument.Certificate)]
        [InlineData("Failed", "Failed", IssuableDocument.StatusReport)]
        [InlineData("Failed", "Passed", IssuableDocument.None)]   // محفوظ راسب، معروض ناجح ⇒ لا شهادة
        [InlineData("Passed", "Failed", IssuableDocument.None)]   // محفوظ ناجح، معروض راسب ⇒ لا تقرير حالة
        public void Issuable_FollowsSavedResult_AndBlocksUnsavedChange(string saved, string shown, IssuableDocument expected)
        {
            Assert.Equal(expected, DocumentIssueRules.Issuable(true, false, saved, shown));
        }

        [Fact]
        public void Issuable_None_WhenRecordUnsavedOrCertificateExists()
        {
            Assert.Equal(IssuableDocument.None, DocumentIssueRules.Issuable(false, false, "Passed", "Passed"));
            Assert.Equal(IssuableDocument.None, DocumentIssueRules.Issuable(true, true, "Passed", "Passed"));
        }

        // ── إبطال الشهادة للمدير وحده ──

        [Fact]
        public void Revoke_AllowedForActiveAdminOnly()
        {
            Assert.True(CertificateRevocationRules.CanRevoke(new User { Role = UserRole.Admin, IsActive = true }));
            Assert.False(CertificateRevocationRules.CanRevoke(new User { Role = UserRole.Admin, IsActive = false }));
            Assert.False(CertificateRevocationRules.CanRevoke(null));
        }

        [Fact]
        public void Revoke_DeniedForEditorWithAllFormPermissions()
        {
            var editor = new User
            {
                Role = UserRole.User,
                IsActive = true,
                IsEditor = true,
                Permissions = SystemPermissions.Records | SystemPermissions.Verification | SystemPermissions.Owners |
                              SystemPermissions.DeviceTypes | SystemPermissions.Reports | SystemPermissions.Settings |
                              SystemPermissions.BackupRestore | SystemPermissions.UserManagement
            };
            Assert.False(CertificateRevocationRules.CanRevoke(editor));
        }
    }
}
