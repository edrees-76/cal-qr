using System;

namespace CAL_QR.Models.DisplayItems
{
    public class OwnerDeviceDisplayItem
    {
        public int SequenceNumber { get; set; }
        public int DeviceId { get; set; }
        public string Model { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string DeviceTypeName { get; set; } = string.Empty;
        public DateTime? LastCalibrationDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string StatusText { get; set; } = string.Empty;
        public string StatusColorHex { get; set; } = string.Empty;

        // Formatted properties for easy binding
        public string LastCalibrationDateString => LastCalibrationDate?.ToString("yyyy-MM-dd") ?? "-";
        public string ExpiryDateString => ExpiryDate?.ToString("yyyy-MM-dd") ?? "-";
        
        // Row background matching the status color
        public string RowBackground => StatusText == "سارية" ? "#E8F5E9" : StatusText == "قريبة الانتهاء" ? "#FFFDE7" : StatusText == "منتهية" ? "#FFEBEE" : "Transparent";
    }
}
