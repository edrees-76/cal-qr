using System;
using System.Data;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Models;

namespace CAL_QR.Data
{
    public static class DatabaseMigrator
    {
        public static void RunMigrations(CalQrDbContext context)
        {
            // First, make sure the database is created
            context.Database.EnsureCreated();

            if (context.Database.IsRelational())
            {
                // Enable Write-Ahead Logging (WAL) and set Busy Timeout
                context.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
                context.Database.ExecuteSqlRaw("PRAGMA busy_timeout=5000;");

                // Create AcknowledgedExpiredDevices table if it doesn't exist
                context.Database.ExecuteSqlRaw(@"
                    CREATE TABLE IF NOT EXISTS AcknowledgedExpiredDevices (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        DeviceId INTEGER NOT NULL,
                        CalibrationRecordId INTEGER NOT NULL,
                        AcknowledgedDate TEXT NOT NULL
                    );
                ");

                // Create Users table if it doesn't exist
                context.Database.ExecuteSqlRaw(@"
                    CREATE TABLE IF NOT EXISTS Users (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Username TEXT NOT NULL UNIQUE,
                        PasswordHash TEXT NOT NULL,
                        FullName TEXT NOT NULL,
                        Role INTEGER NOT NULL,
                        Permissions INTEGER NOT NULL,
                        IsEditor INTEGER NOT NULL,
                        IsActive INTEGER NOT NULL DEFAULT 1,
                        CreatedAt TEXT NOT NULL,
                        LastLoginAt TEXT,
                        FailedLoginAttempts INTEGER NOT NULL DEFAULT 0,
                        LockedUntil TEXT
                    );
                ");

                // Execute defensive migrations for AuditLogs
                ExecuteSqlIfColumnMissing(context, "AuditLogs", "UserId", "ALTER TABLE AuditLogs ADD COLUMN UserId INTEGER;");
                ExecuteSqlIfColumnMissing(context, "AuditLogs", "Username", "ALTER TABLE AuditLogs ADD COLUMN Username TEXT;");

                // Execute defensive migrations for PaperTemplates
                ExecuteSqlIfColumnMissing(context, "PaperTemplates", "MarginRightMm", "ALTER TABLE PaperTemplates ADD COLUMN MarginRightMm TEXT DEFAULT '0' NOT NULL;");
                ExecuteSqlIfColumnMissing(context, "PaperTemplates", "MarginBottomMm", "ALTER TABLE PaperTemplates ADD COLUMN MarginBottomMm TEXT DEFAULT '0' NOT NULL;");
            }

            // Perform any safe check/migration of columns if they are missing
            // e.g. ExecuteSqlIfColumnMissing(context, "TableName", "ColumnName", "ALTER TABLE TableName ADD COLUMN ColumnName TYPE;");
            
            // Seed default settings if they don't exist
            SeedDefaultSettings(context);

            // Seed default admin user if no users exist (under upgrade condition)
            SeedDefaultAdminUser(context);

            // One-time migration for splitting CertificateManagement (Records) to Verification, Owners, and DeviceTypes
            try
            {
                var migrationCompletedSetting = context.AppSettings.FirstOrDefault(s => s.Key == "PermissionsSplitMigrated_v1");
                if (migrationCompletedSetting == null || migrationCompletedSetting.Value != "true")
                {
                    var users = context.Users.ToList();
                    foreach (var user in users)
                    {
                        if (user.Permissions.HasFlag(SystemPermissions.Records))
                        {
                            user.Permissions |= SystemPermissions.Verification | 
                                                SystemPermissions.Owners | 
                                                SystemPermissions.DeviceTypes;
                        }
                    }

                    if (migrationCompletedSetting == null)
                    {
                        context.AppSettings.Add(new AppSetting { Key = "PermissionsSplitMigrated_v1", Value = "true" });
                    }
                    else
                    {
                        migrationCompletedSetting.Value = "true";
                    }
                    context.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DatabaseMigrator Error] Permissions split migration failed: {ex.Message}");
            }

            if (context.Database.IsRelational())
            {
                // Sync DatabasePath key in AppSettings with the active database connection DataSource
                try
                {
                    var connectionString = context.Database.GetDbConnection().ConnectionString;
                    var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString);
                    var activeDbPath = System.IO.Path.GetFullPath(builder.DataSource);

                    var dbPathSetting = context.AppSettings.FirstOrDefault(s => s.Key == "DatabasePath");
                    if (dbPathSetting == null)
                    {
                        context.AppSettings.Add(new AppSetting { Key = "DatabasePath", Value = activeDbPath });
                        context.SaveChanges();
                    }
                    else if (string.IsNullOrWhiteSpace(dbPathSetting.Value) || System.IO.Path.GetFullPath(dbPathSetting.Value) != activeDbPath)
                    {
                        dbPathSetting.Value = activeDbPath;
                        context.SaveChanges();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DatabaseMigrator Error] Syncing database path failed: {ex.Message}");
                }
            }
        }

        public static void ExecuteSqlIfColumnMissing(CalQrDbContext context, string tableName, string columnName, string alterTableSql)
        {
            var connection = context.Database.GetDbConnection();
            var alreadyOpen = connection.State == ConnectionState.Open;

            try
            {
                if (!alreadyOpen) connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = $"PRAGMA table_info({tableName});";
                
                bool columnExists = false;
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var name = reader["name"]?.ToString();
                        if (string.Equals(name, columnName, StringComparison.OrdinalIgnoreCase))
                        {
                            columnExists = true;
                            break;
                        }
                    }
                }

                if (!columnExists)
                {
                    using var alterCommand = connection.CreateCommand();
                    alterCommand.CommandText = alterTableSql;
                    alterCommand.ExecuteNonQuery();
                }
            }
            finally
            {
                if (!alreadyOpen) connection.Close();
            }
        }

        private static void SeedDefaultSettings(CalQrDbContext context)
        {
            var defaultSettings = new[]
            {
                new AppSetting { Key = "DatabasePath", Value = "" },
                new AppSetting { Key = "AttachmentsPath", Value = "" },
                new AppSetting { Key = "QrOutputPath", Value = "" },
                new AppSetting { Key = "BackupPath", Value = "" },
                new AppSetting { Key = "BackupSchedule", Value = "Daily" },
                new AppSetting { Key = "AlertDaysThreshold", Value = "30" },
                new AppSetting { Key = "AutoLockMinutes", Value = "10" },
                new AppSetting { Key = "DefaultTemplateId", Value = "0" },
                new AppSetting { Key = "PasswordHash", Value = "" },
                new AppSetting { Key = "LastPrinterName", Value = "" },
                new AppSetting { Key = "LastTemplateId", Value = "0" },
                new AppSetting { Key = "DateFormat", Value = "YYYY-MM-DD" },
                new AppSetting { Key = "Language", Value = "ar" },
                new AppSetting { Key = "FirstRunCompleted", Value = "" },
                new AppSetting { Key = "SecurityQuestion", Value = "" },
                new AppSetting { Key = "SecurityAnswer", Value = "" }
            };

            bool changed = false;
            foreach (var setting in defaultSettings)
            {
                // AppSettings query should be fast and standard
                var exists = context.AppSettings.Any(s => s.Key == setting.Key);
                if (!exists)
                {
                    context.AppSettings.Add(setting);
                    changed = true;
                }
            }

            if (changed)
            {
                context.SaveChanges();
            }
        }

        private static void SeedDefaultAdminUser(CalQrDbContext context)
        {
            try
            {
                var firstRunSetting = context.AppSettings.FirstOrDefault(s => s.Key == "FirstRunCompleted");
                bool isFirstRunCompleted = firstRunSetting != null && firstRunSetting.Value == "true";

                if (isFirstRunCompleted && !context.Users.Any())
                {
                    // Generate a random temporary password
                    string tempPassword = Guid.NewGuid().ToString("N").Substring(0, 10);
                    string passwordHash = BCrypt.Net.BCrypt.HashPassword(tempPassword);

                    var adminUser = new User
                    {
                        Username = "admin",
                        PasswordHash = passwordHash,
                        FullName = "مدير النظام الافتراضي",
                        Role = UserRole.Admin,
                        Permissions = SystemPermissions.Records |
                                      SystemPermissions.Verification |
                                      SystemPermissions.Owners |
                                      SystemPermissions.DeviceTypes |
                                      SystemPermissions.Reports |
                                      SystemPermissions.Settings |
                                      SystemPermissions.BackupRestore |
                                      SystemPermissions.UserManagement,
                        IsEditor = true,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    context.Users.Add(adminUser);
                    context.SaveChanges();

                    string warningMsg = $@"
========================================================================
⚠️ [تحذير أمني] تم إنشاء حساب المسؤول الافتراضي بنجاح!
اسم المستخدم: admin
كلمة المرور المؤقتة: {tempPassword}
يرجى تسجيل الدخول وتغيير كلمة المرور فوراً لأسباب أمنية.
========================================================================
";
                    System.Diagnostics.Debug.WriteLine(warningMsg);
                    Console.WriteLine(warningMsg);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DatabaseMigrator Error] Seeding admin user failed: {ex.Message}");
            }
        }
    }
}
