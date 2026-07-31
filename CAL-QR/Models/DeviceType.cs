using System;
using System.Collections.Generic;

namespace CAL_QR.Models
{
    /// <summary>
    /// نوع الجهاز، وهو أيضاً **حامل قالب الشهادة** لهذا النوع.
    ///
    /// القيم أدناه تُنسخ نصاً إلى الشهادة وقت الإصدار ثم تُجمَّد فيها. تغيير القالب
    /// لاحقاً لا يُغيّر شهادة صدرت — تطبيقاً لمبدأ الوثيقة التاريخية المجمّدة
    /// الموثّق في Certificate.
    ///
    /// كلها nullable عمداً: القيمة الخالية تعني «لا قالب لهذا الحقل في هذا النوع»
    /// لا «قيمة فارغة»، وهو التمييز الذي يعتمد عليه بذر «الملء لا الدهس».
    ///
    /// ملاحظة على ما ليس قالباً: RadiationSource يتغير بكل معايرة حسب المصادر
    /// المستعملة فعلاً، فهو إدخال يدوي على الشهادة ولا قالب له هنا.
    /// </summary>
    public class DeviceType
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        // ── قالب الشهادة: حقول نصية ──
        public string? ProcedureNo { get; set; }
        public string? CalibrationLocation { get; set; }
        public string? ReferenceGeometry { get; set; }
        public string? CountingTime { get; set; }
        public string? CountingUnit { get; set; }
        public string? CalibrationMode { get; set; }
        public string? MethodologyText { get; set; }
        public string? TraceabilityReference { get; set; }
        public string? ComplianceVerdict { get; set; }
        public string? CalibrationStandard { get; set; }
        public string? Notes { get; set; }
        public string? AdditionalInformation { get; set; }
        public string? MeasurementType { get; set; }
        public string? Distance { get; set; }
        public string? CorrectedReadingFormula { get; set; }
        public string? DetectorType { get; set; }
        public string? Instrumentation { get; set; }

        // ── قالب الشهادة: علما إظهار القسمين ──
        // منطقيان لا nullable: false قيمة مشروعة («القسم مخفي») لا «لم يُضبط».
        // ولذلك يُضبطان عند إنشاء النوع فقط، ولا يمسّهما مسار الملء في البذر —
        // إذ لا سبيل لتمييز «لم يُضبط» من «ضُبط إلى لا».
        public bool UncertaintyEnabled { get; set; } = false;
        public bool MethodologyEnabled { get; set; } = false;

        public bool IsDeleted { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<Device> Devices { get; set; } = new List<Device>();

        public virtual ICollection<DeviceTypeFunctionalCheckTemplate> FunctionalCheckTemplates { get; set; }
            = new List<DeviceTypeFunctionalCheckTemplate>();

        public virtual ICollection<DeviceTypeUncertaintyComponentTemplate> UncertaintyComponentTemplates { get; set; }
            = new List<DeviceTypeUncertaintyComponentTemplate>();
    }
}
