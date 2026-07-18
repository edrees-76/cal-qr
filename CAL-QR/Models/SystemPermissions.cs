using System;

namespace CAL_QR.Models
{
    [Flags]
    public enum SystemPermissions
    {
        None = 0,
        Records = 1 << 0,           // السجلات والأجهزة
        Reports = 1 << 1,           // التقارير
        Settings = 1 << 2,          // الإعدادات
        BackupRestore = 1 << 3,     // النسخ والاحتياطي والاستعادة
        UserManagement = 1 << 4,    // إدارة المستخدمين
        Verification = 1 << 5,      // التحقق
        Owners = 1 << 6,            // الجهات
        DeviceTypes = 1 << 7        // الأنواع
    }
}
