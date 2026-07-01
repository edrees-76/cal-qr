using System.Collections.Generic;
using System.Threading.Tasks;
using CAL_QR.Models;

namespace CAL_QR.Repositories
{
    public interface IDeviceRepository
    {
        Task<IEnumerable<Device>> GetAllAsync();
        Task<Device?> GetByIdAsync(int id);
        Task<Device?> GetBySerialNumberAsync(string serialNumber);
        Task AddAsync(Device device);
        Task UpdateAsync(Device device);
        Task SoftDeleteAsync(int id);
    }
}
