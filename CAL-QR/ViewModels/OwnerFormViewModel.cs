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
    public class OwnerFormViewModel : BaseViewModel
    {
        private readonly IOwnerRepository _ownerRepository;
        
        private string _name = string.Empty;
        private string _address = string.Empty;
        private string _contactPhone = string.Empty;
        private string _contactPerson = string.Empty;
        private bool _isEditMode;
        private int _ownerId;

        public OwnerFormViewModel(IOwnerRepository ownerRepository)
        {
            _ownerRepository = ownerRepository;

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

        public string Address
        {
            get => _address;
            set => SetProperty(ref _address, value);
        }

        public string ContactPhone
        {
            get => _contactPhone;
            set => SetProperty(ref _contactPhone, value);
        }

        public string ContactPerson
        {
            get => _contactPerson;
            set => SetProperty(ref _contactPerson, value);
        }

        public bool IsEditMode
        {
            get => _isEditMode;
            set => SetProperty(ref _isEditMode, value);
        }

        public int OwnerId
        {
            get => _ownerId;
            set => SetProperty(ref _ownerId, value);
        }

        public Action<bool>? CloseWindowAction { get; set; }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public void LoadForEdit(OwnerDisplayItem owner)
        {
            OwnerId = owner.Id;
            Name = owner.Name;
            Address = owner.Address ?? string.Empty;
            ContactPhone = owner.ContactPhone ?? string.Empty;
            ContactPerson = owner.ContactPerson ?? string.Empty;
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
                    var owner = new Owner
                    {
                        Id = OwnerId,
                        Name = Name,
                        Address = Address,
                        ContactPhone = ContactPhone,
                        ContactPerson = ContactPerson
                    };
                    await _ownerRepository.UpdateAsync(owner);
                }
                else
                {
                    var owner = new Owner
                    {
                        Name = Name,
                        Address = Address,
                        ContactPhone = ContactPhone,
                        ContactPerson = ContactPerson
                    };
                    await _ownerRepository.AddAsync(owner);
                }

                // Raise the event to reload owners list
                MasterDataEvents.RaiseOwnerAdded();

                // Close the dialog returning true
                CloseWindowAction?.Invoke(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في حفظ الجهة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel()
        {
            CloseWindowAction?.Invoke(false);
        }
    }
}
