namespace CAL_QR.Helpers
{
    /// <summary>
    /// نصّ الرسالة التي تعرض كلمة مرور حساب المدير الاحتياطيّ المؤقّتة عند الإقلاع.
    /// دالّة نقيّة ليُختبر النصّ بلا واجهة.
    /// </summary>
    public static class SeededAdminNotice
    {
        public const string Title = "تم إنشاء حساب مدير احتياطيّ";

        public static string BuildMessage(string username, string temporaryPassword) =>
            "لم يُعثر على أيّ مستخدم في قاعدة البيانات رغم اكتمال الإعداد الأوّل، " +
            "فأنشأ البرنامج حساب مدير احتياطيّاً.\n\n" +
            $"اسم المستخدم: {username}\n" +
            $"كلمة المرور المؤقّتة: {temporaryPassword}\n\n" +
            "دوِّن كلمة المرور الآن: لن تُعرض مرّة أخرى ولا تُحفظ في أيّ ملفّ.\n" +
            "سجّل الدخول بها ثمّ غيّرها فوراً.";
    }
}
