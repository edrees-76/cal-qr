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
            }

            // Perform any safe check/migration of columns if they are missing
            // e.g. ExecuteSqlIfColumnMissing(context, "TableName", "ColumnName", "ALTER TABLE TableName ADD COLUMN ColumnName TYPE;");
            
            // Seed default settings if they don't exist
            SeedDefaultSettings(context);

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
    }
}
