using System.Collections.Generic;
using System.Threading.Tasks;
using CAL_QR.Models;

namespace CAL_QR.Repositories
{
    public interface ICalibrationRepository
    {
        Task<IEnumerable<CalibrationRecord>> GetAllAsync();
        Task<CalibrationRecord?> GetByIdAsync(int id);
        Task<CalibrationRecord?> GetByCertificateNumberAsync(string certificateNumber);
        Task<IEnumerable<CalibrationRecord>> GetByDeviceIdAsync(int deviceId);
        Task AddAsync(CalibrationRecord record);
        Task UpdateAsync(CalibrationRecord record);
        Task SoftDeleteAsync(int id);
    }
}
