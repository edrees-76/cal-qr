using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows.Input;
using System.Windows.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CAL_QR.ViewModels.Base;
using CAL_QR.Data;
using CAL_QR.Helpers;
using CAL_QR.Models;
using CAL_QR.Services;

namespace CAL_QR.ViewModels
{
    public class MainViewModel : BaseViewModel, IDisposable
    {
        // Tab index constants
        public const int TabIndexDashboard = 0;
        public const int TabIndexDevices = 1;
        public const int TabIndexQrVerify = 2;
        public const int TabIndexOwners = 3;
        public const int TabIndexDeviceTypes = 4;
        public const int TabIndexReports = 5;
        public const int TabIndexAuditLog = 6;
        public const int TabIndexSettings = 7;
        public const int TabIndexAbout = 8;
        public const int TabIndexHelp = 9;

        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;
        private readonly ISearchService _searchService;
        private int _selectedTabIndex;
        private string _userName = "مهندس المعايرة";
        private bool _isTabHeaderVisible = true;

        private int _totalAlerts;
        private string _bannerMessage = string.Empty;
        private bool _hasAlerts;
        private bool _isBannerDismissed;

        // Search Fields
        private string _searchText = string.Empty;
        private ObservableCollection<SearchResultItem> _searchResults = new();
        private bool _isSearchResultsOpen;
        private DispatcherTimer? _searchDebounceTimer;
        private CancellationTokenSource? _searchCancellationTokenSource;

        // Inactivity Timer
        private DispatcherTimer? _inactivityTimer;
        private DateTime _lastActivityTime;
        private int _autoLockMinutes = 10;

        public event EventHandler? LockRequested;
        public event EventHandler? SearchFocusRequested;

        public MainViewModel(IDbContextFactory<CalQrDbContext> contextFactory, ISearchService searchService)
        {
            _contextFactory = contextFactory;
            _searchService = searchService;
            ToggleTabHeaderCommand = new RelayCommand(ToggleTabHeader);
            ChangeTabCommand = new RelayCommand(ChangeTab);
            DismissBannerCommand = new RelayCommand(DismissBanner);
            SelectSearchResultCommand = new RelayCommand(async (p) => await SelectSearchResultAsync(p));
            FocusSearchCommand = new RelayCommand(FocusSearch);

            CalibrationEvents.CalibrationChanged += OnCalibrationChanged;

            // Setup search debounce timer
            _searchDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(350)
            };
            _searchDebounceTimer.Tick += SearchDebounceTimer_Tick;

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

        // Search Properties
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    OnSearchTextChanged();
                }
            }
        }

        public ObservableCollection<SearchResultItem> SearchResults
        {
            get => _searchResults;
            set => SetProperty(ref _searchResults, value);
        }

        public bool IsSearchResultsOpen
        {
            get => _isSearchResultsOpen;
            set => SetProperty(ref _isSearchResultsOpen, value);
        }

        public ICommand ToggleTabHeaderCommand { get; }
        public ICommand ChangeTabCommand { get; }
        public ICommand DismissBannerCommand { get; }
        public ICommand SelectSearchResultCommand { get; }
        public ICommand FocusSearchCommand { get; }

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

        private void OnSearchTextChanged()
        {
            _searchDebounceTimer?.Stop();

            if (string.IsNullOrWhiteSpace(SearchText) || SearchText.Trim().Length < 2)
            {
                SearchResults.Clear();
                IsSearchResultsOpen = false;
                
                // Cancel pending search
                _searchCancellationTokenSource?.Cancel();
                _searchCancellationTokenSource?.Dispose();
                _searchCancellationTokenSource = null;
                return;
            }

            _searchDebounceTimer?.Start();
        }

        private void SearchDebounceTimer_Tick(object? sender, EventArgs e)
        {
            _searchDebounceTimer?.Stop();
            _ = PerformSearchAsync();
        }

        private async Task PerformSearchAsync()
        {
            var query = SearchText;
            if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
            {
                SearchResults.Clear();
                IsSearchResultsOpen = false;
                return;
            }

            // Cancel any previous search task
            _searchCancellationTokenSource?.Cancel();
            _searchCancellationTokenSource?.Dispose();
            _searchCancellationTokenSource = new CancellationTokenSource();

            var token = _searchCancellationTokenSource.Token;

            try
            {
                var results = await _searchService.SearchAsync(query, token);
                
                if (!token.IsCancellationRequested)
                {
                    SearchResults = new ObservableCollection<SearchResultItem>(results);
                    IsSearchResultsOpen = SearchResults.Count > 0;
                }
            }
            catch (OperationCanceledException)
            {
                // Silent ignore for expected cancellation
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] Search error: {ex}");
                SearchResults.Clear();
                IsSearchResultsOpen = false;
            }
        }

        private async Task SelectSearchResultAsync(object? parameter)
        {
            if (parameter is not SearchResultItem item) return;

            // Reset search state
            IsSearchResultsOpen = false;
            SearchText = string.Empty;
            SearchResults.Clear();

            switch (item.EntityType)
            {
                case SearchEntityType.Device:
                    SelectedTabIndex = TabIndexDevices;
                    SearchEvents.RaiseNavigateToDevice(item.Id);
                    break;
                case SearchEntityType.Owner:
                    SelectedTabIndex = TabIndexOwners;
                    SearchEvents.RaiseNavigateToOwner(item.Id);
                    break;
                case SearchEntityType.DeviceType:
                    SelectedTabIndex = TabIndexDeviceTypes;
                    SearchEvents.RaiseNavigateToDeviceType(item.Id);
                    break;
                case SearchEntityType.CalibrationRecord:
                    SelectedTabIndex = TabIndexDevices;
                    SearchEvents.RaiseNavigateToCalibrationRecord(item.Id);
                    break;
            }
            await Task.CompletedTask;
        }

        private void FocusSearch()
        {
            SearchFocusRequested?.Invoke(this, EventArgs.Empty);
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
            
            if (_searchDebounceTimer != null)
            {
                _searchDebounceTimer.Stop();
                _searchDebounceTimer = null;
            }

            _searchCancellationTokenSource?.Cancel();
            _searchCancellationTokenSource?.Dispose();
            _searchCancellationTokenSource = null;
        }
    }
}
