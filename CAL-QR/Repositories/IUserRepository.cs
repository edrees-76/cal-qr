using System.Collections.Generic;
using System.Threading.Tasks;
using CAL_QR.Models;

namespace CAL_QR.Repositories
{
    public interface IUserRepository
    {
        Task<IEnumerable<User>> GetAllAsync();
        Task<User?> GetByIdAsync(int id);
        Task<User?> GetByUsernameAsync(string username);
        Task AddAsync(User user);
        Task UpdateAsync(User user);
        Task<int> GetActiveAdminsCountAsync();
    }
}
