using Xunit;
using CAL_QR.Models;
using CAL_QR.Validation;

namespace CAL_QR.Tests
{
    /// <summary>
    /// التبويب ونتيجة البحث يُفتحان فقط لمن يملك صلاحية التبويب، أيّاً كان طريق الوصول
    /// (الشريط الجانبيّ، البحث الشامل، أحداث التنقّل).
    /// </summary>
    public class TabAccessRulesTests
    {
        private static User NonAdmin(SystemPermissions permissions) =>
            new User { Role = UserRole.User, IsEditor = true, Permissions = permissions };

        [Theory]
        [InlineData(TabAccessRules.Dashboard)]
        [InlineData(TabAccessRules.Devices)]
        [InlineData(TabAccessRules.Owners)]
        [InlineData(TabAccessRules.Help)]
        public void NoUser_CannotOpenAnyTab(int tab)
        {
            Assert.False(TabAccessRules.CanOpenTab(null, tab));
        }

        [Theory]
        [InlineData(TabAccessRules.Dashboard)]
        [InlineData(TabAccessRules.Devices)]
        [InlineData(TabAccessRules.QrVerify)]
        [InlineData(TabAccessRules.Owners)]
        [InlineData(TabAccessRules.DeviceTypes)]
        [InlineData(TabAccessRules.Reports)]
        [InlineData(TabAccessRules.Users)]
        [InlineData(TabAccessRules.Settings)]
        [InlineData(TabAccessRules.Help)]
        [InlineData(TabAccessRules.About)]
        public void Admin_CanOpenEveryTab(int tab)
        {
            Assert.True(TabAccessRules.CanOpenTab(new User { Role = UserRole.Admin }, tab));
        }

        [Theory]
        [InlineData(TabAccessRules.Dashboard)]
        [InlineData(TabAccessRules.Help)]
        [InlineData(TabAccessRules.About)]
        public void UserWithoutPermissions_CanOpenOnlyPublicTabs(int tab)
        {
            Assert.True(TabAccessRules.CanOpenTab(NonAdmin(SystemPermissions.None), tab));
        }

        [Theory]
        [InlineData(TabAccessRules.Devices, SystemPermissions.Records)]
        [InlineData(TabAccessRules.QrVerify, SystemPermissions.Verification)]
        [InlineData(TabAccessRules.Owners, SystemPermissions.Owners)]
        [InlineData(TabAccessRules.DeviceTypes, SystemPermissions.DeviceTypes)]
        [InlineData(TabAccessRules.Reports, SystemPermissions.Reports)]
        [InlineData(TabAccessRules.Settings, SystemPermissions.Settings)]
        [InlineData(TabAccessRules.Users, SystemPermissions.UserManagement)]
        public void ProtectedTab_RequiresItsPermission(int tab, SystemPermissions required)
        {
            Assert.False(TabAccessRules.CanOpenTab(NonAdmin(SystemPermissions.None), tab));
            Assert.True(TabAccessRules.CanOpenTab(NonAdmin(required), tab));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(10)]
        public void UnknownTab_IsDenied(int tab)
        {
            Assert.False(TabAccessRules.CanOpenTab(new User { Role = UserRole.Admin }, tab));
        }

        [Theory]
        [InlineData(SearchEntityType.Device, TabAccessRules.Devices)]
        [InlineData(SearchEntityType.CalibrationRecord, TabAccessRules.Devices)]
        [InlineData(SearchEntityType.Owner, TabAccessRules.Owners)]
        [InlineData(SearchEntityType.DeviceType, TabAccessRules.DeviceTypes)]
        public void SearchResult_OpensItsTab(SearchEntityType type, int expectedTab)
        {
            Assert.Equal(expectedTab, TabAccessRules.TabFor(type));
        }

        [Theory]
        [InlineData(SearchEntityType.Device, SystemPermissions.Records)]
        [InlineData(SearchEntityType.CalibrationRecord, SystemPermissions.Records)]
        [InlineData(SearchEntityType.Owner, SystemPermissions.Owners)]
        [InlineData(SearchEntityType.DeviceType, SystemPermissions.DeviceTypes)]
        public void SearchResult_RequiresItsTabPermission(SearchEntityType type, SystemPermissions required)
        {
            Assert.False(TabAccessRules.CanOpenSearchResult(null, type));
            Assert.False(TabAccessRules.CanOpenSearchResult(NonAdmin(SystemPermissions.None), type));
            Assert.True(TabAccessRules.CanOpenSearchResult(NonAdmin(required), type));
            Assert.True(TabAccessRules.CanOpenSearchResult(new User { Role = UserRole.Admin }, type));
        }

        [Fact]
        public void OwnersPermission_DoesNotOpenDeviceResults()
        {
            var ownersOnly = NonAdmin(SystemPermissions.Owners);

            Assert.True(TabAccessRules.CanOpenSearchResult(ownersOnly, SearchEntityType.Owner));
            Assert.False(TabAccessRules.CanOpenSearchResult(ownersOnly, SearchEntityType.Device));
            Assert.False(TabAccessRules.CanOpenSearchResult(ownersOnly, SearchEntityType.CalibrationRecord));
            Assert.False(TabAccessRules.CanOpenSearchResult(ownersOnly, SearchEntityType.DeviceType));
        }
    }
}
