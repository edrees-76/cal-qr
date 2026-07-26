using System;
using System.Collections.Generic;

namespace CAL_QR.Models
{
    public class Device
    {
        public int Id { get; set; }
        public string Model { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public int OwnerId { get; set; }
        public int DeviceTypeId { get; set; }
        public bool IsDeleted { get; set; } = false;
        public bool IsSeedTestData { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual Owner? Owner { get; set; }
        public virtual DeviceType? DeviceType { get; set; }
        public virtual ICollection<CalibrationRecord> CalibrationRecords { get; set; } = new List<CalibrationRecord>();
    }
}
