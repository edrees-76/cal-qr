namespace CAL_QR.Validation
{
    /// <summary>طريقة انتهاء جلسة المستخدم في النافذة الرئيسيّة.</summary>
    public enum SessionEndReason
    {
        LogoutButton,
        WindowClosed,
        IdleLock
    }

    /// <summary>
    /// نصّ قيد سجلّ العمليّات عند انتهاء الجلسة. فعل واحد «تسجيل خروج» لكلّ الطرق ليجمعها البحث،
    /// والتفاصيل تذكر الطريقة.
    /// </summary>
    public static class SessionEndAuditRules
    {
        public const string Action = "تسجيل خروج";

        public static string Details(string username, SessionEndReason reason)
        {
            string how = reason switch
            {
                SessionEndReason.LogoutButton => "بزرّ الخروج",
                SessionEndReason.WindowClosed => "بإغلاق النافذة الرئيسيّة",
                SessionEndReason.IdleLock => "بالقفل التلقائيّ بعد الخمول",
                _ => "بطريقة غير معروفة"
            };
            return $"انتهت جلسة الحساب {username} {how}.";
        }
    }
}
