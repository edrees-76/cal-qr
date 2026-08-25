using System.Windows.Media.Imaging;

namespace CAL_QR.Services
{
    public interface IQrService
    {
        BitmapSource GenerateQrCodeImage(string content, int sizePx);

        /// <summary>بايتات PNG للرمز — لمستهلكين خارج WPF مثل QuestPDF.</summary>
        byte[] GenerateQrCodePngBytes(string content, int sizePx);

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

    }
}
