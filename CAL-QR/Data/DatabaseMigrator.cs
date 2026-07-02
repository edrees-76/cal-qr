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

            // Perform any safe check/migration of columns if they are missing
            // e.g. ExecuteSqlIfColumnMissing(context, "TableName", "ColumnName", "ALTER TABLE TableName ADD COLUMN ColumnName TYPE;");
            
            // Seed default settings if they don't exist
            SeedDefaultSettings(context);
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
