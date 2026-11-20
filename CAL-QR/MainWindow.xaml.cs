using System;
using System.Linq;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CAL_QR.Data;
using CAL_QR.ViewModels;
using CAL_QR.Views;
using CAL_QR.Models;
using CAL_QR.Services;

namespace CAL_QR
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;
        private readonly ICurrentUserService _currentUserService;
        private readonly IdleLockService _idleLockService;
        private readonly CAL_QR.Repositories.IAuditLogRepository _auditLogRepository;

        private bool _isLoggingOut = false;

        public MainWindow(MainViewModel viewModel, IDbContextFactory<CalQrDbContext> contextFactory, ICurrentUserService currentUserService, IdleLockService idleLockService, CAL_QR.Repositories.IAuditLogRepository auditLogRepository)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _contextFactory = contextFactory;
            _currentUserService = currentUserService;
            _idleLockService = idleLockService;
            _auditLogRepository = auditLogRepository;
            DataContext = _viewModel;

            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
            Closing += MainWindow_Closing;

            // Inactivity timer event handlers
            _idleLockService.LockRequested += ViewModel_LockRequested;
            _idleLockService.Start();

            // Search event handlers
            _viewModel.SearchFocusRequested += ViewModel_SearchFocusRequested;
        }

        private void ViewModel_SearchFocusRequested(object? sender, EventArgs e)
        {
            SearchTextBox.Focus();
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_isLoggingOut || App.IsRestarting)
            {
                return;
            }

            var result = MessageBox.Show(this, "هل تريد الخروج من المنظومة؟", "تأكيد الخروج", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
            if (result != MessageBoxResult.Yes)
            {
                e.Cancel = true;
            }
            else
            {
                CloseSecondaryWindows();
                AuditSessionEnd(CAL_QR.Validation.SessionEndReason.WindowClosed);
                _currentUserService.ClearCurrentUser();
                _isLoggingOut = true;
                var loginWindow = App.ServiceProvider.GetRequiredService<LoginWindow>();
                loginWindow.Show();
            }
        }

        /// <summary>
        /// يسجّل انتهاء الجلسة في سجلّ العمليّات قبل مسح المستخدم الحاليّ. المستخدم يُمرَّر صراحةً
        /// لأنّ الكتابة غير متزامنة وقد تنتهي بعد ClearCurrentUser. فشل الكتابة لا يمنع الخروج،
        /// لكنّه يُسجَّل في السجلّ الدائم.
        /// </summary>
        private void AuditSessionEnd(CAL_QR.Validation.SessionEndReason reason)
        {
            var user = _currentUserService.CurrentUser;
            if (user == null) return;
            _ = WriteSessionEndAsync(user.Id, user.Username, reason);
        }

        private async System.Threading.Tasks.Task WriteSessionEndAsync(int userId, string username, CAL_QR.Validation.SessionEndReason reason)
        {
            try
            {
                await _auditLogRepository.LogAsync(CAL_QR.Validation.SessionEndAuditRules.Action, "User", userId.ToString(),
                    CAL_QR.Validation.SessionEndAuditRules.Details(username, reason), userId, username);
            }
            catch (Exception ex)
            {
                AppLog.Error($"Audit '{CAL_QR.Validation.SessionEndAuditRules.Action}' for user {username}", ex);
            }
        }

        /// <summary>
        /// يغلق كلّ نافذة أخرى عند انتهاء الجلسة (خمول · خروج يدويّ · زرّ X).
        /// بلا سؤال حفظ: مربّع حوار ينتظر إجابة من شخص غادر يُبطل القفل نفسه.
        /// التعداد لا التتبّع بـ Owner — حوارات عدّة تُفتح بلا مالك.
        /// عكسيًّا: الأحدث أوّلًا = الأعمق تعشيقًا أوّلًا، لتُفكّ حلقات ShowDialog بسلامة.
        /// </summary>
        private void CloseSecondaryWindows()
        {
            var windows = Application.Current.Windows.OfType<Window>().ToList();

            for (int i = windows.Count - 1; i >= 0; i--)
            {
                var window = windows[i];

                if (ReferenceEquals(window, this)) continue;
                if (window is LoginWindow) continue;
                if (window is Views.SplashWindow) continue;
                if (window is Views.ScreensaverWindow) continue;

                try
                {
                    if (window is Views.Dialogs.CertificateFormDialog certificateForm)
                    {
                        certificateForm.CloseWithoutPrompt();
                    }
                    else
                    {
                        window.Close();
                    }
                }
                catch (System.Exception swallowEx1)
                {
                    CAL_QR.Services.AppLog.Error("MainWindow.xaml.cs:101", swallowEx1);
                    // فشل إغلاق نافذة واحدة لا يمنع إغلاق البقيّة.
                }
            }
        }

        private void ViewModel_LockRequested(object? sender, EventArgs e)
        {
            CloseSecondaryWindows();
            AuditSessionEnd(CAL_QR.Validation.SessionEndReason.IdleLock);
            _currentUserService.ClearCurrentUser();
            var screensaverWindow = App.ServiceProvider.GetRequiredService<Views.ScreensaverWindow>();
            screensaverWindow.Dismissed += (s, args) =>
            {
                screensaverWindow.Close();
                var loginWindow = App.ServiceProvider.GetRequiredService<LoginWindow>();
                loginWindow.Show();
            };
            screensaverWindow.Show();

            _isLoggingOut = true;
            this.Close();
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            // Clean up to prevent leaks
            _idleLockService.LockRequested -= ViewModel_LockRequested;
            _viewModel.SearchFocusRequested -= ViewModel_SearchFocusRequested;
            // يفكّ اشتراكات الأحداث الساكنة والمؤقّت: بدونه يبقى VM النافذة المغلقة (بعد تسجيل الخروج) حيّاً.
            _viewModel.Dispose();
            _idleLockService.Stop();

            // If no other windows are open (user closed via X), shut down app
            if (Application.Current.Windows.Count == 0)
            {
                Application.Current.Shutdown();
            }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // أوّل نافذة تُنشأ (الشاشة الافتتاحيّة) تصير Application.MainWindow تلقائيّاً وتُغلق لاحقاً؛
            // ونوافذ الحوار تستعمله مالكاً لها، فيُضبط صراحةً على النافذة الرئيسيّة الحيّة.
            Application.Current.MainWindow = this;

            // معالج حدث (async void لا مفرّ منه): الجسم كلّه داخل try حتى لا يخرج
            // استثناء إلى الـDispatcher فينهار التطبيق عند فتح النافذة الرئيسيّة.
            try
            {
                await _viewModel.CheckAlertsAsync();
                if (_viewModel.ShouldShowAlertPopup)
                {
                    var popup = App.ServiceProvider.GetRequiredService<Views.Dialogs.AlertPopupDialog>();
                    popup.Owner = this;
                    popup.DataContext = _viewModel;
                    popup.ShowDialog();
                }

                await ShowDeviceTypeSeedNoticeAsync();
            }
            catch (Exception ex)
            {
                CAL_QR.Services.AppLog.Error("MainWindow_Loaded", ex);
            }
        }

        /// <summary>
        /// يعرض للمدير حصيلة بذر أنواع الأجهزة المخزَّنة في AppSettings (التبعية الحاجزة رقم 7).
        /// فشل العرض لا يمنع فتح النافذة، لكنّه يُسجَّل في السجلّ الدائم.
        /// </summary>
        private async System.Threading.Tasks.Task ShowDeviceTypeSeedNoticeAsync()
        {
            if (!CAL_QR.Validation.DeviceTypeSeedNoticeRules.CanSeeNotice(_currentUserService.CurrentUser))
                return;

            try
            {
                var noticeService = App.ServiceProvider.GetRequiredService<IDeviceTypeSeedNoticeService>();
                var snapshot = await noticeService.LoadAsync();

                string? message = CAL_QR.Validation.DeviceTypeSeedNoticeRules.BuildMessage(
                    snapshot.Conflicts, snapshot.SeedFailure, snapshot.RepairFailure);
                if (message == null)
                    return;

                bool canAcknowledge = CAL_QR.Validation.DeviceTypeSeedNoticeRules.HasAcknowledgeableConflicts(snapshot.Conflicts);
                var answer = MessageBox.Show(this, message, CAL_QR.Validation.DeviceTypeSeedNoticeRules.Title,
                    canAcknowledge ? MessageBoxButton.YesNo : MessageBoxButton.OK, MessageBoxImage.Warning,
                    canAcknowledge ? MessageBoxResult.No : MessageBoxResult.OK,
                    MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);

                if (canAcknowledge && answer == MessageBoxResult.Yes)
                {
                    await noticeService.AcknowledgeConflictsAsync(snapshot.Conflicts!);
                }
            }
            catch (Exception ex)
            {
                AppLog.Error("MainWindow: showing device-type seed notice failed", ex);
            }
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(this, "هل تريد الخروج من المنظومة؟", "تأكيد الخروج", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
            if (result == MessageBoxResult.Yes)
            {
                CloseSecondaryWindows();
                AuditSessionEnd(CAL_QR.Validation.SessionEndReason.LogoutButton);
                _currentUserService.ClearCurrentUser();
                _isLoggingOut = true;
                var loginWindow = App.ServiceProvider.GetRequiredService<LoginWindow>();
                loginWindow.Show();
                this.Close();
            }
        }

        private void SearchResultItem_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is SearchResultItem item)
            {
                _viewModel.SelectSearchResultCommand.Execute(item);
                e.Handled = true;
            }
        }
    }
}