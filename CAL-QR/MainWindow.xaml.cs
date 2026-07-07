using System;
using System.Linq;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CAL_QR.Data;
using CAL_QR.ViewModels;
using CAL_QR.Views;
using CAL_QR.Models;

namespace CAL_QR
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;

        private bool _isLoggingOut = false;

        public MainWindow(MainViewModel viewModel, IDbContextFactory<CalQrDbContext> contextFactory)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _contextFactory = contextFactory;
            DataContext = _viewModel;

            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
            Closing += MainWindow_Closing;

            // Inactivity timer event handlers
            _viewModel.LockRequested += ViewModel_LockRequested;
            this.PreviewMouseMove += (s, e) => _viewModel.ResetInactivity();
            this.PreviewKeyDown += (s, e) => _viewModel.ResetInactivity();

            // Search event handlers
            _viewModel.SearchFocusRequested += ViewModel_SearchFocusRequested;
        }

        private void ViewModel_SearchFocusRequested(object? sender, EventArgs e)
        {
            SearchTextBox.Focus();
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_isLoggingOut)
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
                _isLoggingOut = true;
                var loginWindow = App.ServiceProvider.GetRequiredService<LoginWindow>();
                loginWindow.Show();
            }
        }

        private void ViewModel_LockRequested(object? sender, EventArgs e)
        {
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
            _viewModel.LockRequested -= ViewModel_LockRequested;
            _viewModel.SearchFocusRequested -= ViewModel_SearchFocusRequested;
            _viewModel.StopInactivityTimer();

            // If no other windows are open (user closed via X), shut down app
            if (Application.Current.Windows.Count == 0)
            {
                Application.Current.Shutdown();
            }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.CheckAlertsAsync();
            if (_viewModel.ShouldShowAlertPopup)
            {
                var popup = App.ServiceProvider.GetRequiredService<Views.Dialogs.AlertPopupDialog>();
                popup.Owner = this;
                popup.DataContext = _viewModel;
                popup.ShowDialog();
            }
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(this, "هل تريد الخروج من المنظومة؟", "تأكيد الخروج", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
            if (result == MessageBoxResult.Yes)
            {
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