namespace CAL_QR.Validation
{
    /// <summary>
    /// قبول كلمة سرّ النسخ الاحتياطيّ عند ضبطها. الطول هو الحماية الوحيدة لنسخة
    /// تسرّبت: من يملك الملفّ يجرّب كلمات السرّ على جهازه بلا حدّ للمحاولات.
    /// </summary>
    public static class BackupPasswordRules
    {
        public const int MinLength = 10;

        /// <summary>يُرجع رسالة الخطأ، أو null إن قُبلت الكلمة.</summary>
        public static string? Validate(string? password, string? confirmation)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return "اكتب كلمة سرّ النسخ الاحتياطيّ أوّلاً.";
            }

            if (password != password.Trim())
            {
                return "كلمة السرّ تبدأ أو تنتهي بمسافة؛ احذفها حتّى لا تضيع عند كتابتها لاحقاً.";
            }

            if (password.Length < MinLength)
            {
                return $"كلمة السرّ قصيرة: {MinLength} محارف على الأقلّ.";
            }

            if (password != confirmation)
            {
                return "كلمتا السرّ غير متطابقتين.";
            }

            return null;
        }
    }
}
