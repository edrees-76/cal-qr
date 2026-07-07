using System;

namespace CAL_QR.Models
{
    public class AcknowledgedExpiredDevice
    {
        public int Id { get; set; }
        public int DeviceId { get; set; }
        public int CalibrationRecordId { get; set; }
        public DateTime AcknowledgedDate { get; set; }
    }
}
