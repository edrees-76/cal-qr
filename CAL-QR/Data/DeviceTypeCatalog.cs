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

        /// <summary>
        /// التوزيع الإحصائي المفترض للمكوّن (Normal · Poisson · Rectangular).
        /// خاصّية ثابتة للمكوّن لا قيمة تُقاس، فهي قالب لا نتيجة — بخلاف
        /// StandardUncertainty و ContributionPercent اللتين تتغيّران بكل معايرة.
        /// </summary>
        public string? Distribution { get; init; }
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
        public string? CalibrationMode { get; init; }
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
    /// المحتوى منقول **حرفياً** من SeedContent-DeviceTypes.md، وهو بدوره منقول
    /// من قوالب م. رضا النهائية الخمسة. **لا يُختلق نصّ هنا ولا يُعاد صوغه**:
    /// نصّ شهادة ISO/IEC 17025 معتمد بصياغته لا بمعناه فقط، فلا يُستبدل
    /// باستنباط ولو كان المعنى محفوظاً.
    ///
    /// ملاحظة على تكرار النصوص بين الأنواع: **مكرَّرة عمداً لا مشتركة، ولا
    /// ثابت واحد يجمع نوعين.** القوالب مستقلة، وتوحيدها كان يمنع تعديل أحدها
    /// دون الآخر — وهو ما حدث فعلاً حين ورث Dose Rate Meter نصّ Gamma بالنويدة
    /// الخطأ عبر `DoseRateMeterMethodologyText = GammaMethodologyText`.
    ///
    /// ملاحظة على النويدات: مثبَّتة في نصوص م. رضا المعتمدة
    /// (Gamma = Co-60 · PED = Co-60 · Dose Rate Meter = Cs-137). عند المعايرة
    /// بنويدة مختلفة أو بأكثر من واحدة، **يُعدَّل MethodologyText يدوياً على
    /// الشهادة قبل الحفظ** — الحقل قابل للتحرير.
    ///
    /// ما لا يظهر في القوالب الخمسة **لا يُبذَر** ويبقى null، ودلالة null هنا
    /// «لا قالب لهذا الحقل في هذا النوع» لا «قيمة فارغة»:
    /// ProcedureNo · CalibrationLocation · Instrumentation · DetectorType ·
    /// MeasurementType · Distance · Manufacturer (بيانات جهاز لا قالب نوع).
    ///
    /// بند «This certificate shall not be reproduced except in full» **محذوف من
    /// الأنواع الخمسة** بقرار إدريس. وبحذفه زالت مسألة الشرطة الطويلة مقابل
    /// العادية في «SSDL – TNRC» — كان ذلك موضعها الوحيد.
    /// </summary>
    public static class DeviceTypeCatalog
    {
        public static IReadOnlyList<DeviceTypeDefinition> All { get; } = new[]
        {
            // ──────────────────────────────── ١ ────────────────────────────────
            // العائلة الكاملة. لا MethodologyText في قالب رضا لهذا النوع رغم أن
            // MethodologyEnabled = true — النصّ الذي كان مزروعاً هنا كان مفبركاً:
            // يذكر رقم إجراء غير موجود في القوالب، ويعرّف CFavg متوسطاً عبر ثلاثة
            // مصادر مرجعية، وهو نقض مباشر لقرار المصادر المتعددة (v3 §٥): المعامل
            // خاصّية طاقة، ومتوسّطه عبر طاقات مختلفة بلا معنى علميّ.
            new DeviceTypeDefinition
            {
                Name = "Pancake Probe",
                CalibrationStandard = "Certified Sr-90/Y-90 Reference Beta Sources",
                ReferenceGeometry = "Direct Contact Geometry",
                CalibrationMode = "Direct Contact Geometry",
                CountingTime = "60 Sec",
                CountingUnit = "kCPM",
                ComplianceVerdict = "APPROVED FOR OPERATIONAL USE",
                CorrectedReadingFormula = "Corrected Reading = Measured Reading × CFavg",
                TraceabilityReference =
                    "Measurement traceability is established through certified Sr-90/Y-90 reference Beta sources " +
                    "maintained by the Secondary Standard Dosimetry Laboratory (SSDL).",
                UncertaintyEnabled = true,
                MethodologyEnabled = true,
                Notes =
                    "The reported expanded uncertainty is based on a standard uncertainty multiplied by a coverage factor k = 2, providing a coverage probability of approximately 95%.\n" +
                    "The calibration results relate strictly and exclusively to the specific physical instrument identified by the serial number above.",
                FunctionalChecks = new[]
                {
                    new CatalogFunctionalCheck { SortOrder = 1, CheckName = "Background Check", Requirement = "Count rate within normal background limits", DefaultResult = "Yes" },
                    new CatalogFunctionalCheck { SortOrder = 2, CheckName = "High Voltage Check", Requirement = "Operating voltage within specified range", DefaultResult = "Yes" },
                    new CatalogFunctionalCheck { SortOrder = 3, CheckName = "Audio/Alarm Check", Requirement = "Audible alarm operational", DefaultResult = "Yes" },
                    new CatalogFunctionalCheck { SortOrder = 4, CheckName = "Visual Inspection", Requirement = "No physical damage to instrument and probe", DefaultResult = "Yes" },
                    new CatalogFunctionalCheck { SortOrder = 5, CheckName = "Detector Window Inspection", Requirement = "Window clean and free from damage", DefaultResult = "Yes" }
                },
                // StandardUncertainty و ContributionPercent متروكان فارغين عمداً:
                // المكوّن ثابت والقيمة تتغير بكل معايرة. أما Distribution فخاصّية
                // ثابتة للمكوّن، فتُزرع.
                UncertaintyComponents = new[]
                {
                    new CatalogUncertaintyComponent { SortOrder = 1, ComponentName = "Source (calibration certificate)", EvaluationType = "Type B", Distribution = "Normal" },
                    new CatalogUncertaintyComponent { SortOrder = 2, ComponentName = "Radioactive decay", EvaluationType = "Type B", Distribution = "Normal" },
                    new CatalogUncertaintyComponent { SortOrder = 3, ComponentName = "Counting statistics", EvaluationType = "Type A", Distribution = "Poisson" },
                    new CatalogUncertaintyComponent { SortOrder = 4, ComponentName = "Repeatability (measurement)", EvaluationType = "Type A", Distribution = "Normal" },
                    new CatalogUncertaintyComponent { SortOrder = 5, ComponentName = "Geometry and positioning", EvaluationType = "Type B", Distribution = "Rectangular" }
                }
            },

            // ──────────────────────────────── ٢ ────────────────────────────────
            // العائلة الكاملة. القالب الفعلي وصل كاملاً وحلّ محلّ نسخة OCR التالفة،
            // فلم يبقَ حقل [يحتاج تأكيد] واحد. النصوص أدناه منسوخة من قالب رضا لا
            // من Pancake — والفارق المقصود في الفحص الخامس:
            // Probe Window Inspection لا Detector Window Inspection.
            new DeviceTypeDefinition
            {
                Name = "Beta Scintillation Probe",
                Aliases = new[] { "Beta Scintillator Probe" },
                CalibrationStandard = "Certified Sr-90/Y-90 Reference Beta Sources",
                ReferenceGeometry = "Direct Contact Geometry",
                CalibrationMode = "Direct Contact Geometry",
                CountingTime = "60 Sec",
                CountingUnit = "kCPM",
                ComplianceVerdict = "APPROVED FOR OPERATIONAL USE",
                CorrectedReadingFormula = "Corrected Reading = Measured Reading × CFavg",
                TraceabilityReference =
                    "Measurement traceability is established through certified Sr-90/Y-90 reference Beta sources " +
                    "maintained by the Secondary Standard Dosimetry Laboratory (SSDL).",
                UncertaintyEnabled = true,
                MethodologyEnabled = true,
                Notes =
                    "The reported expanded uncertainty is based on a standard uncertainty multiplied by a coverage factor k = 2, providing a coverage probability of approximately 95%.\n" +
                    "The calibration results relate strictly and exclusively to the specific physical instrument identified by the serial number above.",
                FunctionalChecks = new[]
                {
                    new CatalogFunctionalCheck { SortOrder = 1, CheckName = "Background Check", Requirement = "Count rate within normal background limits", DefaultResult = "Yes" },
                    new CatalogFunctionalCheck { SortOrder = 2, CheckName = "High Voltage Check", Requirement = "Operating voltage within specified range", DefaultResult = "Yes" },
                    new CatalogFunctionalCheck { SortOrder = 3, CheckName = "Audio/Alarm Check", Requirement = "Audible alarm operational", DefaultResult = "Yes" },
                    new CatalogFunctionalCheck { SortOrder = 4, CheckName = "Visual Inspection", Requirement = "No physical damage to instrument and probe", DefaultResult = "Yes" },
                    new CatalogFunctionalCheck { SortOrder = 5, CheckName = "Probe Window Inspection", Requirement = "Window clean and free from damage", DefaultResult = "Yes" }
                },
                UncertaintyComponents = new[]
                {
                    new CatalogUncertaintyComponent { SortOrder = 1, ComponentName = "Source (calibration certificate)", EvaluationType = "Type B", Distribution = "Normal" },
                    new CatalogUncertaintyComponent { SortOrder = 2, ComponentName = "Radioactive decay", EvaluationType = "Type B", Distribution = "Normal" },
                    new CatalogUncertaintyComponent { SortOrder = 3, ComponentName = "Counting statistics", EvaluationType = "Type A", Distribution = "Poisson" },
                    new CatalogUncertaintyComponent { SortOrder = 4, ComponentName = "Repeatability (measurement)", EvaluationType = "Type A", Distribution = "Normal" },
                    new CatalogUncertaintyComponent { SortOrder = 5, ComponentName = "Geometry and positioning", EvaluationType = "Type B", Distribution = "Rectangular" }
                }
            },

            // ──────────────────────────────── ٣ ────────────────────────────────
            // العائلة البسيطة. النويدة Co-60 مثبَّتة في نصّ رضا.
            new DeviceTypeDefinition
            {
                Name = "Gamma Scintillation Probe",
                Aliases = new[] { "Gamma Probe" },
                ReferenceGeometry = "Distance = 1.0 meter (Axis configuration)",
                ComplianceVerdict = "APPROVED FOR OPERATIONAL RADIATION SAFETY USE",
                CorrectedReadingFormula = "Corrected Reading = Measured Reading × CF",
                TraceabilityReference =
                    "Measurement traceability is established through a calibrated Farmer ionization chamber " +
                    "referenced to the International Atomic Energy Agency (IAEA).",
                UncertaintyEnabled = false,
                MethodologyEnabled = true,
                MethodologyText =
                    "The calibration was performed using an instrument-specific validated method. " +
                    "The calibration was carried out using a Co-60 point gamma source. " +
                    "Reference dose rates were determined using a calibrated Farmer ionization chamber " +
                    "traceable to the International Atomic Energy Agency (IAEA). " +
                    "The inverse square law was applied for distance calculation. " +
                    "Results are expressed as calibration factor (CF) and absolute relative error (AE).",
                Notes =
                    "The calibration results relate to the instrument configuration listed above.\n" +
                    "The calibration results are valid only for the conditions and geometry specified.\n" +
                    "Traceability is maintained to the International Atomic Energy Agency (IAEA).",
                AdditionalInformation =
                    "Calibration performed at reference distance of 1.0 meter (axis configuration).\n" +
                    "The instrument performance is within acceptable limits in accordance with laboratory procedures.\n" +
                    "This certificate is valid only for the instrument configuration and conditions specified.\n" +
                    "Measurements are traceable to the International Atomic Energy Agency (IAEA).",
                FunctionalChecks = new[]
                {
                    new CatalogFunctionalCheck { SortOrder = 1, CheckName = "Background Check", Requirement = "Count rate within normal background limits", DefaultResult = "Acceptable" },
                    new CatalogFunctionalCheck { SortOrder = 2, CheckName = "High Voltage Check", Requirement = "Operating voltage within specified range", DefaultResult = "Acceptable" },
                    new CatalogFunctionalCheck { SortOrder = 3, CheckName = "Audio/Alarm Check", Requirement = "Audible alarm operational", DefaultResult = "Acceptable" },
                    new CatalogFunctionalCheck { SortOrder = 4, CheckName = "Visual Inspection", Requirement = "No physical damage to instrument and probe", DefaultResult = "Acceptable" },
                    new CatalogFunctionalCheck { SortOrder = 5, CheckName = "Detector Response Check", Requirement = "Detector response within acceptable range when exposed to different radiation sources", DefaultResult = "Acceptable" }
                }
                // لا جدول مكوّنات عدم يقين في هذا النموذج — UncertaintyEnabled = false
            },

            // ──────────────────────────── Teletector ────────────────────────────
            // مجس جاما على ذراع تمديد (AUTOMESS 6150 AD-T). عائلة أ (وحدة قراءة + مجس)،
            // نمط مبسّط بنويدة Cs-137. نصوص المنهجية/التتبّع مطابقة لنصّ Dose Rate Meter
            // لكنها **مكرَّرة عمداً لا مشتركة** التزاماً بسياسة الكتالوج. الفحوص خاصّة بـTELETECTOR.
            new DeviceTypeDefinition
            {
                Name = "Teletector Gamma Probe",
                ReferenceGeometry = "Distance = 1.0 meter (Axis configuration)",
                ComplianceVerdict = "APPROVED FOR OPERATIONAL RADIATION SAFETY USE",
                CorrectedReadingFormula = "Corrected Reading = Measured Reading × CF",
                TraceabilityReference =
                    "Measurement traceability is established through a calibrated Farmer ionization chamber " +
                    "referenced to the International Atomic Energy Agency (IAEA).",
                UncertaintyEnabled = false,
                MethodologyEnabled = true,
                MethodologyText =
                    "The calibration was performed using an instrument-specific validated method. " +
                    "The calibration was carried out using a Cs-137 point gamma source. " +
                    "Reference dose rates were determined using a calibrated Farmer ionization chamber " +
                    "traceable to the International Atomic Energy Agency (IAEA). " +
                    "The inverse square law was applied for distance calculation. " +
                    "Results are expressed as calibration factor (CF) and absolute relative error (AE).",
                Notes =
                    "The calibration results relate to the instrument configuration listed above.\n" +
                    "The calibration results are valid only for the conditions and geometry specified.\n" +
                    "Traceability is maintained to the International Atomic Energy Agency (IAEA).",
                AdditionalInformation =
                    "Calibration performed at reference distance of 1.0 meter (axis configuration).\n" +
                    "The instrument performance is within acceptable limits in accordance with laboratory procedures.\n" +
                    "This certificate is valid only for the instrument configuration and conditions specified.\n" +
                    "Measurements are traceable to the International Atomic Energy Agency (IAEA).",
                FunctionalChecks = new[]
                {
                    new CatalogFunctionalCheck { SortOrder = 1, CheckName = "Survey Meter Power & Display Check", Requirement = "Display operational and survey meter powers on normally", DefaultResult = "Acceptable" },
                    new CatalogFunctionalCheck { SortOrder = 2, CheckName = "Survey Meter Operating Check", Requirement = "Survey meter operates normally with connected TELETECTOR probe", DefaultResult = "Acceptable" },
                    new CatalogFunctionalCheck { SortOrder = 3, CheckName = "Survey Meter Audio / Alarm Check", Requirement = "Audible alarm operational", DefaultResult = "Acceptable" },
                    new CatalogFunctionalCheck { SortOrder = 4, CheckName = "Visual Inspection", Requirement = "No physical damage to the survey meter and TELETECTOR probe", DefaultResult = "Acceptable" },
                    new CatalogFunctionalCheck { SortOrder = 5, CheckName = "TELETECTOR Probe Response Check", Requirement = "Probe response within acceptable range when exposed to reference gamma radiation", DefaultResult = "Acceptable" }
                }
            },

            // ──────────────────────────────── ٤ ────────────────────────────────
            // العائلة البسيطة. «عائلة ب» المنفصلة لـPED **ملغاة**: قالب رضا الفعلي
            // بلا عدم يقين إطلاقاً ⇒ UncertaintyEnabled = false.
            // النويدة Co-60 في الموضعين — تضارب القالب (خانة المصدر Co-60 /
            // المنهجية Cs-137) كان خطأ كتابة في المنهجية، أكّد رضا تصحيحه.
            new DeviceTypeDefinition
            {
                Name = "Personal Electronic Dosimeter (PED)",
                Aliases = new[] { "PED" },
                ReferenceGeometry = "Distance = 1.0 meter (Axis configuration)",
                ComplianceVerdict = "APPROVED FOR OPERATIONAL RADIATION SAFETY USE",
                CorrectedReadingFormula = "Corrected Reading = Measured Reading × CF",
                TraceabilityReference =
                    "Measurement traceability is established through a calibrated Farmer ionization chamber " +
                    "referenced to the International Atomic Energy Agency (IAEA).",
                UncertaintyEnabled = false,
                MethodologyEnabled = true,
                MethodologyText =
                    "The calibration was performed using an instrument-specific validated method. " +
                    "The calibration was carried out using a Co-60 point gamma source. " +
                    "Reference dose rates were determined using a calibrated Farmer ionization chamber " +
                    "traceable to the International Atomic Energy Agency (IAEA). " +
                    "The inverse square law was applied for distance calculation. " +
                    "Results are expressed as calibration factor (CF) and absolute relative error (AE).",
                Notes =
                    "The calibration results relate to the instrument configuration listed above.\n" +
                    "The calibration results are valid only for the conditions and geometry specified.\n" +
                    "Traceability is maintained to the International Atomic Energy Agency (IAEA).",
                AdditionalInformation =
                    "Calibration performed at reference distance of 1.0 meter (axis configuration).\n" +
                    "The instrument performance is within acceptable limits in accordance with laboratory procedures.\n" +
                    "This certificate is valid only for the instrument configuration and conditions specified.\n" +
                    "Measurements are traceable to the International Atomic Energy Agency (IAEA).",
                FunctionalChecks = new[]
                {
                    new CatalogFunctionalCheck { SortOrder = 1, CheckName = "Battery & Display Check", Requirement = "LCD operational, battery level normal", DefaultResult = "Acceptable" },
                    new CatalogFunctionalCheck { SortOrder = 2, CheckName = "High Voltage Check", Requirement = "Operating voltage within specified range", DefaultResult = "Acceptable" },
                    new CatalogFunctionalCheck { SortOrder = 3, CheckName = "Audio/Alarm Check", Requirement = "Audible alarm operational", DefaultResult = "Acceptable" },
                    new CatalogFunctionalCheck { SortOrder = 4, CheckName = "Visual Inspection", Requirement = "No physical damage to instrument and probe", DefaultResult = "Acceptable" },
                    new CatalogFunctionalCheck { SortOrder = 5, CheckName = "Dose Response Check", Requirement = "Response within acceptable range when exposed to radiation", DefaultResult = "Acceptable" }
                }
                // لا جدول مكوّنات عدم يقين — UncertaintyEnabled = false
            },

            // ──────────────────────────────── ٥ ────────────────────────────────
            // العائلة البسيطة. النويدة **Cs-137** — تختلف عن Gamma و PED، ولذلك
            // نصّ المنهجية مكتوب هنا مستقلاً لا موروثاً بثابت مشترك.
            new DeviceTypeDefinition
            {
                Name = "Dose Rate Meter",
                ReferenceGeometry = "Distance = 1.0 meter (Axis configuration)",
                ComplianceVerdict = "APPROVED FOR OPERATIONAL RADIATION SAFETY USE",
                CorrectedReadingFormula = "Corrected Reading = Measured Reading × CF",
                TraceabilityReference =
                    "Measurement traceability is established through a calibrated Farmer ionization chamber " +
                    "referenced to the International Atomic Energy Agency (IAEA).",
                UncertaintyEnabled = false,
                MethodologyEnabled = true,
                MethodologyText =
                    "The calibration was performed using an instrument-specific validated method. " +
                    "The calibration was carried out using a Cs-137 point gamma source. " +
                    "Reference dose rates were determined using a calibrated Farmer ionization chamber " +
                    "traceable to the International Atomic Energy Agency (IAEA). " +
                    "The inverse square law was applied for distance calculation. " +
                    "Results are expressed as calibration factor (CF) and absolute relative error (AE).",
                Notes =
                    "The calibration results relate to the instrument configuration listed above.\n" +
                    "The calibration results are valid only for the conditions and geometry specified.\n" +
                    "Traceability is maintained to the International Atomic Energy Agency (IAEA).",
                AdditionalInformation =
                    "Calibration performed at reference distance of 1.0 meter (axis configuration).\n" +
                    "The instrument performance is within acceptable limits in accordance with laboratory procedures.\n" +
                    "This certificate is valid only for the instrument configuration and conditions specified.\n" +
                    "Measurements are traceable to the International Atomic Energy Agency (IAEA).",
                FunctionalChecks = new[]
                {
                    new CatalogFunctionalCheck { SortOrder = 1, CheckName = "Background Check", Requirement = "Count rate within normal background limits", DefaultResult = "Acceptable" },
                    new CatalogFunctionalCheck { SortOrder = 2, CheckName = "High Voltage Check", Requirement = "Operating voltage within specified range", DefaultResult = "Acceptable" },
                    new CatalogFunctionalCheck { SortOrder = 3, CheckName = "Audio/Alarm Check", Requirement = "Audible alarm operational", DefaultResult = "Acceptable" },
                    new CatalogFunctionalCheck { SortOrder = 4, CheckName = "Visual Inspection", Requirement = "No physical damage to instrument and probe", DefaultResult = "Acceptable" },
                    new CatalogFunctionalCheck { SortOrder = 5, CheckName = "Dose Rate Response Check", Requirement = "Detector response within acceptable range when exposed to different radiation sources", DefaultResult = "Acceptable" }
                }
                // لا جدول مكوّنات عدم يقين — UncertaintyEnabled = false
            }
        };

        /// <summary>الأسماء المعتمدة الخمسة بترتيب الكتالوج.</summary>
        public static IReadOnlyList<string> CanonicalNames { get; } =
            All.Select(d => d.Name).ToList();
    }
}
