using System.Collections.Generic;
using System.Threading.Tasks;
using CAL_QR.Models;

namespace CAL_QR.Repositories
{
    public interface IDeviceTypeRepository
    {
        Task<IEnumerable<DeviceType>> GetAllAsync();
        Task<DeviceType?> GetByIdAsync(int id);
        Task AddAsync(DeviceType type);
        Task UpdateAsync(DeviceType type);
        Task SoftDeleteAsync(int id);
    }
}
