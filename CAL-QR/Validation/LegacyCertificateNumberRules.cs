using System;
using System.Text.RegularExpressions;
using CAL_QR.Services;

namespace CAL_QR.Validation
{
    /// <summary>
    /// حقل الرقم القديم في سجلّ المعايرة (CalibrationRecord.CertificateNumber) يبقى قابلاً للتحرير
    /// للسجلّات المُرحَّلة بترقيم ورقيّ. لكنّ صيغة الترقيم الرسميّة TNRC-SSDL-YYYY-XXXX محجوزة
    /// للأرقام التي يخصّصها النظام عند الإصدار، فلا تُكتب فيه يدويّاً، ولا يُكتب فيه رقم شهادة
    /// موجودة. وإلّا أمكن حجز رقم سيُخصَّص لاحقاً لسجلّ آخر (NV-7).
    ///
    /// القيمة المخزَّنة سابقاً لا تُرفض إن لم تتغيّر: السجلّ الذي أُبطلت شهادته يحمل رقمها
    /// منسوخاً في هذا الحقل، وحفظ تعديل على حقوله الأخرى يجب ألّا يفشل بسببه.
    /// </summary>
    public static class LegacyCertificateNumberRules
    {
        public const string ReservedFormatMessage =
            "تنبيه: صيغة الرقم TNRC-SSDL-سنة-تسلسل محجوزة للأرقام التي يخصّصها النظام عند إصدار الشهادة، ولا تُكتب يدويّاً.";

        public const string ExistingCertificateMessage =
            "تنبيه: هذا الرقم يخصّ شهادة صادرة في النظام، ولا يُكتب في سجلّ آخر.";

        private static readonly Regex ReservedPattern = new Regex(
            "^" + Regex.Escape(CertificateNumberService.Prefix) + @"-\d{4}-\d+$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>هل الرقم بصيغة الترقيم الرسميّة؟ (بعد قصّ الفراغات، دون تمييز حالة الأحرف)</summary>
        public static bool IsReservedFormat(string? number) =>
            !string.IsNullOrWhiteSpace(number) && ReservedPattern.IsMatch(number.Trim());

        /// <summary>هل تغيّرت القيمة عن المخزَّنة؟ يُقارَن بعد قصّ الفراغات.</summary>
        public static bool IsChanged(string? original, string? typed) =>
            !string.Equals((original ?? string.Empty).Trim(), (typed ?? string.Empty).Trim(), StringComparison.Ordinal);

        /// <summary>
        /// رسالة الرفض أو null. الفحص لا يقع إلّا على قيمة غير فارغة تغيّرت عن المخزَّنة.
        /// </summary>
        /// <param name="original">القيمة المخزَّنة وقت التحميل.</param>
        /// <param name="typed">القيمة المراد حفظها.</param>
        /// <param name="belongsToExistingCertificate">هل في جدول الشهادات شهادة بهذا الرقم (سارية أو مُبطَلة)؟</param>
        public static string? Validate(string? original, string? typed, bool belongsToExistingCertificate)
        {
            if (string.IsNullOrWhiteSpace(typed) || !IsChanged(original, typed))
                return null;

            if (IsReservedFormat(typed))
                return ReservedFormatMessage;

            if (belongsToExistingCertificate)
                return ExistingCertificateMessage;

            return null;
        }
    }
}
