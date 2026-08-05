using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Repositories;
using CAL_QR.Services;
using CAL_QR.Validation;
using CAL_QR.ViewModels.Base;

namespace CAL_QR.ViewModels
{
    /// <summary>حدث الإصدار: يحمل الرقم المخصَّص إلى المستدعي ليُعيده إلى السجل.</summary>
    public class CertificateIssuedEventArgs : EventArgs
    {
        public string CertificateNumber { get; }
        public int CalibrationRecordId { get; }

        public CertificateIssuedEventArgs(string certificateNumber, int calibrationRecordId)
        {
            CertificateNumber = certificateNumber;
            CalibrationRecordId = calibrationRecordId;
        }
    }

    /// <summary>
    /// نموذج إنشاء شهادة من سجل معايرة قائم.
    ///
    /// ⚠ لا يولّد رقماً ولا توقيعاً ولا QR ولا DueDate. كل ذلك مسؤولية
    /// CertificateRepository.AddAsync وحدها، وترتيب خطواتها ملزم ولا يُحاكى هنا.
    /// </summary>
    public class CertificateFormViewModel : BaseViewModel
    {
        private readonly IDeviceTypeRepository _deviceTypeRepository;
        private readonly ICertificateDraftBuilder _draftBuilder;
        private readonly ICertificateRepository _certificateRepository;
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;

        private int _calibrationRecordId;
        private bool _isSaving;
        private bool _isLoaded;

        public event EventHandler<CertificateIssuedEventArgs>? CertificateIssued;

        public CertificateFormViewModel(
            IDeviceTypeRepository deviceTypeRepository,
            ICertificateDraftBuilder draftBuilder,
            ICertificateRepository certificateRepository,
            IAuditLogRepository auditLogRepository,
            IDbContextFactory<CalQrDbContext> contextFactory)
        {
            _deviceTypeRepository = deviceTypeRepository;
            _draftBuilder = draftBuilder;
            _certificateRepository = certificateRepository;
            _auditLogRepository = auditLogRepository;
            _contextFactory = contextFactory;

            SaveCommand = new RelayCommand(async () => await SaveAsync(), CanSave);

            AddNuclideSummaryCommand = new RelayCommand(() =>
                NuclideSummaries.Add(new CertificateNuclideSummary()));
            RemoveNuclideSummaryCommand = new RelayCommand(p =>
            {
                if (p is CertificateNuclideSummary row) NuclideSummaries.Remove(row);
            });

            AddCalibrationResultCommand = new RelayCommand(() =>
                CalibrationResults.Add(new CertificateCalibrationResult()));
            RemoveCalibrationResultCommand = new RelayCommand(p =>
            {
                if (p is CertificateCalibrationResult row) CalibrationResults.Remove(row);
            });

            AddUncertaintyComponentCommand = new RelayCommand(() =>
                UncertaintyComponents.Add(new CertificateUncertaintyComponent()));
            RemoveUncertaintyComponentCommand = new RelayCommand(p =>
            {
                if (p is CertificateUncertaintyComponent row) UncertaintyComponents.Remove(row);
            });

            AddFunctionalCheckCommand = new RelayCommand(() =>
                FunctionalChecks.Add(new CertificateFunctionalCheck()));
            RemoveFunctionalCheckCommand = new RelayCommand(p =>
            {
                if (p is CertificateFunctionalCheck row) FunctionalChecks.Remove(row);
            });
        }

        #region ١. بيانات الوثيقة والجهاز

        private string _certificateTemplateType = string.Empty;
        public string CertificateTemplateType
        {
            get => _certificateTemplateType;
            set => SetProperty(ref _certificateTemplateType, value);
        }

        private string _referenceNo = string.Empty;
        public string ReferenceNo
        {
            get => _referenceNo;
            set => SetProperty(ref _referenceNo, value);
        }

        private string _clientName = string.Empty;
        public string ClientName
        {
            get => _clientName;
            set => SetProperty(ref _clientName, value);
        }

        private string _clientAddress = string.Empty;
        public string ClientAddress
        {
            get => _clientAddress;
            set => SetProperty(ref _clientAddress, value);
        }

        private string _deviceModel = string.Empty;
        public string DeviceModel
        {
            get => _deviceModel;
            set => SetProperty(ref _deviceModel, value);
        }

        private string _deviceSerialNumber = string.Empty;
        public string DeviceSerialNumber
        {
            get => _deviceSerialNumber;
            set => SetProperty(ref _deviceSerialNumber, value);
        }

        private string _deviceManufacturer = string.Empty;
        public string DeviceManufacturer
        {
            get => _deviceManufacturer;
            set => SetProperty(ref _deviceManufacturer, value);
        }

        private string _surveyMeterModel = string.Empty;
        public string SurveyMeterModel
        {
            get => _surveyMeterModel;
            set => SetProperty(ref _surveyMeterModel, value);
        }

        private string _surveyMeterSerialNumber = string.Empty;
        public string SurveyMeterSerialNumber
        {
            get => _surveyMeterSerialNumber;
            set => SetProperty(ref _surveyMeterSerialNumber, value);
        }

        private string _procedureNo = string.Empty;
        public string ProcedureNo
        {
            get => _procedureNo;
            set => SetProperty(ref _procedureNo, value);
        }

        private string _calibrationLocation = string.Empty;
        public string CalibrationLocation
        {
            get => _calibrationLocation;
            set => SetProperty(ref _calibrationLocation, value);
        }

        private string _instrumentation = string.Empty;
        public string Instrumentation
        {
            get => _instrumentation;
            set => SetProperty(ref _instrumentation, value);
        }

        private string _detectorType = string.Empty;
        public string DetectorType
        {
            get => _detectorType;
            set => SetProperty(ref _detectorType, value);
        }

        private string _measurementType = string.Empty;
        public string MeasurementType
        {
            get => _measurementType;
            set => SetProperty(ref _measurementType, value);
        }

        private string _distance = string.Empty;
        public string Distance
        {
            get => _distance;
            set => SetProperty(ref _distance, value);
        }

        private string _countingTime = string.Empty;
        public string CountingTime
        {
            get => _countingTime;
            set => SetProperty(ref _countingTime, value);
        }

        private string _countingUnit = string.Empty;
        public string CountingUnit
        {
            get => _countingUnit;
            set => SetProperty(ref _countingUnit, value);
        }

        private string _calibrationMode = string.Empty;
        public string CalibrationMode
        {
            get => _calibrationMode;
            set => SetProperty(ref _calibrationMode, value);
        }

        private string _calibrationStandard = string.Empty;
        public string CalibrationStandard
        {
            get => _calibrationStandard;
            set => SetProperty(ref _calibrationStandard, value);
        }

        private DateTime _calibrationDate = DateTime.Today;
        public DateTime CalibrationDate
        {
            get => _calibrationDate;
            set => SetProperty(ref _calibrationDate, value);
        }

        /// <summary>تاريخ إداري مستقلّ عن IssuedAt (ختم النظام). افتراضه اليوم.</summary>
        private DateTime _issueDate = DateTime.Today;
        public DateTime IssueDate
        {
            get => _issueDate;
            set => SetProperty(ref _issueDate, value);
        }

        #endregion

        #region ٢. الظروف البيئية

        private string _temperature = string.Empty;
        public string Temperature
        {
            get => _temperature;
            set => SetProperty(ref _temperature, value);
        }

        private string _relativeHumidity = string.Empty;
        public string RelativeHumidity
        {
            get => _relativeHumidity;
            set => SetProperty(ref _relativeHumidity, value);
        }

        private string _atmosphericPressure = string.Empty;
        public string AtmosphericPressure
        {
            get => _atmosphericPressure;
            set => SetProperty(ref _atmosphericPressure, value);
        }

        #endregion

        #region ٣. المنهجية (قسم مشروط)

        private bool _methodologyEnabled;
        public bool MethodologyEnabled
        {
            get => _methodologyEnabled;
            set => SetProperty(ref _methodologyEnabled, value);
        }

        private string _radiationSource = string.Empty;
        public string RadiationSource
        {
            get => _radiationSource;
            set => SetProperty(ref _radiationSource, value);
        }

        private string _referenceGeometry = string.Empty;
        public string ReferenceGeometry
        {
            get => _referenceGeometry;
            set => SetProperty(ref _referenceGeometry, value);
        }

        private string _methodologyText = string.Empty;
        public string MethodologyText
        {
            get => _methodologyText;
            set => SetProperty(ref _methodologyText, value);
        }

        private string _traceabilityReference = string.Empty;
        public string TraceabilityReference
        {
            get => _traceabilityReference;
            set => SetProperty(ref _traceabilityReference, value);
        }

        #endregion

        #region ٤. النتائج والنويدات

        public ObservableCollection<CertificateCalibrationResult> CalibrationResults { get; }
            = new ObservableCollection<CertificateCalibrationResult>();

        public ObservableCollection<CertificateNuclideSummary> NuclideSummaries { get; }
            = new ObservableCollection<CertificateNuclideSummary>();

        private string _correctedReadingFormula = string.Empty;
        public string CorrectedReadingFormula
        {
            get => _correctedReadingFormula;
            set => SetProperty(ref _correctedReadingFormula, value);
        }

        private string _complianceVerdict = string.Empty;
        public string ComplianceVerdict
        {
            get => _complianceVerdict;
            set => SetProperty(ref _complianceVerdict, value);
        }

        #endregion

        #region ٥. عدم اليقين (قسم مشروط)

        private bool _uncertaintyEnabled;
        public bool UncertaintyEnabled
        {
            get => _uncertaintyEnabled;
            set => SetProperty(ref _uncertaintyEnabled, value);
        }

        public ObservableCollection<CertificateUncertaintyComponent> UncertaintyComponents { get; }
            = new ObservableCollection<CertificateUncertaintyComponent>();

        private string _combinedUncertainty = string.Empty;
        public string CombinedUncertainty
        {
            get => _combinedUncertainty;
            set => SetProperty(ref _combinedUncertainty, value);
        }

        private string _expandedUncertainty = string.Empty;
        public string ExpandedUncertainty
        {
            get => _expandedUncertainty;
            set => SetProperty(ref _expandedUncertainty, value);
        }

        private string _coverageFactor = string.Empty;
        public string CoverageFactor
        {
            get => _coverageFactor;
            set => SetProperty(ref _coverageFactor, value);
        }

        #endregion

        #region ٦. الفحوص الوظيفية والنصوص الحرة

        public ObservableCollection<CertificateFunctionalCheck> FunctionalChecks { get; }
            = new ObservableCollection<CertificateFunctionalCheck>();

        private string _additionalInformation = string.Empty;
        public string AdditionalInformation
        {
            get => _additionalInformation;
            set => SetProperty(ref _additionalInformation, value);
        }

        private string _notes = string.Empty;
        public string Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        #endregion

        #region ٧. الاعتماد — الموقّعون الأربعة

        private string _calibratedByName = string.Empty;
        public string CalibratedByName
        {
            get => _calibratedByName;
            set => SetProperty(ref _calibratedByName, value);
        }

        private string _calibratedByTitle = string.Empty;
        public string CalibratedByTitle
        {
            get => _calibratedByTitle;
            set => SetProperty(ref _calibratedByTitle, value);
        }

        private DateTime? _calibratedByDate;
        public DateTime? CalibratedByDate
        {
            get => _calibratedByDate;
            set => SetProperty(ref _calibratedByDate, value);
        }

        private string _reviewedByName = string.Empty;
        public string ReviewedByName
        {
            get => _reviewedByName;
            set => SetProperty(ref _reviewedByName, value);
        }

        private string _reviewedByTitle = string.Empty;
        public string ReviewedByTitle
        {
            get => _reviewedByTitle;
            set => SetProperty(ref _reviewedByTitle, value);
        }

        private DateTime? _reviewedByDate;
        public DateTime? ReviewedByDate
        {
            get => _reviewedByDate;
            set => SetProperty(ref _reviewedByDate, value);
        }

        private string _approvedByName = string.Empty;
        public string ApprovedByName
        {
            get => _approvedByName;
            set => SetProperty(ref _approvedByName, value);
        }

        private string _approvedByTitle = string.Empty;
        public string ApprovedByTitle
        {
            get => _approvedByTitle;
            set => SetProperty(ref _approvedByTitle, value);
        }

        private DateTime? _approvedByDate;
        public DateTime? ApprovedByDate
        {
            get => _approvedByDate;
            set => SetProperty(ref _approvedByDate, value);
        }

        private string _authorizedByName = string.Empty;
        public string AuthorizedByName
        {
            get => _authorizedByName;
            set => SetProperty(ref _authorizedByName, value);
        }

        private string _authorizedByTitle = string.Empty;
        public string AuthorizedByTitle
        {
            get => _authorizedByTitle;
            set => SetProperty(ref _authorizedByTitle, value);
        }

        private DateTime? _authorizedByDate;
        public DateTime? AuthorizedByDate
        {
            get => _authorizedByDate;
            set => SetProperty(ref _authorizedByDate, value);
        }

        #endregion

        #region حالة الواجهة

        /// <summary>شريط ظاهر غير حاجب. لا أثر له على إمكان الحفظ.</summary>
        private bool _hasNoTemplateWarning;
        public bool HasNoTemplateWarning
        {
            get => _hasNoTemplateWarning;
            set => SetProperty(ref _hasNoTemplateWarning, value);
        }

        private string _noTemplateWarningText = string.Empty;
        public string NoTemplateWarningText
        {
            get => _noTemplateWarningText;
            set => SetProperty(ref _noTemplateWarningText, value);
        }

        private string _validationErrors = string.Empty;
        public string ValidationErrors
        {
            get => _validationErrors;
            set
            {
                if (SetProperty(ref _validationErrors, value))
                {
                    OnPropertyChanged(nameof(HasValidationErrors));
                }
            }
        }

        public bool HasValidationErrors => !string.IsNullOrEmpty(ValidationErrors);

        public Action? CloseWindowAction { get; set; }

        #endregion

        #region الأوامر

        public ICommand SaveCommand { get; }
        public ICommand AddNuclideSummaryCommand { get; }
        public ICommand RemoveNuclideSummaryCommand { get; }
        public ICommand AddCalibrationResultCommand { get; }
        public ICommand RemoveCalibrationResultCommand { get; }
        public ICommand AddUncertaintyComponentCommand { get; }
        public ICommand RemoveUncertaintyComponentCommand { get; }
        public ICommand AddFunctionalCheckCommand { get; }
        public ICommand RemoveFunctionalCheckCommand { get; }

        #endregion

        /// <summary>
        /// يقرأ السجل والجهاز والمالك والنوع بقوالبه، يبني المسوّدة عبر الخدمة، ويملأ الخصائص.
        /// </summary>
        public void LoadForRecord(int calibrationRecordId)
        {
            _calibrationRecordId = calibrationRecordId;

            try
            {
                CalibrationRecord? record;
                using (var context = _contextFactory.CreateDbContext())
                {
                    record = context.CalibrationRecords
                        .AsNoTracking()
                        .Include(r => r.Device)
                            .ThenInclude(d => d!.Owner)
                        .FirstOrDefault(r => r.Id == calibrationRecordId && !r.IsDeleted);
                }

                if (record == null || record.Device == null || record.Device.Owner == null)
                {
                    ValidationErrors = "تعذّر تحميل سجل المعايرة أو الجهاز أو الجهة المالكة.";
                    return;
                }

                var deviceType = Task
                    .Run(async () => await _deviceTypeRepository.GetByIdWithTemplatesAsync(record.Device.DeviceTypeId))
                    .Result;

                if (deviceType == null)
                {
                    ValidationErrors = "تعذّر تحميل نوع الجهاز.";
                    return;
                }

                var draft = _draftBuilder.Build(deviceType, record, record.Device, record.Device.Owner);
                ApplyDraft(draft);
                LoadDefaultSigners();

                _isLoaded = true;
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحميل بيانات الشهادة: {ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyDraft(CertificateDraftResult draft)
        {
            var c = draft.Certificate;

            CertificateTemplateType = c.CertificateTemplateType ?? string.Empty;
            ReferenceNo = c.ReferenceNo ?? string.Empty;
            ClientName = c.ClientName;
            ClientAddress = c.ClientAddress ?? string.Empty;
            DeviceModel = c.DeviceModel;
            DeviceSerialNumber = c.DeviceSerialNumber;
            DeviceManufacturer = c.DeviceManufacturer ?? string.Empty;
            SurveyMeterModel = c.SurveyMeterModel ?? string.Empty;
            SurveyMeterSerialNumber = c.SurveyMeterSerialNumber ?? string.Empty;
            ProcedureNo = c.ProcedureNo ?? string.Empty;
            CalibrationLocation = c.CalibrationLocation ?? string.Empty;
            Instrumentation = c.Instrumentation ?? string.Empty;
            DetectorType = c.DetectorType ?? string.Empty;
            MeasurementType = c.MeasurementType ?? string.Empty;
            Distance = c.Distance ?? string.Empty;
            CountingTime = c.CountingTime ?? string.Empty;
            CountingUnit = c.CountingUnit ?? string.Empty;
            CalibrationMode = c.CalibrationMode ?? string.Empty;
            CalibrationStandard = c.CalibrationStandard ?? string.Empty;
            CalibrationDate = c.CalibrationDate;
            IssueDate = DateTime.Today;

            Temperature = c.Temperature ?? string.Empty;
            RelativeHumidity = c.RelativeHumidity ?? string.Empty;
            AtmosphericPressure = c.AtmosphericPressure ?? string.Empty;

            MethodologyEnabled = c.MethodologyEnabled;
            RadiationSource = c.RadiationSource ?? string.Empty;
            ReferenceGeometry = c.ReferenceGeometry ?? string.Empty;
            MethodologyText = c.MethodologyText ?? string.Empty;
            TraceabilityReference = c.TraceabilityReference ?? string.Empty;

            CorrectedReadingFormula = c.CorrectedReadingFormula ?? string.Empty;
            ComplianceVerdict = c.ComplianceVerdict ?? string.Empty;

            UncertaintyEnabled = c.UncertaintyEnabled;
            CombinedUncertainty = c.CombinedUncertainty ?? string.Empty;
            ExpandedUncertainty = c.ExpandedUncertainty ?? string.Empty;
            CoverageFactor = c.CoverageFactor ?? string.Empty;

            AdditionalInformation = c.AdditionalInformation ?? string.Empty;
            Notes = c.Notes ?? string.Empty;

            CalibrationResults.Clear();
            foreach (var row in c.CalibrationResults) CalibrationResults.Add(row);

            NuclideSummaries.Clear();
            foreach (var row in c.NuclideSummaries) NuclideSummaries.Add(row);

            UncertaintyComponents.Clear();
            foreach (var row in c.UncertaintyComponents) UncertaintyComponents.Add(row);

            FunctionalChecks.Clear();
            foreach (var row in c.FunctionalChecks) FunctionalChecks.Add(row);

            HasNoTemplateWarning = !draft.HasTemplate;
            NoTemplateWarningText = draft.HasTemplate
                ? string.Empty
                : $"نوع الجهاز «{CertificateTemplateType}» لا يملك قالب شهادة. "
                  + "كل الحقول والجداول أدناه فارغة وتحتاج ملئاً يدوياً كاملاً. "
                  + "يمكنك المتابعة والحفظ، ويمكنك ضبط قالب لهذا النوع لاحقاً من شاشة أنواع الأجهزة.";
        }

        /// <summary>
        /// قيم افتراضية ساكنة من الإعدادات، ظاهرة في الحقول وقابلة للتعديل.
        /// لا اشتقاق من المستخدم المسجَّل دخوله.
        /// </summary>
        private void LoadDefaultSigners()
        {
            Dictionary<string, string> settings;
            using (var context = _contextFactory.CreateDbContext())
            {
                settings = context.AppSettings
                    .AsNoTracking()
                    .Where(s => s.Key.StartsWith("Default") && s.Key.Contains("By"))
                    .ToDictionary(s => s.Key, s => s.Value);
            }

            string Get(string key) => settings.TryGetValue(key, out var value) ? value ?? string.Empty : string.Empty;

            CalibratedByName = Get("DefaultCalibratedByName");
            CalibratedByTitle = Get("DefaultCalibratedByTitle");
            ReviewedByName = Get("DefaultReviewedByName");
            ReviewedByTitle = Get("DefaultReviewedByTitle");
            ApprovedByName = Get("DefaultApprovedByName");
            ApprovedByTitle = Get("DefaultApprovedByTitle");
            AuthorizedByName = Get("DefaultAuthorizedByName");
            AuthorizedByTitle = Get("DefaultAuthorizedByTitle");
        }

        private bool CanSave() => _isLoaded && !_isSaving && _calibrationRecordId > 0;

        private async Task SaveAsync()
        {
            if (_isSaving) return;
            _isSaving = true;
            CommandManager.InvalidateRequerySuggested();

            try
            {
                ValidationErrors = string.Empty;

                // نفس التوقيع حرفياً كما يستدعيه CertificateRepository.AddAsync —
                // ثلاثة وسائط بلا existingCertificateNumber. أي اختلاف كان سينتج
                // رسالتين متناقضتين على نفس التاريخ.
                var validation = CertificateDateRules.Validate(
                    CalibrationDate,
                    IssueDate,
                    DateTime.Today);

                if (!validation.IsValid)
                {
                    ValidationErrors = validation.ErrorMessage ?? "تاريخ غير صالح.";
                    return;
                }

                var certificate = BuildCertificateFromForm();

                // ١. الإصدار: الرقم والتوقيع والحمولة و DueDate كلها من AddAsync.
                string number = await _certificateRepository.AddAsync(certificate);

                // ٢. طور ما بعد الـCommit — خارج أي معاملة، وفشله لا يُبطل الإصدار.
                string postCommitError = string.Empty;
                try
                {
                    await _auditLogRepository.LogAsync(
                        "إصدار شهادة",
                        "Certificate",
                        certificate.Id.ToString(),
                        $"إصدار شهادة رقم {number} لسجل المعايرة {_calibrationRecordId}");
                }
                catch (Exception ex)
                {
                    postCommitError = $"\n- خطأ تسجيل العمليات (Audit Log): {ex.Message}";
                }

                // ٣. إعادة الرقم إلى المستدعي.
                CertificateIssued?.Invoke(this, new CertificateIssuedEventArgs(number, _calibrationRecordId));

                // ٤. رسالة النجاح ثم الإغلاق.
                if (postCommitError.Length > 0)
                {
                    MessageBox.Show(
                        $"تم إصدار الشهادة بنجاح برقم {number}، ولكن حدث خطأ بعد الحفظ:{postCommitError}"
                        + "\n\n(لا تحتاج لإعادة الإصدار — الشهادة مسجلة بنجاح).",
                        "تحذير - فشل جزئي بعد الحفظ", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    MessageBox.Show($"تم إصدار الشهادة بنجاح برقم {number}.", "تم الإصدار",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }

                CloseWindowAction?.Invoke();
            }
            catch (Exception ex)
            {
                string message = ex.Message;
                if (ex.InnerException != null)
                {
                    message += $"\nتفاصيل إضافية: {ex.InnerException.Message}";
                }
                MessageBox.Show($"خطأ أثناء إصدار الشهادة: {message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isSaving = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        /// <summary>
        /// إعادة تجميع الكائن من الخصائص. الحقول النصية الاختيارية تعود null لا ""،
        /// لأن نص التوقيع يميّز بينهما ولأن NULL هي «لا قيمة» في المخطط.
        /// SortOrder يُعاد ترقيمه هنا: المستخدم قد يكون أضاف أو حذف صفوفاً.
        /// </summary>
        private Certificate BuildCertificateFromForm()
        {
            var certificate = new Certificate
            {
                CalibrationRecordId = _calibrationRecordId,
                CertificateTemplateType = Nullify(CertificateTemplateType),
                ReferenceNo = Nullify(ReferenceNo),

                ClientName = ClientName.Trim(),
                ClientAddress = Nullify(ClientAddress),
                DeviceModel = DeviceModel.Trim(),
                DeviceSerialNumber = DeviceSerialNumber.Trim(),
                DeviceManufacturer = Nullify(DeviceManufacturer),
                SurveyMeterModel = Nullify(SurveyMeterModel),
                SurveyMeterSerialNumber = Nullify(SurveyMeterSerialNumber),

                ProcedureNo = Nullify(ProcedureNo),
                CalibrationLocation = Nullify(CalibrationLocation),
                Instrumentation = Nullify(Instrumentation),
                DetectorType = Nullify(DetectorType),
                MeasurementType = Nullify(MeasurementType),
                Distance = Nullify(Distance),
                CountingTime = Nullify(CountingTime),
                CountingUnit = Nullify(CountingUnit),
                CalibrationMode = Nullify(CalibrationMode),
                CalibrationStandard = Nullify(CalibrationStandard),

                Temperature = Nullify(Temperature),
                RelativeHumidity = Nullify(RelativeHumidity),
                AtmosphericPressure = Nullify(AtmosphericPressure),

                MethodologyEnabled = MethodologyEnabled,
                RadiationSource = Nullify(RadiationSource),
                ReferenceGeometry = Nullify(ReferenceGeometry),
                MethodologyText = Nullify(MethodologyText),
                TraceabilityReference = Nullify(TraceabilityReference),

                CorrectedReadingFormula = Nullify(CorrectedReadingFormula),
                ComplianceVerdict = Nullify(ComplianceVerdict),

                UncertaintyEnabled = UncertaintyEnabled,
                CombinedUncertainty = Nullify(CombinedUncertainty),
                ExpandedUncertainty = Nullify(ExpandedUncertainty),
                CoverageFactor = Nullify(CoverageFactor),

                AdditionalInformation = Nullify(AdditionalInformation),
                Notes = Nullify(Notes),

                CalibrationDate = CalibrationDate.Date,
                IssueDate = IssueDate.Date,

                CalibratedByName = Nullify(CalibratedByName),
                CalibratedByTitle = Nullify(CalibratedByTitle),
                CalibratedByDate = CalibratedByDate,
                ReviewedByName = Nullify(ReviewedByName),
                ReviewedByTitle = Nullify(ReviewedByTitle),
                ReviewedByDate = ReviewedByDate,
                ApprovedByName = Nullify(ApprovedByName),
                ApprovedByTitle = Nullify(ApprovedByTitle),
                ApprovedByDate = ApprovedByDate,
                AuthorizedByName = Nullify(AuthorizedByName),
                AuthorizedByTitle = Nullify(AuthorizedByTitle),
                AuthorizedByDate = AuthorizedByDate
            };

            int order = 0;
            foreach (var row in NuclideSummaries)
            {
                row.SortOrder = order++;
                certificate.NuclideSummaries.Add(row);
            }

            order = 0;
            foreach (var row in CalibrationResults)
            {
                row.SortOrder = order++;
                certificate.CalibrationResults.Add(row);
            }

            order = 0;
            foreach (var row in UncertaintyComponents)
            {
                row.SortOrder = order++;
                certificate.UncertaintyComponents.Add(row);
            }

            order = 0;
            foreach (var row in FunctionalChecks)
            {
                row.SortOrder = order++;
                certificate.FunctionalChecks.Add(row);
            }

            return certificate;
        }

        private static string? Nullify(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
