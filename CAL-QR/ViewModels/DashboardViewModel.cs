using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using CAL_QR.ViewModels.Base;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Repositories;
using CAL_QR.Helpers;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;

namespace CAL_QR.ViewModels
{
    public class DashboardViewModel : BaseViewModel, IDisposable
    {
        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;
        private readonly IDeviceRepository _deviceRepository;
        private readonly ICalibrationRepository _calibrationRepository;

        private int _totalDevices;
        private int _validDevices;
        private int _expiringSoonDevices;
        private int _expiredDevices;
        private int _alertThresholdDays = 30;

        private ObservableCollection<ExpiringDeviceDisplayItem> _expiringDevices = new();
        private ObservableCollection<ExpiredDeviceDisplayItem> _expiredDevicesList = new();
        private ObservableCollection<OwnerDeviceStat> _ownerDeviceStats = new();

        public DashboardViewModel(
            IDbContextFactory<CalQrDbContext> contextFactory,
            IDeviceRepository deviceRepository,
            ICalibrationRepository calibrationRepository)
        {
            _contextFactory = contextFactory;
            _deviceRepository = deviceRepository;
            _calibrationRepository = calibrationRepository;

            CalibrationEvents.CalibrationChanged += OnCalibrationChanged;

            NavigateToDeviceCommand = new RelayCommand(p =>
            {
                if (p is int id)
                {
                    SearchEvents.RaiseNavigateToDevice(id);
                }
            });

            _ = LoadDataAsync();
        }

        #region Properties
        public int TotalDevices { get => _totalDevices; set => SetProperty(ref _totalDevices, value); }
        public int ValidDevices { get => _validDevices; set => SetProperty(ref _validDevices, value); }
        public int ExpiringSoonDevices { get => _expiringSoonDevices; set => SetProperty(ref _expiringSoonDevices, value); }
        public int ExpiredDevices { get => _expiredDevices; set => SetProperty(ref _expiredDevices, value); }
        public int AlertThresholdDays { get => _alertThresholdDays; set => SetProperty(ref _alertThresholdDays, value); }

        public ObservableCollection<ExpiringDeviceDisplayItem> ExpiringDevices
        {
            get => _expiringDevices;
            set => SetProperty(ref _expiringDevices, value);
        }

        public ObservableCollection<ExpiredDeviceDisplayItem> ExpiredDevicesList
        {
            get => _expiredDevicesList;
            set => SetProperty(ref _expiredDevicesList, value);
        }

        public ObservableCollection<OwnerDeviceStat> OwnerDeviceStats
        {
            get => _ownerDeviceStats;
            set => SetProperty(ref _ownerDeviceStats, value);
        }

        public System.Windows.Input.ICommand NavigateToDeviceCommand { get; }
        #endregion

        public async Task LoadDataAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Read threshold setting
                int alertDays = 30;
                var thresholdSetting = await context.AppSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Key == "AlertDaysThreshold");
                if (thresholdSetting != null && int.TryParse(thresholdSetting.Value, out int parsedDays))
                {
                    alertDays = parsedDays;
                }
                AlertThresholdDays = alertDays;

                DateTime today = DateTime.Today;
                DateTime alertLimit = today.AddDays(alertDays);

                // Load all active devices with protection lambdas
                var allDevices = await context.Devices
                    .AsNoTracking()
                    .Include(d => d.Owner!)
                    .Include(d => d.DeviceType!)
                    .Include(d => d.CalibrationRecords!)
                    .Where(d => !d.IsDeleted)
                    .ToListAsync();

                int total = allDevices.Count;
                int valid = 0;
                int expiring = 0;
                int expired = 0;

                var expiringSoonList = new List<ExpiringDeviceDisplayItem>();
                var expiredList = new List<ExpiredDeviceDisplayItem>();
                var ownerCounts = new Dictionary<string, int>();

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
                            expired++;
                            int daysExpired = (today - latestCal.ExpiryDate).Days;
                            expiredList.Add(new ExpiredDeviceDisplayItem
                            {
                                DeviceId = device.Id,
                                DeviceModel = device.Model ?? "",
                                SerialNumber = device.SerialNumber ?? "",
                                OwnerName = device.Owner?.Name ?? "غير محدد",
                                ExpiryDate = latestCal.ExpiryDate,
                                DaysExpired = daysExpired
                            });
                        }
                        else if (latestCal.ExpiryDate <= alertLimit)
                        {
                            expiring++;
                            int remainingDays = (latestCal.ExpiryDate - today).Days;
                            expiringSoonList.Add(new ExpiringDeviceDisplayItem
                            {
                                DeviceId = device.Id,
                                DeviceModel = device.Model ?? "",
                                SerialNumber = device.SerialNumber ?? "",
                                OwnerName = device.Owner?.Name ?? "غير محدد",
                                ExpiryDate = latestCal.ExpiryDate,
                                DaysRemaining = remainingDays
                            });
                        }
                        else
                        {
                            valid++;
                        }
                    }
                    else
                    {
                        expired++;
                        expiredList.Add(new ExpiredDeviceDisplayItem
                        {
                            DeviceId = device.Id,
                            DeviceModel = device.Model ?? "",
                            SerialNumber = device.SerialNumber ?? "",
                            OwnerName = device.Owner?.Name ?? "غير محدد",
                            ExpiryDate = DateTime.MinValue,
                            DaysExpired = 0
                        });
                    }

                    string ownerName = device.Owner?.Name ?? "غير محدد";
                    if (ownerCounts.ContainsKey(ownerName))
                        ownerCounts[ownerName]++;
                    else
                        ownerCounts[ownerName] = 1;
                }

                TotalDevices = total;
                ValidDevices = valid;
                ExpiringSoonDevices = expiring;
                ExpiredDevices = expired;

                // Sort expiring list by days remaining ascending and calculate Rank
                var sortedExpiring = expiringSoonList.OrderBy(x => x.DaysRemaining).ToList();
                for (int i = 0; i < sortedExpiring.Count; i++)
                {
                    sortedExpiring[i].Rank = i + 1;
                }
                ExpiringDevices = new ObservableCollection<ExpiringDeviceDisplayItem>(sortedExpiring);
 
                // Sort expired list by expiry date ascending (oldest/never calibrated first) and calculate Rank
                var sortedExpired = expiredList.OrderBy(x => x.ExpiryDate).ToList();
                for (int i = 0; i < sortedExpired.Count; i++)
                {
                    sortedExpired[i].Rank = i + 1;
                }
                ExpiredDevicesList = new ObservableCollection<ExpiredDeviceDisplayItem>(sortedExpired);

                // Sort ownerCounts descending and calculate percentage for custom bar chart
                var sortedOwners = ownerCounts.OrderByDescending(p => p.Value).ToList();
                int maxCount = sortedOwners.Any() ? sortedOwners.Max(p => p.Value) : 1;

                var stats = sortedOwners.Select((p, idx) => new OwnerDeviceStat
                {
                    Rank = idx + 1,
                    OwnerName = p.Key,
                    Count = p.Value,
                    BarWidthPercentage = maxCount > 0 ? ((double)p.Value / maxCount) * 100 : 0
                }).ToList();

                OwnerDeviceStats = new ObservableCollection<OwnerDeviceStat>(stats);
            }
            catch (Exception ex)
            {
                // Handle load failures gracefully
                System.Diagnostics.Debug.WriteLine($"Dashboard load error: {ex.Message}");
            }
        }

        private void OnCalibrationChanged(object? sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(async () => await LoadDataAsync());
        }

        public void Dispose()
        {
            CalibrationEvents.CalibrationChanged -= OnCalibrationChanged;
        }
    }

    public class ExpiringDeviceDisplayItem
    {
        public int DeviceId { get; set; }
        public int Rank { get; set; }
        public string DeviceModel { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public DateTime ExpiryDate { get; set; }
        public int DaysRemaining { get; set; }

        public string ExpiryDateString => ExpiryDate.ToString("yyyy-MM-dd");
    }

    public class ExpiredDeviceDisplayItem
    {
        public int DeviceId { get; set; }
        public int Rank { get; set; }
        public string DeviceModel { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public DateTime ExpiryDate { get; set; }
        public int DaysExpired { get; set; }
 
        public string ExpiryDateString => ExpiryDate == DateTime.MinValue ? "بدون شهادة" : ExpiryDate.ToString("yyyy-MM-dd");
        public string DaysExpiredText => ExpiryDate == DateTime.MinValue ? "غير معاير" : (DaysExpired == 0 ? "منتهي اليوم" : $"{DaysExpired} يوم مضت");
    }

    public class OwnerDeviceStat
    {
        public int Rank { get; set; }
        public string OwnerName { get; set; } = string.Empty;
        public int Count { get; set; }
        public double BarWidthPercentage { get; set; }
    }
}
