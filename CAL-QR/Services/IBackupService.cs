using System.Threading.Tasks;

namespace CAL_QR.Services
{
    public interface IBackupService
    {
        Task<bool> BackupNowAsync(string destinationFolder);
        Task RestoreAsync(string zipFilePath);
        void StartScheduledBackupTimer();
    }
}
