using CAL_QR.Models;

namespace CAL_QR.Validation
{
    /// <summary>نتيجة فحص السماح بحذف سجلّ معايرة.</summary>
    public enum CalibrationRecordDeletionDecision
    {
        Allowed,
        NotAdmin,
        HasCertificate,
        LegacyIssuedRecord
    }

    /// <summary>
    /// حذف سجلّ معايرة <b>لم تصدر له أيّ شهادة</b> (سجلّ أُدخل بالخطأ مثلاً): للمدير وحده.
    ///
    /// الشهادة الصادرة وثيقة تاريخيّة مجمّدة، فسجلّ له شهادة (سارية أو مبطَلة) لا يُحذف أبداً؛
    /// طريقه الوحيد الإبطال. والسجلّ المُرحَّل القديم (له توقيع أو رقم شهادة في العمود المهجور
    /// على السجلّ) ملصقه ورمزه خارج النظام بالفعل، فيُعامَل معاملة سجلّ له شهادة.
    ///
    /// قاعدة «آخر سجلّ للجهاز» لا تُطبَّق هنا: هي كانت تحمي الشهادة، وهذا المسار لا يمرّ
    /// إلا بسجلّ بلا شهادة، فلا طريق مسدود.
    /// </summary>
    public static class CalibrationRecordDeletionRules
    {
        public static CalibrationRecordDeletionDecision Evaluate(
            User? user,
            bool hasAnyCertificateRow,
            string? legacyCertificateNumber,
            string? legacyHmacSignature)
        {
            if (user == null || !user.IsActive || user.Role != UserRole.Admin)
                return CalibrationRecordDeletionDecision.NotAdmin;

            if (hasAnyCertificateRow)
                return CalibrationRecordDeletionDecision.HasCertificate;

            if (!string.IsNullOrWhiteSpace(legacyCertificateNumber)
                || !string.IsNullOrWhiteSpace(legacyHmacSignature))
                return CalibrationRecordDeletionDecision.LegacyIssuedRecord;

            return CalibrationRecordDeletionDecision.Allowed;
        }

        /// <summary>نصّ المنع بالعربيّة، أو null حين يُسمح بالحذف.</summary>
        public static string? BlockMessage(CalibrationRecordDeletionDecision decision) => decision switch
        {
            CalibrationRecordDeletionDecision.Allowed => null,
            CalibrationRecordDeletionDecision.NotAdmin =>
                "حذف سجلّ المعايرة متاح لمدير النظام وحده.",
            CalibrationRecordDeletionDecision.HasCertificate =>
                "لا يمكن حذف هذا السجلّ لأنّ له شهادة صادرة. الشهادة وثيقة مجمّدة؛ إن كانت سارية فأبطِلها من شاشتها، ويبقى السجلّ محفوظاً.",
            CalibrationRecordDeletionDecision.LegacyIssuedRecord =>
                "لا يمكن حذف هذا السجلّ لأنّه سجلّ مُرحَّل قديم له رقم شهادة أو توقيع ورمز تحقّق متداولان.",
            _ => "لا يمكن حذف هذا السجلّ."
        };
    }
}
