using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Data;
using CAL_QR.Models;

namespace CAL_QR.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;

        public UserRepository(IDbContextFactory<CalQrDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<IEnumerable<User>> GetAllAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Users
                .AsNoTracking()
                .OrderBy(u => u.FullName)
                .ToListAsync();
        }

        public async Task<User?> GetByIdAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<User?> GetByUsernameAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return null;

            using var context = await _contextFactory.CreateDbContextAsync();
            string normalized = username.Trim().ToLower();
            return await context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Username.ToLower() == normalized);
        }

        public async Task AddAsync(User user)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            using var context = await _contextFactory.CreateDbContextAsync();
            user.Username = user.Username.Trim();
            user.CreatedAt = DateTime.UtcNow;
            context.Users.Add(user);
            await context.SaveChangesAsync();
        }

        public async Task UpdateAsync(User user)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            using var context = await _contextFactory.CreateDbContextAsync();
            
            // We read the existing entity to check if it's there
            var existing = await context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
            if (existing == null)
            {
                throw new InvalidOperationException($"المستخدم ذو المعرف {user.Id} غير موجود.");
            }

            // Update properties
            existing.Username = user.Username.Trim();
            existing.FullName = user.FullName;
            existing.Role = user.Role;
            existing.Permissions = user.Permissions;
            existing.IsEditor = user.IsEditor;
            existing.IsActive = user.IsActive;
            existing.FailedLoginAttempts = user.FailedLoginAttempts;
            existing.LockedUntil = user.LockedUntil;
            existing.LastLoginAt = user.LastLoginAt;

            // Only update password hash if a new one is set
            if (!string.IsNullOrEmpty(user.PasswordHash))
            {
                existing.PasswordHash = user.PasswordHash;
            }

            await context.SaveChangesAsync();
        }

        public async Task<int> GetActiveAdminsCountAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Users
                .AsNoTracking()
                .CountAsync(u => u.Role == UserRole.Admin && u.IsActive);
        }
    }
}
