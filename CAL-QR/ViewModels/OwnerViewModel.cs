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

namespace CAL_QR.ViewModels
{
    public class OwnerViewModel : BaseViewModel, IDisposable
    {
        private readonly IOwnerRepository _ownerRepository;
        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;
        private ObservableCollection<OwnerDisplayItem> _owners = new();
        private string _searchText = string.Empty;

        // Form Fields for Add/Edit Dialog
        private string _formName = string.Empty;
        private string _formAddress = string.Empty;
        private string _formPhone = string.Empty;
        private string _formContactPerson = string.Empty;
        private OwnerDisplayItem? _selectedOwner;
        private bool _isEditMode;

        public OwnerViewModel(IOwnerRepository ownerRepository, IDbContextFactory<CalQrDbContext> contextFactory)
        {
            _ownerRepository = ownerRepository;
            _contextFactory = contextFactory;

            LoadDataCommand = new RelayCommand(async () => await LoadDataAsync());
            SaveCommand = new RelayCommand(async () => await SaveOwnerAsync(), CanSave);
            DeleteCommand = new RelayCommand(async (p) => await DeleteOwnerAsync(p));
            ClearFormCommand = new RelayCommand(ClearForm);

            // Subscribe to search navigation
            SearchEvents.NavigateToOwner += OnNavigateToOwner;
        }

        public ObservableCollection<OwnerDisplayItem> Owners
        {
            get => _owners;
            set => SetProperty(ref _owners, value);
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    FilterOwners();
                }
            }
        }

        public string FormName
        {
            get => _formName;
            set => SetProperty(ref _formName, value);
        }

        public string FormAddress
        {
            get => _formAddress;
            set => SetProperty(ref _formAddress, value);
        }

        public string FormPhone
        {
            get => _formPhone;
            set => SetProperty(ref _formPhone, value);
        }

        public string FormContactPerson
        {
            get => _formContactPerson;
            set => SetProperty(ref _formContactPerson, value);
        }

        public OwnerDisplayItem? SelectedOwner
        {
            get => _selectedOwner;
            set
            {
                if (SetProperty(ref _selectedOwner, value) && value != null)
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
                var ownersList = await _ownerRepository.GetAllAsync();
                
                using (var context = _contextFactory.CreateDbContext())
                {
                    var displayItems = ownersList.Select(o => new OwnerDisplayItem
                    {
                        Id = o.Id,
                        Name = o.Name,
                        Address = o.Address,
                        ContactPhone = o.ContactPhone,
                        ContactPerson = o.ContactPerson,
                        DeviceCount = context.Devices.AsNoTracking().Count(d => d.OwnerId == o.Id && !d.IsDeleted)
                    }).ToList();

                    Owners = new ObservableCollection<OwnerDisplayItem>(displayItems);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحميل البيانات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FilterOwners()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                _ = LoadDataAsync();
                return;
            }

            var query = SearchText.ToLower();
            var filtered = Owners.Where(o => 
                o.Name.ToLower().Contains(query) ||
                (o.Address?.ToLower().Contains(query) ?? false) ||
                (o.ContactPerson?.ToLower().Contains(query) ?? false)
            ).ToList();

            Owners = new ObservableCollection<OwnerDisplayItem>(filtered);
        }

        private bool CanSave()
        {
            return !string.IsNullOrWhiteSpace(FormName);
        }

        private async Task SaveOwnerAsync()
        {
            try
            {
                if (IsEditMode && SelectedOwner != null)
                {
                    var owner = new Owner
                    {
                        Id = SelectedOwner.Id,
                        Name = FormName,
                        Address = FormAddress,
                        ContactPhone = FormPhone,
                        ContactPerson = FormContactPerson
                    };
                    await _ownerRepository.UpdateAsync(owner);
                }
                else
                {
                    var owner = new Owner
                    {
                        Name = FormName,
                        Address = FormAddress,
                        ContactPhone = FormPhone,
                        ContactPerson = FormContactPerson
                    };
                    await _ownerRepository.AddAsync(owner);
                }

                ClearForm();
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في حفظ البيانات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task DeleteOwnerAsync(object? parameter)
        {
            if (parameter is not OwnerDisplayItem item) return;

            if (item.DeviceCount > 0)
            {
                MessageBox.Show("لا يمكن حذف هذه الجهة لوجود أجهزة مسجلة باسمها في النظام. يرجى نقل أو حذف الأجهزة أولاً.", "تنبيه الحذف", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show($"هل أنت متأكد من حذف الجهة '{item.Name}'؟", "تأكيد الحذف", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _ownerRepository.SoftDeleteAsync(item.Id);
                    await LoadDataAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"خطأ في حذف الجهة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void PopulateForm(OwnerDisplayItem item)
        {
            FormName = item.Name;
            FormAddress = item.Address ?? string.Empty;
            FormPhone = item.ContactPhone ?? string.Empty;
            FormContactPerson = item.ContactPerson ?? string.Empty;
            IsEditMode = true;
        }

        private void ClearForm()
        {
            FormName = string.Empty;
            FormAddress = string.Empty;
            FormPhone = string.Empty;
            FormContactPerson = string.Empty;
            SelectedOwner = null;
            IsEditMode = false;
        }

        private void OnNavigateToOwner(int ownerId)
        {
            var matchedOwner = Owners.FirstOrDefault(o => o.Id == ownerId);
            if (matchedOwner == null)
            {
                try
                {
                    using var context = _contextFactory.CreateDbContext();
                    var o = context.Owners.AsNoTracking().FirstOrDefault(x => x.Id == ownerId && !x.IsDeleted);
                    if (o != null)
                    {
                        var displayItem = new OwnerDisplayItem
                        {
                            Id = o.Id,
                            Name = o.Name,
                            Address = o.Address,
                            ContactPhone = o.ContactPhone,
                            ContactPerson = o.ContactPerson,
                            DeviceCount = context.Devices.AsNoTracking().Count(d => d.OwnerId == o.Id && !d.IsDeleted)
                        };
                        SelectedOwner = displayItem;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"خطأ في تحميل بيانات الجهة المالكة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                SelectedOwner = matchedOwner;
            }
        }

        public void Dispose()
        {
            SearchEvents.NavigateToOwner -= OnNavigateToOwner;
        }
    }

    public class OwnerDisplayItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? ContactPhone { get; set; }
        public string? ContactPerson { get; set; }
        public int DeviceCount { get; set; }
    }
}
