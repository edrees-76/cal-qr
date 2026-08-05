using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using CAL_QR.Validation;

namespace CAL_QR.Models
{
    public class CalibrationRecord
    {
        public int Id { get; set; }
        public int DeviceId { get; set; }
        public string CertificateNumber { get; set; } = string.Empty;
        public DateTime CalibrationDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string EngineerName { get; set; } = string.Empty;
        public string? CalibrationDescription { get; set; }
        public string Result { get; set; } = "Passed"; // "Passed" | "Failed" | "Conditional"
        public string HmacSignature { get; set; } = string.Empty;
        public bool IsDeleted { get; set; } = false;
        public bool IsSeedTestData { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [NotMapped]
        public int SequenceNumber { get; set; }

        /// <summary>
        /// رقم الشهادة كما يُعرض — يُقرأ من جدول Certificates لا من العمود
        /// CertificateNumber أعلاه، لأن الشهادة صاحبة الكلمة بقرار معماريّ.
        /// يملؤه CertificateNumberDisplayRules.Populate عند تحميل السجلات،
        /// ويبقى "—" لسجل بلا شهادة.
        ///
        /// [NotMapped] كنظير SequenceNumber تماماً: خاصّية عرض لا عمود، فلا
        /// ترقية قاعدة ولا مساس بالمخطط المجمَّد.
        /// </summary>
        [NotMapped]
        public string DisplayCertificateNumber { get; set; } = CertificateNumberDisplayRules.None;

        [NotMapped]
        public string ResultAr => Result == "Passed" ? "✅ ناجح" : Result == "Failed" ? "❌ راسب" : Result == "Conditional" ? "⚠️ مشروط" : Result;

        public virtual Device? Device { get; set; }
        public virtual ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
    }
}
