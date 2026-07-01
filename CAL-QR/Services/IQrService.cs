using System.Windows.Media.Imaging;

namespace CAL_QR.Services
{
    public interface IQrService
    {
        BitmapSource GenerateQrCodeImage(string content, int sizePx);
        
        string GenerateVerificationText(
            string ownerName,
            string deviceType,
            string model,
            string serial,
            string certNo,
            string calDate,
            string expDate,
            string engineerName,
            string description,
            string result,
            string verifyCode);

        void SaveQrCodeImage(string content, string certificateNumber);

        void GenerateAndSaveQrForRecord(
            string ownerName,
            string deviceType,
            string model,
            string serial,
            string certNo,
            string calDate,
            string expDate,
            string engineerName,
            string description,
            string result,
            string verifyCode);
    }
}
