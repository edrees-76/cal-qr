using Xunit;
using CAL_QR.Models;
using CAL_QR.Validation;

namespace CAL_QR.Tests
{
    /// <summary>
    /// الاستعادة وكلمة سرّ النسخ للمدير وحده؛ «نسخ الآن» لمن يملك «الإعدادات» ويملك التعديل.
    /// صلاحية «الإعدادات» وحدها لم تعد كافية لأيّ منها.
    /// </summary>
    public class BackupAuthorizationRulesTests
    {
        private static User NonAdmin(UserRole role, bool isEditor, SystemPermissions permissions) =>
            new User { Role = role, IsEditor = isEditor, Permissions = permissions };

        [Fact]
        public void NoUser_IsDeniedEverything()
        {
            Assert.False(BackupAuthorizationRules.CanBackupNow(null));
            Assert.False(BackupAuthorizationRules.CanRestore(null));
            Assert.False(BackupAuthorizationRules.CanChangeBackupPassword(null));
        }

        [Fact]
        public void Admin_IsAllowedEverything()
        {
            var admin = new User { Role = UserRole.Admin };

            Assert.True(BackupAuthorizationRules.CanBackupNow(admin));
            Assert.True(BackupAuthorizationRules.CanRestore(admin));
            Assert.True(BackupAuthorizationRules.CanChangeBackupPassword(admin));
        }

        [Theory]
        [InlineData(UserRole.Viewer, false)]
        [InlineData(UserRole.Viewer, true)]
        [InlineData(UserRole.User, false)]
        [InlineData(UserRole.User, true)]
        public void NonAdmin_WithSettings_CannotRestoreOrChangeBackupPassword(UserRole role, bool isEditor)
        {
            var user = NonAdmin(role, isEditor, SystemPermissions.Settings);

            Assert.False(BackupAuthorizationRules.CanRestore(user));
            Assert.False(BackupAuthorizationRules.CanChangeBackupPassword(user));
        }

        [Theory]
        [InlineData(UserRole.Viewer)]
        [InlineData(UserRole.User)]
        public void NonAdminEditor_WithSettings_CanBackupNow(UserRole role)
        {
            Assert.True(BackupAuthorizationRules.CanBackupNow(NonAdmin(role, true, SystemPermissions.Settings)));
        }

        [Theory]
        [InlineData(UserRole.Viewer)]
        [InlineData(UserRole.User)]
        public void ReadOnlyUser_WithSettings_CannotBackupNow(UserRole role)
        {
            Assert.False(BackupAuthorizationRules.CanBackupNow(NonAdmin(role, false, SystemPermissions.Settings)));
        }

        [Fact]
        public void Editor_WithoutSettings_CannotBackupNow()
        {
            Assert.False(BackupAuthorizationRules.CanBackupNow(NonAdmin(UserRole.User, true, SystemPermissions.Records)));
        }

        [Fact]
        public void LegacyBackupRestoreFlag_DoesNotGrantRestore()
        {
            // العلم القديم BackupRestore ما زال في بيانات بعض الحسابات؛ لا يمنح الاستعادة لغير المدير.
            var user = NonAdmin(UserRole.User, true, SystemPermissions.Settings | SystemPermissions.BackupRestore);

            Assert.False(BackupAuthorizationRules.CanRestore(user));
            Assert.False(BackupAuthorizationRules.CanChangeBackupPassword(user));
        }
    }
}
