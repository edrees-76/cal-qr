using CAL_QR.Models;

namespace CAL_QR.Services
{
    public interface ICertificateSignatureService
    {
        /// <summary>الإصدار الذي تُوقَّع به الشهادات الجديدة.</summary>
        string CurrentVersion { get; }

        /// <summary>
        /// نص التوقيع الداخلي — لا يُطبع ولا يظهر في الـQR.
        /// إصدار غير معروف يُلقي استثناءً: لا سقوط تلقائي إلى الإصدار الحالي.
        /// </summary>
        string BuildSignaturePayload(Certificate certificate, string version);

        /// <summary>توقيع HMAC بكامل خاناته الستة عشر — لا اشتقاق ولا اقتطاع.</summary>
        string ComputeVerifyCode(Certificate certificate, string version);

        /// <summary>حمولة الـQR — ما يُرمَّز فعلاً. مختلفة تماماً عن نص التوقيع.</summary>
        string BuildQrPayload(Certificate certificate);

        /// <summary>هل الإصدار مدعوم ببانٍ مسجَّل؟</summary>
        bool SupportsVersion(string? version);
    }
}
