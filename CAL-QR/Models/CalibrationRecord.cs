using System;
using System.Collections.Generic;

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
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public virtual Device? Device { get; set; }
        public virtual ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
    }
}
