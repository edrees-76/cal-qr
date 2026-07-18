using System;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CAL_QR.Data;
using CAL_QR.Helpers;

namespace CAL_QR.Views
{
    public partial class FirstRunWizard : Window
    {
        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;
        private int _currentStep = 1;

        // In-memory fields (no DB writes until Step 4 finish)
        private string _databasePath = string.Empty;
        private string _adminFullName = string.Empty;
        private string _adminUsername = string.Empty;
        private string _password = string.Empty;
        private string _confirmPassword = string.Empty;
        private string _securityQuestion = string.Empty;
        private string _securityAnswer = string.Empty;

        public FirstRunWizard(IDbContextFactory<CalQrDbContext> contextFactory)
        {
            InitializeComponent();
            _contextFactory = contextFactory;

            // Set default path
            _databasePath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cal-qr.db"));
            TxtDatabasePath.Text = _databasePath;

            UpdateStepUI();
        }

        private void BrowsePath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                _databasePath = Path.Combine(dialog.FolderName, "cal-qr.db");
                TxtDatabasePath.Text = _databasePath;
            }
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep == 3)
            {
                // Validate Step 3 inputs only — no DB writes
                _adminFullName = TxtAdminFullName.Text.Trim();
                _adminUsername = TxtAdminUsername.Text.Trim();

                _password = TxtWizardPassword.Visibility == Visibility.Visible 
                    ? TxtWizardPassword.Password 
                    : TxtWizardPasswordReveal.Text;

                _confirmPassword = TxtWizardConfirmPassword.Visibility == Visibility.Visible 
                    ? TxtWizardConfirmPassword.Password 
                    : TxtWizardConfirmPasswordReveal.Text;

                _securityQuestion = TxtSecurityQuestion.Text.Trim();
                _securityAnswer = TxtSecurityAnswer.Text.Trim();

                if (string.IsNullOrWhiteSpace(_adminFullName))
                {
                    ShowStepError("يرجى إدخال الاسم الكامل للمسؤول.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(_adminUsername))
                {
                    ShowStepError("يرجى إدخال اسم المستخدم للمسؤول.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(_password) || _password.Length < 4)
                {
                    ShowStepError("كلمة المرور يجب أن تكون 4 أحرف على الأقل.");
                    return;
                }

                if (_password != _confirmPassword)
                {
                    ShowStepError("كلمتا المرور غير متطابقتين.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(_securityQuestion))
                {
                    ShowStepError("يرجى إدخال السؤال السري.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(_securityAnswer))
                {
                    ShowStepError("يرجى إدخال إجابة السؤال السري.");
                    return;
                }

                HideStepError();
            }

            if (_currentStep < 4)
            {
                _currentStep++;
                UpdateStepUI();
            }
        }

        private void BtnRevealPassword_Click(object sender, RoutedEventArgs e)
        {
            if (TxtWizardPassword.Visibility == Visibility.Visible)
            {
                TxtWizardPasswordReveal.Text = TxtWizardPassword.Password;
                TxtWizardPasswordReveal.Visibility = Visibility.Visible;
                TxtWizardPassword.Visibility = Visibility.Collapsed;
                IconRevealPassword.Kind = MaterialDesignThemes.Wpf.PackIconKind.EyeOff;
                TxtWizardPasswordReveal.Focus();
                if (TxtWizardPasswordReveal.Text.Length > 0)
                    TxtWizardPasswordReveal.CaretIndex = TxtWizardPasswordReveal.Text.Length;
            }
            else
            {
                TxtWizardPassword.Password = TxtWizardPasswordReveal.Text;
                TxtWizardPassword.Visibility = Visibility.Visible;
                TxtWizardPasswordReveal.Visibility = Visibility.Collapsed;
                IconRevealPassword.Kind = MaterialDesignThemes.Wpf.PackIconKind.Eye;
                TxtWizardPassword.Focus();
            }
        }

        private void BtnRevealConfirmPassword_Click(object sender, RoutedEventArgs e)
        {
            if (TxtWizardConfirmPassword.Visibility == Visibility.Visible)
            {
                TxtWizardConfirmPasswordReveal.Text = TxtWizardConfirmPassword.Password;
                TxtWizardConfirmPasswordReveal.Visibility = Visibility.Visible;
                TxtWizardConfirmPassword.Visibility = Visibility.Collapsed;
                IconRevealConfirmPassword.Kind = MaterialDesignThemes.Wpf.PackIconKind.EyeOff;
                TxtWizardConfirmPasswordReveal.Focus();
                if (TxtWizardConfirmPasswordReveal.Text.Length > 0)
                    TxtWizardConfirmPasswordReveal.CaretIndex = TxtWizardConfirmPasswordReveal.Text.Length;
            }
            else
            {
                TxtWizardConfirmPassword.Password = TxtWizardConfirmPasswordReveal.Text;
                TxtWizardConfirmPassword.Visibility = Visibility.Visible;
                TxtWizardConfirmPasswordReveal.Visibility = Visibility.Collapsed;
                IconRevealConfirmPassword.Kind = MaterialDesignThemes.Wpf.PackIconKind.Eye;
                TxtWizardConfirmPassword.Focus();
            }
        }

        private void BtnPrevious_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep > 1)
            {
                _currentStep--;
                UpdateStepUI();
            }
        }

        private void BtnFinish_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Atomic Save: Write settings and User to DB in one transaction
                using var context = _contextFactory.CreateDbContext();

                SetAppSetting(context, "DatabasePath", _databasePath);
                SetAppSetting(context, "SecurityQuestion", _securityQuestion);
                SetAppSetting(context, "FirstRunCompleted", "true");

                // Create the admin user
                var adminUser = new Models.User
                {
                    FullName = _adminFullName.Trim(),
                    Username = _adminUsername.Trim().ToLowerInvariant(),
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(_password),
                    Role = Models.UserRole.Admin,
                    Permissions = Models.SystemPermissions.Records |
                                  Models.SystemPermissions.Verification |
                                  Models.SystemPermissions.Owners |
                                  Models.SystemPermissions.DeviceTypes |
                                  Models.SystemPermissions.Reports |
                                  Models.SystemPermissions.Settings |
                                  Models.SystemPermissions.BackupRestore |
                                  Models.SystemPermissions.UserManagement,
                    IsEditor = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                context.Users.Add(adminUser);
                context.SaveChanges();

                // Open LoginWindow and close wizard
                var loginWindow = App.ServiceProvider.GetRequiredService<LoginWindow>();
                loginWindow.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء حفظ الإعدادات:\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetAppSetting(CalQrDbContext context, string key, string value)
        {
            var setting = context.AppSettings.FirstOrDefault(s => s.Key == key);
            if (setting != null)
            {
                setting.Value = value;
            }
            else
            {
                context.AppSettings.Add(new Models.AppSetting { Key = key, Value = value });
            }
        }

        private void UpdateStepUI()
        {
            Step1Panel.Visibility = _currentStep == 1 ? Visibility.Visible : Visibility.Collapsed;
            Step2Panel.Visibility = _currentStep == 2 ? Visibility.Visible : Visibility.Collapsed;
            Step3Panel.Visibility = _currentStep == 3 ? Visibility.Visible : Visibility.Collapsed;
            Step4Panel.Visibility = _currentStep == 4 ? Visibility.Visible : Visibility.Collapsed;

            BtnPrevious.Visibility = _currentStep > 1 && _currentStep < 4 ? Visibility.Visible : Visibility.Collapsed;
            BtnNext.Visibility = _currentStep < 4 ? Visibility.Visible : Visibility.Collapsed;
            BtnFinish.Visibility = _currentStep == 4 ? Visibility.Visible : Visibility.Collapsed;

            TxtStepIndicator.Text = $"الخطوة {_currentStep} من 4";

            HideStepError();
        }

        private void ShowStepError(string message)
        {
            TxtStepError.Text = message;
            TxtStepError.Visibility = Visibility.Visible;
        }

        private void HideStepError()
        {
            TxtStepError.Visibility = Visibility.Collapsed;
        }
    }
}
