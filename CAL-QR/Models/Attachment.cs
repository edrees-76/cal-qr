using System;

namespace CAL_QR.Models
{
    public class Attachment
    {
        public int Id { get; set; }
        public int CalibrationRecordId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string FileExtension { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        public virtual CalibrationRecord? CalibrationRecord { get; set; }
    }
}
