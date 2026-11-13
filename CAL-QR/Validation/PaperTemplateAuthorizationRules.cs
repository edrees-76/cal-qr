using CAL_QR.Models;

namespace CAL_QR.Validation
{
    /// <summary>
    /// حفظ قالب ورق الملصقات (إضافةً أو تعديلاً أو جعله افتراضيّاً) يغيّر تخطيط كلّ طباعة لاحقة،
    /// فهو للمدير أو لمن يملك التعديل — نفس حدّ أوامر القوالب في الإعدادات. ويبقى الفحص في نافذة
    /// القالب نفسها لأنّها تُفتح أيضاً من معاينة الطباعة («قالب جديد») المتاحة لكلّ من يطبع.
    /// </summary>
    public static class PaperTemplateAuthorizationRules
    {
        public const string SaveNotAllowedMessage = "حفظ قوالب الطباعة متاح لمدير النظام أو لمن يملك صلاحية التعديل.";

        public static bool CanSaveTemplate(User? user) =>
            user != null && (user.Role == UserRole.Admin || user.IsEditor);
    }
}
