using System.Threading.Tasks;
using CAL_QR.Models;

namespace CAL_QR.Services
{
    public interface ICertificatePdfService
    {
        /// <summary>توليد متزامن في الذاكرة — النواة القابلة للاختبار.</summary>
        byte[] GenerateBytes(Certificate certificate);

        /// <summary>كتابة إلى ملف خارج خيط الواجهة، أسوةً بـ ExportService.</summary>
        Task GenerateFileAsync(Certificate certificate, string filePath);
    }
}
