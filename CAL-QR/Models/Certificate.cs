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

        // الظروف البيئية
        public string? Temperature { get; set; }
        public string? RelativeHumidity { get; set; }
        public string? AtmosphericPressure { get; set; }

        // نتائج على مستوى الشهادة
        public string? AverageCorrectionFactor { get; set; }
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

        // رمز QR — الوعاء فقط، المحتوى يُحدَّد في مرحلة لاحقة
        public string? QrPayload { get; set; }
        public string? QrPayloadVersion { get; set; }

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
        public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public virtual CalibrationRecord? CalibrationRecord { get; set; }
        public virtual ICollection<CertificateCalibrationResult> CalibrationResults { get; set; } = new List<CertificateCalibrationResult>();
        public virtual ICollection<CertificateUncertaintyComponent> UncertaintyComponents { get; set; } = new List<CertificateUncertaintyComponent>();
        public virtual ICollection<CertificateFunctionalCheck> FunctionalChecks { get; set; } = new List<CertificateFunctionalCheck>();
    }
}
