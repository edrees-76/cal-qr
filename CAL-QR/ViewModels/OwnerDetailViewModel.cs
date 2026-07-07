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
    public class OwnerDetailViewModel : BaseViewModel
    {
        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;
        private readonly IOwnerRepository _ownerRepository;
        private readonly IDeviceRepository _deviceRepository;

        private OwnerDisplayItem? _owner;
        private ObservableCollection<OwnerDeviceDisplayItem> _associatedDevices = new();

        public OwnerDetailViewModel(
            IDbContextFactory<CalQrDbContext> contextFactory,
            IOwnerRepository ownerRepository,
            IDeviceRepository deviceRepository)
        {
            _contextFactory = contextFactory;
            _ownerRepository = ownerRepository;
            _deviceRepository = deviceRepository;

            NavigateToDeviceCommand = new RelayCommand(NavigateToDevice);
        }

        public OwnerDisplayItem? Owner
        {
            get => _owner;
            set => SetProperty(ref _owner, value);
        }

        public ObservableCollection<OwnerDeviceDisplayItem> AssociatedDevices
        {
            get => _associatedDevices;
            set => SetProperty(ref _associatedDevices, value);
        }

        public Action? CloseWindowAction { get; set; }

        public ICommand NavigateToDeviceCommand { get; }

        public void LoadDetails(int ownerId)
        {
            try
            {
                var o = Task.Run(async () => await _ownerRepository.GetByIdAsync(ownerId)).Result;
                if (o == null) return;

                Owner = new OwnerDisplayItem
                {
                    Id = o.Id,
                    Name = o.Name,
                    Address = o.Address,
                    ContactPhone = o.ContactPhone,
                    ContactPerson = o.ContactPerson
                };

                var devicesList = Task.Run(async () => await _deviceRepository.GetByOwnerIdAsync(ownerId)).Result.ToList();

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

                        return new OwnerDeviceDisplayItem
                        {
                            SequenceNumber = index + 1,
                            DeviceId = d.Id,
                            Model = d.Model,
                            SerialNumber = d.SerialNumber,
                            DeviceTypeName = d.DeviceType?.Name ?? "غير محدد",
                            LastCalibrationDate = latestCal?.CalibrationDate,
                            ExpiryDate = latestCal?.ExpiryDate,
                            StatusText = statusText,
                            StatusColorHex = statusColor
                        };
                    }).ToList();

                    AssociatedDevices = new ObservableCollection<OwnerDeviceDisplayItem>(items);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحميل تفاصيل الجهة المالكة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
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
