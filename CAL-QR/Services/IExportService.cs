using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CAL_QR.Models;

namespace CAL_QR.Services
{
    public class DistributionItem
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class PerformanceReportData
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalRecords { get; set; }
        
        public int PassedCount { get; set; }
        public int FailedCount { get; set; }
        public int ConditionalCount { get; set; }
        
        public double PassedPercent { get; set; }
        public double FailedPercent { get; set; }
        public double ConditionalPercent { get; set; }

        public List<DistributionItem> ByDeviceType { get; set; } = new();
        public List<DistributionItem> ByOwner { get; set; } = new();
        public List<DistributionItem> ByEngineer { get; set; } = new();

        public int NewDevices { get; set; }
        public int NewOwners { get; set; }
        public int NewDeviceTypes { get; set; }

        public bool IsDetailed { get; set; }
        public List<CalibrationRecord> Records { get; set; } = new();
    }

    public interface IExportService
    {
        Task ExportToExcelAsync(IEnumerable<CalibrationRecord> records, string reportType, string filePath);
        Task ExportToPdfAsync(IEnumerable<CalibrationRecord> records, string reportType, string filePath);
        Task ExportPerformanceReportToExcelAsync(PerformanceReportData data, string filePath);
        Task ExportPerformanceReportToPdfAsync(PerformanceReportData data, string filePath);
    }
}
