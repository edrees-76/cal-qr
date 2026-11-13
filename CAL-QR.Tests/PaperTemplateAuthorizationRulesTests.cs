using Xunit;
using CAL_QR.Models;
using CAL_QR.Validation;

namespace CAL_QR.Tests
{
    /// <summary>حفظ قالب الطباعة للمدير أو لمن يملك التعديل، مهما كانت صلاحيّاته الأخرى.</summary>
    public class PaperTemplateAuthorizationRulesTests
    {
        [Fact]
        public void NoUser_CannotSave()
        {
            Assert.False(PaperTemplateAuthorizationRules.CanSaveTemplate(null));
        }

        [Fact]
        public void Admin_CanSave()
        {
            Assert.True(PaperTemplateAuthorizationRules.CanSaveTemplate(new User { Role = UserRole.Admin }));
        }

        [Theory]
        [InlineData(UserRole.User)]
        [InlineData(UserRole.Viewer)]
        public void Editor_CanSave(UserRole role)
        {
            Assert.True(PaperTemplateAuthorizationRules.CanSaveTemplate(new User { Role = role, IsEditor = true }));
        }

        [Theory]
        [InlineData(UserRole.User)]
        [InlineData(UserRole.Viewer)]
        public void ReadOnlyUser_CannotSave_EvenWithAllPermissions(UserRole role)
        {
            var user = new User
            {
                Role = role,
                IsEditor = false,
                Permissions = SystemPermissions.Records | SystemPermissions.Reports | SystemPermissions.Settings |
                              SystemPermissions.UserManagement | SystemPermissions.Verification |
                              SystemPermissions.Owners | SystemPermissions.DeviceTypes
            };

            Assert.False(PaperTemplateAuthorizationRules.CanSaveTemplate(user));
        }
    }
}
