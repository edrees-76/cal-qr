using CAL_QR.Models;

namespace CAL_QR.Services
{
    /// <summary>
    /// نتيجة بناء المسوّدة: الشهادة غير المحفوظة، وعلَم يخبر الـViewModel أن النوع
    /// بلا قالب فيعرض التحذير.
    ///
    /// العلَم قيمة راجعة لا استثناء: نوع بلا قالب حالة مشروعة تماماً — نوع أُنشئ
    /// يدوياً من نموذج المعايرة مثلاً — والشهادة تصدر منه صحيحة بعد ملء يدوي كامل.
    /// رمي استثناء كان سيمنع إصدار شهادة سليمة.
    /// </summary>
    public sealed class CertificateDraftResult
    {
        public Certificate Certificate { get; init; } = null!;

        /// <summary>false ⇒ النوع بلا قالب: لا حقل نصي واحد ولا صفّ قالب واحد.</summary>
        public bool HasTemplate { get; init; }
    }

    /// <summary>
    /// بانِي مسوّدة الشهادة: دالّة صرفة بلا DbContext وبلا حالة.
    ///
    /// كل ما تفعله نسخٌ حقلاً بحقل من القالب والسياق إلى كائن Certificate غير محفوظ.
    /// لا تخصّص رقماً ولا توقيعاً ولا QR ولا DueDate — تلك كلها مسؤولية
    /// CertificateRepository.AddAsync وحدها. ولا تضبط IssueDate: تاريخ إداري
    /// يقرّره الـViewModel، لا نسخةٌ من قالب.
    /// </summary>
    public interface ICertificateDraftBuilder
    {
        /// <param name="deviceType">النوع بقوالبه مضمومة (GetByIdWithTemplatesAsync).</param>
        CertificateDraftResult Build(
            DeviceType deviceType,
            CalibrationRecord record,
            Device device,
            Owner owner);
    }
}
