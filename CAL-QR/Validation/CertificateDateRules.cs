using System;
using System.Globalization;

namespace CAL_QR.Validation
{
    /// <summary>نتيجة تحقق: صالحة، أو رسالة عربية واحدة تُعرض للمستخدم.</summary>
    public readonly struct DateValidationResult
    {
        public bool IsValid { get; }
        public string? ErrorMessage { get; }

        private DateValidationResult(bool isValid, string? errorMessage)
        {
            IsValid = isValid;
            ErrorMessage = errorMessage;
        }

        public static DateValidationResult Valid() => new DateValidationResult(true, null);
        public static DateValidationResult Invalid(string message) => new DateValidationResult(false, message);
    }

    /// <summary>
    /// قواعد تواريخ الشهادة — دوالّ نقية بلا حقن ولا سياق قاعدة بيانات.
    ///
    /// تُستدعى من موضعين بقاعدة واحدة:
    ///   (١) الـViewModel — منع فوري بلا زر تجاوز، ورسالة عربية.
    ///   (٢) CertificateRepository قبل SaveChanges — خط الدفاع الأخير.
    ///
    /// لماذا لا الكيان؟ خصائص POCO لا تستطيع رفض قيمة، وDataAnnotations لا
    /// يستدعيها EF Core تلقائياً عند SaveChanges.
    /// لماذا لا الـViewModel وحده؟ لأن أي مسار لا يمر به (بذر، استيراد، دفعات)
    /// كان سيكتب تواريخ غير صالحة — وDevTestDataSeeder مثال حيّ على مثل هذا المسار.
    ///
    /// كلها قواعد منع صارمة: لا زر تجاوز ولا تحذير قابل للتخطي.
    /// </summary>
    public static class CertificateDateRules
    {
        /// <summary>DueDate = CalibrationDate + سنة. من تاريخ المعايرة لا الإصدار.</summary>
        public static DateTime ComputeDueDate(DateTime calibrationDate) =>
            calibrationDate.Date.AddYears(1);

        /// <summary>
        /// التحقق الكامل. <paramref name="today"/> يُمرَّر صراحةً بدل DateTime.Today
        /// حتى تكون القاعدة قابلة للاختبار على حدودها.
        /// <paramref name="existingCertificateNumber"/> يُمرَّر عند التعديل فقط:
        /// وجوده يعني أن رقماً وُلّد، فتُفعَّل قاعدة حبس السنة.
        /// </summary>
        public static DateValidationResult Validate(
            DateTime calibrationDate,
            DateTime issueDate,
            DateTime today,
            string? existingCertificateNumber = null)
        {
            DateTime calibration = calibrationDate.Date;
            DateTime issue = issueDate.Date;

            if (issue < calibration)
            {
                return DateValidationResult.Invalid(
                    "تاريخ الإصدار لا يجوز أن يسبق تاريخ المعايرة.");
            }

            if (issue > today.Date)
            {
                return DateValidationResult.Invalid(
                    "تاريخ الإصدار لا يجوز أن يكون في المستقبل.");
            }

            if (!string.IsNullOrWhiteSpace(existingCertificateNumber))
            {
                int? numberYear = ExtractYear(existingCertificateNumber);

                // السنة تُقرأ من الرقم نفسه لا من عمود إضافي: الرقم يحمل سنته
                // (TNRC-SSDL-2026-0018)، وهو المرجع الذي طُبع على الوثيقة فعلاً.
                if (numberYear.HasValue && issue.Year != numberYear.Value)
                {
                    return DateValidationResult.Invalid(
                        $"بعد توليد رقم الشهادة، لا يجوز نقل تاريخ الإصدار خارج سنة الرقم ({numberYear.Value}).");
                }
            }

            return DateValidationResult.Valid();
        }

        /// <summary>
        /// استخراج السنة من رقم بصيغة TNRC-SSDL-YYYY-XXXX.
        /// رقم بصيغة أخرى (شهادة مرحَّلة ورقياً مثلاً) يُعيد null فتُتخطى القاعدة
        /// بدل رفض تعديل مشروع على سجل قديم.
        /// </summary>
        public static int? ExtractYear(string? certificateNumber)
        {
            if (string.IsNullOrWhiteSpace(certificateNumber))
            {
                return null;
            }

            string[] parts = certificateNumber.Trim().Split('-');
            if (parts.Length != 4)
            {
                return null;
            }

            if (int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int year)
                && parts[2].Length == 4)
            {
                return year;
            }

            return null;
        }

        /// <summary>
        /// استخراج التسلسل من رقم بصيغة TNRC-SSDL-YYYY-XXXX. نظيرة ExtractYear
        /// وفي نفس الصنف عمداً: صيغة الرقم تُقرأ من مصدر حقيقة واحد لا من مواضع
        /// محارف مكتوبة يدوياً في أكثر من موضع.
        /// رقم بصيغة أخرى يُعيد null.
        /// </summary>
        public static int? ExtractSequence(string? certificateNumber)
        {
            if (string.IsNullOrWhiteSpace(certificateNumber))
            {
                return null;
            }

            string[] parts = certificateNumber.Trim().Split('-');
            if (parts.Length != 4)
            {
                return null;
            }

            if (int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out int sequence)
                && parts[3].Length == 4)
            {
                return sequence;
            }

            return null;
        }
    }
}
