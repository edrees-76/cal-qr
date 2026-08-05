using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using CAL_QR.ViewModels.Base;
using CAL_QR.Models;
using CAL_QR.Repositories;
using CAL_QR.Data;
using CAL_QR.Services;
using CAL_QR.Helpers;
using CAL_QR.Validation;

namespace CAL_QR.ViewModels
{
    public class DevicesViewModel : BaseViewModel, IDisposable
    {
        private readonly IDeviceRepository _deviceRepository;
        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;
        private readonly IOwnerRepository _ownerRepository;
        private readonly IDeviceTypeRepository _deviceTypeRepository;
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly IQrService _qrService;
        private readonly IPrintService _printService;
        private readonly IPaperTemplateRepository _templateRepository;
        private readonly Func<Views.Dialogs.DeviceDetailDialog> _deviceDetailDialogFactory;
        private readonly Func<Views.Dialogs.CalibrationFormDialog> _calibrationFormDialogFactory;
        private readonly Func<Views.Dialogs.CertificateFormDialog> _certificateFormDialogFactory;
        private readonly ICertificateRepository _certificateRepository;
        private readonly ICurrentUserService _currentUserService;

        private ObservableCollection<DeviceDisplayItem> _devices = new();
        private ObservableCollection<Owner> _ownersFilter = new();
        private ObservableCollection<DeviceType> _deviceTypesFilter = new();

        // Search properties
        private string _searchText = string.Empty;
        private bool _isAdvancedSearchVisible;

        // Advanced Search Filters
        private Owner? _selectedOwnerFilter;
        private DeviceType? _selectedDeviceTypeFilter;
        private string _filterModel = string.Empty;
        private string _filterSerialNumber = string.Empty;
        private DateTime? _filterStartDate;
        private DateTime? _filterEndDate;
        private string _filterResult = "الكل"; // الكل | ناجح | راسب | مشروط | غير معاير
        private string _filterStatus = "الكل"; // الكل | سارية | قريبة | منتهية

        // Year scope (independent of the advanced search panel — always visible, applied on change)
        // The bound collection holds text, matching the "النتيجة" and "الحالة" filters on this screen:
        // a null ComboBox item means "no selection" and leaves the closed box blank, so the
        // catch-all option is a real string item. The numeric value is kept separately for the query.
        private const string AllYearsLabel = "كل السنوات";
        private ObservableCollection<string> _availableYears = new();
        private string _selectedYearText = AllYearsLabel;
        private int? _selectedYear;
        private int? _defaultYear;
        // Raised only once the default has been computed from a non-empty year list, so a system
        // that starts with no records still resolves its default after data appears.
        private bool _defaultYearResolved = false;

        // View configuration
        private bool _isTableView = true; // true = Table, false = Cards
        private int _pageSize = 20; // 10 | 20 | 50
        private int _currentPage = 1;
        private int _totalPages = 1;
        private int _totalCount = 0;

        public DevicesViewModel(
            IDeviceRepository deviceRepository,
            IDbContextFactory<CalQrDbContext> contextFactory,
            IOwnerRepository ownerRepository,
            IDeviceTypeRepository deviceTypeRepository,
            IAuditLogRepository auditLogRepository,
            IQrService qrService,
            IPrintService printService,
            IPaperTemplateRepository templateRepository,
            Func<Views.Dialogs.DeviceDetailDialog> deviceDetailDialogFactory,
            Func<Views.Dialogs.CalibrationFormDialog> calibrationFormDialogFactory,
            Func<Views.Dialogs.CertificateFormDialog> certificateFormDialogFactory,
            ICertificateRepository certificateRepository,
            ICurrentUserService currentUserService)
        {
            _deviceRepository = deviceRepository;
            _contextFactory = contextFactory;
            _ownerRepository = ownerRepository;
            _deviceTypeRepository = deviceTypeRepository;
            _auditLogRepository = auditLogRepository;
            _qrService = qrService;
            _printService = printService;
            _templateRepository = templateRepository;
            _deviceDetailDialogFactory = deviceDetailDialogFactory;
            _calibrationFormDialogFactory = calibrationFormDialogFactory;
            _certificateFormDialogFactory = certificateFormDialogFactory;
            _certificateRepository = certificateRepository;
            _currentUserService = currentUserService;

            LoadDataCommand = new RelayCommand(async () => await LoadDataAsync());
            SearchCommand = new RelayCommand(async () => { CurrentPage = 1; await LoadDataAsync(); });
            ClearFiltersCommand = new RelayCommand(async () => await ClearFiltersAsync());
            ToggleAdvancedSearchCommand = new RelayCommand(() => IsAdvancedSearchVisible = !IsAdvancedSearchVisible);
            ToggleViewCommand = new RelayCommand(() => IsTableView = !IsTableView);
            
            // Pagination Commands
            NextPageCommand = new RelayCommand(async () => { CurrentPage++; await LoadDataAsync(); }, () => CurrentPage < TotalPages);
            PrevPageCommand = new RelayCommand(async () => { CurrentPage--; await LoadDataAsync(); }, () => CurrentPage > 1);

            // Dialog triggers
            AddDeviceCommand = new RelayCommand(OpenAddDeviceDialog, () => CanEdit);
            ViewDetailsCommand = new RelayCommand(OpenDetailsDialog);
            EditDeviceCommand = new RelayCommand(OpenEditDialog, (p) => CanEdit);
            EditCertificateCommand = new RelayCommand(OpenEditCertificateDialog);
            ToggleSignedCopyCommand = new RelayCommand(async (p) => await ToggleSignedCopy(p));
            PrintDeviceCommand = new RelayCommand(PrintSpecificCertificate);
            PrintBatchCommand = new RelayCommand(async () => await PrintBatchAsync());
            PrintSpecificCertificateCommand = new RelayCommand(PrintSpecificCertificate);

            // Search Events Subscription
            SearchEvents.NavigateToDevice += OnNavigateToDevice;
            SearchEvents.NavigateToCalibrationRecord += OnNavigateToCalibrationRecord;
        }

        public bool CanEdit => _currentUserService.CurrentUser != null && 
                               (_currentUserService.CurrentUser.Role == UserRole.Admin || _currentUserService.CurrentUser.IsEditor);

        public ICommand PrintDeviceCommand { get; }
        public ICommand PrintBatchCommand { get; }
        public ICommand PrintSpecificCertificateCommand { get; }

        private bool _isAllSelected;
        public bool IsAllSelected
        {
            get => _isAllSelected;
            set
            {
                if (SetProperty(ref _isAllSelected, value))
                {
                    foreach (var device in Devices)
                    {
                        device.IsSelected = value;
                    }
                }
            }
        }

        #region Properties
        public ObservableCollection<DeviceDisplayItem> Devices
        {
            get => _devices;
            set => SetProperty(ref _devices, value);
        }

        public ObservableCollection<Owner> OwnersFilter
        {
            get => _ownersFilter;
            set => SetProperty(ref _ownersFilter, value);
        }

        public ObservableCollection<DeviceType> DeviceTypesFilter
        {
            get => _deviceTypesFilter;
            set => SetProperty(ref _deviceTypesFilter, value);
        }

        public string SearchText
        {
            get => _searchText;
            set => SetProperty(ref _searchText, value);
        }

        public bool IsAdvancedSearchVisible
        {
            get => _isAdvancedSearchVisible;
            set => SetProperty(ref _isAdvancedSearchVisible, value);
        }

        public Owner? SelectedOwnerFilter
        {
            get => _selectedOwnerFilter;
            set => SetProperty(ref _selectedOwnerFilter, value);
        }

        public DeviceType? SelectedDeviceTypeFilter
        {
            get => _selectedDeviceTypeFilter;
            set => SetProperty(ref _selectedDeviceTypeFilter, value);
        }

        public string FilterModel
        {
            get => _filterModel;
            set => SetProperty(ref _filterModel, value);
        }

        public string FilterSerialNumber
        {
            get => _filterSerialNumber;
            set => SetProperty(ref _filterSerialNumber, value);
        }

        public DateTime? FilterStartDate
        {
            get => _filterStartDate;
            set => SetProperty(ref _filterStartDate, value);
        }

        public DateTime? FilterEndDate
        {
            get => _filterEndDate;
            set => SetProperty(ref _filterEndDate, value);
        }

        public string FilterResult
        {
            get => _filterResult;
            set => SetProperty(ref _filterResult, value);
        }

        public string FilterStatus
        {
            get => _filterStatus;
            set => SetProperty(ref _filterStatus, value);
        }

        public ObservableCollection<string> AvailableYears => _availableYears;

        public string SelectedYearText
        {
            get => _selectedYearText;
            set
            {
                if (SetProperty(ref _selectedYearText, value))
                {
                    _selectedYear = ParseYearText(value);
                    CurrentPage = 1;
                    _ = LoadDataAsync();
                }
            }
        }

        public bool IsTableView
        {
            get => _isTableView;
            set => SetProperty(ref _isTableView, value);
        }

        public int PageSize
        {
            get => _pageSize;
            set
            {
                if (SetProperty(ref _pageSize, value))
                {
                    CurrentPage = 1;
                    _ = LoadDataAsync();
                }
            }
        }

        public int CurrentPage
        {
            get => _currentPage;
            set => SetProperty(ref _currentPage, value);
        }

        public int TotalPages
        {
            get => _totalPages;
            set => SetProperty(ref _totalPages, value);
        }

        public int TotalCount
        {
            get => _totalCount;
            set => SetProperty(ref _totalCount, value);
        }
        #endregion

        #region Commands
        public ICommand LoadDataCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand ClearFiltersCommand { get; }
        public ICommand ToggleAdvancedSearchCommand { get; }
        public ICommand ToggleViewCommand { get; }
        public ICommand NextPageCommand { get; }
        public ICommand PrevPageCommand { get; }
        public ICommand AddDeviceCommand { get; }
        public ICommand ViewDetailsCommand { get; }
        public ICommand EditDeviceCommand { get; }
        public ICommand EditCertificateCommand { get; }
        public ICommand ToggleSignedCopyCommand { get; }
        #endregion

        public async Task LoadDataAsync()
        {
            try
            {
                if (OwnersFilter.Count == 0)
                {
                    var owners = await _ownerRepository.GetAllAsync();
                    OwnersFilter = new ObservableCollection<Owner>(owners);
                }

                if (DeviceTypesFilter.Count == 0)
                {
                    var types = await _deviceTypeRepository.GetAllAsync();
                    DeviceTypesFilter = new ObservableCollection<DeviceType>(types);
                }

                await LoadAvailableYearsAsync(computeDefault: !_defaultYearResolved);

                using (var context = _contextFactory.CreateDbContext())
                {
                    var thresholdSetting = context.AppSettings.AsNoTracking().FirstOrDefault(s => s.Key == "AlertDaysThreshold");
                    int thresholdDays = int.TryParse(thresholdSetting?.Value, out var val) ? val : 30;

                    var today = DateTime.Today;
                    var alertLimit = today.AddDays(thresholdDays);

                    IQueryable<CalibrationRecord> query = context.CalibrationRecords
                        .AsNoTracking()
                        .Include(r => r.Device)
                            .ThenInclude(d => d!.Owner)
                        .Include(r => r.Device)
                            .ThenInclude(d => d!.DeviceType)
                        .Where(r => !r.IsDeleted && !r.Device!.IsDeleted);

                    if (!string.IsNullOrWhiteSpace(SearchText))
                    {
                        var simple = SearchText.ToLower();
                        query = query.Where(r =>
                            r.Device!.Model.ToLower().Contains(simple) ||
                            r.Device!.SerialNumber.ToLower().Contains(simple) ||
                            r.Device!.Owner!.Name.ToLower().Contains(simple) ||
                            // الرقم يُطابَق على الشهادة لا على العمود المهجور، وإلا عرض
                            // العمودُ رقماً لا يجده البحث عنه.
                            context.Certificates.Any(c =>
                                c.CalibrationRecordId == r.Id &&
                                !c.IsDeleted &&
                                c.CertificateNumber.ToLower().Contains(simple))
                        );
                    }

                    if (_selectedYear.HasValue)
                    {
                        query = query.Where(r => r.CalibrationDate.Year == _selectedYear.Value);
                    }

                    if (IsAdvancedSearchVisible)
                    {
                        if (SelectedOwnerFilter != null)
                        {
                            query = query.Where(r => r.Device!.OwnerId == SelectedOwnerFilter.Id);
                        }

                        if (SelectedDeviceTypeFilter != null)
                        {
                            query = query.Where(r => r.Device!.DeviceTypeId == SelectedDeviceTypeFilter.Id);
                        }

                        if (!string.IsNullOrWhiteSpace(FilterModel))
                        {
                            var modelFilter = FilterModel.ToLower();
                            query = query.Where(r => r.Device!.Model.ToLower().Contains(modelFilter));
                        }

                        if (!string.IsNullOrWhiteSpace(FilterSerialNumber))
                        {
                            var snFilter = FilterSerialNumber.ToLower();
                            query = query.Where(r => r.Device!.SerialNumber.ToLower().Contains(snFilter));
                        }
                    }

                    if (IsAdvancedSearchVisible)
                    {
                        if (FilterResult != "الكل")
                        {
                            string mappedResult = FilterResult == "ناجح" ? "Passed" : FilterResult == "راسب" ? "Failed" : FilterResult == "مشروط" ? "Conditional" : "غير معاير";
                            query = query.Where(r => r.Result == mappedResult);
                        }

                        if (FilterStatus != "الكل")
                        {
                            if (FilterStatus == "منتهية")
                            {
                                query = query.Where(r => r.ExpiryDate < today);
                            }
                            else if (FilterStatus == "قريبة الانتهاء")
                            {
                                query = query.Where(r => r.ExpiryDate >= today && r.ExpiryDate <= alertLimit);
                            }
                            else if (FilterStatus == "سارية")
                            {
                                query = query.Where(r => r.ExpiryDate > alertLimit);
                            }
                        }

                        if (FilterStartDate.HasValue)
                        {
                            query = query.Where(r => r.CalibrationDate >= FilterStartDate.Value);
                        }

                        if (FilterEndDate.HasValue)
                        {
                            query = query.Where(r => r.CalibrationDate <= FilterEndDate.Value);
                        }
                    }

                    TotalCount = await query.CountAsync();
                    TotalPages = (int)Math.Ceiling((double)TotalCount / PageSize);
                    if (TotalPages == 0) TotalPages = 1;
                    if (CurrentPage > TotalPages) CurrentPage = TotalPages;
                    if (CurrentPage < 1) CurrentPage = 1;

                    // الضمّ بعد Skip/Take لا قبلهما: الترقيم يقع على السجلات وحدها،
                    // فلا يتأثّر عدد الصفحات بوجود شهادة أو غيابها.
                    //
                    // استعلام فرعيّ مترابط يترجمه EF Core إلى LEFT JOIN. لا Include هنا
                    // لسببين: لا تنقّل عكسيّ على المخطط (مجمَّد بقرار)، ولا يقبل Include
                    // مرشّحاً على تنقّل مرجعيّ — فكان !IsDeleted سيسقط.
                    var rawList = await query
                        .OrderByDescending(r => r.CalibrationDate)
                        .Skip((CurrentPage - 1) * PageSize)
                        .Take(PageSize)
                        .Select(r => new
                        {
                            Record = r,
                            IssuedNumber = context.Certificates
                                .Where(c => c.CalibrationRecordId == r.Id && !c.IsDeleted)
                                .Select(c => c.CertificateNumber)
                                .FirstOrDefault(),
                            IssuedCertificateId = context.Certificates
                                .Where(c => c.CalibrationRecordId == r.Id && !c.IsDeleted)
                                .Select(c => c.Id)
                                .FirstOrDefault(),
                            IsSignedCopyAttached = context.Certificates
                                .Where(c => c.CalibrationRecordId == r.Id && !c.IsDeleted)
                                .Select(c => c.IsSignedCopyAttached)
                                .FirstOrDefault()
                        })
                        .ToListAsync();

                    var displayItems = rawList.Select(x =>
                    {
                        var r = x.Record;
                        var d = r.Device!;
                        string certNumber = CertificateNumberDisplayRules.Display(x.IssuedNumber);
                        DateTime? calDate = r.CalibrationDate;
                        DateTime? expDate = r.ExpiryDate;
                        string result = r.Result;

                        string status = "غير معاير";
                        string statusBrush = "#9E9E9E";

                        if (expDate.HasValue)
                        {
                            if (expDate.Value < today)
                            {
                                status = "منتهية";
                                statusBrush = "#C62828";
                            }
                            else if (expDate.Value <= alertLimit)
                            {
                                status = "قريبة الانتهاء";
                                statusBrush = "#F9A825";
                            }
                            else
                            {
                                status = "سارية";
                                statusBrush = "#2E7D32";
                            }
                        }

                        return new DeviceDisplayItem
                        {
                            Device = d,
                            Id = d.Id,
                            DeviceId = d.Id,
                            CalibrationRecordId = r.Id,
                            Model = d.Model,
                            SerialNumber = d.SerialNumber,
                            OwnerName = d.Owner?.Name ?? "غير محدد",
                            DeviceTypeName = d.DeviceType?.Name ?? "غير محدد",
                            CertificateNumber = certNumber,
                            CertificateId = x.IssuedCertificateId,
                            IsSignedCopyAttached = x.IsSignedCopyAttached,
                            CalibrationDate = calDate,
                            ExpiryDate = expDate,
                            Result = result,
                            Status = status,
                            StatusColor = statusBrush,
                            LatestCalibrationRecordId = r.Id
                        };
                    }).ToList();

                    int startIndex = (CurrentPage - 1) * PageSize;
                    for (int i = 0; i < displayItems.Count; i++)
                    {
                        displayItems[i].SequenceNumber = startIndex + i + 1;
                    }

                    Devices = new ObservableCollection<DeviceDisplayItem>(displayItems);

                    _isAllSelected = false;
                    OnPropertyChanged(nameof(IsAllSelected));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحميل الشهادات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadAvailableYearsAsync(bool computeDefault)
        {
            using (var context = _contextFactory.CreateDbContext())
            {
                var years = await context.CalibrationRecords
                    .AsNoTracking()
                    .Where(r => !r.IsDeleted && !r.Device!.IsDeleted)
                    .Select(r => r.CalibrationDate.Year)
                    .Distinct()
                    .OrderByDescending(y => y)
                    .ToListAsync();

                if (computeDefault)
                {
                    _defaultYear = ComputeDefaultYear(years);
                    // Only a non-empty list settles the default. On an empty database the answer
                    // is not yet knowable, so leave the flag down and retry on the next load.
                    if (years.Count > 0)
                    {
                        _defaultYearResolved = true;
                    }
                }

                // Guard against an endless cycle: rebuilding the bound collection resets the
                // ComboBox selection to null, which fires the SelectedYearText setter and reloads.
                // Rebuild only when the set of years actually changed.
                var current = _availableYears.Skip(1).ToList();
                if (_availableYears.Count > 0 && current.SequenceEqual(years.Select(y => y.ToString())))
                {
                    return;
                }

                int? preserved = computeDefault ? _defaultYear : _selectedYear;

                _availableYears.Clear();
                _availableYears.Add(AllYearsLabel);
                foreach (var year in years)
                {
                    _availableYears.Add(year.ToString());
                }

                if (!preserved.HasValue || years.Contains(preserved.Value))
                {
                    // Assign the backing field directly: the property setter would trigger a second load cycle
                    _selectedYear = preserved;
                }
                else
                {
                    // The selected year no longer exists: recompute from the new list and keep
                    // _defaultYear consistent with reality (ClearFiltersAsync relies on it).
                    _defaultYear = ComputeDefaultYear(years);
                    _selectedYear = _defaultYear;
                }
                _selectedYearText = YearToText(_selectedYear);
                OnPropertyChanged(nameof(SelectedYearText));
            }
        }

        private static int? ComputeDefaultYear(System.Collections.Generic.List<int> years)
        {
            int currentYear = DateTime.Today.Year;
            return years.Contains(currentYear)
                ? currentYear
                : (years.Count > 0 ? years[0] : (int?)null);
        }

        // The bound value is text; the query needs a number. These two keep the pair in step
        // without smuggling a sentinel number (0 / -1) into a range that means calendar years.
        private static int? ParseYearText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text) || text == AllYearsLabel)
            {
                return null;
            }
            return int.TryParse(text, out var year) ? year : (int?)null;
        }

        private static string YearToText(int? year) =>
            year.HasValue ? year.Value.ToString() : AllYearsLabel;

        private async Task ClearFiltersAsync()
        {
            SearchText = string.Empty;
            SelectedOwnerFilter = null;
            SelectedDeviceTypeFilter = null;
            FilterModel = string.Empty;
            FilterSerialNumber = string.Empty;
            FilterStartDate = null;
            FilterEndDate = null;
            FilterResult = "الكل";
            FilterStatus = "الكل";
            // Backing field directly: LoadDataAsync is already called at the end of this method
            _selectedYear = _defaultYear;
            _selectedYearText = YearToText(_selectedYear);
            OnPropertyChanged(nameof(SelectedYearText));
            CurrentPage = 1;
            await LoadDataAsync();
        }

        private void OpenAddDeviceDialog()
        {
            if (!CanEdit) return;
            var dialog = _calibrationFormDialogFactory();
            var vm = (CalibrationFormViewModel)dialog.DataContext;
            vm.Saved += async (s, e) => await LoadDataAsync();
            dialog.ShowDialog();
        }

        private void OpenDetailsDialog(object? parameter)
        {
            if (parameter is not DeviceDisplayItem item) return;

            var dialog = _deviceDetailDialogFactory();
            var vm = (DeviceDetailViewModel)dialog.DataContext;
            int? preferredRecordId = item.CalibrationRecordId > 0 ? item.CalibrationRecordId : (int?)null;
            vm.LoadDeviceDetails(item.DeviceId, preferredRecordId);
            vm.Saved += async (s, e) => await LoadDataAsync();
            dialog.ShowDialog();
        }

        private void OpenEditDialog(object? parameter)
        {
            if (!CanEdit) return;
            if (parameter is not DeviceDisplayItem item) return;

            var dialog = _calibrationFormDialogFactory();
            var vm = (CalibrationFormViewModel)dialog.DataContext;
            
            if (item.CalibrationRecordId > 0)
            {
                vm.LoadForEdit(item.CalibrationRecordId);
            }
            else
            {
                vm.LoadForDeviceOnly(item.DeviceId);
            }

            vm.Saved += async (s, e) => await LoadDataAsync();
            dialog.ShowDialog();
        }

        private void OpenEditCertificateDialog(object? parameter)
        {
            if (parameter is not DeviceDisplayItem item) return;
            if (item.CertificateId <= 0) return;

            try
            {
                var dialog = _certificateFormDialogFactory();
                dialog.LoadForEdit(item.CertificateId);
                dialog.Owner = Application.Current?.Windows
                    .OfType<Window>()
                    .FirstOrDefault(w => w.IsActive);
                dialog.ShowDialog();
                _ = LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في فتح نموذج تعديل الشهادة: {ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ToggleSignedCopy(object? parameter)
        {
            if (parameter is not DeviceDisplayItem item) return;
            if (item.CertificateId <= 0) return;

            string message = item.IsSignedCopyAttached
                ? $"هل تريد إلغاء تأكيد إرفاق النسخة الموقّعة للشهادة {item.CertificateNumber}؟"
                : $"هل تؤكد إرفاق النسخة الموقّعة والمختومة للشهادة {item.CertificateNumber}؟";

            string title = item.IsSignedCopyAttached ? "إلغاء التأكيد" : "تأكيد الإرفاق";

            var result = MessageBox.Show(message, title,
                MessageBoxButton.YesNo, MessageBoxImage.Question,
                MessageBoxResult.No);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                bool newState = await _certificateRepository.ToggleSignedCopyAsync(item.CertificateId);

                try
                {
                    await _auditLogRepository.LogAsync(
                        newState ? "تأكيد إرفاق نسخة موقّعة" : "إلغاء تأكيد إرفاق نسخة موقّعة",
                        "Certificate",
                        item.CertificateId.ToString(),
                        $"الشهادة {item.CertificateNumber} — {(newState ? "تم التأكيد" : "تم الإلغاء")}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Audit] {ex.Message}");
                }

                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ: {ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrintSpecificCertificate(object? parameter)
        {
            int recordId = 0;
            if (parameter is int id)
            {
                recordId = id;
            }
            else if (parameter is DeviceDisplayItem item)
            {
                recordId = item.CalibrationRecordId;
            }

            if (recordId <= 0) return;

            var dialog = new Views.Dialogs.PrintPreviewDialog(recordId);
            dialog.Owner = Application.Current.MainWindow;
            dialog.ShowDialog();
        }

        private async Task PrintBatchAsync()
        {
            var selectedItems = Devices.Where(d => d.IsSelected).ToList();
            if (selectedItems.Count == 0)
            {
                MessageBox.Show(
                    Application.Current.MainWindow,
                    "يرجى تحديد جهاز واحد على الأقل للطباعة.",
                    "تنبيه",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning,
                    MessageBoxResult.OK,
                    MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading
                );
                return;
            }

            var printableItems = selectedItems.Where(d => d.LatestCalibrationRecordId > 0).ToList();
            if (printableItems.Count == 0)
            {
                MessageBox.Show(
                    Application.Current.MainWindow,
                    "الأجهزة المحددة لا تحتوي على أي سجلات معايرة صالحة للطباعة.",
                    "تنبيه",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning,
                    MessageBoxResult.OK,
                    MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading
                );
                return;
            }

            var recordIds = printableItems.Select(item => item.LatestCalibrationRecordId).ToList();

            Window dialog;
            if (recordIds.Count == 1)
            {
                dialog = new Views.Dialogs.PrintPreviewDialog(recordIds[0]);
            }
            else
            {
                dialog = new Views.Dialogs.BatchPrintPreviewDialog(recordIds);
            }
            dialog.Owner = Application.Current.MainWindow;
            dialog.ShowDialog();
            
            await Task.CompletedTask;
        }

        private void OnNavigateToDevice(int deviceId)
        {
            ShowDeviceDetailsById(deviceId);
        }

        private void OnNavigateToCalibrationRecord(int recordId)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                var record = context.CalibrationRecords
                    .AsNoTracking()
                    .FirstOrDefault(r => r.Id == recordId && !r.IsDeleted);
                if (record != null)
                {
                    ShowDeviceDetailsById(record.DeviceId, record.Id);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحديد الجهاز المرتبط بالشهادة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowDeviceDetailsById(int deviceId, int? preferredRecordId = null)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                var d = context.Devices
                    .AsNoTracking()
                    .Include(x => x.Owner)
                    .Include(x => x.DeviceType)
                    .Include(x => x.CalibrationRecords)
                    .FirstOrDefault(x => x.Id == deviceId && !x.IsDeleted);

                if (d == null) return;

                var latestCal = d.CalibrationRecords?
                    .Where(r => !r.IsDeleted)
                    .OrderByDescending(r => r.CalibrationDate)
                    .FirstOrDefault();

                // الرقم يُقرأ من جدول Certificates عبر القاعدة الموحّدة، لا من العمود
                // المهجور على CalibrationRecord — نفس المصدر المستخدم في LoadDataAsync.
                string certNumber = CertificateNumberDisplayRules.None;
                int certificateId = 0;
                bool isSignedCopyAttached = false;
                if (latestCal != null)
                {
                    var issuedNumbers = CertificateNumberDisplayRules.Load(context, new[] { latestCal.Id });
                    certNumber = CertificateNumberDisplayRules.Display(
                        issuedNumbers.TryGetValue(latestCal.Id, out var issuedNumber) ? issuedNumber : null);

                    var issuedCert = context.Certificates
                        .AsNoTracking()
                        .Where(c => c.CalibrationRecordId == latestCal.Id && !c.IsDeleted)
                        .Select(c => new { c.Id, c.IsSignedCopyAttached })
                        .FirstOrDefault();
                    certificateId = issuedCert?.Id ?? 0;
                    isSignedCopyAttached = issuedCert?.IsSignedCopyAttached ?? false;
                }

                var item = new DeviceDisplayItem
                {
                    Device = d,
                    Id = d.Id,
                    DeviceId = d.Id,
                    CalibrationRecordId = latestCal?.Id ?? 0,
                    Model = d.Model,
                    SerialNumber = d.SerialNumber,
                    OwnerName = d.Owner?.Name ?? "غير محدد",
                    DeviceTypeName = d.DeviceType?.Name ?? "غير محدد",
                    CertificateNumber = certNumber,
                    CertificateId = certificateId,
                    IsSignedCopyAttached = isSignedCopyAttached,
                    CalibrationDate = latestCal?.CalibrationDate,
                    ExpiryDate = latestCal?.ExpiryDate,
                    Result = latestCal?.Result ?? "غير معاير",
                    LatestCalibrationRecordId = latestCal?.Id ?? 0
                };

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var dialog = _deviceDetailDialogFactory();
                    var vm = (DeviceDetailViewModel)dialog.DataContext;
                    vm.LoadDeviceDetails(item.DeviceId, preferredRecordId);
                    dialog.ShowDialog();
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في فتح تفاصيل الجهاز: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void Dispose()
        {
            SearchEvents.NavigateToDevice -= OnNavigateToDevice;
            SearchEvents.NavigateToCalibrationRecord -= OnNavigateToCalibrationRecord;
        }
    }

    public class DeviceDisplayItem : BaseViewModel
    {
        public Device? Device { get; set; }
        public int Id { get; set; }
        public int DeviceId { get; set; }
        public int CalibrationRecordId { get; set; }
        
        private int _sequenceNumber;
        public int SequenceNumber
        {
            get => _sequenceNumber;
            set => SetProperty(ref _sequenceNumber, value);
        }

        public string Model { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string DeviceTypeName { get; set; } = string.Empty;
        public string CertificateNumber { get; set; } = string.Empty;
        public int CertificateId { get; set; }
        public bool HasIssuedCertificate => CertificateId > 0;
        public bool IsSignedCopyAttached { get; set; }

        /// <summary>
        /// حالة الشهادة للعرض: فارغة إن لم توجد شهادة، "بانتظار النسخة الموقّعة"
        /// إن وُجدت ولم تُؤكَّد، "مكتملة ✓" إن أُكّدت.
        /// </summary>
        public string CertificateStatus =>
            CertificateId <= 0 ? string.Empty :
            IsSignedCopyAttached ? "مكتملة ✓" : "بانتظار النسخة الموقّعة";

        public DateTime? CalibrationDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string Result { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusColor { get; set; } = string.Empty;
        public int LatestCalibrationRecordId { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public string CalibrationDateString => CalibrationDate?.ToString("yyyy-MM-dd") ?? "-";
        public string ExpiryDateString => ExpiryDate?.ToString("yyyy-MM-dd") ?? "-";
        
        public string ResultAr => Result == "Passed" ? "✅ ناجح" : Result == "Failed" ? "❌ راسب" : Result == "Conditional" ? "⚠️ مشروط" : Result;

        public string RowBackground => Status == "سارية" ? "#F4FBF7" : Status == "قريبة الانتهاء" ? "#FFFDF0" : Status == "منتهية" ? "#FFF5F5" : "Transparent";
    }
}
