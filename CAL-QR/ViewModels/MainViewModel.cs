using System;
using System.Linq;
using System.Windows.Input;
using System.Windows.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CAL_QR.ViewModels.Base;
using CAL_QR.Data;
using CAL_QR.Helpers;
using CAL_QR.Models;

namespace CAL_QR.ViewModels
{
    public class MainViewModel : BaseViewModel, IDisposable
    {
        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;
        private int _selectedTabIndex;
        private string _userName = "مهندس المعايرة";
        private bool _isTabHeaderVisible = true;

        private int _totalAlerts;
        private string _bannerMessage = string.Empty;
        private bool _hasAlerts;
        private bool _isBannerDismissed;
        
        // Inactivity Timer
        private DispatcherTimer? _inactivityTimer;
        private DateTime _lastActivityTime;
        private int _autoLockMinutes = 10;

        public event EventHandler? LockRequested;

        public MainViewModel(IDbContextFactory<CalQrDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
            ToggleTabHeaderCommand = new RelayCommand(ToggleTabHeader);
            ChangeTabCommand = new RelayCommand(ChangeTab);
            DismissBannerCommand = new RelayCommand(DismissBanner);

            CalibrationEvents.CalibrationChanged += OnCalibrationChanged;

            LoadSettingsAndStartInactivityTimer();
        }

        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetProperty(ref _selectedTabIndex, value);
        }

        public string UserName
        {
            get => _userName;
            set => SetProperty(ref _userName, value);
        }

        public bool IsTabHeaderVisible
        {
            get => _isTabHeaderVisible;
            set => SetProperty(ref _isTabHeaderVisible, value);
        }

        public int TotalAlerts
        {
            get => _totalAlerts;
            set => SetProperty(ref _totalAlerts, value);
        }

        public string BannerMessage
        {
            get => _bannerMessage;
            set => SetProperty(ref _bannerMessage, value);
        }

        public bool HasAlerts
        {
            get => _hasAlerts;
            set => SetProperty(ref _hasAlerts, value);
        }

        public bool IsBannerDismissed
        {
            get => _isBannerDismissed;
            set
            {
                if (SetProperty(ref _isBannerDismissed, value))
                {
                    OnPropertyChanged(nameof(IsBannerVisible));
                }
            }
        }

        public bool IsBannerVisible => HasAlerts && !IsBannerDismissed;

        public int AutoLockMinutes
        {
            get => _autoLockMinutes;
            set
            {
                if (SetProperty(ref _autoLockMinutes, value))
                {
                    SaveSetting("AutoLockMinutes", value.ToString());
                }
            }
        }

        private void SaveSetting(string key, string value)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                var setting = context.AppSettings.FirstOrDefault(s => s.Key == key);
                if (setting == null)
                {
                    setting = new AppSetting { Key = key, Value = value };
                    context.AppSettings.Add(setting);
                }
                else
                {
                    setting.Value = value;
                }
                context.SaveChanges();
            }
            catch
            {
                // Suppress
            }
        }

        public ICommand ToggleTabHeaderCommand { get; }
        public ICommand ChangeTabCommand { get; }
        public ICommand DismissBannerCommand { get; }

        private void ToggleTabHeader()
        {
            IsTabHeaderVisible = !IsTabHeaderVisible;
        }

        private void DismissBanner()
        {
            IsBannerDismissed = true;
        }

        private void ChangeTab(object? parameter)
        {
            if (parameter != null && int.TryParse(parameter.ToString(), out var index))
            {
                SelectedTabIndex = index;
            }
        }

        // Inactivity Timer Logic
        private void LoadSettingsAndStartInactivityTimer()
        {
            try
            {
                using (var context = _contextFactory.CreateDbContext())
                {
                    var setting = context.AppSettings.FirstOrDefault(s => s.Key == "AutoLockMinutes");
                    if (setting != null && int.TryParse(setting.Value, out var minutes))
                    {
                        _autoLockMinutes = minutes;
                    }
                }
            }
            catch
            {
                _autoLockMinutes = 10; // Fallback
            }

            _lastActivityTime = DateTime.Now;

            _inactivityTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5) // Check every 5 seconds
            };
            _inactivityTimer.Tick += InactivityTimer_Tick;
            _inactivityTimer.Start();
        }

        private void InactivityTimer_Tick(object? sender, EventArgs e)
        {
            var inactiveTime = DateTime.Now - _lastActivityTime;
            if (inactiveTime.TotalMinutes >= _autoLockMinutes)
            {
                _inactivityTimer?.Stop();
                LockRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        public void ResetInactivity()
        {
            _lastActivityTime = DateTime.Now;
        }

        public void StopInactivityTimer()
        {
            _inactivityTimer?.Stop();
        }

        public async Task CheckAlertsAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                int alertDays = 30;
                var thresholdSetting = await context.AppSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Key == "AlertDaysThreshold");
                if (thresholdSetting != null && int.TryParse(thresholdSetting.Value, out int parsedDays))
                {
                    alertDays = parsedDays;
                }

                DateTime today = DateTime.Today;
                DateTime alertLimit = today.AddDays(alertDays);

                var allDevices = await context.Devices
                    .AsNoTracking()
                    .Include(d => d.CalibrationRecords!)
                    .Where(d => !d.IsDeleted)
                    .ToListAsync();

                int expiringCount = 0;
                int expiredCount = 0;

                foreach (var device in allDevices)
                {
                    var latestCal = device.CalibrationRecords?
                        .Where(r => !r.IsDeleted)
                        .OrderByDescending(r => r.CalibrationDate)
                        .FirstOrDefault();

                    if (latestCal != null)
                    {
                        if (latestCal.ExpiryDate < today)
                        {
                            expiredCount++;
                        }
                        else if (latestCal.ExpiryDate <= alertLimit)
                        {
                            expiringCount++;
                        }
                    }
                    else
                    {
                        expiredCount++;
                    }
                }

                TotalAlerts = expiringCount + expiredCount;
                HasAlerts = TotalAlerts > 0;
                BannerMessage = $"يوجد {expiringCount} جهاز سينتهي خلال {alertDays} يوم | {expiredCount} جهاز منتهي الصلاحية";
                OnPropertyChanged(nameof(IsBannerVisible));
            }
            catch
            {
                // Fallback
            }
        }

        private void OnCalibrationChanged(object? sender, EventArgs e)
        {
            Dispatcher.CurrentDispatcher.Invoke(async () =>
            {
                await CheckAlertsAsync();
                LoadAutoLockSetting();
            });
        }

        private void LoadAutoLockSetting()
        {
            try
            {
                using (var context = _contextFactory.CreateDbContext())
                {
                    var setting = context.AppSettings.FirstOrDefault(s => s.Key == "AutoLockMinutes");
                    if (setting != null && int.TryParse(setting.Value, out var minutes))
                    {
                        _autoLockMinutes = minutes;
                    }
                }
            }
            catch
            {
                // Suppress
            }
        }

        public void Dispose()
        {
            CalibrationEvents.CalibrationChanged -= OnCalibrationChanged;
        }
    }
}
