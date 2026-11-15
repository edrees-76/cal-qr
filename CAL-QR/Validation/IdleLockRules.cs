namespace CAL_QR.Validation
{
    /// <summary>
    /// حدّ مدّة الإغلاق التلقائيّ عند الخمول (بالدقائق).
    ///
    /// صفر أو قيمة سالبة تجعل شرط المؤقّت (الخمول ≥ المدّة) صحيحاً دائماً، فيقفل البرنامج
    /// خلال ثوانٍ من كلّ دخول ولكلّ المستخدمين، ولا يبقى وقت للوصول إلى الإعدادات لتصحيحها.
    /// لذلك تُرفض عند الحفظ، وتُهمَل إن وُجدت مخزَّنة في قاعدة قائمة.
    /// </summary>
    public static class IdleLockRules
    {
        public const int MinMinutes = 1;
        public const int DefaultMinutes = 10;

        /// <summary>يُرجع رسالة الخطأ، أو null إن قُبلت المدّة.</summary>
        public static string? Validate(int minutes)
        {
            if (minutes < MinMinutes)
                return $"مدّة الإغلاق التلقائيّ عند الخمول يجب ألّا تقلّ عن {MinMinutes} دقيقة.";
            return null;
        }

        /// <summary>
        /// المدّة التي يعمل بها المؤقّت: القيمة المخزَّنة إن كانت صالحة، وإلّا
        /// <paramref name="current"/> (المدّة المعمول بها قبل القراءة).
        /// </summary>
        public static int EffectiveMinutes(string? storedValue, int current) =>
            TryReadStored(storedValue, out int minutes) ? minutes : current;

        /// <summary>هل القيمة المخزَّنة عدد صحيح مقبول؟ نصّ غير رقميّ أو مدّة مرفوضة ⇒ false.</summary>
        public static bool TryReadStored(string? storedValue, out int minutes) =>
            int.TryParse(storedValue, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out minutes)
            && Validate(minutes) == null;
    }
}
