using System;

namespace CAL_QR.Models
{
    public enum UserRole
    {
        Viewer = 0,
        User = 1,
        Admin = 2
    }

    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public UserRole Role { get; set; } = UserRole.Viewer;
        public SystemPermissions Permissions { get; set; } = SystemPermissions.None;
        public bool IsEditor { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }
        public int FailedLoginAttempts { get; set; }
        public DateTime? LockedUntil { get; set; }

        // Computed properties
        public string RoleDisplayName => Role switch
        {
            UserRole.Admin => "مدير النظام",
            UserRole.User => "مستخدم",
            UserRole.Viewer => "مشاهد",
            _ => "غير معروف"
        };

        public string StatusDisplayName => IsActive ? "نشط" : "موقوف";

        public string IsEditorDisplayName => IsEditor ? "يمكنه التعديل" : "قراءة فقط";

        public bool CanManageUsers => Role == UserRole.Admin;

        public bool HasPermission(SystemPermissions perm)
        {
            return Role == UserRole.Admin || Permissions.HasFlag(perm);
        }
    }
}
