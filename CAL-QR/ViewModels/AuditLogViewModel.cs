using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CAL_QR.ViewModels.Base;
using CAL_QR.Models;
using CAL_QR.Repositories;

namespace CAL_QR.ViewModels
{
    public class AuditLogViewModel : BaseViewModel
    {
        private readonly IAuditLogRepository _auditLogRepository;

        private ObservableCollection<AuditLog> _logs = new();
        private List<string> _actionTypes = new() 
        { 
            "الكل", "إضافة معايرة", "تعديل معايرة", "حذف جهاز", 
            "طباعة ملصق QR", "تصدير تقرير", "نسخ احتياطي", 
            "استعادة نسخة احتياطية", "تغيير إعدادات", "تغيير كلمة المرور" 
        };

        private string _selectedActionType = "الكل";
        private DateTime? _startDate;
        private DateTime? _endDate;
        private List<AuditLog> _allLogs = new();

        public AuditLogViewModel(IAuditLogRepository auditLogRepository)
        {
            _auditLogRepository = auditLogRepository;

            FilterCommand = new RelayCommand(ApplyFilters);
            ClearCommand = new RelayCommand(async () => await ClearFiltersAsync());

            _ = LoadLogsAsync();
        }

        #region Properties
        public ObservableCollection<AuditLog> Logs { get => _logs; set => SetProperty(ref _logs, value); }
        public List<string> ActionTypes => _actionTypes;

        public string SelectedActionType
        {
            get => _selectedActionType;
            set
            {
                if (SetProperty(ref _selectedActionType, value))
                    ApplyFilters();
            }
        }

        public DateTime? StartDate
        {
            get => _startDate;
            set
            {
                if (SetProperty(ref _startDate, value))
                    ApplyFilters();
            }
        }

        public DateTime? EndDate
        {
            get => _endDate;
            set
            {
                if (SetProperty(ref _endDate, value))
                    ApplyFilters();
            }
        }

        public ICommand FilterCommand { get; }
        public ICommand ClearCommand { get; }
        #endregion

        private async Task LoadLogsAsync()
        {
            try
            {
                var list = await _auditLogRepository.GetAllAsync();
                _allLogs = list.ToList();
                ApplyFilters();
            }
            catch
            {
                Logs = new ObservableCollection<AuditLog>();
            }
        }

        private void ApplyFilters()
        {
            var filtered = _allLogs.AsEnumerable();

            if (SelectedActionType != "الكل")
            {
                filtered = filtered.Where(l => l.Action == SelectedActionType);
            }

            if (StartDate.HasValue)
            {
                filtered = filtered.Where(l => l.ActionAt.Date >= StartDate.Value.Date);
            }

            if (EndDate.HasValue)
            {
                filtered = filtered.Where(l => l.ActionAt.Date <= EndDate.Value.Date);
            }

            Logs = new ObservableCollection<AuditLog>(filtered.OrderByDescending(l => l.ActionAt));
        }

        private async Task ClearFiltersAsync()
        {
            _selectedActionType = "الكل";
            _startDate = null;
            _endDate = null;

            OnPropertyChanged(nameof(SelectedActionType));
            OnPropertyChanged(nameof(StartDate));
            OnPropertyChanged(nameof(EndDate));

            await LoadLogsAsync();
        }
    }
}
