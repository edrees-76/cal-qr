using System;
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
        Task<User?> GetFirstActiveAdminAsync();
        /// <summary>Counts a failed login for the user; returns the new lock end (UTC) when this failure locked the account.</summary>
        Task<DateTime?> RegisterFailedLoginAsync(int userId, DateTime nowUtc);
        Task ResetLoginFailuresAsync(int userId);
    }
}
