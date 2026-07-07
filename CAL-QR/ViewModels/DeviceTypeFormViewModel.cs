using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CAL_QR.ViewModels.Base;
using CAL_QR.Models;
using CAL_QR.Repositories;
using CAL_QR.Helpers;

namespace CAL_QR.ViewModels
{
    public class DeviceTypeFormViewModel : BaseViewModel
    {
        private readonly IDeviceTypeRepository _deviceTypeRepository;
        
        private string _name = string.Empty;
        private bool _isEditMode;
        private int _deviceTypeId;
        private bool? _dialogResult;

        public DeviceTypeFormViewModel(IDeviceTypeRepository deviceTypeRepository)
        {
            _deviceTypeRepository = deviceTypeRepository;

            SaveCommand = new RelayCommand(async () => await SaveAsync(), CanSave);
            CancelCommand = new RelayCommand(Cancel);
        }

        public string Name
        {
            get => _name;
            set
            {
                if (SetProperty(ref _name, value))
                {
                    (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsEditMode
        {
            get => _isEditMode;
            set => SetProperty(ref _isEditMode, value);
        }

        public int DeviceTypeId
        {
            get => _deviceTypeId;
            set => SetProperty(ref _deviceTypeId, value);
        }

        public bool? DialogResult
        {
            get => _dialogResult;
            set => SetProperty(ref _dialogResult, value);
        }

        public Action? CloseWindowAction { get; set; }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public void LoadForEdit(DeviceTypeDisplayItem type)
        {
            DeviceTypeId = type.Id;
            Name = type.Name;
            IsEditMode = true;
        }

        private bool CanSave()
        {
            return !string.IsNullOrWhiteSpace(Name);
        }

        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(Name)) return;

            try
            {
                if (IsEditMode)
                {
                    var type = new DeviceType
                    {
                        Id = DeviceTypeId,
                        Name = Name
                    };
                    await _deviceTypeRepository.UpdateAsync(type);
                }
                else
                {
                    var type = new DeviceType
                    {
                        Name = Name
                    };
                    await _deviceTypeRepository.AddAsync(type);
                }

                // Raise the event to reload types list
                MasterDataEvents.RaiseDeviceTypeAdded();

                DialogResult = true;
                CloseWindowAction?.Invoke();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في حفظ نوع الجهاز: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel()
        {
            DialogResult = false;
            CloseWindowAction?.Invoke();
        }
    }
}
