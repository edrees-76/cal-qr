using CAL_QR.Models;

namespace CAL_QR.Validation
{
    /// <summary>
    /// من يملك عمليّات النسخ الاحتياطيّ. إظهار البطاقة يبقى مربوطاً بصلاحية «الإعدادات»،
    /// أمّا التنفيذ فله حدود أضيق:
    /// - الاستعادة تستبدل القاعدة كاملة (الحسابات والأدوار ومفتاح التوقيع)، فهي سلطة مدير:
    ///   للمدير وحده، كالتصفير الكامل.
    /// - كلمة سرّ النسخ تحكم قراءة كلّ نسخة لاحقة: للمدير وحده.
    /// - «نسخ الآن» لا يغيّر القاعدة ويُقفَل بالكلمة المضبوطة: لمن يملك «الإعدادات» ويملك التعديل.
    /// </summary>
    public static class BackupAuthorizationRules
    {
        public const string OnlyAdminRestoresMessage = "استعادة نسخة احتياطيّة تستبدل قاعدة البيانات كاملة، وهي متاحة لمدير النظام وحده.";
        public const string OnlyAdminSetsBackupPasswordMessage = "ضبط كلمة سرّ النسخ الاحتياطيّ أو تغييرها متاح لمدير النظام وحده.";
        public const string BackupNowNotAllowedMessage = "لا يملك حسابك صلاحية إنشاء نسخة احتياطيّة.";

        public static bool CanBackupNow(User? user) =>
            user != null &&
            user.HasPermission(SystemPermissions.Settings) &&
            (user.Role == UserRole.Admin || user.IsEditor);

        public static bool CanRestore(User? user) => user?.Role == UserRole.Admin;

        public static bool CanChangeBackupPassword(User? user) => user?.Role == UserRole.Admin;
    }
}
