using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CAL_QR.ViewModels.Base;
using CAL_QR.Models;
using CAL_QR.Repositories;
using CAL_QR.Services;
using CAL_QR.Validation;

namespace CAL_QR.ViewModels
{
    public class UsersViewModel : BaseViewModel
    {
        private readonly IUserRepository _userRepository;
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly Func<Views.Dialogs.UserFormDialog> _userFormDialogFactory;

        private ObservableCollection<User> _users = new();
        private ObservableCollection<User> _filterUsers = new();
        private ObservableCollection<AuditLog> _logs = new();
        private User? _selectedFilterUser;
        private DateTime? _startDate;
        private DateTime? _endDate;
        private string _searchText = string.Empty;
        private bool _isSaving;

        private readonly ICurrentUserService _currentUserService;

        public UsersViewModel(
            IUserRepository userRepository,
            IAuditLogRepository auditLogRepository,
            Func<Views.Dialogs.UserFormDialog> userFormDialogFactory,
            ICurrentUserService currentUserService)
        {
            _userRepository = userRepository;
            _auditLogRepository = auditLogRepository;
            _userFormDialogFactory = userFormDialogFactory;
            _currentUserService = currentUserService;

            AddUserCommand = new RelayCommand(async () => await OpenAddUserAsync(), () => CanEdit);
            EditUserCommand = new RelayCommand(async (p) => await OpenEditUserAsync(p), (p) => CanEdit);
            ToggleActiveCommand = new RelayCommand(async (p) => await ToggleActiveAsync(p), (p) => CanEdit && !_isSaving);
            FilterCommand = new RelayCommand(async () => await ApplyFiltersAsync());
            ClearCommand = new RelayCommand(async () => await ClearFiltersAsync());

            _ = LoadUsersAsync();
            _ = LoadAuditLogsAsync();
        }

        public bool IsAuditLogTabVisible => _currentUserService.CurrentUser?.Role == UserRole.Admin;
        public bool CanEdit => _currentUserService.CurrentUser != null && 
                               (_currentUserService.CurrentUser.Role == UserRole.Admin || _currentUserService.CurrentUser.IsEditor);

        #region Properties
        public ObservableCollection<User> Users { get => _users; set => SetProperty(ref _users, value); }
        public ObservableCollection<User> FilterUsers { get => _filterUsers; set => SetProperty(ref _filterUsers, value); }
        public ObservableCollection<AuditLog> Logs { get => _logs; set => SetProperty(ref _logs, value); }

        public User? SelectedFilterUser
        {
            get => _selectedFilterUser;
            set
            {
                if (SetProperty(ref _selectedFilterUser, value))
                    _ = ApplyFiltersAsync();
            }
        }

        public DateTime? StartDate
        {
            get => _startDate;
            set
            {
                if (SetProperty(ref _startDate, value))
                    _ = ApplyFiltersAsync();
            }
        }

        public DateTime? EndDate
        {
            get => _endDate;
            set
            {
                if (SetProperty(ref _endDate, value))
                    _ = ApplyFiltersAsync();
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    FilterUsersList();
            }
        }

        public ICommand AddUserCommand { get; }
        public ICommand EditUserCommand { get; }
        public ICommand ToggleActiveCommand { get; }
        public ICommand FilterCommand { get; }
        public ICommand ClearCommand { get; }
        #endregion

        #region Guard Rails (Pure Methods)
        public bool CanFreezeUser(User targetUser, int? currentUserId, int activeAdminsCount)
        {
            if (targetUser == null) return false;

            // Guard 1: Prevent self-freezing
            if (currentUserId.HasValue && targetUser.Id == currentUserId.Value)
            {
                return false;
            }

            // Guard 2: Prevent freezing last active admin
            if (targetUser.Role == UserRole.Admin && targetUser.IsActive && activeAdminsCount <= 1)
            {
                return false;
            }

            return true;
        }
        #endregion

        #region Operations
        public async Task LoadUsersAsync()
        {
            try
            {
                var list = await _userRepository.GetAllAsync();
                Users = new ObservableCollection<User>(list);

                // Populate filter users dropdown
                var dropdown = new List<User> { new User { Id = 0, FullName = "الكل", Username = "" } };
                dropdown.AddRange(list);
                FilterUsers = new ObservableCollection<User>(dropdown);
                
                // Select "All" as default
                if (SelectedFilterUser == null || !FilterUsers.Any(u => u.Id == SelectedFilterUser.Id))
                {
                    SelectedFilterUser = FilterUsers.FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحميل الحسابات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task LoadAuditLogsAsync()
        {
            try
            {
                int? filterUserId = (SelectedFilterUser != null && SelectedFilterUser.Id > 0) ? SelectedFilterUser.Id : null;
                var list = await _auditLogRepository.GetFilteredAsync(StartDate, EndDate, filterUserId);
                for (int i = 0; i < list.Count; i++)
                {
                    list[i].SequenceNumber = i + 1;
                }
                Logs = new ObservableCollection<AuditLog>(list);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحميل سجل العمليات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FilterUsersList()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                _ = LoadUsersAsync();
                return;
            }

            string query = SearchText.Trim().ToLower();
            var filtered = Users.Where(u => 
                u.FullName.ToLower().Contains(query) || 
                u.Username.ToLower().Contains(query)
            ).ToList();

            Users = new ObservableCollection<User>(filtered);
        }

        private async Task OpenAddUserAsync()
        {
            if (!CanEdit) return;
            var dialog = _userFormDialogFactory();
            if (dialog.ShowDialog() == true)
            {
                if (dialog.DataContext is UserFormViewModel created)
                {
                    await LogUserChangeAsync("إضافة مستخدم", null, created.Username,
                        $"إضافة المستخدم {created.Username} بدور {created.Role}.");
                }
                await LoadUsersAsync();
                await LoadAuditLogsAsync();
            }
        }

        private async Task LogUserChangeAsync(string action, int? entityId, string username, string details)
        {
            try
            {
                await _auditLogRepository.LogAsync(
                    action, "User", entityId?.ToString() ?? string.Empty, details,
                    _currentUserService.CurrentUser?.Id,
                    _currentUserService.CurrentUser?.Username);
            }
            catch (Exception ex)
            {
                // فشل التدقيق لا يُبطل عمليّة نجحت؛ يُسجَّل في ملفّ الأخطاء.
                CAL_QR.Services.AppLog.Error($"Audit '{action}' for user {username}", ex);
            }
        }

        private async Task OpenEditUserAsync(object? parameter)
        {
            if (!CanEdit) return;
            if (parameter is not User user) return;

            string? denial = UserManagementRules.CanModifyUser(_currentUserService.CurrentUser?.Role ?? UserRole.Viewer, user.Role);
            if (denial != null)
            {
                MessageBox.Show(denial, "تنبيه الحماية", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                return;
            }

            var dialog = _userFormDialogFactory();
            if (dialog.DataContext is UserFormViewModel vm)
            {
                vm.LoadForEdit(user);
            }

            var roleBefore = user.Role;
            if (dialog.ShowDialog() == true)
            {
                if (dialog.DataContext is UserFormViewModel edited)
                {
                    string roleNote = edited.Role != roleBefore ? $" (الدور: {roleBefore} → {edited.Role})" : string.Empty;
                    string passwordNote = !string.IsNullOrEmpty(edited.Password) ? " مع تغيير كلمة المرور" : string.Empty;
                    await LogUserChangeAsync("تعديل مستخدم", user.Id, edited.Username,
                        $"تعديل بيانات المستخدم {edited.Username}{roleNote}{passwordNote}.");
                }
                await LoadUsersAsync();
                await LoadAuditLogsAsync();
            }
        }

        private async Task ToggleActiveAsync(object? parameter)
        {
            if (!CanEdit) return;
            if (parameter is not User user) return;

            string? denial = UserManagementRules.CanModifyUser(_currentUserService.CurrentUser?.Role ?? UserRole.Viewer, user.Role);
            if (denial != null)
            {
                MessageBox.Show(denial, "تنبيه الحماية", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                return;
            }

            int? currentUserId = _currentUserService.CurrentUser?.Id; 
            int activeAdmins = await _userRepository.GetActiveAdminsCountAsync();

            if (user.IsActive) // Requesting freeze
            {
                if (!CanFreezeUser(user, currentUserId, activeAdmins))
                {
                    if (user.Role == UserRole.Admin && activeAdmins <= 1)
                    {
                        MessageBox.Show("لا يمكن تجميد هذا الحساب لأنه آخر حساب مدير نشط في النظام.", "تنبيه الحماية", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    else
                    {
                        MessageBox.Show("لا يمكنك تجميد حسابك الحالي النشط في النظام.", "تنبيه الحماية", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    return;
                }
            }

            _isSaving = true;
            (ToggleActiveCommand as RelayCommand)?.RaiseCanExecuteChanged();

            try
            {
                user.IsActive = !user.IsActive;
                await _userRepository.UpdateAsync(user);

                string action = user.IsActive ? "تفعيل حساب" : "تجميد حساب";
                await _auditLogRepository.LogAsync(
                    action, 
                    "User", 
                    user.Id.ToString(), 
                    $"تمت عملية {action} للمستخدم {user.Username} بنجاح.",
                    _currentUserService.CurrentUser?.Id,
                    _currentUserService.CurrentUser?.Username);

                await LoadUsersAsync();
                await LoadAuditLogsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تعديل حالة الحساب: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isSaving = false;
                (ToggleActiveCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        private async Task ApplyFiltersAsync()
        {
            await LoadAuditLogsAsync();
        }

        private async Task ClearFiltersAsync()
        {
            _startDate = null;
            _endDate = null;
            
            OnPropertyChanged(nameof(StartDate));
            OnPropertyChanged(nameof(EndDate));

            if (FilterUsers.Any())
            {
                SelectedFilterUser = FilterUsers.First();
            }

            await LoadAuditLogsAsync();
        }
        #endregion
    }
}
