using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CAL_QR.ViewModels.Base;
using CAL_QR.Models;
using CAL_QR.Repositories;
using CAL_QR.Helpers;

namespace CAL_QR.ViewModels
{
    // ── صفوف DataGrid ───────────────────────────────────────────────────────────

    public class FunctionalCheckRow : INotifyPropertyChanged
    {
        private int _sortOrder;
        private string _checkName = string.Empty;
        private string? _requirement;
        private string? _defaultResult;

        public int SortOrder
        {
            get => _sortOrder;
            set { _sortOrder = value; OnPropertyChanged(); }
        }
        public string CheckName
        {
            get => _checkName;
            set { _checkName = value; OnPropertyChanged(); }
        }
        public string? Requirement
        {
            get => _requirement;
            set { _requirement = value; OnPropertyChanged(); }
        }
        public string? DefaultResult
        {
            get => _defaultResult;
            set { _defaultResult = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    public class UncertaintyComponentRow : INotifyPropertyChanged
    {
        private int _sortOrder;
        private string _componentName = string.Empty;
        private string? _evaluationType;
        private string? _distribution;

        public int SortOrder
        {
            get => _sortOrder;
            set { _sortOrder = value; OnPropertyChanged(); }
        }
        public string ComponentName
        {
            get => _componentName;
            set { _componentName = value; OnPropertyChanged(); }
        }
        public string? EvaluationType
        {
            get => _evaluationType;
            set { _evaluationType = value; OnPropertyChanged(); }
        }
        public string? Distribution
        {
            get => _distribution;
            set { _distribution = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    // ── ViewModel ────────────────────────────────────────────────────────────────

    public class DeviceTypeFormViewModel : BaseViewModel
    {
        private readonly IDeviceTypeRepository _repo;

        private int _deviceTypeId;
        private bool _isEditMode;
        private bool? _dialogResult;

        // الاسم
        private string _name = string.Empty;

        // الحقول النصية
        private string? _procedureNo;
        private string? _calibrationLocation;
        private string? _referenceGeometry;
        private string? _countingTime;
        private string? _countingUnit;
        private string? _calibrationMode;
        private string? _methodologyText;
        private string? _traceabilityReference;
        private string? _complianceVerdict;
        private string? _calibrationStandard;
        private string? _notes;
        private string? _additionalInformation;
        private string? _measurementType;
        private string? _distance;
        private string? _correctedReadingFormula;
        private string? _detectorType;
        private string? _instrumentation;

        // العلمان
        private bool _uncertaintyEnabled;
        private bool _methodologyEnabled;

        // تنبيه: نوع الكتالوج؟
        private bool _isCatalogType;

        public DeviceTypeFormViewModel(IDeviceTypeRepository repo)
        {
            _repo = repo;

            SaveCommand          = new RelayCommand(async () => await SaveAsync(), CanSave);
            CancelCommand        = new RelayCommand(Cancel);
            AddFunctionalCheckCommand   = new RelayCommand(AddFunctionalCheck);
            RemoveFunctionalCheckCommand = new RelayCommand(RemoveFunctionalCheck);
            AddUncertaintyComponentCommand    = new RelayCommand(AddUncertaintyComponent);
            RemoveUncertaintyComponentCommand = new RelayCommand(RemoveUncertaintyComponent);
        }

        // ── خصائص ──────────────────────────────────────────────────────────────

        public int DeviceTypeId     { get => _deviceTypeId;  set => SetProperty(ref _deviceTypeId, value); }
        public bool IsEditMode      { get => _isEditMode;    set => SetProperty(ref _isEditMode, value); }
        public bool? DialogResult   { get => _dialogResult;  set => SetProperty(ref _dialogResult, value); }
        public Action? CloseWindowAction { get; set; }

        public bool IsCatalogType   { get => _isCatalogType; set => SetProperty(ref _isCatalogType, value); }

        public string Name
        {
            get => _name;
            set { if (SetProperty(ref _name, value)) (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged(); }
        }

        public string? ProcedureNo           { get => _procedureNo;           set => SetProperty(ref _procedureNo, value); }
        public string? CalibrationLocation   { get => _calibrationLocation;   set => SetProperty(ref _calibrationLocation, value); }
        public string? ReferenceGeometry     { get => _referenceGeometry;     set => SetProperty(ref _referenceGeometry, value); }
        public string? CountingTime          { get => _countingTime;          set => SetProperty(ref _countingTime, value); }
        public string? CountingUnit          { get => _countingUnit;          set => SetProperty(ref _countingUnit, value); }
        public string? CalibrationMode       { get => _calibrationMode;       set => SetProperty(ref _calibrationMode, value); }
        public string? MethodologyText       { get => _methodologyText;       set => SetProperty(ref _methodologyText, value); }
        public string? TraceabilityReference { get => _traceabilityReference; set => SetProperty(ref _traceabilityReference, value); }
        public string? ComplianceVerdict     { get => _complianceVerdict;     set => SetProperty(ref _complianceVerdict, value); }
        public string? CalibrationStandard   { get => _calibrationStandard;   set => SetProperty(ref _calibrationStandard, value); }
        public string? Notes                 { get => _notes;                 set => SetProperty(ref _notes, value); }
        public string? AdditionalInformation { get => _additionalInformation; set => SetProperty(ref _additionalInformation, value); }
        public string? MeasurementType       { get => _measurementType;       set => SetProperty(ref _measurementType, value); }
        public string? Distance              { get => _distance;              set => SetProperty(ref _distance, value); }
        public string? CorrectedReadingFormula { get => _correctedReadingFormula; set => SetProperty(ref _correctedReadingFormula, value); }
        public string? DetectorType          { get => _detectorType;          set => SetProperty(ref _detectorType, value); }
        public string? Instrumentation       { get => _instrumentation;       set => SetProperty(ref _instrumentation, value); }

        public bool UncertaintyEnabled { get => _uncertaintyEnabled; set => SetProperty(ref _uncertaintyEnabled, value); }
        public bool MethodologyEnabled { get => _methodologyEnabled; set => SetProperty(ref _methodologyEnabled, value); }

        public ObservableCollection<FunctionalCheckRow> FunctionalChecks { get; }
            = new ObservableCollection<FunctionalCheckRow>();

        public ObservableCollection<UncertaintyComponentRow> UncertaintyComponents { get; }
            = new ObservableCollection<UncertaintyComponentRow>();

        public FunctionalCheckRow? SelectedFunctionalCheck { get; set; }
        public UncertaintyComponentRow? SelectedUncertaintyComponent { get; set; }

        // ── أوامر ──────────────────────────────────────────────────────────────

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand AddFunctionalCheckCommand { get; }
        public ICommand RemoveFunctionalCheckCommand { get; }
        public ICommand AddUncertaintyComponentCommand { get; }
        public ICommand RemoveUncertaintyComponentCommand { get; }

        // ── تحميل للتعديل (يُستدعى بعد بناء النافذة) ──────────────────────────

        public async Task LoadForEditAsync(int id)
        {
            DeviceTypeId = id;
            IsEditMode   = true;

            var type = await _repo.GetByIdWithTemplatesAsync(id);
            if (type == null) return;

            Name                   = type.Name;
            ProcedureNo            = type.ProcedureNo;
            CalibrationLocation    = type.CalibrationLocation;
            ReferenceGeometry      = type.ReferenceGeometry;
            CountingTime           = type.CountingTime;
            CountingUnit           = type.CountingUnit;
            CalibrationMode        = type.CalibrationMode;
            MethodologyText        = type.MethodologyText;
            TraceabilityReference  = type.TraceabilityReference;
            ComplianceVerdict      = type.ComplianceVerdict;
            CalibrationStandard    = type.CalibrationStandard;
            Notes                  = type.Notes;
            AdditionalInformation  = type.AdditionalInformation;
            MeasurementType        = type.MeasurementType;
            Distance               = type.Distance;
            CorrectedReadingFormula = type.CorrectedReadingFormula;
            DetectorType           = type.DetectorType;
            Instrumentation        = type.Instrumentation;
            UncertaintyEnabled     = type.UncertaintyEnabled;
            MethodologyEnabled     = type.MethodologyEnabled;

            IsCatalogType = Data.DeviceTypeCatalog.Resolve(type.Name) != null;

            FunctionalChecks.Clear();
            foreach (var fc in type.FunctionalCheckTemplates)
                FunctionalChecks.Add(new FunctionalCheckRow
                {
                    SortOrder     = fc.SortOrder,
                    CheckName     = fc.CheckName,
                    Requirement   = fc.Requirement,
                    DefaultResult = fc.DefaultResult
                });

            UncertaintyComponents.Clear();
            foreach (var uc in type.UncertaintyComponentTemplates)
                UncertaintyComponents.Add(new UncertaintyComponentRow
                {
                    SortOrder      = uc.SortOrder,
                    ComponentName  = uc.ComponentName,
                    EvaluationType = uc.EvaluationType,
                    Distribution   = uc.Distribution
                });
        }

        // ── تحميل للإضافة (يُستدعى عند فتح نموذج جديد) ─────────────────────────

        public void LoadForEdit(DeviceTypeDisplayItem type)
        {
            // المسار القديم: مستدعَى من OpenEditTypeAsync لأنواع غير مُحمَّلة بعد.
            // الآن يُستبدَل بـ LoadForEditAsync — لكن يُبقى للتوافق.
            DeviceTypeId = type.Id;
            Name = type.Name;
            IsEditMode = true;
        }

        // ── أوامر القوائم ────────────────────────────────────────────────────────

        private void AddFunctionalCheck(object? _)
        {
            FunctionalChecks.Add(new FunctionalCheckRow
            {
                SortOrder     = FunctionalChecks.Count + 1,
                CheckName     = string.Empty,
                Requirement   = null,
                DefaultResult = "Acceptable"
            });
        }

        private void RemoveFunctionalCheck(object? parameter)
        {
            if (parameter is FunctionalCheckRow row)
                FunctionalChecks.Remove(row);
            else if (SelectedFunctionalCheck != null)
                FunctionalChecks.Remove(SelectedFunctionalCheck);
        }

        private void AddUncertaintyComponent(object? _)
        {
            UncertaintyComponents.Add(new UncertaintyComponentRow
            {
                SortOrder     = UncertaintyComponents.Count + 1,
                ComponentName = string.Empty,
                EvaluationType = null,
                Distribution   = null
            });
        }

        private void RemoveUncertaintyComponent(object? parameter)
        {
            if (parameter is UncertaintyComponentRow row)
                UncertaintyComponents.Remove(row);
            else if (SelectedUncertaintyComponent != null)
                UncertaintyComponents.Remove(SelectedUncertaintyComponent);
        }

        // ── حفظ ─────────────────────────────────────────────────────────────────

        private bool CanSave() => !string.IsNullOrWhiteSpace(Name);

        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(Name)) return;

            try
            {
                if (IsEditMode)
                {
                    var updated = BuildDeviceType();
                    updated.Id = DeviceTypeId;
                    await _repo.UpdateTemplateAsync(updated);
                }
                else
                {
                    var type = BuildDeviceType();
                    await _repo.AddAsync(type);
                }

                MasterDataEvents.RaiseDeviceTypeAdded();
                DialogResult = true;
                CloseWindowAction?.Invoke();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في حفظ نوع الجهاز: {ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private DeviceType BuildDeviceType()
        {
            var type = new DeviceType
            {
                Name                    = Name.Trim(),
                ProcedureNo             = NullIfEmpty(ProcedureNo),
                CalibrationLocation     = NullIfEmpty(CalibrationLocation),
                ReferenceGeometry       = NullIfEmpty(ReferenceGeometry),
                CountingTime            = NullIfEmpty(CountingTime),
                CountingUnit            = NullIfEmpty(CountingUnit),
                CalibrationMode         = NullIfEmpty(CalibrationMode),
                MethodologyText         = NullIfEmpty(MethodologyText),
                TraceabilityReference   = NullIfEmpty(TraceabilityReference),
                ComplianceVerdict       = NullIfEmpty(ComplianceVerdict),
                CalibrationStandard     = NullIfEmpty(CalibrationStandard),
                Notes                   = NullIfEmpty(Notes),
                AdditionalInformation   = NullIfEmpty(AdditionalInformation),
                MeasurementType         = NullIfEmpty(MeasurementType),
                Distance                = NullIfEmpty(Distance),
                CorrectedReadingFormula = NullIfEmpty(CorrectedReadingFormula),
                DetectorType            = NullIfEmpty(DetectorType),
                Instrumentation         = NullIfEmpty(Instrumentation),
                UncertaintyEnabled      = UncertaintyEnabled,
                MethodologyEnabled      = MethodologyEnabled
            };

            int fcOrder = 1;
            foreach (var row in FunctionalChecks)
                type.FunctionalCheckTemplates.Add(new DeviceTypeFunctionalCheckTemplate
                {
                    SortOrder     = fcOrder++,
                    CheckName     = row.CheckName.Trim(),
                    Requirement   = NullIfEmpty(row.Requirement),
                    DefaultResult = NullIfEmpty(row.DefaultResult)
                });

            int ucOrder = 1;
            foreach (var row in UncertaintyComponents)
                type.UncertaintyComponentTemplates.Add(new DeviceTypeUncertaintyComponentTemplate
                {
                    SortOrder      = ucOrder++,
                    ComponentName  = row.ComponentName.Trim(),
                    EvaluationType = NullIfEmpty(row.EvaluationType),
                    Distribution   = NullIfEmpty(row.Distribution)
                });

            return type;
        }

        private static string? NullIfEmpty(string? s)
            => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        private void Cancel()
        {
            DialogResult = false;
            CloseWindowAction?.Invoke();
        }
    }
}
