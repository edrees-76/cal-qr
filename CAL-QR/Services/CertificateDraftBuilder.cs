using System;
using System.Linq;
using CAL_QR.Models;

namespace CAL_QR.Services
{
    /// <summary>
    /// تنفيذ صرف: بلا حقن، بلا حالة، بلا قراءة وقت أو قاعدة بيانات. لذلك يُسجَّل Singleton.
    /// </summary>
    public class CertificateDraftBuilder : ICertificateDraftBuilder
    {
        // ── هيكل الظروف البيئيّة ──
        // ليس قيمة مرجعيّة بل قالب كتابة: يعفي المعايِر من كتابة ± والوحدة،
        // ويجعل النسيان مرئيًّا بدل أن يكون فراغًا صامتًا. القيم نفسها قياس
        // فعليّ لكلّ معايرة — لا تُبذَر أبدًا، فهي تدخل نصّ التوقيع (TP/RH/AP).
        // EnvironmentPlaceholderMarker هو ما يفحصه حارس SaveAsync: أيّ حقل
        // ما زال يحمله = لم يُملأ. الوحدات مطابقة لقوالب م. رضا الستّة.
        public const string EnvironmentPlaceholderMarker = "__";
        public const string TemperatureTemplate = "__ ± __ °C";
        public const string RelativeHumidityTemplate = "__ ± __ % RH";
        public const string AtmosphericPressureTemplate = "__ ± __ kPa";

        /// <summary>
        /// الحكم المبذور لتقرير الحالة، منقول حرفيًّا من خانة STATUS / VERDICT
        /// في قالب م. رضا. يُخزَّن على الشهادة ويدخل SIG1 — فهو قيمة صفّ لا نصّ
        /// ثابت مطبوع، ولذلك هنا مع قوالب الظروف البيئيّة لا في CertificateTexts
        /// (ذاك موثَّق صراحةً بأنّه خارج SIG1 وليس حقلاً على Certificate).
        /// </summary>
        public const string StatusReportDefaultVerdict = "NOT PERFORMED";

        public CertificateDraftResult Build(
            DeviceType deviceType,
            CalibrationRecord record,
            Device device,
            Owner owner)
        {
            if (deviceType == null) throw new ArgumentNullException(nameof(deviceType));
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (device == null) throw new ArgumentNullException(nameof(device));
            if (owner == null) throw new ArgumentNullException(nameof(owner));

            var certificate = new Certificate
            {
                CalibrationRecordId = record.Id,

                // نوع النموذج المطبوع، منسوخ نصاً لا مقروءاً حياً — انظر تعليق
                // CertificateTemplateType في Certificate.
                CertificateTemplateType = deviceType.Name,

                // ── الحقول النصية القالبية: خريطة ١:١، الأسماء متطابقة على الطرفين ──
                ProcedureNo = deviceType.ProcedureNo,
                CalibrationLocation = deviceType.CalibrationLocation,
                ReferenceGeometry = deviceType.ReferenceGeometry,
                CountingTime = deviceType.CountingTime,
                CountingUnit = deviceType.CountingUnit,
                CalibrationMode = deviceType.CalibrationMode,
                MethodologyText = deviceType.MethodologyText,
                TraceabilityReference = deviceType.TraceabilityReference,
                ComplianceVerdict = deviceType.ComplianceVerdict,
                CalibrationStandard = deviceType.CalibrationStandard,
                Notes = deviceType.Notes,
                AdditionalInformation = deviceType.AdditionalInformation,
                MeasurementType = deviceType.MeasurementType,
                Distance = deviceType.Distance,
                CorrectedReadingFormula = deviceType.CorrectedReadingFormula,
                DetectorType = deviceType.DetectorType,
                Instrumentation = deviceType.Instrumentation,

                // ── العَلَمان ──
                UncertaintyEnabled = deviceType.UncertaintyEnabled,
                MethodologyEnabled = deviceType.MethodologyEnabled,

                // ── بيانات السياق، منسوخة نصاً وقت الإصدار ──
                ClientName = owner.Name,
                ClientAddress = owner.Address,
                DeviceModel = device.Model,
                DeviceSerialNumber = device.SerialNumber,

                // لا حقل مصنّع على Device — إدخال يدوي على الشهادة.
                DeviceManufacturer = null,

                Temperature = TemperatureTemplate,
                RelativeHumidity = RelativeHumidityTemplate,
                AtmosphericPressure = AtmosphericPressureTemplate,

                CalibrationDate = record.CalibrationDate

                // IssueDate و DueDate و CertificateNumber و VerifyCode و QrPayload
                // ليست من شأن هذا الباني عمداً.
            };

            foreach (var template in deviceType.FunctionalCheckTemplates
                         .OrderBy(t => t.SortOrder)
                         .ThenBy(t => t.Id))
            {
                certificate.FunctionalChecks.Add(new CertificateFunctionalCheck
                {
                    SortOrder = certificate.FunctionalChecks.Count,
                    CheckName = template.CheckName,
                    Requirement = template.Requirement,

                    // القالب يقترح قيمة ابتدائية، والمعايِر يملك تغييرها.
                    Result = template.DefaultResult,
                    Remarks = null
                });
            }

            foreach (var template in deviceType.UncertaintyComponentTemplates
                         .OrderBy(t => t.SortOrder)
                         .ThenBy(t => t.Id))
            {
                certificate.UncertaintyComponents.Add(new CertificateUncertaintyComponent
                {
                    SortOrder = certificate.UncertaintyComponents.Count,
                    ComponentName = template.ComponentName,
                    EvaluationType = template.EvaluationType,
                    Distribution = template.Distribution,

                    // صراحةً فارغتان: القيمة تتغير بكل معايرة، ونسخُها من القالب
                    // كان سيُنتج ميزانية تبدو محسوبة وهي منسوخة.
                    StandardUncertainty = null,
                    ContributionPercent = null
                });
            }

            // NuclideSummaries و CalibrationResults تبدأ فارغة — يملؤها المستخدم.

            return new CertificateDraftResult
            {
                Certificate = certificate,
                HasTemplate = HasAnyTemplate(deviceType)
            };
        }

        /// <summary>
        /// «بلا قالب» = لا حقل نصي واحد ولا صفّ قالب واحد. العَلَمان خارج الفحص عمداً:
        /// false فيهما قيمة مشروعة («القسم مخفي») لا «لم يُضبط»، فلا يشهدان على وجود قالب.
        /// </summary>
        private static bool HasAnyTemplate(DeviceType type)
        {
            return HasText(type.ProcedureNo)
                || HasText(type.CalibrationLocation)
                || HasText(type.ReferenceGeometry)
                || HasText(type.CountingTime)
                || HasText(type.CountingUnit)
                || HasText(type.CalibrationMode)
                || HasText(type.MethodologyText)
                || HasText(type.TraceabilityReference)
                || HasText(type.ComplianceVerdict)
                || HasText(type.CalibrationStandard)
                || HasText(type.Notes)
                || HasText(type.AdditionalInformation)
                || HasText(type.MeasurementType)
                || HasText(type.Distance)
                || HasText(type.CorrectedReadingFormula)
                || HasText(type.DetectorType)
                || HasText(type.Instrumentation)
                || type.FunctionalCheckTemplates.Count > 0
                || type.UncertaintyComponentTemplates.Count > 0;
        }

        private static bool HasText(string? value) => !string.IsNullOrWhiteSpace(value);
    }
}
