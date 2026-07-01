using System.Threading.Tasks;

namespace CAL_QR.Services
{
    public interface IBackupService
    {
        Task BackupNowAsync(string destinationFolder);
        Task RestoreAsync(string zipFilePath);
        void StartScheduledBackupTimer();
    }
}
