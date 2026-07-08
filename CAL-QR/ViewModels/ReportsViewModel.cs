using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using CAL_QR.ViewModels.Base;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Repositories;
using CAL_QR.Services;
using System.Threading;

namespace CAL_QR.ViewModels
{
    public class ReportsViewModel : BaseViewModel
    {
        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;
        private readonly IOwnerRepository _ownerRepository;
        private readonly IExportService _exportService;
        private readonly IAuditLogRepository _auditLogRepository;

        private ObservableCollection<Owner> _owners = new();
        private List<string> _statuses = new() { "الكل", "سارية", "قريبة الانتهاء", "منتهية الصلاحية" };

        private static readonly Device AllDevicesSentinel = new Device { Id = 0, Model = "-- الكل --" };
        private CancellationTokenSource? _deviceLoadCts;
        private ObservableCollection<Device> _filteredDevices = new();
        private Device? _selectedDevice;

        private Owner? _selectedOwner;
        private DateTime? _startDate;
        private DateTime? _endDate;
        private string _selectedStatus = "الكل";
        
        private bool _isDetailedReport = true;
        private bool _isPdfFormat = true;
        private int _matchingCount;
        private bool _isLoading;

        private int _filterChangeCounter = 0;

        // Performance Report Fields
        private DateTime? _perfStartDate = DateTime.Today.AddMonths(-1);
        private DateTime? _perfEndDate = DateTime.Today;
        private bool _isPerfDetailed = true;
        private bool _isPerfPdf = true;
        private bool _isPerfLoading;

        public ReportsViewModel(
            IDbContextFactory<CalQrDbContext> contextFactory,
            IOwnerRepository ownerRepository,
            IExportService exportService,
            IAuditLogRepository auditLogRepository)
        {
            _contextFactory = contextFactory;
            _ownerRepository = ownerRepository;
            _exportService = exportService;
            _auditLogRepository = auditLogRepository;

            ExportCommand = new RelayCommand(async () => await ExportAsync(), CanExport);
            ExportPerformanceCommand = new RelayCommand(async () => await ExportPerformanceAsync(), CanExportPerformance);

            _ = InitializeDataAsync();
        }

        #region Properties
        public ObservableCollection<Owner> Owners { get => _owners; set => SetProperty(ref _owners, value); }
        public List<string> Statuses { get => _statuses; }

        public Owner? SelectedOwner
        {
            get => _selectedOwner;
            set
            {
                if (SetProperty(ref _selectedOwner, value))
                {
                    _deviceLoadCts?.Cancel();
                    _deviceLoadCts = new CancellationTokenSource();
                    _ = UpdateFilteredDevicesAsync(_deviceLoadCts.Token);
                    TriggerFilterChange();
                }
            }
        }

        public ObservableCollection<Device> FilteredDevices { get => _filteredDevices; set => SetProperty(ref _filteredDevices, value); }

        public Device? SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                if (SetProperty(ref _selectedDevice, value))
                    TriggerFilterChange();
            }
        }

        private async Task UpdateFilteredDevicesAsync(CancellationToken cancellationToken)
        {
            if (_selectedOwner == null)
            {
                FilteredDevices.Clear();
                SelectedDevice = null;
                return;
            }

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
                var devices = await context.Devices
                    .AsNoTracking()
                    .Where(d => d.OwnerId == _selectedOwner.Id && !d.IsDeleted)
                    .ToListAsync(cancellationToken);

                if (cancellationToken.IsCancellationRequested) return;

                var list = new List<Device> { AllDevicesSentinel };
                list.AddRange(devices);

                FilteredDevices = new ObservableCollection<Device>(list);
                SelectedDevice = AllDevicesSentinel;
            }
            catch (OperationCanceledException)
            {
                // تجاهل الإلغاء المتوقع عند تغيير الاختيار بسرعة
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading filtered devices: {ex.Message}");
                MessageBox.Show("حدث خطأ أثناء تحميل أجهزة الجهة المالكة المحددة.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                FilteredDevices.Clear();
                SelectedDevice = null;
            }
        }


        public DateTime? StartDate
        {
            get => _startDate;
            set
            {
                if (SetProperty(ref _startDate, value))
                    TriggerFilterChange();
            }
        }

        public DateTime? EndDate
        {
            get => _endDate;
            set
            {
                if (SetProperty(ref _endDate, value))
                    TriggerFilterChange();
            }
        }

        public string SelectedStatus
        {
            get => _selectedStatus;
            set
            {
                if (SetProperty(ref _selectedStatus, value))
                    TriggerFilterChange();
            }
        }

        public bool IsDetailedReport
        {
            get => _isDetailedReport;
            set
            {
                if (SetProperty(ref _isDetailedReport, value))
                {
                    OnPropertyChanged(nameof(IsSummaryReport));
                }
            }
        }

        public bool IsSummaryReport
        {
            get => !_isDetailedReport;
            set
            {
                if (value)
                {
                    IsDetailedReport = false;
                }
            }
        }

        public bool IsPdfFormat
        {
            get => _isPdfFormat;
            set
            {
                if (SetProperty(ref _isPdfFormat, value))
                {
                    OnPropertyChanged(nameof(IsExcelFormat));
                }
            }
        }

        public bool IsExcelFormat
        {
            get => !_isPdfFormat;
            set
            {
                if (value)
                {
                    IsPdfFormat = false;
                }
            }
        }

        public int MatchingCount
        {
            get => _matchingCount;
            set
            {
                if (SetProperty(ref _matchingCount, value))
                    (ExportCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public ICommand ExportCommand { get; }

        // Performance Report Properties
        public DateTime? PerfStartDate { get => _perfStartDate; set => SetProperty(ref _perfStartDate, value); }
        public DateTime? PerfEndDate { get => _perfEndDate; set => SetProperty(ref _perfEndDate, value); }
        
        public bool IsPerfDetailed
        {
            get => _isPerfDetailed;
            set
            {
                if (SetProperty(ref _isPerfDetailed, value))
                {
                    OnPropertyChanged(nameof(IsPerfSummary));
                }
            }
        }

        public bool IsPerfSummary
        {
            get => !_isPerfDetailed;
            set
            {
                if (value)
                {
                    IsPerfDetailed = false;
                }
            }
        }

        public bool IsPerfPdf
        {
            get => _isPerfPdf;
            set
            {
                if (SetProperty(ref _isPerfPdf, value))
                {
                    OnPropertyChanged(nameof(IsPerfExcel));
                }
            }
        }

        public bool IsPerfExcel
        {
            get => !_isPerfPdf;
            set
            {
                if (value)
                {
                    IsPerfPdf = false;
                }
            }
        }

        public bool IsPerfLoading { get => _isPerfLoading; set => SetProperty(ref _isPerfLoading, value); }
        public ICommand ExportPerformanceCommand { get; }
        #endregion

        private async Task InitializeDataAsync()
        {
            try
            {
                var ownersList = await _ownerRepository.GetAllAsync();
                Owners = new ObservableCollection<Owner>(ownersList);

                await UpdateMatchingCountAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading filters: {ex.Message}");
            }
        }

        private void TriggerFilterChange()
        {
            int current = ++_filterChangeCounter;
            Task.Delay(300).ContinueWith(t =>
            {
                if (current == _filterChangeCounter)
                {
                    Application.Current.Dispatcher.Invoke(async () => await UpdateMatchingCountAsync());
                }
            });
        }

        private async Task UpdateMatchingCountAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var query = context.CalibrationRecords
                    .AsNoTracking()
                    .Include(r => r.Device!)
                        .ThenInclude(d => d.Owner)
                    .Include(r => r.Device!)
                        .ThenInclude(d => d.DeviceType)
                    .Where(r => !r.IsDeleted);

                query = await ApplyFiltersAsync(query, context);

                MatchingCount = await query.CountAsync();
            }
            catch
            {
                MatchingCount = 0;
            }
        }

        private async Task<IQueryable<CalibrationRecord>> ApplyFiltersAsync(IQueryable<CalibrationRecord> query, CalQrDbContext context)
        {
            if (SelectedOwner == null)
            {
                // لا فلترة على الجهة أو الجهاز إطلاقاً
            }
            else if (SelectedDevice == null || SelectedDevice.Id == 0)
            {
                query = query.Where(r => r.Device != null && r.Device.OwnerId == SelectedOwner.Id);
            }
            else
            {
                query = query.Where(r => r.DeviceId == SelectedDevice.Id);
            }
            if (StartDate.HasValue)
            {
                query = query.Where(r => r.CalibrationDate >= StartDate.Value);
            }
            if (EndDate.HasValue)
            {
                query = query.Where(r => r.CalibrationDate <= EndDate.Value);
            }

            if (SelectedStatus != "الكل")
            {
                int alertDays = 30;
                var setting = await context.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "AlertDaysThreshold");
                if (setting != null && int.TryParse(setting.Value, out int val))
                {
                    alertDays = val;
                }

                DateTime today = DateTime.Today;
                DateTime alertLimit = today.AddDays(alertDays);

                if (SelectedStatus == "سارية")
                {
                    query = query.Where(r => r.ExpiryDate > alertLimit);
                }
                else if (SelectedStatus == "قريبة الانتهاء")
                {
                    query = query.Where(r => r.ExpiryDate >= today && r.ExpiryDate <= alertLimit);
                }
                else if (SelectedStatus == "منتهية الصلاحية")
                {
                    query = query.Where(r => r.ExpiryDate < today);
                }
            }

            return query;
        }

        private bool CanExport()
        {
            return MatchingCount > 0 && !IsLoading;
        }

        private async Task ExportAsync()
        {
            if (MatchingCount == 0) return;

            var saveFileDialog = new SaveFileDialog
            {
                Filter = IsPdfFormat ? "PDF Files (*.pdf)|*.pdf" : "Excel Files (*.xlsx)|*.xlsx",
                FileName = $"تقرير_المعايرة_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                IsLoading = true;
                string filePath = saveFileDialog.FileName;
                string reportType = IsDetailedReport ? "Detailed" : "Summary";

                try
                {
                    // Fetch final records
                    using var context = await _contextFactory.CreateDbContextAsync();
                    var query = context.CalibrationRecords
                        .AsNoTracking()
                        .Include(r => r.Device!)
                            .ThenInclude(d => d.Owner)
                        .Include(r => r.Device!)
                            .ThenInclude(d => d.DeviceType)
                        .Where(r => !r.IsDeleted);

                    query = await ApplyFiltersAsync(query, context);
                    var records = await query.ToListAsync();

                    if (IsPdfFormat)
                    {
                        await _exportService.ExportToPdfAsync(records, reportType, filePath);
                    }
                    else
                    {
                        await _exportService.ExportToExcelAsync(records, reportType, filePath);
                    }

                    MessageBox.Show("تم تصدير التقرير بنجاح.", "تم التصدير", MessageBoxButton.OK, MessageBoxImage.Information);
                    await _auditLogRepository.LogAsync("تصدير تقرير", "نظام", "Reports", $"تصدير تقرير ({reportType}) بصيغة {(IsPdfFormat ? "PDF" : "Excel")} إلى: {Path.GetFileName(filePath)}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"حدث خطأ أثناء التصدير: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    IsLoading = false;
                }
            }
        }

        private bool CanExportPerformance()
        {
            return !IsPerfLoading;
        }

        private async Task ExportPerformanceAsync()
        {
            DateTime start = PerfStartDate ?? DateTime.Today.AddMonths(-1);
            DateTime end = PerfEndDate ?? DateTime.Today;

            var saveFileDialog = new SaveFileDialog
            {
                Filter = IsPerfPdf ? "PDF Files (*.pdf)|*.pdf" : "Excel Files (*.xlsx)|*.xlsx",
                FileName = $"تقرير_أداء_وحدة_المعايرة_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                IsPerfLoading = true;
                string filePath = saveFileDialog.FileName;

                try
                {
                    using var context = await _contextFactory.CreateDbContextAsync();

                    // Get all calibration records in the period
                    var records = await context.CalibrationRecords
                        .AsNoTracking()
                        .Include(r => r.Device!)
                            .ThenInclude(d => d.Owner)
                        .Include(r => r.Device!)
                            .ThenInclude(d => d.DeviceType)
                        .Where(r => !r.IsDeleted && r.CalibrationDate >= start && r.CalibrationDate <= end)
                        .ToListAsync();

                    int total = records.Count;

                    // Results
                    int passed = records.Count(r => r.Result == "Passed");
                    int failed = records.Count(r => r.Result == "Failed");
                    int conditional = records.Count(r => r.Result == "Conditional");

                    // Distributions
                    var byOwner = records
                        .Where(r => r.Device?.Owner != null)
                        .GroupBy(r => r.Device!.Owner!.Name)
                        .Select(g => new DistributionItem { Name = g.Key, Count = g.Count() })
                        .OrderByDescending(x => x.Count)
                        .ToList();

                    var byDeviceType = records
                        .Where(r => r.Device?.DeviceType != null)
                        .GroupBy(r => r.Device!.DeviceType!.Name)
                        .Select(g => new DistributionItem { Name = g.Key, Count = g.Count() })
                        .OrderByDescending(x => x.Count)
                        .ToList();

                    var byEngineer = records
                        .Where(r => !string.IsNullOrEmpty(r.EngineerName))
                        .GroupBy(r => r.EngineerName)
                        .Select(g => new DistributionItem { Name = g.Key, Count = g.Count() })
                        .OrderByDescending(x => x.Count)
                        .ToList();

                    // New entities
                    DateTime endOfDay = end.Date.AddDays(1).AddTicks(-1);
                    int newDevices = await context.Devices.AsNoTracking()
                        .CountAsync(d => !d.IsDeleted && d.CreatedAt >= start && d.CreatedAt <= endOfDay);
                    int newOwners = await context.Owners.AsNoTracking()
                        .CountAsync(o => !o.IsDeleted && o.CreatedAt >= start && o.CreatedAt <= endOfDay);
                    int newDeviceTypes = await context.DeviceTypes.AsNoTracking()
                        .CountAsync(t => !t.IsDeleted && t.CreatedAt >= start && t.CreatedAt <= endOfDay);

                    var reportData = new PerformanceReportData
                    {
                        StartDate = start,
                        EndDate = end,
                        TotalRecords = total,
                        PassedCount = passed,
                        FailedCount = failed,
                        ConditionalCount = conditional,
                        PassedPercent = total > 0 ? (double)passed / total * 100 : 0,
                        FailedPercent = total > 0 ? (double)failed / total * 100 : 0,
                        ConditionalPercent = total > 0 ? (double)conditional / total * 100 : 0,
                        ByOwner = byOwner,
                        ByDeviceType = byDeviceType,
                        ByEngineer = byEngineer,
                        NewDevices = newDevices,
                        NewOwners = newOwners,
                        NewDeviceTypes = newDeviceTypes,
                        IsDetailed = IsPerfDetailed,
                        Records = records
                    };

                    if (IsPerfPdf)
                    {
                        await _exportService.ExportPerformanceReportToPdfAsync(reportData, filePath);
                    }
                    else
                    {
                        await _exportService.ExportPerformanceReportToExcelAsync(reportData, filePath);
                    }

                    MessageBox.Show("تم تصدير تقرير الأداء بنجاح.", "تم التصدير", MessageBoxButton.OK, MessageBoxImage.Information);
                    await _auditLogRepository.LogAsync("تصدير تقرير أداء", "نظام", "Reports", $"تصدير تقرير أداء وحدة المعايرة ({ (IsPerfDetailed ? "مفصل" : "مختصر") }) للفترة من {start:yyyy-MM-dd} إلى {end:yyyy-MM-dd} بصيغة {(IsPerfPdf ? "PDF" : "Excel")}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"حدث خطأ أثناء تصدير تقرير الأداء: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    IsPerfLoading = false;
                }
            }
        }
    }
}
