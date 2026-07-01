using System;
using System.Collections.Generic;

namespace CAL_QR.Models
{
    public class DeviceType
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsDeleted { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<Device> Devices { get; set; } = new List<Device>();
    }
}
