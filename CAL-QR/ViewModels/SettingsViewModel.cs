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
            BrowseBackupPathCommand = new RelayCommand(BrowseBackupPath);
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
        public string DatabasePath { get => _databasePath; set => SetProperty(ref _databasePath, value); }
        public string AttachmentsPath { get => _attachmentsPath; set => SetProperty(ref _attachmentsPath, value); }
        public string QrOutputPath { get => _qrOutputPath; set => SetProperty(ref _qrOutputPath, value); }

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
        public ICommand BrowseBackupPathCommand { get; }
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

                // Load display paths
                string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cal-qr.db");
                DatabasePath = Path.GetFullPath(dbPath);
                AttachmentsPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Attachments"));
                QrOutputPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "QR_Output"));

                // Load templates
                var templatesList = await _templateRepository.GetAllAsync();
                Templates = new ObservableCollection<PaperTemplate>(templatesList);
                SelectedDefaultTemplate = Templates.FirstOrDefault(t => t.IsDefault);

                // Notify view
                OnPropertyChanged(nameof(AlertDaysThreshold));
                OnPropertyChanged(nameof(AutoLockMinutes));
                OnPropertyChanged(nameof(BackupPath));
                OnPropertyChanged(nameof(SelectedBackupSchedule));
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
