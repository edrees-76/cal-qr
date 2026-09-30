using CAL_QR.Models;

namespace CAL_QR.Validation
{
    /// <summary>
    /// قواعد إدارة الحسابات: وحده المدير (Admin) يُنشئ مديراً أو يغيّر دور أيّ حساب من/إلى Admin
    /// أو يعدّل حساب مدير قائم. كلّ دالّة تُرجع null إن سُمح، أو رسالة الرفض.
    /// </summary>
    public static class UserManagementRules
    {
        public const string OnlyAdminAssignsAdminMessage = "لا يملك حسابك صلاحية منح دور «مدير» أو سحبه. هذا الإجراء للمدير فقط.";
        public const string OnlyAdminModifiesAdminMessage = "لا يملك حسابك صلاحية تعديل حساب مدير. هذا الإجراء للمدير فقط.";
        public const string LastActiveAdminMessage = "لا يمكن إتمام العملية لأنها تترك النظام بلا أيّ حساب مدير نشط.";

        /// <summary>هل يجوز للفاعل أن يُسنِد الدور المختار (إنشاء أو تغيير)؟ null = مسموح.</summary>
        public static string? CanAssignRole(UserRole actorRole, UserRole targetRole)
        {
            if (targetRole == UserRole.Admin && actorRole != UserRole.Admin)
            {
                return OnlyAdminAssignsAdminMessage;
            }

            return null;
        }

        /// <summary>هل يجوز للفاعل تعديل/تفعيل/تجميد/إعادة ضبط كلمة سرّ حساب قائم دوره الحاليّ المعطى؟ null = مسموح.</summary>
        public static string? CanModifyUser(UserRole actorRole, UserRole existingTargetRole)
        {
            if (existingTargetRole == UserRole.Admin && actorRole != UserRole.Admin)
            {
                return OnlyAdminModifiesAdminMessage;
            }

            return null;
        }

        /// <summary>
        /// هل يترك التغيير النظام بلا مدير نشط؟ true = يُرفض.
        /// activeAdminsCount هو العدّاد الحاليّ قبل الحفظ (يشمل الهدف إن كان مديراً نشطاً).
        /// </summary>
        public static bool WouldLeaveNoActiveAdmin(bool targetWasActiveAdmin, bool targetWillBeActiveAdmin, int activeAdminsCount)
        {
            if (!targetWasActiveAdmin || targetWillBeActiveAdmin)
            {
                return false;
            }

            return activeAdminsCount <= 1;
        }

        /// <summary>هل الحساب مدير نشط (دور Admin وفعّال)؟</summary>
        public static bool IsActiveAdmin(UserRole role, bool isActive) => role == UserRole.Admin && isActive;
    }
}
