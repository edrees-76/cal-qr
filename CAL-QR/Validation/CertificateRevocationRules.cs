using CAL_QR.Models;

namespace CAL_QR.Validation
{
    /// <summary>
    /// إبطال شهادة صادرة: فحص في طبقة التطبيق بعلم SystemPermissions مخصّص، لا في المستودع
    /// ولا بـ UserRole (قرارات ميزة الشهادة الكاملة §5). العلم لا يُمنح من نموذج المستخدمين،
    /// و HasPermission يمنحه للمدير دائماً ⇒ الإبطال للمدير وحده (قرار إدريس، 18 نوفمبر 2026).
    /// </summary>
    public static class CertificateRevocationRules
    {
        public static bool CanRevoke(User? user) =>
            user != null && user.IsActive && user.HasPermission(SystemPermissions.RevokeCertificates);
    }
}
