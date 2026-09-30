namespace CAL_QR.Validation
{
    /// <summary>الحدّ الأدنى لكلمة مرور حساب المستخدم عند إنشائها أو تغييرها (الحسابات القائمة لا تتأثّر حتّى تُغيَّر).</summary>
    public static class UserPasswordRules
    {
        public const int MinLength = 8;

        /// <summary>يُرجع رسالة الخطأ، أو null إن قُبلت الكلمة.</summary>
        public static string? Validate(string? password)
        {
            if (string.IsNullOrWhiteSpace(password))
                return "الرجاء إدخال كلمة المرور.";
            if (password.Length < MinLength)
                return $"كلمة المرور قصيرة: {MinLength} أحرف على الأقلّ.";
            return null;
        }
    }
}
