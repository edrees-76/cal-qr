using System;
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

namespace CAL_QR.ViewModels
{
    public class DeviceTypeViewModel : BaseViewModel
    {
        private readonly IDeviceTypeRepository _deviceTypeRepository;
        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;
        private ObservableCollection<DeviceTypeDisplayItem> _deviceTypes = new();
        private string _searchText = string.Empty;

        // Form Fields
        private string _formName = string.Empty;
        private DeviceTypeDisplayItem? _selectedDeviceType;
        private bool _isEditMode;

        public DeviceTypeViewModel(IDeviceTypeRepository deviceTypeRepository, IDbContextFactory<CalQrDbContext> contextFactory)
        {
            _deviceTypeRepository = deviceTypeRepository;
            _contextFactory = contextFactory;

            LoadDataCommand = new RelayCommand(async () => await LoadDataAsync());
            SaveCommand = new RelayCommand(async () => await SaveDeviceTypeAsync(), CanSave);
            DeleteCommand = new RelayCommand(async (p) => await DeleteDeviceTypeAsync(p));
            ClearFormCommand = new RelayCommand(ClearForm);
        }

        public ObservableCollection<DeviceTypeDisplayItem> DeviceTypes
        {
            get => _deviceTypes;
            set => SetProperty(ref _deviceTypes, value);
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    FilterDeviceTypes();
                }
            }
        }

        public string FormName
        {
            get => _formName;
            set => SetProperty(ref _formName, value);
        }

        public DeviceTypeDisplayItem? SelectedDeviceType
        {
            get => _selectedDeviceType;
            set
            {
                if (SetProperty(ref _selectedDeviceType, value) && value != null)
                {
                    PopulateForm(value);
                }
            }
        }

        public bool IsEditMode
        {
            get => _isEditMode;
            set => SetProperty(ref _isEditMode, value);
        }

        public ICommand LoadDataCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ClearFormCommand { get; }

        public async Task LoadDataAsync()
        {
            try
            {
                var list = await _deviceTypeRepository.GetAllAsync();
                
                using (var context = _contextFactory.CreateDbContext())
                {
                    var displayItems = list.Select(t => new DeviceTypeDisplayItem
                    {
                        Id = t.Id,
                        Name = t.Name,
                        DeviceCount = context.Devices.AsNoTracking().Count(d => d.DeviceTypeId == t.Id && !d.IsDeleted)
                    }).ToList();

                    DeviceTypes = new ObservableCollection<DeviceTypeDisplayItem>(displayItems);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحميل البيانات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FilterDeviceTypes()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                _ = LoadDataAsync();
                return;
            }

            var query = SearchText.ToLower();
            var filtered = DeviceTypes.Where(t => t.Name.ToLower().Contains(query)).ToList();
            DeviceTypes = new ObservableCollection<DeviceTypeDisplayItem>(filtered);
        }

        private bool CanSave()
        {
            return !string.IsNullOrWhiteSpace(FormName);
        }

        private async Task SaveDeviceTypeAsync()
        {
            try
            {
                if (IsEditMode && SelectedDeviceType != null)
                {
                    var type = new DeviceType
                    {
                        Id = SelectedDeviceType.Id,
                        Name = FormName
                    };
                    await _deviceTypeRepository.UpdateAsync(type);
                }
                else
                {
                    var type = new DeviceType
                    {
                        Name = FormName
                    };
                    await _deviceTypeRepository.AddAsync(type);
                }

                ClearForm();
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في حفظ البيانات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task DeleteDeviceTypeAsync(object? parameter)
        {
            if (parameter is not DeviceTypeDisplayItem item) return;

            if (item.DeviceCount > 0)
            {
                MessageBox.Show("لا يمكن حذف هذا النوع لوجود أجهزة مسجلة تنتمي إليه. يرجى نقل أو حذف الأجهزة أولاً.", "تنبيه الحذف", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show($"هل أنت متأكد من حذف نوع الجهاز '{item.Name}'؟", "تأكيد الحذف", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _deviceTypeRepository.SoftDeleteAsync(item.Id);
                    await LoadDataAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"خطأ في حذف نوع الجهاز: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void PopulateForm(DeviceTypeDisplayItem item)
        {
            FormName = item.Name;
            IsEditMode = true;
        }

        private void ClearForm()
        {
            FormName = string.Empty;
            SelectedDeviceType = null;
            IsEditMode = false;
        }
    }

    public class DeviceTypeDisplayItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int DeviceCount { get; set; }
    }
}
