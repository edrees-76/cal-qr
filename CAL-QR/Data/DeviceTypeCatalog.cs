using System;
using System.Collections.Generic;
using System.Linq;

namespace CAL_QR.Data
{
    /// <summary>قالب فحص وظيفي في الكتالوج (نسخة قراءة فقط، لا كيان قاعدة بيانات).</summary>
    public sealed class CatalogFunctionalCheck
    {
        public int SortOrder { get; init; }
        public string CheckName { get; init; } = string.Empty;
        public string? Requirement { get; init; }
        public string? DefaultResult { get; init; }
    }

    /// <summary>قالب مكوّن عدم يقين في الكتالوج.</summary>
    public sealed class CatalogUncertaintyComponent
    {
        public int SortOrder { get; init; }
        public string ComponentName { get; init; } = string.Empty;
        public string? EvaluationType { get; init; }
    }

    /// <summary>تعريف نوع جهاز واحد: اسمه المعتمد، ومرادفاته القديمة، وقالبه.</summary>
    public sealed class DeviceTypeDefinition
    {
        /// <summary>الاسم المعتمد. يُنسخ إلى Certificate.CertificateTemplateType ويدخل التوقيع.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// الأسماء القديمة لنفس النوع في قواعد مثبَّتة. إصابة أيٍّ منها تعني
        /// **إعادة تسمية الصفّ القائم** لا إنشاء صفّ جديد.
        /// </summary>
        public string[] Aliases { get; init; } = Array.Empty<string>();

        public string? ProcedureNo { get; init; }
        public string? CalibrationLocation { get; init; }
        public string? ReferenceGeometry { get; init; }
        public string? CountingTime { get; init; }
        public string? CountingUnit { get; init; }
        public string? MethodologyText { get; init; }
        public string? TraceabilityReference { get; init; }
        public string? ComplianceVerdict { get; init; }
        public string? CalibrationStandard { get; init; }
        public string? Notes { get; init; }
        public string? AdditionalInformation { get; init; }
        public string? MeasurementType { get; init; }
        public string? Distance { get; init; }
        public string? CorrectedReadingFormula { get; init; }
        public string? DetectorType { get; init; }
        public string? Instrumentation { get; init; }

        public bool UncertaintyEnabled { get; init; }
        public bool MethodologyEnabled { get; init; }

        public CatalogFunctionalCheck[] FunctionalChecks { get; init; } = Array.Empty<CatalogFunctionalCheck>();
        public CatalogUncertaintyComponent[] UncertaintyComponents { get; init; } = Array.Empty<CatalogUncertaintyComponent>();
    }

    /// <summary>
    /// ╔════════════════════════════════════════════════════════════════════════╗
    /// ║  مصدر الحقيقة الوحيد لأنواع الأجهزة الخمسة وقوالبها.                  ║
    /// ╚════════════════════════════════════════════════════════════════════════╝
    ///
    /// ⚠ لا تُكرَّر هذه الأسماء في أي ملف آخر. كان الازدواج قائماً فعلاً:
    /// SystemResetService كان يحمل مصفوفة أسماء خاصة به، وثلاثة من خمسة فيها
    /// تخالف أسماء النماذج — فتصفير مصنع واحد كان كفيلاً بإنتاج قائمة مضاعفة
    /// وقوالب معلّقة على أنواع بلا أجهزة. كلٌّ من DatabaseMigrator و
    /// SystemResetService يقرأ من هنا الآن.
    ///
    /// المحتوى منقول حرفياً من SeedContent-DeviceTypes.md في جذر المستودع،
    /// وهو بدوره منقول من الشهادات الصادرة. **لا يُختلق نص هنا**: ما تعذّر
    /// استخراجه من نموذج Beta (بسبب تلف OCR) يبقى null حتى تصل النسخة الأصلية.
    ///
    /// ملاحظة على تكرار النصوص بين Gamma و Dose Rate Meter: مكرَّرة عمداً لا
    /// مشتركة. القالبان مستقلان، وتوحيدهما في ثابت واحد كان يمنع تعديل أحدهما
    /// دون الآخر.
    ///
    /// ملاحظة على الشرطة: نصّ Gamma/DRM يستعمل الشرطة الطويلة (–، U+2013) ونصّ
    /// PED يستعمل العادية (-، U+002D). الفارق منقول كما ورد من نموذجين مختلفين،
    /// ولا يُوحَّد بلا تأكيد.
    /// </summary>
    public static class DeviceTypeCatalog
    {
        // نصوص مشتركة الظاهر بين Gamma و DRM — تُسند لكلٍّ على حدة عبر ثابتين
        // منفصلين لا ثابت واحد، حتى يبقى تعديل أحدهما ممكناً دون الآخر.
        private const string GammaMethodologyText =
            "Calibration was performed using instrument-specific validated methods. " +
            "The calibration was carried out using a Co-60, Cs-137 and point gamma source. " +
            "Reference dose rates were determined using a calibrated Farmer ionization chamber " +
            "traceable to the International Atomic Energy Agency (IAEA). " +
            "The inverse square law was applied for distance calculations. " +
            "Results are expressed as Calibration Factor (CF) and Relative Error (%).";

        private const string GammaTraceability =
            "Measurement traceability is established through a calibrated Farmer ionization chamber " +
            "referenced to the International Atomic Energy Agency (IAEA).";

        private const string GammaNotes =
            "- The calibration results relate to the instrument configuration listed above.\n" +
            "- This certificate shall not be reproduced except in full, without the express written permission of the SSDL – TNRC laboratory management.\n" +
            "- The calibration results are valid only for the conditions and geometry specified.\n" +
            "- Traceability is maintained to the International Atomic Energy Agency (IAEA).";

        private const string DoseRateMeterMethodologyText = GammaMethodologyText;
        private const string DoseRateMeterTraceability = GammaTraceability;
        private const string DoseRateMeterNotes = GammaNotes;

        public static IReadOnlyList<DeviceTypeDefinition> All { get; } = new[]
        {
            // ──────────────────────────────── ١ ────────────────────────────────
            new DeviceTypeDefinition
            {
                Name = "Pancake Probe",
                ProcedureNo = "SSDL-PED-IS-CP-01",
                CalibrationLocation = "SSDL Calibration Laboratory, Tajoura - Libya",
                ReferenceGeometry = "Contact",
                CountingTime = "60 s",
                CountingUnit = "kCPM",
                ComplianceVerdict = "Instrument complies with laboratory acceptance criteria.",
                CorrectedReadingFormula = "Corrected Reading = Measured Reading × CFavg",
                UncertaintyEnabled = true,
                MethodologyEnabled = true,
                MethodologyText =
                    "• The calibration was performed in accordance with SSDL procedure SSDL-PED-IS-CP-01.\n" +
                    "• The Average Correction Factor (CFavg) is the mean of the correction factors obtained from three reference sources.\n" +
                    "• The reported expanded uncertainty is based on a coverage factor k = 2 (approximately 95% confidence level).",
                AdditionalInformation =
                    "This certificate is valid only for the instrument and detector identified above.\n" +
                    "Recalibration is recommended before the due date to ensure continued measurement accuracy.",
                FunctionalChecks = new[]
                {
                    new CatalogFunctionalCheck { SortOrder = 1, CheckName = "Background Check", Requirement = "Count rate within normal background limits", DefaultResult = "Yes" },
                    new CatalogFunctionalCheck { SortOrder = 2, CheckName = "High Voltage Check", Requirement = "Operating voltage within specified range", DefaultResult = "Yes" },
                    new CatalogFunctionalCheck { SortOrder = 3, CheckName = "Audio Alarm Check", Requirement = "Audible alarm operational", DefaultResult = "Yes" },
                    new CatalogFunctionalCheck { SortOrder = 4, CheckName = "Visual Inspection", Requirement = "No physical damage to instrument and probe", DefaultResult = "Yes" },
                    new CatalogFunctionalCheck { SortOrder = 5, CheckName = "Detector Window Inspection", Requirement = "Window clean and free from damage", DefaultResult = "Yes" }
                },
                // StandardUncertainty و ContributionPercent متروكان فارغين عمداً:
                // المكوّن ثابت والقيمة تتغير بكل معايرة.
                UncertaintyComponents = new[]
                {
                    new CatalogUncertaintyComponent { SortOrder = 1, ComponentName = "Source (calibration certificate)", EvaluationType = "Type B" },
                    new CatalogUncertaintyComponent { SortOrder = 2, ComponentName = "Radioactive decay", EvaluationType = "Type B" },
                    new CatalogUncertaintyComponent { SortOrder = 3, ComponentName = "Counting statistics", EvaluationType = "Type A" },
                    new CatalogUncertaintyComponent { SortOrder = 4, ComponentName = "Repeatability (measurement)", EvaluationType = "Type A" },
                    new CatalogUncertaintyComponent { SortOrder = 5, ComponentName = "Geometry and positioning", EvaluationType = "Type B" }
                }
            },

            // ──────────────────────────────── ٢ ────────────────────────────────
            // ⚠ نموذج Beta: نص OCR تالف. المؤكَّد وحده مزروع.
            // MethodologyText · ComplianceVerdict · CountingTime · CountingUnit ·
            // ReferenceGeometry · الفحوص · مكوّنات عدم اليقين — كلها [يحتاج تأكيد]
            // وتبقى فارغة. مرجَّح أنها تطابق Pancake، والترجيح ليس مصدراً لنصّ
            // يُطبع على شهادة معايرة.
            new DeviceTypeDefinition
            {
                Name = "Beta Scintillation Probe",
                Aliases = new[] { "Beta Scintillator Probe" },
                CalibrationStandard =
                    "SSDL-TNRC Internal Calibration Procedure Ref.: SSDL-CP-01 " +
                    "Implemented in accordance with ISO/IEC 17025:2017 requirements.",
                CorrectedReadingFormula = "Corrected Reading = Measured Reading × CFavg",
                UncertaintyEnabled = true,
                MethodologyEnabled = true
            },

            // ──────────────────────────────── ٣ ────────────────────────────────
            new DeviceTypeDefinition
            {
                Name = "Gamma Scintillation Probe",
                Aliases = new[] { "Gamma Probe" },
                ReferenceGeometry = "Distance = 1.0 meter (Axis configuration)",
                ComplianceVerdict = "APPROVED FOR OPERATIONAL RADIATION SAFETY USE",
                CorrectedReadingFormula = "Corrected Reading = Measured Reading × CF",
                UncertaintyEnabled = false,
                MethodologyEnabled = true,
                MethodologyText = GammaMethodologyText,
                TraceabilityReference = GammaTraceability,
                Notes = GammaNotes,
                AdditionalInformation =
                    "Calibration performed at reference distance of 1.0 meter (axis configuration). " +
                    "The instrument performance is within acceptable limits in accordance with laboratory procedures. " +
                    "This certificate is valid only for the instrument configuration and conditions specified. " +
                    "Measurements are traceable to the International Atomic Energy Agency (IAEA).",
                FunctionalChecks = new[]
                {
                    new CatalogFunctionalCheck { SortOrder = 1, CheckName = "Background Check", Requirement = "Count rate within normal background limits", DefaultResult = "Acceptable" },
                    new CatalogFunctionalCheck { SortOrder = 2, CheckName = "High Voltage Check", Requirement = "Operating voltage within specified range", DefaultResult = "Acceptable" },
                    new CatalogFunctionalCheck { SortOrder = 3, CheckName = "Audio / Alarm Check", Requirement = "Audible alarm operational", DefaultResult = "Acceptable" },
                    new CatalogFunctionalCheck { SortOrder = 4, CheckName = "Visual Inspection", Requirement = "No physical damage to instrument and probe", DefaultResult = "Acceptable" },
                    new CatalogFunctionalCheck { SortOrder = 5, CheckName = "Detector Response Check", Requirement = "Detector response within acceptable range when exposed to different radiation sources", DefaultResult = "Acceptable" }
                }
                // لا جدول مكوّنات عدم يقين في هذا النموذج — UncertaintyEnabled = false
            },

            // ──────────────────────────────── ٤ ────────────────────────────────
            new DeviceTypeDefinition
            {
                Name = "Personal Electronic Dosimeter (PED)",
                Aliases = new[] { "PED" },
                DetectorType = "Electronic Personal Dosimeter",
                CalibrationStandard =
                    "SSDL-TNRC Internal Calibration Procedure Ref.: SSDL-CP-PED " +
                    "in accordance with ISO/IEC 17025:2017 & IAEA standards.",
                ComplianceVerdict = "APPROVED FOR OPERATIONAL RADIATION SAFETY USE",
                CorrectedReadingFormula = "Corrected Dose = Measured Dose × CFavg",
                UncertaintyEnabled = true,
                MethodologyEnabled = true,
                MethodologyText =
                    "Radiation Source: Cs-137 Point Source | Reference Standard: Farmer Ionization Chamber (IAEA Traceable)\n" +
                    "Calibration was performed by exposing the Personal Electronic Dosimeter (PED) to gamma radiation fields " +
                    "produced by a calibrated Cs-137 source. Measurements were evaluated in terms of Personal Dose Measurement " +
                    "(µSv or mSv) against reference values. The Calibration Factor (CF) and Absolute Relative Error (AE) were " +
                    "determined. Corrected Reading = Measured Dose × CF.",
                AdditionalInformation =
                    "The reported expanded uncertainty is based on a standard uncertainty multiplied by a coverage factor k = 2, " +
                    "providing a coverage probability of approximately 95%.\n" +
                    "Measurements are traceable to the International Atomic Energy Agency (IAEA) standards.\n" +
                    "This certificate shall not be reproduced except in full, without the express written permission of the SSDL - TNRC laboratory management.\n" +
                    "The calibration results relate strictly and exclusively to the specific physical instrument identified by the serial number above.",
                FunctionalChecks = new[]
                {
                    new CatalogFunctionalCheck { SortOrder = 1, CheckName = "Battery & Display Check", Requirement = "LCD operational, battery level normal", DefaultResult = "Acceptable" },
                    new CatalogFunctionalCheck { SortOrder = 2, CheckName = "High Voltage / Detector Check", Requirement = "Operating voltage within specified range", DefaultResult = "Acceptable" },
                    new CatalogFunctionalCheck { SortOrder = 3, CheckName = "Audio / Alarm Check", Requirement = "Audible and visual alarm operational", DefaultResult = "Acceptable" },
                    new CatalogFunctionalCheck { SortOrder = 4, CheckName = "Visual Inspection", Requirement = "No physical damage to casing or clip", DefaultResult = "Acceptable" },
                    new CatalogFunctionalCheck { SortOrder = 5, CheckName = "Dose Response Check", Requirement = "Response within acceptable range when exposed to radiation", DefaultResult = "Acceptable" }
                }
                // UncertaintyEnabled = true لكن بلا جدول مكوّنات: النموذج يعرض
                // ExpandedUncertainty و CoverageFactor فقط.
            },

            // ──────────────────────────────── ٥ ────────────────────────────────
            new DeviceTypeDefinition
            {
                Name = "Dose Rate Meter",
                MeasurementType = "Dose Rate Measurement (µSv/h)",
                Distance = "1.0 meter",
                ReferenceGeometry = "Distance 1.0 meter (Axis configuration)",
                ComplianceVerdict = "APPROVED FOR OPERATIONAL RADIATION SAFETY USE",
                CorrectedReadingFormula = "Corrected Reading = Measured Reading × CF",
                UncertaintyEnabled = false,
                MethodologyEnabled = true,
                MethodologyText = DoseRateMeterMethodologyText,
                TraceabilityReference = DoseRateMeterTraceability,
                Notes = DoseRateMeterNotes,
                FunctionalChecks = new[]
                {
                    new CatalogFunctionalCheck { SortOrder = 1, CheckName = "Background Check", Requirement = "Count rate within normal background limits", DefaultResult = "Acceptable" },
                    new CatalogFunctionalCheck { SortOrder = 2, CheckName = "High Voltage Check", Requirement = "Operating voltage within specified range", DefaultResult = "Acceptable" },
                    new CatalogFunctionalCheck { SortOrder = 3, CheckName = "Audio / Alarm Check", Requirement = "Audible alarm operational", DefaultResult = "Acceptable" },
                    new CatalogFunctionalCheck { SortOrder = 4, CheckName = "Visual Inspection", Requirement = "No physical damage to instrument", DefaultResult = "Acceptable" },
                    new CatalogFunctionalCheck { SortOrder = 5, CheckName = "Dose Rate Response Check", Requirement = "Detector response within acceptable range when exposed to different radiation sources", DefaultResult = "Acceptable" }
                }
            }
        };

        /// <summary>الأسماء المعتمدة الخمسة بترتيب الكتالوج.</summary>
        public static IReadOnlyList<string> CanonicalNames { get; } =
            All.Select(d => d.Name).ToList();
    }
}
