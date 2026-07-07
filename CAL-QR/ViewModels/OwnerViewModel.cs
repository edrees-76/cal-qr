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
        private readonly Func<Views.Dialogs.OwnerFormDialog> _ownerFormDialogFactory;
        private readonly Func<Views.Dialogs.OwnerDetailDialog> _ownerDetailDialogFactory;

        private ObservableCollection<OwnerDisplayItem> _owners = new();
        private string _searchText = string.Empty;
        private OwnerDisplayItem? _selectedOwner;

        public OwnerViewModel(
            IOwnerRepository ownerRepository, 
            IDbContextFactory<CalQrDbContext> contextFactory,
            Func<Views.Dialogs.OwnerFormDialog> ownerFormDialogFactory,
            Func<Views.Dialogs.OwnerDetailDialog> ownerDetailDialogFactory)
        {
            _ownerRepository = ownerRepository;
            _contextFactory = contextFactory;
            _ownerFormDialogFactory = ownerFormDialogFactory;
            _ownerDetailDialogFactory = ownerDetailDialogFactory;

            LoadDataCommand = new RelayCommand(async () => await LoadDataAsync());
            AddOwnerCommand = new RelayCommand(async () => await OpenAddOwnerAsync());
            EditOwnerCommand = new RelayCommand(async (p) => await OpenEditOwnerAsync(p));
            ViewDetailsCommand = new RelayCommand(OpenOwnerDetails);
            DeleteCommand = new RelayCommand(async (p) => await DeleteOwnerAsync(p));

            // Subscribe to search navigation
            SearchEvents.NavigateToOwner += OnNavigateToOwner;
            MasterDataEvents.OwnerAdded += OnOwnerAdded;
        }

        private void OnOwnerAdded(object? sender, EventArgs e)
        {
            _ = LoadDataAsync();
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

        public OwnerDisplayItem? SelectedOwner
        {
            get => _selectedOwner;
            set => SetProperty(ref _selectedOwner, value);
        }

        public ICommand LoadDataCommand { get; }
        public ICommand AddOwnerCommand { get; }
        public ICommand EditOwnerCommand { get; }
        public ICommand ViewDetailsCommand { get; }
        public ICommand DeleteCommand { get; }

        public async Task LoadDataAsync()
        {
            try
            {
                var ownersList = await _ownerRepository.GetAllAsync();
                
                using (var context = _contextFactory.CreateDbContext())
                {
                    var displayItems = ownersList.Select((o, index) => new OwnerDisplayItem
                    {
                        Id = o.Id,
                        SequenceNumber = index + 1,
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

            for (int i = 0; i < filtered.Count; i++)
            {
                filtered[i].SequenceNumber = i + 1;
            }

            Owners = new ObservableCollection<OwnerDisplayItem>(filtered);
        }

        private async Task OpenAddOwnerAsync()
        {
            var dialog = _ownerFormDialogFactory();
            if (dialog.ShowDialog() == true)
            {
                await LoadDataAsync();
            }
        }

        private async Task OpenEditOwnerAsync(object? parameter)
        {
            if (parameter is not OwnerDisplayItem item) return;

            var dialog = _ownerFormDialogFactory();
            if (dialog.DataContext is OwnerFormViewModel vm)
            {
                vm.LoadForEdit(item);
            }

            if (dialog.ShowDialog() == true)
            {
                await LoadDataAsync();
            }
        }

        private void OpenOwnerDetails(object? parameter)
        {
            if (parameter is not OwnerDisplayItem item) return;

            var dialog = _ownerDetailDialogFactory();
            if (dialog.DataContext is OwnerDetailViewModel vm)
            {
                vm.LoadDetails(item.Id);
            }
            dialog.ShowDialog();
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
            MasterDataEvents.OwnerAdded -= OnOwnerAdded;
        }
    }

    public class OwnerDisplayItem
    {
        public int Id { get; set; }
        public int SequenceNumber { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? ContactPhone { get; set; }
        public string? ContactPerson { get; set; }
        public int DeviceCount { get; set; }
    }
}
