using System;
using System.Threading.Tasks;
using CAL_QR.Models;

namespace CAL_QR.Repositories
{
    /// <summary>حالة التحقق. «تعذّر التحقق» ≠ «فشل التحقق» — والتمييز شرط صحة.</summary>
    public enum CertificateVerificationStatus
    {
        /// <summary>الرمز يطابق الرمز الحالي.</summary>
        Authentic,

        /// <summary>الرمز يطابق رمزاً تاريخياً: الوثيقة أصلية لكنها عُدّلت بعد طباعتها.</summary>
        AuthenticAmended,

        /// <summary>لا رمز حالياً ولا تاريخياً يطابق.</summary>
        NotFound,

        /// <summary>الشهادة موجودة لكن إصدار توقيعها غير مدعوم — تعذّر لا فشل.</summary>
        UnsupportedVersion
    }

    public class CertificateVerificationResult
    {
        public CertificateVerificationStatus Status { get; set; }
        public Certificate? Certificate { get; set; }
        public DateTime? AmendedAt { get; set; }
    }

    public interface ICertificateRepository
    {
        Task<Certificate?> GetByIdAsync(int id);
        Task<Certificate?> GetByCertificateNumberAsync(string certificateNumber);

        /// <summary>
        /// إصدار شهادة: تخصيص الرقم والتوقيع والحفظ في معاملة واحدة.
        /// يُعيد الرقم المخصَّص.
        /// </summary>
        Task<string> AddAsync(Certificate certificate);

        /// <summary>
        /// تعديل شهادة، مع إعادة حساب التوقيع وأرشفة الرمز السابق عند لزومه.
        /// يُعيد true إن دُوِّر الرمز فعلاً.
        /// </summary>
        Task<bool> UpdateAsync(Certificate certificate);

        /// <summary>تسجيل أول طباعة. استدعاؤها ثانيةً لا يغيّر شيئاً.</summary>
        Task MarkPrintedAsync(int certificateId);

        /// <summary>يبدّل حالة إرفاق النسخة الموقّعة. يُرجع الحالة الجديدة.</summary>
        Task<bool> ToggleSignedCopyAsync(int certificateId);

        /// <summary>بحث بالرمز في الرموز الحالية أولاً ثم التاريخية.</summary>
        Task<CertificateVerificationResult> VerifyByCodeAsync(string verifyCode);

        // ملاحظة: لا توجد دالّة إبطال شهادة في هذه المرحلة عمداً — انظر البند 5
        // في جدول التبعيات الحاجزة بملف القرارات. المخطط يدعمها فعلاً (الفهرس
        // الفريد المشروط على CalibrationRecordId يسمح بشهادة بديلة بعد الإبطال)،
        // لكن فحص التصريح شأن طبقة التطبيق لا المستودع.
    }
}
