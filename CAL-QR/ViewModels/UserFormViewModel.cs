using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CAL_QR.ViewModels.Base;
using CAL_QR.Models;
using CAL_QR.Repositories;

namespace CAL_QR.ViewModels
{
    public class UserFormViewModel : BaseViewModel
    {
        private readonly IUserRepository _userRepository;

        private string _fullName = string.Empty;
        private string _username = string.Empty;
        private string _password = string.Empty;
        private UserRole _role = UserRole.Viewer;
        private bool _isEditor;
        private bool _isActive = true;
        private bool _isEditMode;
        private int _userId;
        private bool _isSaving;

        // Permissions flag checkboxes
        private bool _canAccessRecords;
        private bool _canAccessVerification;
        private bool _canAccessOwners;
        private bool _canAccessDeviceTypes;
        private bool _canViewReports;
        private bool _canManageSettings;
        private bool _canManageUsers;

        public UserFormViewModel(IUserRepository userRepository)
        {
            _userRepository = userRepository;

            SaveCommand = new RelayCommand(async () => await SaveAsync(), CanSave);
            CancelCommand = new RelayCommand(Cancel);
        }

        #region Properties
        public string FullName
        {
            get => _fullName;
            set
            {
                if (SetProperty(ref _fullName, value))
                    (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public string Username
        {
            get => _username;
            set
            {
                if (SetProperty(ref _username, value))
                    (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                if (SetProperty(ref _password, value))
                    (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public UserRole Role
        {
            get => _role;
            set
            {
                if (SetProperty(ref _role, value))
                {
                    OnPropertyChanged(nameof(IsNotAdminRole));
                    (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsEditor
        {
            get => _isEditor;
            set => SetProperty(ref _isEditor, value);
        }

        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        public bool IsEditMode
        {
            get => _isEditMode;
            set => SetProperty(ref _isEditMode, value);
        }

        public int UserId
        {
            get => _userId;
            set => SetProperty(ref _userId, value);
        }

        public bool IsNotAdminRole => Role != UserRole.Admin;

        // Toggle permissions bindings
        public bool CanAccessRecords { get => _canAccessRecords; set => SetProperty(ref _canAccessRecords, value); }
        public bool CanAccessVerification { get => _canAccessVerification; set => SetProperty(ref _canAccessVerification, value); }
        public bool CanAccessOwners { get => _canAccessOwners; set => SetProperty(ref _canAccessOwners, value); }
        public bool CanAccessDeviceTypes { get => _canAccessDeviceTypes; set => SetProperty(ref _canAccessDeviceTypes, value); }
        public bool CanViewReports { get => _canViewReports; set => SetProperty(ref _canViewReports, value); }
        public bool CanManageSettings { get => _canManageSettings; set => SetProperty(ref _canManageSettings, value); }
        public bool CanManageUsers { get => _canManageUsers; set => SetProperty(ref _canManageUsers, value); }

        public Action<bool>? CloseWindowAction { get; set; }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        #endregion

        public void LoadForEdit(User user)
        {
            UserId = user.Id;
            FullName = user.FullName;
            Username = user.Username;
            Role = user.Role;
            IsEditor = user.IsEditor;
            IsActive = user.IsActive;
            IsEditMode = true;
            Password = string.Empty; // Keep empty unless updating

            // Decode permissions
            CanAccessRecords = user.Permissions.HasFlag(SystemPermissions.Records);
            CanAccessVerification = user.Permissions.HasFlag(SystemPermissions.Verification);
            CanAccessOwners = user.Permissions.HasFlag(SystemPermissions.Owners);
            CanAccessDeviceTypes = user.Permissions.HasFlag(SystemPermissions.DeviceTypes);
            CanViewReports = user.Permissions.HasFlag(SystemPermissions.Reports);
            CanManageSettings = user.Permissions.HasFlag(SystemPermissions.Settings);
            CanManageUsers = user.Permissions.HasFlag(SystemPermissions.UserManagement);
        }

        private bool CanSave()
        {
            if (_isSaving) return false;

            bool baseValid = !string.IsNullOrWhiteSpace(FullName) && 
                            !string.IsNullOrWhiteSpace(Username);

            if (IsEditMode)
            {
                return baseValid;
            }
            else
            {
                return baseValid && !string.IsNullOrWhiteSpace(Password);
            }
        }

        private async Task SaveAsync()
        {
            if (_isSaving) return;

            // Username validation
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(FullName)) return;

            _isSaving = true;
            (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();

            try
            {
                // Check if username is unique
                var existing = await _userRepository.GetByUsernameAsync(Username);
                if (existing != null && (!IsEditMode || existing.Id != UserId))
                {
                    MessageBox.Show("اسم المستخدم هذا مسجل مسبقاً في النظام. يرجى اختيار اسم مستخدم آخر.", "خطأ في التحقق", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Combine permissions
                SystemPermissions permissions = SystemPermissions.None;
                if (Role != UserRole.Admin)
                {
                    if (CanAccessRecords) permissions |= SystemPermissions.Records;
                    if (CanAccessVerification) permissions |= SystemPermissions.Verification;
                    if (CanAccessOwners) permissions |= SystemPermissions.Owners;
                    if (CanAccessDeviceTypes) permissions |= SystemPermissions.DeviceTypes;
                    if (CanViewReports) permissions |= SystemPermissions.Reports;
                    if (CanManageSettings) permissions |= SystemPermissions.Settings;
                    if (CanManageUsers) permissions |= SystemPermissions.UserManagement;
                }
                else
                {
                    // Admins implicitly have all permissions
                    permissions = SystemPermissions.Records |
                                  SystemPermissions.Verification |
                                  SystemPermissions.Owners |
                                  SystemPermissions.DeviceTypes |
                                  SystemPermissions.Reports |
                                  SystemPermissions.Settings |
                                  SystemPermissions.BackupRestore |
                                  SystemPermissions.UserManagement;
                }

                if (IsEditMode)
                {
                    var user = new User
                    {
                        Id = UserId,
                        FullName = FullName,
                        Username = Username,
                        Role = Role,
                        Permissions = permissions,
                        IsEditor = IsEditor,
                        IsActive = IsActive,
                        PasswordHash = !string.IsNullOrEmpty(Password) ? BCrypt.Net.BCrypt.HashPassword(Password) : string.Empty
                    };
                    await _userRepository.UpdateAsync(user);
                }
                else
                {
                    var user = new User
                    {
                        FullName = FullName,
                        Username = Username,
                        Role = Role,
                        Permissions = permissions,
                        IsEditor = IsEditor,
                        IsActive = IsActive,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                        CreatedAt = DateTime.UtcNow
                    };
                    await _userRepository.AddAsync(user);
                }

                // Close window Action returning true
                CloseWindowAction?.Invoke(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء حفظ المستخدم:\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isSaving = false;
                (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        private void Cancel()
        {
            CloseWindowAction?.Invoke(false);
        }
    }
}
