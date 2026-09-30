using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CAL_QR.Data;
using CAL_QR.Helpers;
using CAL_QR.Services;
using CAL_QR.Repositories;
using CAL_QR.Validation;

namespace CAL_QR.Views
{
    public partial class LoginWindow : Window
    {
        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUserRepository _userRepository;
        private readonly IRecoveryAnswerService _recoveryAnswerService;
        private readonly IAuditLogRepository _auditLogRepository;
        private int _failedAttempts = 0;
        private DispatcherTimer? _lockoutTimer;
        private DateTime _lockoutEndsUtc;

        // قفل الحساب المحفوظ: عدّاد تنازليّ حيّ يظهر بمجرّد اختيار الحساب المقفول (لا بعد محاولة دخول).
        private DispatcherTimer? _accountLockTimer;
        private DateTime _accountLockEndsUtc;
        private bool _showingAccountLock;

        public LoginWindow(IDbContextFactory<CalQrDbContext> contextFactory, ICurrentUserService currentUserService, IUserRepository userRepository, IRecoveryAnswerService recoveryAnswerService, IAuditLogRepository auditLogRepository)
        {
            InitializeComponent();
            _contextFactory = contextFactory;
            _currentUserService = currentUserService;
            _userRepository = userRepository;
            _recoveryAnswerService = recoveryAnswerService;
            _auditLogRepository = auditLogRepository;
            Loaded += LoginWindow_Loaded;
            TxtUsername.LostKeyboardFocus += (_, _) => _ = RefreshAccountLockAsync();
            TxtUsername.SelectionChanged += (_, _) => _ = RefreshAccountLockAsync();
            Closed += (_, _) =>
            {
                _accountLockTimer?.Stop();
                _lockoutTimer?.Stop();
            };
        }

        private async void LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var users = await _userRepository.GetAllAsync();
                var activeUsernames = users
                    .Where(u => u.IsActive)
                    .Select(u => u.Username)
                    .ToList();
                TxtUsername.ItemsSource = activeUsernames;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading users for login dropdown: {ex.Message}");
            }

            TxtUsername.Focus();
            await RefreshAccountLockAsync();
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

        private void TxtUsername_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (TxtPassword.Visibility == Visibility.Visible)
                    TxtPassword.Focus();
                else
                    TxtPasswordReveal.Focus();
            }
        }

        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string username = TxtUsername.Text.Trim();
            string password = TxtPassword.Visibility == Visibility.Visible 
                ? TxtPassword.Password 
                : TxtPasswordReveal.Text;

            if (string.IsNullOrEmpty(password))
            {
                ShowError("الرجاء إدخال كلمة المرور.");
                return;
            }

            BtnLogin.IsEnabled = false;
            TxtUsername.IsEnabled = false;
            TxtPassword.IsEnabled = false;
            TxtPasswordReveal.IsEnabled = false;
            BtnRevealPassword.IsEnabled = false;

            bool loginSuccess = false;
            Models.User? targetUser = null;
            TimeSpan? persistedLock = null;

            try
            {
                if (!string.IsNullOrEmpty(username))
                {
                    var user = await _userRepository.GetByUsernameAsync(username);
                    if (user != null)
                    {
                        // القفل المحفوظ يُفحص قبل التحقّق من كلمة المرور: حتى الصحيحة تُرفض أثناءه.
                        persistedLock = LoginLockoutRules.RemainingLock(user.LockedUntil, DateTime.UtcNow);
                        if (persistedLock == null && BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                        {
                            if (user.IsActive)
                            {
                                targetUser = user;
                                loginSuccess = true;
                                await _userRepository.ResetLoginFailuresAsync(user.Id);
                            }
                            else
                            {
                                ShowError("هذا الحساب موقوف حالياً. يرجى مراجعة مدير النظام.");
                                SetInputsEnabled(true);
                                return;
                            }
                        }
                        else if (persistedLock == null)
                        {
                            var lockedUntil = await _userRepository.RegisterFailedLoginAsync(user.Id, DateTime.UtcNow);
                            persistedLock = LoginLockoutRules.RemainingLock(lockedUntil, DateTime.UtcNow);
                            if (persistedLock != null)
                            {
                                await TryAuditAsync("قفل الدخول", user,
                                    $"قُفل الدخول للحساب {user.Username} مؤقّتاً بعد {LoginLockoutRules.MaxAttempts} محاولات خاطئة.");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Error("Login", ex);
                ShowError("تعذّر إتمام تسجيل الدخول بسبب خطأ داخليّ. راجع سجلّ الأخطاء أو اتّصل بمدير النظام.");
                SetInputsEnabled(true);
                return;
            }

            if (loginSuccess && targetUser != null)
            {
                _currentUserService.SetCurrentUser(targetUser);
                await TryAuditAsync("تسجيل دخول", targetUser, $"تسجيل دخول ناجح للحساب {targetUser.Username}.");

                var mainWindow = App.ServiceProvider.GetRequiredService<MainWindow>();
                mainWindow.Show();
                this.Close();
                return;
            }

            SetInputsEnabled(true);

            if (persistedLock != null)
            {
                // القفل خاصّ بحساب واحد: تبقى الحقول مفعَّلة ليدخل مستخدم آخر بحسابه.
                StartAccountLockDisplay(DateTime.UtcNow + persistedLock.Value);
                TxtPassword.Password = string.Empty;
                TxtPasswordReveal.Text = string.Empty;
                TxtUsername.Focus();
                return;
            }

            // اسم غير موجود: لا صفّ يحمل العدّاد، فيبقى عدّاد النافذة (لا حساب لحمايته).
            _failedAttempts++;
            if (_failedAttempts >= LoginLockoutRules.MaxAttempts)
            {
                _failedAttempts = 0;
                StartLockout(LoginLockoutRules.LockDuration);
            }
            else
            {
                ShowError($"اسم المستخدم أو كلمة المرور غير صحيحة! محاولات متبقية: {LoginLockoutRules.MaxAttempts - _failedAttempts}");
                TxtPassword.Password = string.Empty;
                TxtPasswordReveal.Text = string.Empty;
                if (TxtPassword.Visibility == Visibility.Visible)
                    TxtPassword.Focus();
                else
                    TxtPasswordReveal.Focus();
            }
        }

        private async System.Threading.Tasks.Task TryAuditAsync(string action, Models.User user, string details)
        {
            try
            {
                await _auditLogRepository.LogAsync(action, "User", user.Id.ToString(), details, user.Id, user.Username);
            }
            catch (Exception ex)
            {
                AppLog.Error($"Audit '{action}' for user {user.Username}", ex);
            }
        }

        /// <summary>يفحص قفل الحساب المكتوب في خانة الاسم ويعرض العدّاد أو يُخفيه.</summary>
        private async System.Threading.Tasks.Task RefreshAccountLockAsync()
        {
            try
            {
                string name = TxtUsername.Text?.Trim() ?? string.Empty;
                Models.User? user = string.IsNullOrEmpty(name) ? null : await _userRepository.GetByUsernameAsync(name);

                // النتيجة قديمة إن تغيّر الاسم أثناء القراءة.
                if (!string.Equals(name, TxtUsername.Text?.Trim() ?? string.Empty, StringComparison.Ordinal)) return;

                var remaining = LoginLockoutRules.RemainingLock(user?.LockedUntil, DateTime.UtcNow);
                if (remaining != null)
                {
                    StartAccountLockDisplay(DateTime.UtcNow + remaining.Value);
                }
                else
                {
                    StopAccountLockDisplay();
                }
            }
            catch (Exception ex)
            {
                AppLog.Error("RefreshAccountLock", ex);
            }
        }

        private void StartAccountLockDisplay(DateTime endsUtc)
        {
            _accountLockEndsUtc = endsUtc;
            _showingAccountLock = true;
            ShowError(LockMessage(endsUtc - DateTime.UtcNow));

            _accountLockTimer?.Stop();
            _accountLockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _accountLockTimer.Tick += (_, _) =>
            {
                var left = _accountLockEndsUtc - DateTime.UtcNow;
                if (left <= TimeSpan.Zero)
                    StopAccountLockDisplay();
                else
                    ShowError(LockMessage(left));
            };
            _accountLockTimer.Start();
        }

        private void StopAccountLockDisplay()
        {
            _accountLockTimer?.Stop();
            if (_showingAccountLock)
            {
                _showingAccountLock = false;
                TxtError.Visibility = Visibility.Collapsed;
            }
        }

        private void SetInputsEnabled(bool enabled)
        {
            BtnLogin.IsEnabled = enabled;
            TxtUsername.IsEnabled = enabled;
            TxtPassword.IsEnabled = enabled;
            TxtPasswordReveal.IsEnabled = enabled;
            BtnRevealPassword.IsEnabled = enabled;
        }

        private string LockMessage(TimeSpan remaining) =>
            $"تم حظر الدخول مؤقتاً بسبب {LoginLockoutRules.MaxAttempts} محاولات خاطئة. يرجى الانتظار {LoginLockoutRules.FormatRemaining(remaining)}...";

        private void StartLockout(TimeSpan duration)
        {
            _lockoutEndsUtc = DateTime.UtcNow + duration;
            SetInputsEnabled(false);
            TxtPassword.Password = string.Empty;
            TxtPasswordReveal.Text = string.Empty;

            ShowError(LockMessage(duration));

            _lockoutTimer?.Stop();
            _lockoutTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _lockoutTimer.Tick += LockoutTimer_Tick;
            _lockoutTimer.Start();
        }

        private void LockoutTimer_Tick(object? sender, EventArgs e)
        {
            var remaining = _lockoutEndsUtc - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                _lockoutTimer?.Stop();
                _failedAttempts = 0;
                SetInputsEnabled(true);
                TxtError.Visibility = Visibility.Collapsed;
                if (TxtPassword.Visibility == Visibility.Visible)
                    TxtPassword.Focus();
                else
                    TxtPasswordReveal.Focus();
            }
            else
            {
                ShowError(LockMessage(remaining));
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
                var result = _recoveryAnswerService.Verify(inputAnswer);

                switch (result.Status)
                {
                    case RecoveryAnswerStatus.Success:
                        // Transition to password reset state
                        PanelVerifyQuestion.Visibility = Visibility.Collapsed;
                        PanelResetPassword.Visibility = Visibility.Visible;
                        TxtNewPassword.Focus();
                        break;

                    case RecoveryAnswerStatus.WrongAnswer:
                        // Show recovery master message on wrong answer, with remaining attempts in the same dialog
                        ShowRecoverySupportMessage($"الإجابة غير صحيحة. المحاولات المتبقّية: {result.RemainingAttempts}", MessageBoxImage.Warning);
                        TxtSecurityAnswerInput.Focus();
                        TxtSecurityAnswerInput.SelectAll();
                        break;

                    case RecoveryAnswerStatus.LockedOut:
                        int minutes = (int)Math.Ceiling((result.RemainingLock ?? TimeSpan.Zero).TotalMinutes);
                        if (minutes < 1) minutes = 1;
                        MessageBox.Show($"تمّ إيقاف محاولات الاسترداد مؤقّتاً بعد محاولات خاطئة متكرّرة. حاول بعد {minutes} دقيقة.", "استرداد كلمة المرور", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                        break;

                    case RecoveryAnswerStatus.NotConfigured:
                    default:
                        ShowRecoverySupportMessage();
                        break;
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

        private async void BtnSaveNewPassword_Click(object sender, RoutedEventArgs e)
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

            var lengthError = UserPasswordRules.Validate(newPassword);
            if (lengthError != null)
            {
                MessageBox.Show(lengthError, "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                // حساب المدير النشط: "admin" إن كان مديراً نشطاً، وإلا أوّل مدير نشط (بالمعرّف)
                var adminUser = await _userRepository.GetByUsernameAsync("admin");
                if (adminUser == null || adminUser.Role != Models.UserRole.Admin || !adminUser.IsActive)
                {
                    adminUser = await _userRepository.GetFirstActiveAdminAsync();
                }
                if (adminUser == null)
                {
                    MessageBox.Show("تعذّر العثور على حساب مدير نشط لإعادة الضبط. يرجى مراجعة مدير آخر من تبويب المستخدمين.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                adminUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
                await _userRepository.UpdateAsync(adminUser);
                await _userRepository.ResetLoginFailuresAsync(adminUser.Id);

                try
                {
                    await _auditLogRepository.LogAsync(
                        "إعادة ضبط كلمة المرور عبر سؤال الاسترداد",
                        "User",
                        adminUser.Id.ToString(),
                        $"إعادة ضبط كلمة المرور عبر سؤال الاسترداد للحساب {adminUser.Username}.",
                        adminUser.Id,
                        adminUser.Username);
                }
                catch (Exception auditEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Audit log failed after password recovery: {auditEx.Message}");
                }

                MessageBox.Show($"تم تغيير كلمة مرور حساب {adminUser.Username} بنجاح. يمكنك الآن تسجيل الدخول بها.", "تم بنجاح", MessageBoxButton.OK, MessageBoxImage.Information);

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

        private void ShowRecoverySupportMessage(string? prefix = null, MessageBoxImage icon = MessageBoxImage.Information)
        {
            const string support = "لاسترداد كلمة المرور، تأكّد من إجابة السؤال السري، أو اطلب من مدير آخر إعادة ضبط كلمتك من تبويب المستخدمين.";
            string text = string.IsNullOrEmpty(prefix) ? support : prefix + "\n\n" + support;
            MessageBox.Show(text, "استرداد كلمة المرور", MessageBoxButton.OK, icon);
        }
    }
}
