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
        /// </summary>
        Task RestoreAsync(string backupFilePath, string? password = null);

        void StartScheduledBackupTimer();
    }
}
