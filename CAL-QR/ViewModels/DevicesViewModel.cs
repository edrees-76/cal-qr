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
            Func<Views.Dialogs.CalibrationFormDialog> calibrationFormDialogFactory)
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

            LoadDataCommand = new RelayCommand(async () => await LoadDataAsync());
            SearchCommand = new RelayCommand(async () => { CurrentPage = 1; await LoadDataAsync(); });
            ClearFiltersCommand = new RelayCommand(async () => await ClearFiltersAsync());
            ToggleAdvancedSearchCommand = new RelayCommand(() => IsAdvancedSearchVisible = !IsAdvancedSearchVisible);
            ToggleViewCommand = new RelayCommand(() => IsTableView = !IsTableView);
            
            // Pagination Commands
            NextPageCommand = new RelayCommand(async () => { CurrentPage++; await LoadDataAsync(); }, () => CurrentPage < TotalPages);
            PrevPageCommand = new RelayCommand(async () => { CurrentPage--; await LoadDataAsync(); }, () => CurrentPage > 1);

            // Dialog triggers
            AddDeviceCommand = new RelayCommand(OpenAddDeviceDialog);
            ViewDetailsCommand = new RelayCommand(OpenDetailsDialog);
            EditDeviceCommand = new RelayCommand(OpenEditDialog);
            DeleteDeviceCommand = new RelayCommand(async (p) => await DeleteDeviceAsync(p));
            PrintDeviceCommand = new RelayCommand(OpenPrintPreviewDialog);
            PrintBatchCommand = new RelayCommand(async () => await PrintBatchAsync());

            // Search Events Subscription
            SearchEvents.NavigateToDevice += OnNavigateToDevice;
            SearchEvents.NavigateToCalibrationRecord += OnNavigateToCalibrationRecord;
        }

        public ICommand PrintDeviceCommand { get; }
        public ICommand PrintBatchCommand { get; }

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
        public ICommand DeleteDeviceCommand { get; }
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

                using (var context = _contextFactory.CreateDbContext())
                {
                    var thresholdSetting = context.AppSettings.FirstOrDefault(s => s.Key == "AlertDaysThreshold");
                    int thresholdDays = int.TryParse(thresholdSetting?.Value, out var val) ? val : 30;

                    var today = DateTime.Today;
                    var alertLimit = today.AddDays(thresholdDays);

                    var query = context.Devices
                        .AsNoTracking()
                        .Include(d => d.Owner)
                        .Include(d => d.DeviceType)
                        .Include(d => d.CalibrationRecords)
                        .Where(d => !d.IsDeleted);

                    if (!string.IsNullOrWhiteSpace(SearchText))
                    {
                        var simple = SearchText.ToLower();
                        query = query.Where(d =>
                            d.Model.ToLower().Contains(simple) ||
                            d.SerialNumber.ToLower().Contains(simple) ||
                            d.Owner!.Name.ToLower().Contains(simple)
                        );
                    }

                    if (IsAdvancedSearchVisible)
                    {
                        if (SelectedOwnerFilter != null)
                        {
                            query = query.Where(d => d.OwnerId == SelectedOwnerFilter.Id);
                        }

                        if (SelectedDeviceTypeFilter != null)
                        {
                            query = query.Where(d => d.DeviceTypeId == SelectedDeviceTypeFilter.Id);
                        }

                        if (!string.IsNullOrWhiteSpace(FilterModel))
                        {
                            var modelFilter = FilterModel.ToLower();
                            query = query.Where(d => d.Model.ToLower().Contains(modelFilter));
                        }

                        if (!string.IsNullOrWhiteSpace(FilterSerialNumber))
                        {
                            var snFilter = FilterSerialNumber.ToLower();
                            query = query.Where(d => d.SerialNumber.ToLower().Contains(snFilter));
                        }
                    }

                    var rawList = await query.ToListAsync();

                    var displayItems = rawList.Select(d =>
                    {
                        var latestCal = d.CalibrationRecords
                            .Where(r => !r.IsDeleted)
                            .OrderByDescending(r => r.CalibrationDate)
                            .FirstOrDefault();

                        string certNumber = latestCal?.CertificateNumber ?? "لا توجد شهادة";
                        DateTime? calDate = latestCal?.CalibrationDate;
                        DateTime? expDate = latestCal?.ExpiryDate;
                        string result = latestCal?.Result ?? "غير معاير";

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
                            Model = d.Model,
                            SerialNumber = d.SerialNumber,
                            OwnerName = d.Owner?.Name ?? "غير محدد",
                            DeviceTypeName = d.DeviceType?.Name ?? "غير محدد",
                            CertificateNumber = certNumber,
                            CalibrationDate = calDate,
                            ExpiryDate = expDate,
                            Result = result,
                            Status = status,
                            StatusColor = statusBrush,
                            LatestCalibrationRecordId = latestCal?.Id ?? 0
                        };
                    }).ToList();

                    if (IsAdvancedSearchVisible)
                    {
                        if (FilterResult != "الكل")
                        {
                            string mappedResult = FilterResult == "ناجح" ? "Passed" : FilterResult == "راسب" ? "Failed" : FilterResult == "مشروط" ? "Conditional" : "غير معاير";
                            displayItems = displayItems.Where(item => item.Result == mappedResult).ToList();
                        }

                        if (FilterStatus != "الكل")
                        {
                            displayItems = displayItems.Where(item => item.Status == FilterStatus).ToList();
                        }

                        if (FilterStartDate.HasValue)
                        {
                            displayItems = displayItems.Where(item => item.CalibrationDate >= FilterStartDate.Value).ToList();
                        }

                        if (FilterEndDate.HasValue)
                        {
                            displayItems = displayItems.Where(item => item.CalibrationDate <= FilterEndDate.Value).ToList();
                        }
                    }

                    TotalCount = displayItems.Count;
                    TotalPages = (int)Math.Ceiling((double)TotalCount / PageSize);
                    if (TotalPages == 0) TotalPages = 1;
                    if (CurrentPage > TotalPages) CurrentPage = TotalPages;

                    var paginated = displayItems
                        .Skip((CurrentPage - 1) * PageSize)
                        .Take(PageSize)
                        .ToList();

                    Devices = new ObservableCollection<DeviceDisplayItem>(paginated);

                    _isAllSelected = false;
                    OnPropertyChanged(nameof(IsAllSelected));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحميل الأجهزة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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
            CurrentPage = 1;
            await LoadDataAsync();
        }

        private void OpenAddDeviceDialog()
        {
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
            vm.LoadDeviceDetails(item.Id);
            dialog.ShowDialog();
        }

        private void OpenEditDialog(object? parameter)
        {
            if (parameter is not DeviceDisplayItem item) return;

            var dialog = _calibrationFormDialogFactory();
            var vm = (CalibrationFormViewModel)dialog.DataContext;
            
            using (var context = _contextFactory.CreateDbContext())
            {
                var latestRecord = context.CalibrationRecords
                    .AsNoTracking()
                    .Where(r => r.DeviceId == item.Id && !r.IsDeleted)
                    .OrderByDescending(r => r.CalibrationDate)
                    .FirstOrDefault();

                if (latestRecord != null)
                {
                    vm.LoadForEdit(latestRecord.Id);
                }
                else
                {
                    vm.LoadForDeviceOnly(item.Id);
                }
            }

            vm.Saved += async (s, e) => await LoadDataAsync();
            dialog.ShowDialog();
        }

        private async Task DeleteDeviceAsync(object? parameter)
        {
            if (parameter is not DeviceDisplayItem item) return;

            var result = MessageBox.Show($"هل أنت متأكد من حذف الجهاز موديل '{item.Model}' ذو الرقم التسلسلي '{item.SerialNumber}'؟\nسيؤدي هذا إلى حذف ناعم لكافة سجلات المعايرة التابعة له.", "تأكيد الحذف", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _deviceRepository.SoftDeleteAsync(item.Id);
                    await _auditLogRepository.LogAsync("حذف جهاز", "Device", item.Id.ToString(), $"حذف ناعم للجهاز موديل {item.Model} رقم تسلسلي {item.SerialNumber}");
                    await LoadDataAsync();
                    CAL_QR.Helpers.CalibrationEvents.RaiseCalibrationChanged();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"خطأ أثناء حذف الجهاز: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OpenPrintPreviewDialog(object? parameter)
        {
            if (parameter is not DeviceDisplayItem item) return;
            if (item.LatestCalibrationRecordId <= 0)
            {
                MessageBox.Show("لا يمكن الطباعة لجهاز غير معاير أو لا يحتوي على سجل معايرة.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new Views.Dialogs.PrintPreviewDialog(item.LatestCalibrationRecordId);
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

            try
            {
                PaperTemplate? template = null;
                string printer = string.Empty;

                using (var context = await _contextFactory.CreateDbContextAsync())
                {
                    // الحل الثاني: استعلام مباشر داخل نفس سياق الاتصال المفتوح
                    var templates = await context.PaperTemplates.AsNoTracking().ToListAsync();
                    var lastTemplateIdSetting = await context.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "LastTemplateId");
                    if (lastTemplateIdSetting != null && int.TryParse(lastTemplateIdSetting.Value, out int tid) && tid > 0)
                    {
                        template = templates.FirstOrDefault(t => t.Id == tid);
                    }
                    template ??= templates.FirstOrDefault(t => t.IsDefault) ?? templates.FirstOrDefault();

                    if (template == null)
                    {
                        MessageBox.Show(
                            Application.Current.MainWindow,
                            "لا يوجد قالب طباعة معرف بالمنظومة.",
                            "تنبيه",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning,
                            MessageBoxResult.OK,
                            MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading
                        );
                        return;
                    }

                    var printers = _printService.GetAvailablePrinters().ToList();
                    var lastPrinter = await context.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "LastPrinterName");
                    if (lastPrinter != null && !string.IsNullOrWhiteSpace(lastPrinter.Value) && printers.Contains(lastPrinter.Value))
                    {
                        printer = lastPrinter.Value;
                    }
                    else if (printers.Count > 0)
                    {
                        printer = printers[0];
                    }
                }

                var recordIds = printableItems.Select(item => item.LatestCalibrationRecordId).ToList();
                List<CalibrationRecord> records;
                using (var context = await _contextFactory.CreateDbContextAsync())
                {
                    records = await context.CalibrationRecords
                        .AsNoTracking()
                        .Include(r => r.Device!)
                            .ThenInclude(d => d.Owner)
                        .Include(r => r.Device!)
                            .ThenInclude(d => d.DeviceType)
                        .Where(r => recordIds.Contains(r.Id) && !r.IsDeleted)
                        .ToListAsync();
                }

                if (records.Count == 0)
                {
                    MessageBox.Show(
                        Application.Current.MainWindow,
                        "لم يتم العثور على سجلات معايرة صالحة للأجهزة المحددة.",
                        "تنبيه",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning,
                        MessageBoxResult.OK,
                        MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading
                    );
                    return;
                }

                var jobs = new List<QrPrintJob>();
                int currentColumn = 1;
                int currentRow = 1;

                foreach (var record in records)
                {
                    string infoText = $"{record.Device?.DeviceType?.Name}\nModel: {record.Device?.Model}\nS/N: {record.Device?.SerialNumber}";

                    string qrContent = _qrService.GenerateVerificationText(
                        ownerName: record.Device?.Owner?.Name ?? "",
                        deviceType: record.Device?.DeviceType?.Name ?? "",
                        model: record.Device?.Model ?? "",
                        serial: record.Device?.SerialNumber ?? "",
                        certNo: record.CertificateNumber,
                        calDate: record.CalibrationDate.ToString("yyyy-MM-dd"),
                        expDate: record.ExpiryDate.ToString("yyyy-MM-dd"),
                        engineerName: record.EngineerName,
                        description: record.CalibrationDescription ?? "",
                        result: record.Result,
                        verifyCode: record.HmacSignature
                    );

                    var qrPrintImage = _qrService.GenerateQrCodeImage(qrContent, 600);

                    jobs.Add(new QrPrintJob
                    {
                        QrImage = qrPrintImage,
                        Template = template,
                        PrinterName = printer,
                        StartColumn = currentColumn,
                        StartRow = currentRow,
                        CertificateNumber = record.CertificateNumber,
                        DeviceInfoText = infoText
                    });

                    if (template.PaperType != "Roll")
                    {
                        currentColumn++;
                        if (currentColumn > template.Columns)
                        {
                            currentColumn = 1;
                            currentRow++;
                        }
                    }
                }

                _printService.PrintMultipleQrLabels(jobs);

                string certNumbers = string.Join(", ", records.Select(r => r.CertificateNumber));
                await _auditLogRepository.LogAsync("طباعة متعددة ملصقات QR", "Devices", "", $"طباعة رمز الاستجابة السريعة للشهادات: {certNumbers}");

                MessageBox.Show(
                    Application.Current.MainWindow,
                    "تم إرسال دفعة الطباعة بنجاح.",
                    "تمت الطباعة الدفيعة",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information,
                    MessageBoxResult.OK,
                    MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    Application.Current.MainWindow,
                    $"خطأ أثناء طباعة الدفعة: {ex.Message}",
                    "خطأ",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error,
                    MessageBoxResult.OK,
                    MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading
                );
        }
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
                    ShowDeviceDetailsById(record.DeviceId);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحديد الجهاز المرتبط بالشهادة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowDeviceDetailsById(int deviceId)
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

                var item = new DeviceDisplayItem
                {
                    Device = d,
                    Id = d.Id,
                    Model = d.Model,
                    SerialNumber = d.SerialNumber,
                    OwnerName = d.Owner?.Name ?? "غير محدد",
                    DeviceTypeName = d.DeviceType?.Name ?? "غير محدد",
                    CertificateNumber = latestCal?.CertificateNumber ?? "لا توجد شهادة",
                    CalibrationDate = latestCal?.CalibrationDate,
                    ExpiryDate = latestCal?.ExpiryDate,
                    Result = latestCal?.Result ?? "غير معاير",
                    LatestCalibrationRecordId = latestCal?.Id ?? 0
                };

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var dialog = _deviceDetailDialogFactory();
                    var vm = (DeviceDetailViewModel)dialog.DataContext;
                    vm.LoadDeviceDetails(item.Id);
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
        public string Model { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string DeviceTypeName { get; set; } = string.Empty;
        public string CertificateNumber { get; set; } = string.Empty;
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

        public string RowBackground => Status == "سارية" ? "#E8F5E9" : Status == "قريبة الانتهاء" ? "#FFFDE7" : Status == "منتهية" ? "#FFEBEE" : "Transparent";
    }
}
