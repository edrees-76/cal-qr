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

namespace CAL_QR.ViewModels
{
    public class ReportsViewModel : BaseViewModel
    {
        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;
        private readonly IOwnerRepository _ownerRepository;
        private readonly IDeviceTypeRepository _deviceTypeRepository;
        private readonly IExportService _exportService;
        private readonly IAuditLogRepository _auditLogRepository;

        private ObservableCollection<Owner> _owners = new();
        private ObservableCollection<DeviceType> _deviceTypes = new();
        private List<string> _statuses = new() { "الكل", "سارية", "قريبة الانتهاء", "منتهية الصلاحية" };

        private Owner? _selectedOwner;
        private DeviceType? _selectedDeviceType;
        private DateTime? _startDate;
        private DateTime? _endDate;
        private string _selectedStatus = "الكل";
        
        private bool _isDetailedReport = true;
        private bool _isPdfFormat = true;
        private int _matchingCount;
        private bool _isLoading;

        private int _filterChangeCounter = 0;

        public ReportsViewModel(
            IDbContextFactory<CalQrDbContext> contextFactory,
            IOwnerRepository ownerRepository,
            IDeviceTypeRepository deviceTypeRepository,
            IExportService exportService,
            IAuditLogRepository auditLogRepository)
        {
            _contextFactory = contextFactory;
            _ownerRepository = ownerRepository;
            _deviceTypeRepository = deviceTypeRepository;
            _exportService = exportService;
            _auditLogRepository = auditLogRepository;

            ExportCommand = new RelayCommand(async () => await ExportAsync(), CanExport);

            _ = InitializeDataAsync();
        }

        #region Properties
        public ObservableCollection<Owner> Owners { get => _owners; set => SetProperty(ref _owners, value); }
        public ObservableCollection<DeviceType> DeviceTypes { get => _deviceTypes; set => SetProperty(ref _deviceTypes, value); }
        public List<string> Statuses { get => _statuses; }

        public Owner? SelectedOwner
        {
            get => _selectedOwner;
            set
            {
                if (SetProperty(ref _selectedOwner, value))
                    TriggerFilterChange();
            }
        }

        public DeviceType? SelectedDeviceType
        {
            get => _selectedDeviceType;
            set
            {
                if (SetProperty(ref _selectedDeviceType, value))
                    TriggerFilterChange();
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
        #endregion

        private async Task InitializeDataAsync()
        {
            try
            {
                var ownersList = await _ownerRepository.GetAllAsync();
                Owners = new ObservableCollection<Owner>(ownersList);

                var typesList = await _deviceTypeRepository.GetAllAsync();
                DeviceTypes = new ObservableCollection<DeviceType>(typesList);

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
            if (SelectedOwner != null)
            {
                query = query.Where(r => r.Device != null && r.Device.OwnerId == SelectedOwner.Id);
            }
            if (SelectedDeviceType != null)
            {
                query = query.Where(r => r.Device != null && r.Device.DeviceTypeId == SelectedDeviceType.Id);
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
    }
}
