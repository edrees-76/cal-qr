using System.Threading.Tasks;

namespace CAL_QR.Services
{
    public interface IBackupService
    {
        /// <summary>
        /// يُنشئ نسخة مشفَّرة (.cqbak) بكلمة سرّ النسخ الاحتياطيّ المضبوطة على هذا الجهاز.
        /// بلا كلمة سرّ مضبوطة ⇒ InvalidOperationException ولا يُكتب أيّ ملفّ.
        /// </summary>
        Task<bool> BackupNowAsync(string destinationFolder);

        /// <summary>
        /// يستعيد نسخة مشفَّرة (.cqbak) أو نسخة قديمة غير مشفَّرة (.zip).
        /// للمشفَّرة: password إن كُتبت، وإلّا كلمة السرّ المضبوطة على هذا الجهاز.
        /// يُرجع اقتراح آخر رقم شهادة لتأكيد المدير، أو null إن لم يُضبط مخزن حدّ العدّاد.
        /// </summary>
        Task<CertificateCounterRestoreReport?> RestoreAsync(string backupFilePath, string? password = null);

        /// <summary>
        /// يثبّت آخر رقم شهادة أكّده المدير بعد الاستعادة: في القاعدة المستعادة (رفعاً فقط)
        /// وفي الحدّ خارجها. لا يخفض شيئاً.
        /// </summary>
        void ConfirmCertificateCounter(CertificateCounterRestoreReport report, int lastNumber);

        void StartScheduledBackupTimer();
    }

    /// <summary>
    /// ما يُعرض على المدير بعد الاستعادة. ProposedLastNumber = الأكبر من عدّاد النسخة المستعادة
    /// والحدّ المحفوظ خارج القاعدة (وفيه عدّاد القاعدة الحيّة قبل استبدالها إن كانت من السجلّ نفسه).
    /// Warning: ما يُضعف الاقتراح (تعذّر قراءة القاعدة الحيّة مثلاً) ليُعرض على المدير لا ليُخفى.
    /// </summary>
    public sealed record CertificateCounterRestoreReport(
        string? LedgerId,
        int Year,
        int RestoredLastNumber,
        int ProposedLastNumber,
        string? Warning);
}
