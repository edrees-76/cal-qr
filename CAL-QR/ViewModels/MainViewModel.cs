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
        public const int TabIndexUsers = 6;
        public const int TabIndexSettings = 7;
        public const int TabIndexHelp = 8;
        public const int TabIndexAbout = 9;

        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;
        private readonly ISearchService _searchService;
        private readonly ICurrentUserService _currentUserService;
        private int _selectedTabIndex;
        private string _userName = "مهندس المعايرة";
        private bool _isTabHeaderVisible = true;

        private int _totalAlerts;
        private bool _hasAlerts;
        private bool _shouldShowAlertPopup;
        private readonly System.Collections.Generic.List<(int DeviceId, int CalibrationRecordId)> _currentExpiredDeviceCalIds = new();

        // Search Fields
        private string _searchText = string.Empty;
        private ObservableCollection<SearchResultItem> _searchResults = new();
        private bool _isSearchResultsOpen;
        private DispatcherTimer? _searchDebounceTimer;
        private CancellationTokenSource? _searchCancellationTokenSource;

        public event EventHandler? SearchFocusRequested;

        /// <summary>
        /// قاعدة البيانات النشطة كما يراها EF فعلاً — تُشتقّ من نصّ الاتّصال الحيّ
        /// لا من إعادة قراءة db_path.txt، فلا يمكن أن تعرض غير ما يُكتب فيه.
        /// العرض = المجلّد الأخير + اسم الملفّ، لأنّ القاعدة الافتراضيّة والقاعدة
        /// الحيّة تحملان الاسم نفسه (cal-qr-simulation.db) ويميّزهما المجلّد وحده.
        /// </summary>
        public string ActiveDatabaseLabel { get; } = "—";

        /// <summary>المسار الكامل — يظهر في ToolTip الهيدر.</summary>
        public string ActiveDatabaseFullPath { get; } = string.Empty;

        public MainViewModel(IDbContextFactory<CalQrDbContext> contextFactory, ISearchService searchService, ICurrentUserService currentUserService)
        {
            _contextFactory = contextFactory;
            _searchService = searchService;
            _currentUserService = currentUserService;

            // قراءة نصّ الاتّصال فقط: لا استعلام ولا فتح اتّصال ⇒ آمنة في المُنشئ.
            try
            {
                using var dbCtx = _contextFactory.CreateDbContext();
                var csBuilder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(
                    dbCtx.Database.GetDbConnection().ConnectionString);
                ActiveDatabaseFullPath = System.IO.Path.GetFullPath(csBuilder.DataSource);

                string fileName = System.IO.Path.GetFileName(ActiveDatabaseFullPath);
                string parentDir = System.IO.Path.GetFileName(
                    System.IO.Path.GetDirectoryName(ActiveDatabaseFullPath) ?? string.Empty);
                ActiveDatabaseLabel = string.IsNullOrEmpty(parentDir)
                    ? fileName
                    : System.IO.Path.Combine(parentDir, fileName);
            }
            catch
            {
                // فشل الاشتقاق لا يمنع إقلاع الواجهة؛ تبقى القيمة الافتراضيّة "—".
            }

            ToggleTabHeaderCommand = new RelayCommand(ToggleTabHeader);
            ChangeTabCommand = new RelayCommand(ChangeTab);
            SelectSearchResultCommand = new RelayCommand(async (p) => await SelectSearchResultAsync(p));
            FocusSearchCommand = new RelayCommand(FocusSearch);

            CalibrationEvents.CalibrationChanged += OnCalibrationChanged;
            SearchEvents.NavigateToDevice += OnNavigateToDevice;
            SearchEvents.NavigateToCalibrationRecord += OnNavigateToCalibrationRecord;
            
            AcknowledgeAllExpiredDevicesCommand = new RelayCommand(async () => await AcknowledgeAllExpiredDevicesAsync());

            // Setup search debounce timer
            _searchDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(350)
            };
            _searchDebounceTimer.Tick += SearchDebounceTimer_Tick;

            _userName = _currentUserService.CurrentUser?.FullName ?? "مهندس المعايرة";

            SelectFirstAllowedTab();
        }

        public bool IsRecordsVisible => _currentUserService.CurrentUser?.HasPermission(SystemPermissions.Records) ?? false;
        public bool IsVerificationVisible => _currentUserService.CurrentUser?.HasPermission(SystemPermissions.Verification) ?? false;
        public bool IsOwnersVisible => _currentUserService.CurrentUser?.HasPermission(SystemPermissions.Owners) ?? false;
        public bool IsDeviceTypesVisible => _currentUserService.CurrentUser?.HasPermission(SystemPermissions.DeviceTypes) ?? false;
        public bool IsReportsVisible => _currentUserService.CurrentUser?.HasPermission(SystemPermissions.Reports) ?? false;
        public bool IsSettingsVisible => _currentUserService.CurrentUser?.HasPermission(SystemPermissions.Settings) ?? false;
        public bool IsUserManagementVisible => _currentUserService.CurrentUser?.HasPermission(SystemPermissions.UserManagement) ?? false;

        private void SelectFirstAllowedTab()
        {
            if (IsTabAllowed(TabIndexDashboard)) SelectedTabIndex = TabIndexDashboard;
            else if (IsTabAllowed(TabIndexDevices)) SelectedTabIndex = TabIndexDevices;
            else if (IsTabAllowed(TabIndexQrVerify)) SelectedTabIndex = TabIndexQrVerify;
            else if (IsTabAllowed(TabIndexOwners)) SelectedTabIndex = TabIndexOwners;
            else if (IsTabAllowed(TabIndexDeviceTypes)) SelectedTabIndex = TabIndexDeviceTypes;
            else if (IsTabAllowed(TabIndexReports)) SelectedTabIndex = TabIndexReports;
            else if (IsTabAllowed(TabIndexUsers)) SelectedTabIndex = TabIndexUsers;
            else if (IsTabAllowed(TabIndexSettings)) SelectedTabIndex = TabIndexSettings;
            else if (IsTabAllowed(TabIndexHelp)) SelectedTabIndex = TabIndexHelp;
            else if (IsTabAllowed(TabIndexAbout)) SelectedTabIndex = TabIndexAbout;
        }

        private bool IsTabAllowed(int tabIndex)
        {
            var user = _currentUserService.CurrentUser;
            if (user == null) return false;

            switch (tabIndex)
            {
                case TabIndexDashboard:
                case TabIndexAbout:
                case TabIndexHelp:
                    return true;
                case TabIndexDevices:
                    return user.HasPermission(SystemPermissions.Records);
                case TabIndexQrVerify:
                    return user.HasPermission(SystemPermissions.Verification);
                case TabIndexOwners:
                    return user.HasPermission(SystemPermissions.Owners);
                case TabIndexDeviceTypes:
                    return user.HasPermission(SystemPermissions.DeviceTypes);
                case TabIndexReports:
                    return user.HasPermission(SystemPermissions.Reports);
                case TabIndexSettings:
                    return user.HasPermission(SystemPermissions.Settings);
                case TabIndexUsers:
                    return user.HasPermission(SystemPermissions.UserManagement);
                default:
                    return false;
            }
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

        public bool HasAlerts
        {
            get => _hasAlerts;
            set => SetProperty(ref _hasAlerts, value);
        }

        public bool ShouldShowAlertPopup
        {
            get => _shouldShowAlertPopup;
            set => SetProperty(ref _shouldShowAlertPopup, value);
        }

        public ICommand AcknowledgeAllExpiredDevicesCommand { get; }

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
        public ICommand SelectSearchResultCommand { get; }
        public ICommand FocusSearchCommand { get; }

        private void ToggleTabHeader()
        {
            IsTabHeaderVisible = !IsTabHeaderVisible;
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

                var acknowledged = await context.AcknowledgedExpiredDevices
                    .AsNoTracking()
                    .ToListAsync();

                int expiringCount = 0;
                int expiredCount = 0;
                int unacknowledgedExpiredCount = 0;

                _currentExpiredDeviceCalIds.Clear();

                foreach (var device in allDevices)
                {
                    var latestCal = device.CalibrationRecords?
                        .Where(r => !r.IsDeleted)
                        .OrderByDescending(r => r.CalibrationDate)
                        .FirstOrDefault();

                    int latestCalId = latestCal?.Id ?? 0;

                    if (latestCal != null)
                    {
                        if (latestCal.ExpiryDate < today)
                        {
                            expiredCount++;
                            _currentExpiredDeviceCalIds.Add((device.Id, latestCalId));

                            bool isAck = acknowledged.Any(a => a.DeviceId == device.Id && a.CalibrationRecordId == latestCalId);
                            if (!isAck)
                            {
                                unacknowledgedExpiredCount++;
                            }
                        }
                        else if (latestCal.ExpiryDate <= alertLimit)
                        {
                            expiringCount++;
                        }
                    }
                    else
                    {
                        expiredCount++;
                        _currentExpiredDeviceCalIds.Add((device.Id, 0));

                        bool isAck = acknowledged.Any(a => a.DeviceId == device.Id && a.CalibrationRecordId == 0);
                        if (!isAck)
                        {
                            unacknowledgedExpiredCount++;
                        }
                    }
                }

                TotalAlerts = expiringCount + expiredCount;
                HasAlerts = TotalAlerts > 0;
                ShouldShowAlertPopup = unacknowledgedExpiredCount > 0;
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
            });
        }

        public async Task AcknowledgeAllExpiredDevicesAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var acknowledged = await context.AcknowledgedExpiredDevices.ToListAsync();

                bool changed = false;
                foreach (var item in _currentExpiredDeviceCalIds)
                {
                    bool exists = acknowledged.Any(a => a.DeviceId == item.DeviceId && a.CalibrationRecordId == item.CalibrationRecordId);
                    if (!exists)
                    {
                        context.AcknowledgedExpiredDevices.Add(new AcknowledgedExpiredDevice
                        {
                            DeviceId = item.DeviceId,
                            CalibrationRecordId = item.CalibrationRecordId,
                            AcknowledgedDate = DateTime.Now
                        });
                        changed = true;
                    }
                }

                if (changed)
                {
                    await context.SaveChangesAsync();
                }

                ShouldShowAlertPopup = false;
            }
            catch
            {
                // Suppress
            }
        }

        private void OnNavigateToDevice(int deviceId)
        {
            SelectedTabIndex = TabIndexDevices;
        }

        private void OnNavigateToCalibrationRecord(int recordId)
        {
            SelectedTabIndex = TabIndexDevices;
        }

        public void Dispose()
        {
            CalibrationEvents.CalibrationChanged -= OnCalibrationChanged;
            SearchEvents.NavigateToDevice -= OnNavigateToDevice;
            SearchEvents.NavigateToCalibrationRecord -= OnNavigateToCalibrationRecord;
            
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
