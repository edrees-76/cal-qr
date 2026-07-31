using System;
using System.Collections.Generic;

namespace CAL_QR.Models
{
    /// <summary>
    /// شهادة معايرة صادرة عن سجل معايرة واحد.
    /// كل القيم الرقمية تُخزَّن نصياً (string) لا رقمياً، لأن عدد الأرقام المعنوية
    /// معلومة قياسية جزء من محتوى الوثيقة تحت ISO/IEC 17025 ("5.40" تختلف عن "5.4")،
    /// ولأن بعض القيم غير رقمية أصلاً ("&lt; 0.1"، "N/A"). النظام لا يُجري أي حساب عليها.
    /// الاستثناء الوحيد: التواريخ تبقى DateTime.
    /// </summary>
    public class Certificate
    {
        public int Id { get; set; }
        public int CalibrationRecordId { get; set; }
        public string CertificateNumber { get; set; } = string.Empty;
        public string? ReferenceNo { get; set; }

        // نوع النموذج المطبوع (مثل "Pancake Probe")، منسوخ نصاً وقت الإصدار من
        // DeviceType.Name. لا يُقرأ حياً عبر CalibrationRecord → Device → DeviceType:
        // إعادة تصنيف الجهاز بعد الإصدار كانت ستُغيّر عنوان شهادة صادرة وموقّعة
        // دون كسر التوقيع — وهو تزوير غير مقصود يخالف مبدأ التجميد أعلاه.
        public string? CertificateTemplateType { get; set; }

        // حقول ظهرت في نماذج Pancake و PED. منسوخة نصاً من قالب DeviceType
        // وقت الإصدار، وقابلة للتعديل داخل الشهادة قبل الحفظ.
        public string? ProcedureNo { get; set; }
        public string? CalibrationLocation { get; set; }
        public string? Instrumentation { get; set; }
        public string? DetectorType { get; set; }

        // بيانات منسوخة نصياً وقت الإصدار (مبدأ الوثيقة التاريخية المجمّدة)
        public string ClientName { get; set; } = string.Empty;
        public string? ClientAddress { get; set; }
        public string DeviceModel { get; set; } = string.Empty;
        public string DeviceSerialNumber { get; set; } = string.Empty;
        public string? DeviceManufacturer { get; set; }
        public string? SurveyMeterModel { get; set; }
        public string? SurveyMeterSerialNumber { get; set; }

        // نوع القياس والمسافة (تظهر في نماذج PED و Dose Rate Meter)
        // مثال: MeasurementType = "Dose Rate Measurement (µSv/h)" أو "Personal Dose Measurement (µSv)"
        // مثال: Distance = "1.0 meter"
        public string? MeasurementType { get; set; }
        public string? Distance { get; set; }

        // معلومات العدّ (تظهر في نماذج المجسات: Pancake و Beta)
        // مثال: CountingTime = "60 Sec"، CountingUnit = "kCPM"
        public string? CountingTime { get; set; }
        public string? CountingUnit { get; set; }

        // نمط المعايرة، يظهر في كتلة TECHNICAL INFORMATION في العائلة الكاملة.
        // مثال: "Direct Contact Geometry". منسوخ نصاً من قالب DeviceType وقت
        // الإصدار، كنظيريه CountingTime و CountingUnit.
        // ملاحظة: خارج SIG1 في هذه المرحلة — لا يُضاف إلى SignaturePayloadBuilderV1.
        public string? CalibrationMode { get; set; }

        // الظروف البيئية
        public string? Temperature { get; set; }
        public string? RelativeHumidity { get; set; }
        public string? AtmosphericPressure { get; set; }

        // نتائج على مستوى الشهادة
        // ملاحظة: AverageCorrectionFactor المفرد أُزيل في المرحلة ٢ — كان يعجز عن
        // تمثيل أكثر من نويدة واحدة. استبدله جدول CertificateNuclideSummary،
        // وأُسقط العمود فعلياً من قاعدة البيانات.
        public string? CorrectedReadingFormula { get; set; }
        public string? ComplianceVerdict { get; set; }

        // المرجع الإجرائي للمعايرة (يظهر في كل النماذج)
        // مثال: "SSDL-TNRC Internal Calibration Procedure Ref.: SSDL-CP-01
        // Implemented in accordance with ISO/IEC 17025:2017 requirements."
        public string? CalibrationStandard { get; set; }

        // منهجية المعايرة (اختياري على مستوى القسم بأكمله)
        public bool MethodologyEnabled { get; set; } = false;
        public string? RadiationSource { get; set; }
        public string? ReferenceGeometry { get; set; }
        public string? MethodologyText { get; set; }
        public string? TraceabilityReference { get; set; }

        // ميزانية عدم اليقين (اختياري على مستوى القسم بأكمله)
        public bool UncertaintyEnabled { get; set; } = false;
        public string? CombinedUncertainty { get; set; }
        public string? ExpandedUncertainty { get; set; }
        public string? CoverageFactor { get; set; }

        // نصوص حرة
        public string? AdditionalInformation { get; set; }
        public string? Notes { get; set; }

        // التواريخ الثلاثة. CalibrationDate و DueDate منسوخان نصياً وقت الإصدار
        // تطبيقاً لمبدأ الوثيقة المجمّدة — الملصق والشهادة يقرآن منهما لا من
        // CalibrationRecord، وإلا تباعدت القيمتان بعد أول تعديل.
        // DueDate = CalibrationDate + سنة (لا من IssueDate).
        public DateTime CalibrationDate { get; set; }
        public DateTime IssueDate { get; set; }
        public DateTime DueDate { get; set; }

        // رمز QR — الوعاء فقط، المحتوى يُحدَّد في مرحلة لاحقة
        public string? QrPayload { get; set; }
        public string? QrPayloadVersion { get; set; }

        // توقيع HMAC بكامل خاناته الستة عشر — لا اشتقاق ولا اقتطاع.
        public string? VerifyCode { get; set; }

        // إصدار بانِي نص التوقيع الذي أنتج VerifyCode (مثل "SIG1"). مفصول عن
        // QrPayloadVersion عمداً: الأول يصف صيغة العرض، وهذا يصف نصاً موقّعاً
        // مجمّداً. إصدار مجهول أو خالٍ ⇒ فشل تحقق صريح، لا سقوط إلى الإصدار الحالي.
        public string? SignaturePayloadVersion { get; set; }

        // الموقّعون الأربعة — منسوخون نصياً
        public string? CalibratedByName { get; set; }
        public string? CalibratedByTitle { get; set; }
        public DateTime? CalibratedByDate { get; set; }
        public string? ReviewedByName { get; set; }
        public string? ReviewedByTitle { get; set; }
        public DateTime? ReviewedByDate { get; set; }
        public string? ApprovedByName { get; set; }
        public string? ApprovedByTitle { get; set; }
        public DateTime? ApprovedByDate { get; set; }
        public string? AuthorizedByName { get; set; }
        public string? AuthorizedByTitle { get; set; }
        public DateTime? AuthorizedByDate { get; set; }

        // النظام
        // IssuedAt ختم نظام (UTC، لحظة كتابة الصف) ولا يُخلط بـ IssueDate الإداري.
        public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

        // أول طباعة فعلية. بدونه يستحيل تنفيذ شرط «أول تعديل بعد الطباعة» حرفياً.
        public DateTime? FirstPrintedAt { get; set; }

        // تاريخ أول تعديل بعد الطباعة — لا آخره. خارج نص التوقيع بالضرورة:
        // لو دخله لَغيَّر تسجيلُ التعديل نصَّ التوقيع بذاته، فتلزم دورة إعادة حساب لا تنتهي.
        public DateTime? AmendedAt { get; set; }

        public bool IsDeleted { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public virtual CalibrationRecord? CalibrationRecord { get; set; }
        public virtual ICollection<CertificateNuclideSummary> NuclideSummaries { get; set; } = new List<CertificateNuclideSummary>();
        public virtual ICollection<CertificateCalibrationResult> CalibrationResults { get; set; } = new List<CertificateCalibrationResult>();
        public virtual ICollection<CertificateUncertaintyComponent> UncertaintyComponents { get; set; } = new List<CertificateUncertaintyComponent>();
        public virtual ICollection<CertificateFunctionalCheck> FunctionalChecks { get; set; } = new List<CertificateFunctionalCheck>();
    }
}
