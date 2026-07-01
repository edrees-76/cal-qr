using System.Collections.Generic;
using System.Threading.Tasks;
using CAL_QR.Models;

namespace CAL_QR.Services
{
    public interface IExportService
    {
        Task ExportToExcelAsync(IEnumerable<CalibrationRecord> records, string reportType, string filePath);
        Task ExportToPdfAsync(IEnumerable<CalibrationRecord> records, string reportType, string filePath);
    }
}
