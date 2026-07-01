using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CAL_QR.Data;
using CAL_QR.Helpers;

namespace CAL_QR.Views
{
    public partial class LoginWindow : Window
    {
        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;
        private int _failedAttempts = 0;
        private DispatcherTimer? _lockoutTimer;
        private int _lockoutSecondsRemaining = 0;

        public LoginWindow(IDbContextFactory<CalQrDbContext> contextFactory)
        {
            InitializeComponent();
            _contextFactory = contextFactory;
            TxtPassword.Focus();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(this, "هل تريد الخروج من المنظومة؟", "تأكيد الخروج", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
            if (result == MessageBoxResult.Yes)
            {
                Application.Current.Shutdown();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                IconMaximize.Kind = MaterialDesignThemes.Wpf.PackIconKind.WindowMaximize;
            }
            else
            {
                this.WindowState = WindowState.Maximized;
                IconMaximize.Kind = MaterialDesignThemes.Wpf.PackIconKind.WindowRestore;
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string password = TxtPassword.Visibility == Visibility.Visible 
                ? TxtPassword.Password 
                : TxtPasswordReveal.Text;

            if (string.IsNullOrEmpty(password))
            {
                ShowError("الرجاء إدخال كلمة المرور.");
                return;
            }

            BtnLogin.IsEnabled = false;
            TxtPassword.IsEnabled = false;
            TxtPasswordReveal.IsEnabled = false;
            BtnRevealPassword.IsEnabled = false;

            bool isPasswordCorrect = false;

            try
            {
                using (var context = _contextFactory.CreateDbContext())
                {
                    var hashSetting = context.AppSettings.FirstOrDefault(s => s.Key == "PasswordHash");
                    string storedHash = hashSetting?.Value ?? string.Empty;

                    isPasswordCorrect = PasswordHelper.VerifyPassword(password, storedHash);
                }
            }
            catch (Exception)
            {
                isPasswordCorrect = PasswordHelper.VerifyPassword(password, string.Empty);
            }

            if (isPasswordCorrect)
            {
                var mainWindow = App.ServiceProvider.GetRequiredService<MainWindow>();
                mainWindow.Show();
                this.Close();
            }
            else
            {
                _failedAttempts++;
                BtnLogin.IsEnabled = true;
                TxtPassword.IsEnabled = true;
                TxtPasswordReveal.IsEnabled = true;
                BtnRevealPassword.IsEnabled = true;

                if (_failedAttempts >= 3)
                {
                    StartLockout();
                }
                else
                {
                    ShowError($"كلمة المرور غير صحيحة! محاولات متبقية: {3 - _failedAttempts}");
                    TxtPassword.Password = string.Empty;
                    TxtPasswordReveal.Text = string.Empty;
                    if (TxtPassword.Visibility == Visibility.Visible)
                        TxtPassword.Focus();
                    else
                        TxtPasswordReveal.Focus();
                }
            }
        }

        private void StartLockout()
        {
            _lockoutSecondsRemaining = 30;
            BtnLogin.IsEnabled = false;
            TxtPassword.IsEnabled = false;
            TxtPasswordReveal.IsEnabled = false;
            BtnRevealPassword.IsEnabled = false;
            TxtPassword.Password = string.Empty;
            TxtPasswordReveal.Text = string.Empty;

            ShowError($"تم حظر الدخول مؤقتاً بسبب 3 محاولات خاطئة. يرجى الانتظار {_lockoutSecondsRemaining} ثانية...");

            _lockoutTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _lockoutTimer.Tick += LockoutTimer_Tick;
            _lockoutTimer.Start();
        }

        private void LockoutTimer_Tick(object? sender, EventArgs e)
        {
            _lockoutSecondsRemaining--;
            if (_lockoutSecondsRemaining <= 0)
            {
                _lockoutTimer?.Stop();
                _failedAttempts = 0;
                BtnLogin.IsEnabled = true;
                TxtPassword.IsEnabled = true;
                TxtPasswordReveal.IsEnabled = true;
                BtnRevealPassword.IsEnabled = true;
                TxtError.Visibility = Visibility.Collapsed;
                if (TxtPassword.Visibility == Visibility.Visible)
                    TxtPassword.Focus();
                else
                    TxtPasswordReveal.Focus();
            }
            else
            {
                ShowError($"تم حظر الدخول مؤقتاً بسبب 3 محاولات خاطئة. يرجى الانتظار {_lockoutSecondsRemaining} ثانية...");
            }
        }

        private void BtnRevealPassword_Click(object sender, RoutedEventArgs e)
        {
            if (TxtPassword.Visibility == Visibility.Visible)
            {
                TxtPasswordReveal.Text = TxtPassword.Password;
                TxtPasswordReveal.Visibility = Visibility.Visible;
                TxtPassword.Visibility = Visibility.Collapsed;
                IconRevealPassword.Kind = MaterialDesignThemes.Wpf.PackIconKind.EyeOff;
                TxtPasswordReveal.Focus();
                if (TxtPasswordReveal.Text.Length > 0)
                    TxtPasswordReveal.CaretIndex = TxtPasswordReveal.Text.Length;
            }
            else
            {
                TxtPassword.Password = TxtPasswordReveal.Text;
                TxtPassword.Visibility = Visibility.Visible;
                TxtPasswordReveal.Visibility = Visibility.Collapsed;
                IconRevealPassword.Kind = MaterialDesignThemes.Wpf.PackIconKind.Eye;
                TxtPassword.Focus();
            }
        }

        private void ShowError(string message)
        {
            TxtError.Text = message;
            TxtError.Visibility = Visibility.Visible;
        }

        private void TxtPassword_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnLogin_Click(sender, e);
            }
        }

        private void ForgotPassword_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var context = _contextFactory.CreateDbContext())
                {
                    var questionSetting = context.AppSettings.FirstOrDefault(s => s.Key == "SecurityQuestion");
                    var answerSetting = context.AppSettings.FirstOrDefault(s => s.Key == "SecurityAnswer");

                    string question = questionSetting?.Value ?? string.Empty;
                    string answer = answerSetting?.Value ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(answer))
                    {
                        ShowRecoverySupportMessage();
                        return;
                    }

                    // Setup recovery interface state
                    TxtSecurityQuestion.Text = question;
                    TxtSecurityAnswerInput.Text = string.Empty;
                    
                    TxtNewPassword.Password = string.Empty;
                    TxtNewPasswordReveal.Text = string.Empty;
                    TxtNewPassword.Visibility = Visibility.Visible;
                    TxtNewPasswordReveal.Visibility = Visibility.Collapsed;
                    IconRevealNewPassword.Kind = MaterialDesignThemes.Wpf.PackIconKind.Eye;

                    TxtConfirmNewPassword.Password = string.Empty;
                    TxtConfirmNewPasswordReveal.Text = string.Empty;
                    TxtConfirmNewPassword.Visibility = Visibility.Visible;
                    TxtConfirmNewPasswordReveal.Visibility = Visibility.Collapsed;
                    IconRevealConfirmNewPassword.Kind = MaterialDesignThemes.Wpf.PackIconKind.Eye;

                    PanelVerifyQuestion.Visibility = Visibility.Visible;
                    PanelResetPassword.Visibility = Visibility.Collapsed;

                    GridLoginContent.Visibility = Visibility.Collapsed;
                    GridForgotPassword.Visibility = Visibility.Visible;
                    TxtSecurityAnswerInput.Focus();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء تحميل بيانات استرداد الحساب: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnBackToLogin_Click(object sender, RoutedEventArgs e)
        {
            GridForgotPassword.Visibility = Visibility.Collapsed;
            GridLoginContent.Visibility = Visibility.Visible;
            TxtPassword.Focus();
        }

        private void BtnVerifyAnswer_Click(object sender, RoutedEventArgs e)
        {
            string inputAnswer = TxtSecurityAnswerInput.Text.Trim();
            if (string.IsNullOrEmpty(inputAnswer))
            {
                MessageBox.Show("يرجى إدخال إجابة السؤال السري أولاً.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var context = _contextFactory.CreateDbContext())
                {
                    var answerSetting = context.AppSettings.FirstOrDefault(s => s.Key == "SecurityAnswer");
                    string storedAnswerHash = answerSetting?.Value ?? string.Empty;

                    if (PasswordHelper.VerifyPassword(inputAnswer, storedAnswerHash))
                    {
                        // Transition to password reset state
                        PanelVerifyQuestion.Visibility = Visibility.Collapsed;
                        PanelResetPassword.Visibility = Visibility.Visible;
                        TxtNewPassword.Focus();
                    }
                    else
                    {
                        // Show recovery master message on wrong answer
                        ShowRecoverySupportMessage();
                        TxtSecurityAnswerInput.Focus();
                        TxtSecurityAnswerInput.SelectAll();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء التحقق من الإجابة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TxtSecurityAnswerInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnVerifyAnswer_Click(sender, e);
            }
        }

        private void BtnRevealNewPassword_Click(object sender, RoutedEventArgs e)
        {
            if (TxtNewPassword.Visibility == Visibility.Visible)
            {
                TxtNewPasswordReveal.Text = TxtNewPassword.Password;
                TxtNewPasswordReveal.Visibility = Visibility.Visible;
                TxtNewPassword.Visibility = Visibility.Collapsed;
                IconRevealNewPassword.Kind = MaterialDesignThemes.Wpf.PackIconKind.EyeOff;
                TxtNewPasswordReveal.Focus();
                if (TxtNewPasswordReveal.Text.Length > 0)
                    TxtNewPasswordReveal.CaretIndex = TxtNewPasswordReveal.Text.Length;
            }
            else
            {
                TxtNewPassword.Password = TxtNewPasswordReveal.Text;
                TxtNewPassword.Visibility = Visibility.Visible;
                TxtNewPasswordReveal.Visibility = Visibility.Collapsed;
                IconRevealNewPassword.Kind = MaterialDesignThemes.Wpf.PackIconKind.Eye;
                TxtNewPassword.Focus();
            }
        }

        private void BtnRevealConfirmNewPassword_Click(object sender, RoutedEventArgs e)
        {
            if (TxtConfirmNewPassword.Visibility == Visibility.Visible)
            {
                TxtConfirmNewPasswordReveal.Text = TxtConfirmNewPassword.Password;
                TxtConfirmNewPasswordReveal.Visibility = Visibility.Visible;
                TxtConfirmNewPassword.Visibility = Visibility.Collapsed;
                IconRevealConfirmNewPassword.Kind = MaterialDesignThemes.Wpf.PackIconKind.EyeOff;
                TxtConfirmNewPasswordReveal.Focus();
                if (TxtConfirmNewPasswordReveal.Text.Length > 0)
                    TxtConfirmNewPasswordReveal.CaretIndex = TxtConfirmNewPasswordReveal.Text.Length;
            }
            else
            {
                TxtConfirmNewPassword.Password = TxtConfirmNewPasswordReveal.Text;
                TxtConfirmNewPassword.Visibility = Visibility.Visible;
                TxtConfirmNewPasswordReveal.Visibility = Visibility.Collapsed;
                IconRevealConfirmNewPassword.Kind = MaterialDesignThemes.Wpf.PackIconKind.Eye;
                TxtConfirmNewPassword.Focus();
            }
        }

        private void BtnSaveNewPassword_Click(object sender, RoutedEventArgs e)
        {
            string newPassword = TxtNewPassword.Visibility == Visibility.Visible 
                ? TxtNewPassword.Password 
                : TxtNewPasswordReveal.Text;
            string confirmPassword = TxtConfirmNewPassword.Visibility == Visibility.Visible 
                ? TxtConfirmNewPassword.Password 
                : TxtConfirmNewPasswordReveal.Text;

            if (string.IsNullOrEmpty(newPassword))
            {
                MessageBox.Show("الرجاء إدخال كلمة المرور الجديدة.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                if (TxtNewPassword.Visibility == Visibility.Visible)
                    TxtNewPassword.Focus();
                else
                    TxtNewPasswordReveal.Focus();
                return;
            }

            if (newPassword != confirmPassword)
            {
                MessageBox.Show("كلمتا المرور غير متطابقتين!", "خطأ في التأكيد", MessageBoxButton.OK, MessageBoxImage.Error);
                if (TxtConfirmNewPassword.Visibility == Visibility.Visible)
                    TxtConfirmNewPassword.Focus();
                else
                    TxtConfirmNewPasswordReveal.Focus();
                return;
            }

            try
            {
                using (var context = _contextFactory.CreateDbContext())
                {
                    var hashSetting = context.AppSettings.FirstOrDefault(s => s.Key == "PasswordHash");
                    string newHash = PasswordHelper.HashPassword(newPassword);

                    if (hashSetting != null)
                    {
                        hashSetting.Value = newHash;
                    }
                    else
                    {
                        context.AppSettings.Add(new Models.AppSetting { Key = "PasswordHash", Value = newHash });
                    }

                    context.SaveChanges();
                }

                MessageBox.Show("تم تغيير كلمة المرور بنجاح. يمكنك الآن تسجيل الدخول بها.", "تم بنجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                
                // Go back to login screen
                GridForgotPassword.Visibility = Visibility.Collapsed;
                GridLoginContent.Visibility = Visibility.Visible;
                TxtPassword.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء حفظ كلمة المرور الجديدة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowRecoverySupportMessage()
        {
            MessageBox.Show("لاسترداد كلمة المرور، يرجى استخدام كلمة المرور الماستر الخاصة بالنظام أو الاتصال بالدعم الفني.", "استرداد كلمة المرور", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
