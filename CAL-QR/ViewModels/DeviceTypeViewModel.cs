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
using CAL_QR.Helpers;
using CAL_QR.Services;

namespace CAL_QR.ViewModels
{
    public class DeviceTypeViewModel : BaseViewModel, IDisposable
    {
        private readonly IDeviceTypeRepository _deviceTypeRepository;
        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;
        private readonly Func<Views.Dialogs.DeviceTypeFormDialog> _deviceTypeFormDialogFactory;
        private readonly Func<Views.Dialogs.DeviceTypeDetailDialog> _deviceTypeDetailDialogFactory;
        private readonly ICurrentUserService _currentUserService;

        private ObservableCollection<DeviceTypeDisplayItem> _deviceTypes = new();
        private string _searchText = string.Empty;
        private DeviceTypeDisplayItem? _selectedDeviceType;

        public DeviceTypeViewModel(
            IDeviceTypeRepository deviceTypeRepository, 
            IDbContextFactory<CalQrDbContext> contextFactory,
            Func<Views.Dialogs.DeviceTypeFormDialog> deviceTypeFormDialogFactory,
            Func<Views.Dialogs.DeviceTypeDetailDialog> deviceTypeDetailDialogFactory,
            ICurrentUserService currentUserService)
        {
            _deviceTypeRepository = deviceTypeRepository;
            _contextFactory = contextFactory;
            _deviceTypeFormDialogFactory = deviceTypeFormDialogFactory;
            _deviceTypeDetailDialogFactory = deviceTypeDetailDialogFactory;
            _currentUserService = currentUserService;

            LoadDataCommand = new RelayCommand(async () => await LoadDataAsync());
            AddTypeCommand = new RelayCommand(async () => await OpenAddTypeAsync(), () => CanEdit);
            EditTypeCommand = new RelayCommand(async (p) => await OpenEditTypeAsync(p), (p) => CanEdit);
            ViewDetailsCommand = new RelayCommand(OpenTypeDetails);
            DeleteCommand = new RelayCommand(async (p) => await DeleteDeviceTypeAsync(p), (p) => CanEdit);

            // Subscribe to search navigation
            SearchEvents.NavigateToDeviceType += OnNavigateToDeviceType;
            MasterDataEvents.DeviceTypeAdded += OnDeviceTypeAdded;
        }

        public bool CanEdit => _currentUserService.CurrentUser != null && 
                               (_currentUserService.CurrentUser.Role == UserRole.Admin || _currentUserService.CurrentUser.IsEditor);

        private void OnDeviceTypeAdded(object? sender, EventArgs e)
        {
            _ = LoadDataAsync();
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

        public DeviceTypeDisplayItem? SelectedDeviceType
        {
            get => _selectedDeviceType;
            set => SetProperty(ref _selectedDeviceType, value);
        }

        public ICommand LoadDataCommand { get; }
        public ICommand AddTypeCommand { get; }
        public ICommand EditTypeCommand { get; }
        public ICommand ViewDetailsCommand { get; }
        public ICommand DeleteCommand { get; }

        public async Task LoadDataAsync()
        {
            try
            {
                var list = await _deviceTypeRepository.GetAllAsync();
                
                using (var context = _contextFactory.CreateDbContext())
                {
                    var displayItems = list.Select((t, index) => new DeviceTypeDisplayItem
                    {
                        Id = t.Id,
                        SequenceNumber = index + 1,
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
            
            for (int i = 0; i < filtered.Count; i++)
            {
                filtered[i].SequenceNumber = i + 1;
            }

            DeviceTypes = new ObservableCollection<DeviceTypeDisplayItem>(filtered);
        }

        private async Task OpenAddTypeAsync()
        {
            if (!CanEdit) return;
            var dialog = _deviceTypeFormDialogFactory();
            if (dialog.ShowDialog() == true)
            {
                await LoadDataAsync();
            }
        }

        private async Task OpenEditTypeAsync(object? parameter)
        {
            if (!CanEdit) return;
            if (parameter is not DeviceTypeDisplayItem item) return;

            var dialog = _deviceTypeFormDialogFactory();
            if (dialog.DataContext is DeviceTypeFormViewModel vm)
            {
                vm.LoadForEdit(item);
            }

            if (dialog.ShowDialog() == true)
            {
                await LoadDataAsync();
            }
        }

        private void OpenTypeDetails(object? parameter)
        {
            if (parameter is not DeviceTypeDisplayItem item) return;

            var dialog = _deviceTypeDetailDialogFactory();
            if (dialog.DataContext is DeviceTypeDetailViewModel vm)
            {
                vm.LoadDetails(item.Id);
            }
            dialog.ShowDialog();
        }

        private async Task DeleteDeviceTypeAsync(object? parameter)
        {
            if (!CanEdit) return;
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

        private void OnNavigateToDeviceType(int deviceTypeId)
        {
            var matchedType = DeviceTypes.FirstOrDefault(t => t.Id == deviceTypeId);
            if (matchedType == null)
            {
                try
                {
                    using var context = _contextFactory.CreateDbContext();
                    var t = context.DeviceTypes.AsNoTracking().FirstOrDefault(x => x.Id == deviceTypeId && !x.IsDeleted);
                    if (t != null)
                    {
                        var displayItem = new DeviceTypeDisplayItem
                        {
                            Id = t.Id,
                            Name = t.Name,
                            DeviceCount = context.Devices.AsNoTracking().Count(d => d.DeviceTypeId == t.Id && !d.IsDeleted)
                        };
                        SelectedDeviceType = displayItem;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"خطأ في تحميل بيانات نوع الجهاز: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                SelectedDeviceType = matchedType;
            }
        }

        public void Dispose()
        {
            SearchEvents.NavigateToDeviceType -= OnNavigateToDeviceType;
            MasterDataEvents.DeviceTypeAdded -= OnDeviceTypeAdded;
        }
    }

    public class DeviceTypeDisplayItem
    {
        public int Id { get; set; }
        public int SequenceNumber { get; set; }
        public string Name { get; set; } = string.Empty;
        public int DeviceCount { get; set; }
    }
}
