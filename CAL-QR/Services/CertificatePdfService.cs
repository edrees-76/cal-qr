using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using QuestPDF.Fluent;
using CAL_QR.Models;
using CAL_QR.Services.Documents;

namespace CAL_QR.Services
{
    public class CertificatePdfService : ICertificatePdfService
    {
        private readonly IQrService _qrService;
        private readonly byte[]? _centerLogoBytes;
        private readonly byte[]? _establishmentLogoBytes;

        public CertificatePdfService(IQrService qrService)
        {
            _qrService = qrService;
            CertificatePdfEnvironment.EnsureInitialized();
            _centerLogoBytes = LoadLogoBytes("logo_original_enhanced_1024.png");
            _establishmentLogoBytes = LoadLogoBytes("establishment-logo.png");
        }

        public byte[] GenerateBytes(Certificate certificate)
        {
            if (certificate == null) throw new ArgumentNullException(nameof(certificate));

            var assets = BuildAssets(certificate);
            var document = new CertificateDocument(certificate, assets);

            // TODO المرحلة 5-b: المستدعي (ViewModel) يستدعي MarkPrintedAsync بعد أول توليد ناجح.
            return document.GeneratePdf();
        }

        public Task GenerateFileAsync(Certificate certificate, string filePath)
        {
            if (certificate == null) throw new ArgumentNullException(nameof(certificate));

            return Task.Run(() =>
            {
                byte[] bytes = GenerateBytes(certificate);
                File.WriteAllBytes(filePath, bytes);
            });
        }

        private CertificateDocumentAssets BuildAssets(Certificate certificate)
        {
            byte[]? qrBytes = null;

            if (!string.IsNullOrWhiteSpace(certificate.QrPayload))
            {
                try
                {
                    qrBytes = _qrService.GenerateQrCodePngBytes(certificate.QrPayload!, 300);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CertificatePdfService] Error generating QR code: {ex.Message}");
                }
            }

            return new CertificateDocumentAssets
            {
                CenterLogoPng = _centerLogoBytes,
                EstablishmentLogoPng = _establishmentLogoBytes,
                QrPng = qrBytes
            };
        }

        private static byte[]? LoadLogoBytes(string fileName)
        {
            try
            {
                string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Logo", fileName);
                if (File.Exists(logoPath))
                {
                    return File.ReadAllBytes(logoPath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CertificatePdfService] Error loading logo: {ex.Message}");
            }

            return null;
        }
    }
}
