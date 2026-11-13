using CAL_QR.Models;

namespace CAL_QR.Validation
{
    /// <summary>
    /// مَن يفتح أيّ تبويب. مصدر واحد لكلّ طرق الوصول: الشريط الجانبيّ، اختصارات لوحة المفاتيح،
    /// نتائج البحث الشامل، وأحداث التنقّل (لوحة التحكّم، تفاصيل الجهة المالكة ونوع الجهاز).
    /// إخفاء رأس التبويب وحده لا يمنع الوصول إليه من هذه الطرق.
    /// </summary>
    public static class TabAccessRules
    {
        public const int Dashboard = 0;
        public const int Devices = 1;
        public const int QrVerify = 2;
        public const int Owners = 3;
        public const int DeviceTypes = 4;
        public const int Reports = 5;
        public const int Users = 6;
        public const int Settings = 7;
        public const int Help = 8;
        public const int About = 9;

        public const string DevicesNotAllowedMessage = "لا يملك حسابك صلاحية عرض الأجهزة والشهادات.";

        public static bool CanOpenTab(User? user, int tabIndex)
        {
            if (user == null) return false;

            return tabIndex switch
            {
                Dashboard or Help or About => true,
                Devices => user.HasPermission(SystemPermissions.Records),
                QrVerify => user.HasPermission(SystemPermissions.Verification),
                Owners => user.HasPermission(SystemPermissions.Owners),
                DeviceTypes => user.HasPermission(SystemPermissions.DeviceTypes),
                Reports => user.HasPermission(SystemPermissions.Reports),
                Settings => user.HasPermission(SystemPermissions.Settings),
                Users => user.HasPermission(SystemPermissions.UserManagement),
                _ => false
            };
        }

        /// <summary>التبويب الذي تفتحه نتيجة البحث من هذا النوع.</summary>
        public static int TabFor(SearchEntityType entityType) => entityType switch
        {
            SearchEntityType.Device or SearchEntityType.CalibrationRecord => Devices,
            SearchEntityType.Owner => Owners,
            SearchEntityType.DeviceType => DeviceTypes,
            _ => -1
        };

        /// <summary>تُعرض نتيجة البحث وتُفتح فقط لمن يملك تبويبها.</summary>
        public static bool CanOpenSearchResult(User? user, SearchEntityType entityType) =>
            CanOpenTab(user, TabFor(entityType));
    }
}
