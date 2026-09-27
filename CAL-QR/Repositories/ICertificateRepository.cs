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
        UnsupportedVersion,

        /// <summary>الشهادة أصلية لكنها ألغيت رسمياً. صادرة ثم سُحبت — التوقيع سليم والمحتوى غير معدَّل.</summary>
        Revoked
    }

    public class CertificateVerificationResult
    {
        public CertificateVerificationStatus Status { get; set; }
        public Certificate? Certificate { get; set; }
        public DateTime? AmendedAt { get; set; }

        // مجموعتان من حقول الإلغاء — تُملأ فقط عند Status == Revoked
        public DateTime? RevokedAt { get; set; }
        public string? RevokedByName { get; set; }
        public string? RevocationReason { get; set; }
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

        /// <summary>
        /// يُعيد اشتقاق حالة النسخة الموقّعة من عدد المرفقات المرتبطة فعلًا
        /// بالشهادة. الحالة نتيجة لا إقرار: لا تصير الشهادة «مكتملة» إلّا
        /// ووراءها ملفّ حقيقيّ. يُرجع الحالة بعد المزامنة.
        /// </summary>
        Task<bool> SyncSignedCopyStateAsync(int certificateId);

        /// <summary>بحث بالرمز في الرموز الحالية أولاً ثم الملغاة ثم التاريخية.</summary>
        Task<CertificateVerificationResult> VerifyByCodeAsync(string verifyCode);

        /// <summary>
        /// إلغاء شهادة صادرة. يضبط IsRevoked = true و IsDeleted = true معاً؛
        /// الجمع بينهما يُطلق الفهرس الفريد المشروط (IsDeleted = 0) ويُتيح إصدار
        /// شهادة بديلة لنفس سجل المعايرة. لا تغيير في التوقيع ولا في حمولة QR
        /// ولا في أي بيانات قياسيّة — الشهادة تُقرأ كاملةً عبر التحقّق لكنّها تُعيد Revoked.
        /// </summary>
        Task RevokeAsync(int certificateId, string revokedByName, string reason);
    }
}
