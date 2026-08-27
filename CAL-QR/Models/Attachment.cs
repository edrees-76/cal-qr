using System;

namespace CAL_QR.Models
{
    public class Attachment
    {
        public int Id { get; set; }
        public int CalibrationRecordId { get; set; }

        // مرفق الشهادة الموقّعة والمختومة. null = مرفق معايرة عاديّ (السلوك
        // القائم بلا مساس). قيمة = نسخة موقّعة تخصّ تلك الشهادة.
        //
        // عمود مفهرس بلا HasOne/FK عمدًا: الجدول له بالفعل مسار حذف متتالٍ من
        // CalibrationRecord، وإضافة مسار ثانٍ تتعثّر في SQLite. والشهادات تُحذف
        // حذفًا ناعمًا (IsDeleted) فما كان الـcascade ليعمل أصلًا.
        public int? CertificateId { get; set; }

        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string FileExtension { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        public virtual CalibrationRecord? CalibrationRecord { get; set; }
    }
}
