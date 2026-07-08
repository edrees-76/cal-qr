using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using CAL_QR.ViewModels.Base;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Repositories;
using CAL_QR.Services;
using CAL_QR.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;

namespace CAL_QR.ViewModels
{
    public class SettingsViewModel : BaseViewModel
    {
        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;
        private readonly IPaperTemplateRepository _templateRepository;
        private readonly IBackupService _backupService;
        private readonly IAuditLogRepository _auditLogRepository;

        // Security Fields
        private string _currentPassword = string.Empty;
        private string _newPassword = string.Empty;
        private string _confirmPassword = string.Empty;

        // Threshold & Auto-Lock Fields
        private int _alertDaysThreshold = 30;
        private int _autoLockMinutes = 10;

        // Paths Fields
        private string _databasePath = string.Empty;
        private string _attachmentsPath = string.Empty;
        private string _qrOutputPath = string.Empty;

        private string _originalDatabasePath = string.Empty;
        private string _originalAttachmentsPath = string.Empty;
        private string _originalQrOutputPath = string.Empty;

        // Backup Fields
        private string _backupPath = string.Empty;
        private string _selectedBackupSchedule = "None";
        private ObservableCollection<string> _backupSchedules = new() { "None", "Daily", "Weekly", "Monthly" };

        // Printing Fields
        private PaperTemplate? _selectedDefaultTemplate;
        private ObservableCollection<PaperTemplate> _templates = new();

        public SettingsViewModel(
            IDbContextFactory<CalQrDbContext> contextFactory,
            IPaperTemplateRepository templateRepository,
            IBackupService backupService,
            IAuditLogRepository auditLogRepository)
        {
            _contextFactory = contextFactory;
            _templateRepository = templateRepository;
            _backupService = backupService;
            _auditLogRepository = auditLogRepository;

            ChangePasswordCommand = new RelayCommand(async () => await ChangePasswordAsync(), CanChangePassword);
            SaveGeneralSettingsCommand = new RelayCommand(async () => await SaveGeneralSettingsAsync());
            SavePathsCommand = new RelayCommand(async () => await SavePathsAsync(), CanSavePaths);
            BrowseBackupPathCommand = new RelayCommand(BrowseBackupPath);
            BrowseDatabasePathCommand = new RelayCommand(BrowseDatabasePath);
            BrowseAttachmentsPathCommand = new RelayCommand(BrowseAttachmentsPath);
            BrowseQrOutputPathCommand = new RelayCommand(BrowseQrOutputPath);
            BackupNowCommand = new RelayCommand(async () => await BackupNowAsync());
            RestoreBackupCommand = new RelayCommand(async () => await RestoreBackupAsync());
            OpenTemplatesDialogCommand = new RelayCommand(OpenTemplatesDialog);

            _ = LoadSettingsAsync();
        }

        #region Properties
        // Password Properties
        public string CurrentPassword { get => _currentPassword; set { if (SetProperty(ref _currentPassword, value)) (ChangePasswordCommand as RelayCommand)?.RaiseCanExecuteChanged(); } }
        public string NewPassword { get => _newPassword; set { if (SetProperty(ref _newPassword, value)) (ChangePasswordCommand as RelayCommand)?.RaiseCanExecuteChanged(); } }
        public string ConfirmPassword { get => _confirmPassword; set { if (SetProperty(ref _confirmPassword, value)) (ChangePasswordCommand as RelayCommand)?.RaiseCanExecuteChanged(); } }

        // General Config Properties
        public int AlertDaysThreshold { get => _alertDaysThreshold; set => SetProperty(ref _alertDaysThreshold, value); }
        public int AutoLockMinutes { get => _autoLockMinutes; set => SetProperty(ref _autoLockMinutes, value); }

        // Path Properties
        public string DatabasePath 
        { 
            get => _databasePath; 
            set 
            { 
                if (SetProperty(ref _databasePath, value)) 
                    (SavePathsCommand as RelayCommand)?.RaiseCanExecuteChanged(); 
            } 
        }

        public string AttachmentsPath 
        { 
            get => _attachmentsPath; 
            set 
            { 
                if (SetProperty(ref _attachmentsPath, value)) 
                    (SavePathsCommand as RelayCommand)?.RaiseCanExecuteChanged(); 
            } 
        }

        public string QrOutputPath 
        { 
            get => _qrOutputPath; 
            set 
            { 
                if (SetProperty(ref _qrOutputPath, value)) 
                    (SavePathsCommand as RelayCommand)?.RaiseCanExecuteChanged(); 
            } 
        }

        // Backup Properties
        public string BackupPath { get => _backupPath; set => SetProperty(ref _backupPath, value); }
        public string SelectedBackupSchedule { get => _selectedBackupSchedule; set => SetProperty(ref _selectedBackupSchedule, value); }
        public ObservableCollection<string> BackupSchedules => _backupSchedules;

        // Print Properties
        public ObservableCollection<PaperTemplate> Templates { get => _templates; set => SetProperty(ref _templates, value); }
        public PaperTemplate? SelectedDefaultTemplate { get => _selectedDefaultTemplate; set => SetProperty(ref _selectedDefaultTemplate, value); }

        // Commands
        public ICommand ChangePasswordCommand { get; }
        public ICommand SaveGeneralSettingsCommand { get; }
        public ICommand SavePathsCommand { get; }
        public ICommand BrowseBackupPathCommand { get; }
        public ICommand BrowseDatabasePathCommand { get; }
        public ICommand BrowseAttachmentsPathCommand { get; }
        public ICommand BrowseQrOutputPathCommand { get; }
        public ICommand BackupNowCommand { get; }
        public ICommand RestoreBackupCommand { get; }
        public ICommand OpenTemplatesDialogCommand { get; }
        #endregion

        private async Task LoadSettingsAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                
                // Read from DB AppSettings
                var alert = await context.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "AlertDaysThreshold");
                if (alert != null && int.TryParse(alert.Value, out int aVal)) _alertDaysThreshold = aVal;

                var lockMin = await context.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "AutoLockMinutes");
                if (lockMin != null && int.TryParse(lockMin.Value, out int lVal)) _autoLockMinutes = lVal;

                var backupPathSetting = await context.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "BackupPath");
                if (backupPathSetting != null) _backupPath = backupPathSetting.Value;

                var backupScheduleSetting = await context.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "BackupSchedule");
                if (backupScheduleSetting != null) _selectedBackupSchedule = backupScheduleSetting.Value;

                // Load display paths dynamically from active connection
                var connectionString = context.Database.GetDbConnection().ConnectionString;
                var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString);
                DatabasePath = Path.GetFullPath(builder.DataSource);

                var attPathSetting = await context.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "AttachmentsPath");
                if (attPathSetting != null && !string.IsNullOrWhiteSpace(attPathSetting.Value))
                {
                    AttachmentsPath = attPathSetting.Value;
                }
                else
                {
                    AttachmentsPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Attachments"));
                }

                var qrPathSetting = await context.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "QrOutputPath");
                if (qrPathSetting != null && !string.IsNullOrWhiteSpace(qrPathSetting.Value))
                {
                    QrOutputPath = qrPathSetting.Value;
                }
                else
                {
                    QrOutputPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "QR_Output"));
                }

                _originalDatabasePath = DatabasePath;
                _originalAttachmentsPath = AttachmentsPath;
                _originalQrOutputPath = QrOutputPath;
                (SavePathsCommand as RelayCommand)?.RaiseCanExecuteChanged();

                // Load templates
                var templatesList = await _templateRepository.GetAllAsync();
                Templates = new ObservableCollection<PaperTemplate>(templatesList);
                SelectedDefaultTemplate = Templates.FirstOrDefault(t => t.IsDefault);

                // Notify view
                OnPropertyChanged(nameof(AlertDaysThreshold));
                OnPropertyChanged(nameof(AutoLockMinutes));
                OnPropertyChanged(nameof(BackupPath));
                OnPropertyChanged(nameof(SelectedBackupSchedule));
                OnPropertyChanged(nameof(DatabasePath));
                OnPropertyChanged(nameof(AttachmentsPath));
                OnPropertyChanged(nameof(QrOutputPath));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ أثناء تحميل الإعدادات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanChangePassword()
        {
            return !string.IsNullOrWhiteSpace(CurrentPassword) &&
                   !string.IsNullOrWhiteSpace(NewPassword) &&
                   NewPassword == ConfirmPassword &&
                   NewPassword.Length >= 4;
        }

        private async Task ChangePasswordAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var dbPassSetting = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == "MasterPassword");
                
                string currentDbHash = dbPassSetting?.Value ?? string.Empty;
                if (!PasswordHelper.VerifyPassword(CurrentPassword, currentDbHash))
                {
                    MessageBox.Show("كلمة المرور الحالية غير صحيحة.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string newHash = PasswordHelper.HashPassword(NewPassword);
                if (dbPassSetting == null)
                {
                    dbPassSetting = new AppSetting { Key = "MasterPassword", Value = newHash };
                    context.AppSettings.Add(dbPassSetting);
                }
                else
                {
                    dbPassSetting.Value = newHash;
                }

                await context.SaveChangesAsync();
                
                await _auditLogRepository.LogAsync("تغيير كلمة المرور", "نظام", "Security", "تم تعديل كلمة مرور النظام بنجاح.");
                MessageBox.Show("تم تغيير كلمة المرور بنجاح.", "تم التغيير", MessageBoxButton.OK, MessageBoxImage.Information);

                CurrentPassword = string.Empty;
                NewPassword = string.Empty;
                ConfirmPassword = string.Empty;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ أثناء تعديل كلمة المرور: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SaveGeneralSettingsAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // 1. Save AlertDaysThreshold
                var alertSetting = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == "AlertDaysThreshold");
                if (alertSetting == null)
                {
                    context.AppSettings.Add(new AppSetting { Key = "AlertDaysThreshold", Value = AlertDaysThreshold.ToString() });
                }
                else
                {
                    alertSetting.Value = AlertDaysThreshold.ToString();
                }

                // 2. Save AutoLockMinutes
                var lockSetting = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == "AutoLockMinutes");
                if (lockSetting == null)
                {
                    context.AppSettings.Add(new AppSetting { Key = "AutoLockMinutes", Value = AutoLockMinutes.ToString() });
                }
                else
                {
                    lockSetting.Value = AutoLockMinutes.ToString();
                }

                // 3. Save BackupPath & Schedule
                var bkpPath = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == "BackupPath");
                if (bkpPath == null)
                {
                    context.AppSettings.Add(new AppSetting { Key = "BackupPath", Value = BackupPath });
                }
                else
                {
                    bkpPath.Value = BackupPath;
                }

                var bkpSchedule = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == "BackupSchedule");
                if (bkpSchedule == null)
                {
                    context.AppSettings.Add(new AppSetting { Key = "BackupSchedule", Value = SelectedBackupSchedule });
                }
                else
                {
                    bkpSchedule.Value = SelectedBackupSchedule;
                }

                await context.SaveChangesAsync();

                // 4. Update default printer template
                if (SelectedDefaultTemplate != null)
                {
                    await _templateRepository.SetDefaultAsync(SelectedDefaultTemplate.Id);
                }

                // Re-trigger global refresh
                CalibrationEvents.RaiseCalibrationChanged();

                await _auditLogRepository.LogAsync("تغيير إعدادات", "نظام", "Settings", "تحديث الإعدادات العامة للنظام.");
                MessageBox.Show("تم حفظ الإعدادات بنجاح وتحديث النظام تلقائياً.", "تم الحفظ", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ أثناء حفظ الإعدادات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BrowseBackupPath()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                BackupPath = dialog.FolderName;
            }
        }

        private void BrowseDatabasePath()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                string dbFileName = !string.IsNullOrWhiteSpace(_originalDatabasePath)
                    ? Path.GetFileName(_originalDatabasePath)
                    : "cal-qr-simulation.db";
                DatabasePath = Path.Combine(dialog.FolderName, "DB", dbFileName);
            }
        }

        private void BrowseAttachmentsPath()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                AttachmentsPath = Path.Combine(dialog.FolderName, "attachments file");
            }
        }

        private void BrowseQrOutputPath()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                QrOutputPath = Path.Combine(dialog.FolderName, "QR");
            }
        }

        private bool CanSavePaths()
        {
            return DatabasePath != _originalDatabasePath ||
                   AttachmentsPath != _originalAttachmentsPath ||
                   QrOutputPath != _originalQrOutputPath;
        }

        private async Task SavePathsAsync()
        {
            bool dbChanged = DatabasePath != _originalDatabasePath;
            bool attChanged = AttachmentsPath != _originalAttachmentsPath;
            bool qrChanged = QrOutputPath != _originalQrOutputPath;

            if (!dbChanged && !attChanged && !qrChanged)
            {
                MessageBox.Show("لم يتم تغيير أي مسار.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // 1. Process Database Path Change
                if (dbChanged)
                {
                    if (string.IsNullOrWhiteSpace(DatabasePath))
                    {
                        MessageBox.Show("مسار قاعدة البيانات الجديد غير صالح.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    string? dbDir = Path.GetDirectoryName(DatabasePath);
                    if (!string.IsNullOrEmpty(dbDir) && !Directory.Exists(dbDir))
                    {
                        Directory.CreateDirectory(dbDir);
                    }

                    string currentDbPath;
                    var connectionString = context.Database.GetDbConnection().ConnectionString;
                    var dbBuilder = new SqliteConnectionStringBuilder(connectionString);
                    currentDbPath = Path.GetFullPath(dbBuilder.DataSource);

                    // SQLite online consistent snapshot backup
                    using (var source = new SqliteConnection($"Data Source={currentDbPath};Default Timeout=5"))
                    {
                        await source.OpenAsync();

                        // Checkpoint WAL frames to db file
                        using (var cmd = source.CreateCommand())
                        {
                            cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                            await cmd.ExecuteNonQueryAsync();
                        }

                        using (var destination = new SqliteConnection($"Data Source={DatabasePath};Default Timeout=5"))
                        {
                            await destination.OpenAsync();
                            source.BackupDatabase(destination);
                        }
                    }

                    // Release connections
                    SqliteConnection.ClearAllPools();

                    // Verify integrity
                    bool integrityOk = false;
                    using (var conn = new SqliteConnection($"Data Source={DatabasePath};Default Timeout=5"))
                    {
                        await conn.OpenAsync();
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "PRAGMA integrity_check;";
                            var result = await cmd.ExecuteScalarAsync() as string;
                            if (result == "ok")
                            {
                                integrityOk = true;
                            }
                        }
                    }

                    if (!integrityOk)
                    {
                        throw new InvalidOperationException("فشل التحقق من سلامة قاعدة البيانات الجديدة.");
                    }

                    // Save local path file db_path.txt
                    string configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "db_path.txt");
                    await File.WriteAllTextAsync(configFilePath, DatabasePath);

                    // Write DatabasePath to the AppSettings table inside the new database
                    using (var newContext = new CalQrDbContext(new DbContextOptionsBuilder<CalQrDbContext>().UseSqlite($"Data Source={DatabasePath}").Options))
                    {
                        var setting = await newContext.AppSettings.FirstOrDefaultAsync(s => s.Key == "DatabasePath");
                        if (setting == null)
                        {
                            newContext.AppSettings.Add(new AppSetting { Key = "DatabasePath", Value = DatabasePath });
                        }
                        else
                        {
                            setting.Value = DatabasePath;
                        }
                        await newContext.SaveChangesAsync();
                    }

                    // Also update it in current DB AppSettings
                    var dbSetting = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == "DatabasePath");
                    if (dbSetting == null)
                    {
                        context.AppSettings.Add(new AppSetting { Key = "DatabasePath", Value = DatabasePath });
                    }
                    else
                    {
                        dbSetting.Value = DatabasePath;
                    }
                }

                // 2. Process Attachments Path Change
                if (attChanged)
                {
                    if (string.IsNullOrWhiteSpace(AttachmentsPath))
                    {
                        MessageBox.Show("مسار مجلد المرفقات الجديد غير صالح.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    if (!Directory.Exists(AttachmentsPath))
                    {
                        Directory.CreateDirectory(AttachmentsPath);
                    }

                    int sourceFilesCount = 0;
                    if (Directory.Exists(_originalAttachmentsPath))
                    {
                        sourceFilesCount = Directory.GetFiles(_originalAttachmentsPath, "*", SearchOption.AllDirectories).Length;
                        if (sourceFilesCount > 0)
                        {
                            CopyDirectory(_originalAttachmentsPath, AttachmentsPath);
                        }
                    }

                    int destFilesCount = Directory.GetFiles(AttachmentsPath, "*", SearchOption.AllDirectories).Length;
                    if (destFilesCount < sourceFilesCount)
                    {
                        throw new InvalidOperationException("فشل نسخ كافة ملفات المرفقات إلى المسار الجديد.");
                    }

                    var attSetting = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == "AttachmentsPath");
                    if (attSetting == null)
                    {
                        context.AppSettings.Add(new AppSetting { Key = "AttachmentsPath", Value = AttachmentsPath });
                    }
                    else
                    {
                        attSetting.Value = AttachmentsPath;
                    }
                }

                // 3. Process QR Output Path Change
                if (qrChanged)
                {
                    if (string.IsNullOrWhiteSpace(QrOutputPath))
                    {
                        MessageBox.Show("مسار مجلد مخرجات QR الجديد غير صالح.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    if (!Directory.Exists(QrOutputPath))
                    {
                        Directory.CreateDirectory(QrOutputPath);
                    }

                    int sourceQrCount = 0;
                    if (Directory.Exists(_originalQrOutputPath))
                    {
                        sourceQrCount = Directory.GetFiles(_originalQrOutputPath, "*", SearchOption.AllDirectories).Length;
                        if (sourceQrCount > 0)
                        {
                            CopyDirectory(_originalQrOutputPath, QrOutputPath);
                        }
                    }

                    int destQrCount = Directory.GetFiles(QrOutputPath, "*", SearchOption.AllDirectories).Length;
                    if (destQrCount < sourceQrCount)
                    {
                        throw new InvalidOperationException("فشل نسخ كافة رموز الاستجابة السريعة إلى المسار الجديد.");
                    }

                    var qrSetting = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == "QrOutputPath");
                    if (qrSetting == null)
                    {
                        context.AppSettings.Add(new AppSetting { Key = "QrOutputPath", Value = QrOutputPath });
                    }
                    else
                    {
                        qrSetting.Value = QrOutputPath;
                    }
                }

                await context.SaveChangesAsync();

                _originalDatabasePath = DatabasePath;
                _originalAttachmentsPath = AttachmentsPath;
                _originalQrOutputPath = QrOutputPath;
                (SavePathsCommand as RelayCommand)?.RaiseCanExecuteChanged();

                await _auditLogRepository.LogAsync("تحديث مسارات ملفات النظام", "نظام", "Settings", "تحديث مسارات قاعدة البيانات والمرفقات بنجاح.");

                MessageBox.Show("تم حفظ المسارات الجديدة ونقل الملفات بنجاح. سيتم الآن إعادة تشغيل البرنامج تلقائياً لتطبيق المسارات الجديدة.", "تم حفظ الإعدادات", MessageBoxButton.OK, MessageBoxImage.Information);

                try
                {
                    // Clear connection pools to release sqlite files
                    SqliteConnection.ClearAllPools();

                    var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        var startInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = exePath,
                            WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                            UseShellExecute = true
                        };
                        System.Diagnostics.Process.Start(startInfo);
                        Application.Current.Shutdown();
                    }
                    else
                    {
                        throw new InvalidOperationException("تعذر العثور على المسار التنفيذي للتطبيق.");
                    }
                }
                catch (Exception restartEx)
                {
                    MessageBox.Show($"فشلت إعادة التشغيل التلقائي: {restartEx.Message}\nيرجى إغلاق البرنامج وإعادة تشغيله يدوياً لتطبيق المسارات الجديدة.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ أثناء نقل وحفظ المسارات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }
            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
                CopyDirectory(subDir, destSubDir);
            }
        }

        private async Task BackupNowAsync()
        {
            if (string.IsNullOrWhiteSpace(BackupPath))
            {
                MessageBox.Show("يرجى اختيار مسار مجلد النسخ الاحتياطي أولاً.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                await _backupService.BackupNowAsync(BackupPath);
                MessageBox.Show("تم إنشاء النسخة الاحتياطية بنجاح.", "تم النسخ الاحتياطي", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ أثناء النسخ الاحتياطي: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task RestoreBackupAsync()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Backup ZIP Files (*.zip)|*.zip"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var confirm = MessageBox.Show("تحذير هام: سيتم استبدال قاعدة البيانات الحالية وكافة المرفقات بمحتويات النسخة الاحتياطية. هذا الإجراء غير قابل للتراجع وسيتم تطبيق الاسترداد فوراً.\n\nهل تود الاستمرار بالاستعادة؟", "تأكيد الاسترداد الحاسم", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirm == MessageBoxResult.Yes)
                {
                    try
                    {
                        await _backupService.RestoreAsync(openFileDialog.FileName);
                        
                        // Force update
                        CalibrationEvents.RaiseCalibrationChanged();

                        MessageBox.Show("تمت استعادة البيانات بنجاح.", "تمت الاستعادة", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"خطأ أثناء استعادة البيانات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void OpenTemplatesDialog()
        {
            var dialog = App.ServiceProvider.GetRequiredService<Views.Dialogs.PaperTemplateDialog>();
            dialog.ShowDialog();
            _ = LoadSettingsAsync(); // Reload templates after dialog closes
        }
    }
}
