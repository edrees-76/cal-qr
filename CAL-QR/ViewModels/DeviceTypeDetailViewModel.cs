using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using CAL_QR.ViewModels.Base;
using CAL_QR.Models;
using CAL_QR.Models.DisplayItems;
using CAL_QR.Repositories;
using CAL_QR.Data;
using CAL_QR.Helpers;

namespace CAL_QR.ViewModels
{
    public class DeviceTypeDetailViewModel : BaseViewModel
    {
        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;
        private readonly IDeviceTypeRepository _deviceTypeRepository;
        private readonly IDeviceRepository _deviceRepository;

        private DeviceTypeDisplayItem? _typeInfo;
        private ObservableCollection<DeviceTypeDeviceDisplayItem> _associatedDevices = new();

        public DeviceTypeDetailViewModel(
            IDbContextFactory<CalQrDbContext> contextFactory,
            IDeviceTypeRepository deviceTypeRepository,
            IDeviceRepository deviceRepository)
        {
            _contextFactory = contextFactory;
            _deviceTypeRepository = deviceTypeRepository;
            _deviceRepository = deviceRepository;

            NavigateToDeviceCommand = new RelayCommand(NavigateToDevice);
        }

        public DeviceTypeDisplayItem? TypeInfo
        {
            get => _typeInfo;
            set => SetProperty(ref _typeInfo, value);
        }

        public ObservableCollection<DeviceTypeDeviceDisplayItem> AssociatedDevices
        {
            get => _associatedDevices;
            set => SetProperty(ref _associatedDevices, value);
        }

        public Action? CloseWindowAction { get; set; }

        public ICommand NavigateToDeviceCommand { get; }

        public void LoadDetails(int deviceTypeId)
        {
            try
            {
                var t = Task.Run(async () => await _deviceTypeRepository.GetByIdAsync(deviceTypeId)).Result;
                if (t == null) return;

                TypeInfo = new DeviceTypeDisplayItem
                {
                    Id = t.Id,
                    Name = t.Name
                };

                var devicesList = Task.Run(async () => await _deviceRepository.GetByDeviceTypeIdAsync(deviceTypeId)).Result.ToList();

                using (var context = _contextFactory.CreateDbContext())
                {
                    var thresholdSetting = context.AppSettings.AsNoTracking().FirstOrDefault(s => s.Key == "AlertDaysThreshold");
                    int thresholdDays = int.TryParse(thresholdSetting?.Value, out var val) ? val : 30;

                    var today = DateTime.Today;
                    var alertLimit = today.AddDays(thresholdDays);

                    var items = devicesList.Select((d, index) =>
                    {
                        var latestCal = d.CalibrationRecords?
                            .Where(r => !r.IsDeleted)
                            .OrderByDescending(r => r.CalibrationDate)
                            .FirstOrDefault();

                        string statusText = "غير معاير";
                        string statusColor = "#9E9E9E";

                        if (latestCal != null)
                        {
                            var expDate = latestCal.ExpiryDate.Date;
                            if (expDate < today)
                            {
                                statusText = "منتهية";
                                statusColor = "#C62828";
                            }
                            else if (expDate <= alertLimit)
                            {
                                statusText = "قريبة الانتهاء";
                                statusColor = "#F9A825";
                            }
                            else
                            {
                                statusText = "سارية";
                                statusColor = "#2E7D32";
                            }
                        }

                        return new DeviceTypeDeviceDisplayItem
                        {
                            SequenceNumber = index + 1,
                            DeviceId = d.Id,
                            Model = d.Model,
                            SerialNumber = d.SerialNumber,
                            OwnerName = d.Owner?.Name ?? "غير محدد",
                            LastCalibrationDate = latestCal?.CalibrationDate,
                            ExpiryDate = latestCal?.ExpiryDate,
                            StatusText = statusText,
                            StatusColorHex = statusColor
                        };
                    }).ToList();

                    AssociatedDevices = new ObservableCollection<DeviceTypeDeviceDisplayItem>(items);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحميل تفاصيل نوع الجهاز: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void NavigateToDevice(object? parameter)
        {
            if (parameter is not int deviceId) return;

            SearchEvents.RaiseNavigateToDevice(deviceId);
            CloseWindowAction?.Invoke();
        }
    }
}
