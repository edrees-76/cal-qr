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
        private ISeries[] _pieSeriesCollection = Array.Empty<ISeries>();
        private ISeries[] _barSeriesCollection = Array.Empty<ISeries>();
        private Axis[] _xAxes = Array.Empty<Axis>();

        public DashboardViewModel(
            IDbContextFactory<CalQrDbContext> contextFactory,
            IDeviceRepository deviceRepository,
            ICalibrationRepository calibrationRepository)
        {
            _contextFactory = contextFactory;
            _deviceRepository = deviceRepository;
            _calibrationRepository = calibrationRepository;

            CalibrationEvents.CalibrationChanged += OnCalibrationChanged;

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

        public ISeries[] PieSeriesCollection
        {
            get => _pieSeriesCollection;
            set => SetProperty(ref _pieSeriesCollection, value);
        }

        public ISeries[] BarSeriesCollection
        {
            get => _barSeriesCollection;
            set => SetProperty(ref _barSeriesCollection, value);
        }

        public Axis[] XAxes
        {
            get => _xAxes;
            set => SetProperty(ref _xAxes, value);
        }
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
                        }
                        else if (latestCal.ExpiryDate <= alertLimit)
                        {
                            expiring++;
                            int remainingDays = (latestCal.ExpiryDate - today).Days;
                            expiringSoonList.Add(new ExpiringDeviceDisplayItem
                            {
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

                // Sort expiring list by days remaining ascending
                ExpiringDevices = new ObservableCollection<ExpiringDeviceDisplayItem>(
                    expiringSoonList.OrderBy(x => x.DaysRemaining)
                );

                // Setup Donut chart values
                PieSeriesCollection = ownerCounts.Select(pair => new PieSeries<int>
                {
                    Values = new[] { pair.Value },
                    Name = pair.Key,
                    InnerRadius = 45
                }).Cast<ISeries>().ToArray();

                // Setup Bar/Column chart values
                BarSeriesCollection = new ISeries[]
                {
                    new ColumnSeries<int>
                    {
                        Name = "سارية",
                        Values = new[] { valid }
                    },
                    new ColumnSeries<int>
                    {
                        Name = "قريبة الانتهاء",
                        Values = new[] { expiring }
                    },
                    new ColumnSeries<int>
                    {
                        Name = "منتهية",
                        Values = new[] { expired }
                    }
                };

                XAxes = new Axis[]
                {
                    new Axis { Labels = new[] { "توزيع حالة الأجهزة" } }
                };
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
        public string DeviceModel { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public DateTime ExpiryDate { get; set; }
        public int DaysRemaining { get; set; }

        public string ExpiryDateString => ExpiryDate.ToString("yyyy-MM-dd");
    }
}
