using System;

namespace CAL_QR.Models
{
    /// <summary>
    /// أرشيف رموز التحقق التي استُبدلت بإعادة حساب بعد تعديل شهادة.
    ///
    /// السبب — مساران قائمان يبحثان بالرمز وحده:
    /// (١) مسار «الكود السريع» في QrVerifyViewModel يبحث بالرمز بلا رقم شهادة،
    ///     فرمز قديم بلا أرشيف يُعيد «غير موجودة».
    /// (٢) الأوراق المطبوعة سابقاً تحمل الرمز القديم، فالمقارنة بعد التعديل
    ///     تُعيد «مزوّرة» على وثيقة أصلية.
    ///
    /// VerifyCode غير فريد عمداً: تكرار رمز ١٦ خانة عبر شهادتين ممكن نظرياً،
    /// وفهرس فريد هنا كان سيُسقِط عملية تعديل مشروعة. التمييز عند العرض لا الكتابة.
    /// </summary>
    public class CertificateVerifyCodeHistory
    {
        public int Id { get; set; }
        public int CertificateId { get; set; }

        /// <summary>الرمز المستبدَل، بكامل خاناته الستة عشر.</summary>
        public string VerifyCode { get; set; } = string.Empty;

        /// <summary>إصدار البانِي الذي أنتج هذا الرمز.</summary>
        public string? SignaturePayloadVersion { get; set; }

        public DateTime ReplacedAt { get; set; } = DateTime.UtcNow;

        public virtual Certificate? Certificate { get; set; }
    }
}
