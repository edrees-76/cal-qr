using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using CAL_QR.ViewModels.Base;
using CAL_QR.Models;
using CAL_QR.Repositories;
using CAL_QR.Data;
using CAL_QR.Helpers;
using CAL_QR.Services;

namespace CAL_QR.ViewModels
{
    public class CalibrationFormViewModel : BaseViewModel
    {
        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;
        private readonly IOwnerRepository _ownerRepository;
        private readonly IDeviceTypeRepository _deviceTypeRepository;
        private readonly IDeviceRepository _deviceRepository;
        private readonly ICalibrationRepository _calibrationRepository;
        private readonly IAttachmentRepository _attachmentRepository;
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly Func<Views.Dialogs.CertificateFormDialog> _certificateFormDialogFactory;

        // Form Fields
        private int _deviceId;
        private int _calibrationRecordId;
        private bool _isEditMode;

        // يُلتقط مرّة واحدة وقت التحميل: هل جاء هذا السجل من القاعدة وله رقم شهادة
        // مكتوب يدوياً؟ (سجل مُرحَّل). لا يُشتقّ من IsEditMode، لأن السجل الجديد يصير
        // IsEditMode = true بعد أول حفظ وهو ليس مُرحَّلاً.
        private bool _hasLegacyCertificateNumber;

        private bool _isSyncingOwnerSelection;
        private bool _isSyncingDeviceTypeSelection;
        private string _ownerText = string.Empty;
        private string _deviceTypeText = string.Empty;

        private Owner? _selectedOwner;
        private DeviceType? _selectedDeviceType;
        private string _model = string.Empty;
        private string _serialNumber = string.Empty;
        private string _certificateNumber = string.Empty;
        private DateTime _calibrationDate = DateTime.Today;
        private DateTime _expiryDate = DateTime.Today.AddYears(1);
        private string _engineerName = string.Empty;
        private string _result = "Passed";
        private string _description = string.Empty;

        // AutoComplete and List sources
        private ObservableCollection<Owner> _owners = new();
        private ObservableCollection<DeviceType> _deviceTypes = new();
        private ObservableCollection<string> _engineers = new();
        private ObservableCollection<AttachmentItem> _attachments = new();

        // Warning and validation
        private bool _isSaving;
        private string _serialNumberWarning = string.Empty;
        private string _validationErrors = string.Empty;

        public event EventHandler? Saved;

        /// <remarks>
        /// لا IHmacService ولا IQrService هنا — عن قصد. التوقيع القديم ورمز الـQR
        /// على مستوى سجل المعايرة أُوقفا: الشهادة صاحبة التوقيع والرمز، وتُصدرهما
        /// CertificateRepository.AddAsync وحدها. إبقاء التبعيتين مُحقَنتين بلا
        /// استعمال كان سيوحي بأن المسار حيّ، ويُنتج تحذير CS0414 يخالف سياسة
        /// «صفر تحذيرات».
        /// </remarks>
        public CalibrationFormViewModel(
            IDbContextFactory<CalQrDbContext> contextFactory,
            IOwnerRepository ownerRepository,
            IDeviceTypeRepository deviceTypeRepository,
            IDeviceRepository deviceRepository,
            ICalibrationRepository calibrationRepository,
            IAttachmentRepository attachmentRepository,
            IAuditLogRepository auditLogRepository,
            Func<Views.Dialogs.CertificateFormDialog> certificateFormDialogFactory)
        {
            _contextFactory = contextFactory;
            _ownerRepository = ownerRepository;
            _deviceTypeRepository = deviceTypeRepository;
            _deviceRepository = deviceRepository;
            _calibrationRepository = calibrationRepository;
            _attachmentRepository = attachmentRepository;
            _auditLogRepository = auditLogRepository;
            _certificateFormDialogFactory = certificateFormDialogFactory;

            SaveCommand = new RelayCommand(async () => await SaveAsync(), CanSave);
            AddAttachmentCommand = new RelayCommand(AddAttachment);
            RemoveAttachmentCommand = new RelayCommand(RemoveAttachment);
            IssueCertificateCommand = new RelayCommand(IssueCertificate, () => CanIssueCertificate);
            
            LoadFormSources();
        }

        #region Properties
        public string OwnerText
        {
            get => _ownerText;
            set
            {
                if (SetProperty(ref _ownerText, value))
                {
                    if (!_isSyncingOwnerSelection)
                    {
                        _isSyncingOwnerSelection = true;
                        try
                        {
                            var matched = Owners.FirstOrDefault(o => o.Name.Trim().Equals(value.Trim(), StringComparison.OrdinalIgnoreCase));
                            SelectedOwner = matched;
                        }
                        finally
                        {
                            _isSyncingOwnerSelection = false;
                        }
                    }
                }
            }
        }

        public string DeviceTypeText
        {
            get => _deviceTypeText;
            set
            {
                if (SetProperty(ref _deviceTypeText, value))
                {
                    if (!_isSyncingDeviceTypeSelection)
                    {
                        _isSyncingDeviceTypeSelection = true;
                        try
                        {
                            var matched = DeviceTypes.FirstOrDefault(t => t.Name.Trim().Equals(value.Trim(), StringComparison.OrdinalIgnoreCase));
                            SelectedDeviceType = matched;
                        }
                        finally
                        {
                            _isSyncingDeviceTypeSelection = false;
                        }
                    }
                }
            }
        }

        public Owner? SelectedOwner
        {
            get => _selectedOwner;
            set
            {
                if (SetProperty(ref _selectedOwner, value))
                {
                    if (!_isSyncingOwnerSelection)
                    {
                        _isSyncingOwnerSelection = true;
                        try
                        {
                            OwnerText = value?.Name ?? string.Empty;
                        }
                        finally
                        {
                            _isSyncingOwnerSelection = false;
                        }
                    }
                }
            }
        }

        public DeviceType? SelectedDeviceType
        {
            get => _selectedDeviceType;
            set
            {
                if (SetProperty(ref _selectedDeviceType, value))
                {
                    if (!_isSyncingDeviceTypeSelection)
                    {
                        _isSyncingDeviceTypeSelection = true;
                        try
                        {
                            DeviceTypeText = value?.Name ?? string.Empty;
                        }
                        finally
                        {
                            _isSyncingDeviceTypeSelection = false;
                        }
                    }
                }
            }
        }

        public string Model
        {
            get => _model;
            set => SetProperty(ref _model, value);
        }

        public string SerialNumber
        {
            get => _serialNumber;
            set
            {
                if (SetProperty(ref _serialNumber, value))
                {
                    CheckSerialNumberDuplicate();
                }
            }
        }

        public string CertificateNumber
        {
            get => _certificateNumber;
            set => SetProperty(ref _certificateNumber, value);
        }

        public DateTime CalibrationDate
        {
            get => _calibrationDate;
            set => SetProperty(ref _calibrationDate, value);
        }

        public DateTime ExpiryDate
        {
            get => _expiryDate;
            set => SetProperty(ref _expiryDate, value);
        }

        public string EngineerName
        {
            get => _engineerName;
            set => SetProperty(ref _engineerName, value);
        }

        public string SelectedResult
        {
            get => _result;
            set => SetProperty(ref _result, value);
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public ObservableCollection<Owner> Owners
        {
            get => _owners;
            set => SetProperty(ref _owners, value);
        }

        public ObservableCollection<DeviceType> DeviceTypes
        {
            get => _deviceTypes;
            set => SetProperty(ref _deviceTypes, value);
        }

        public ObservableCollection<string> Engineers
        {
            get => _engineers;
            set => SetProperty(ref _engineers, value);
        }

        public ObservableCollection<AttachmentItem> Attachments
        {
            get => _attachments;
            set => SetProperty(ref _attachments, value);
        }

        public string SerialNumberWarning
        {
            get => _serialNumberWarning;
            set
            {
                if (SetProperty(ref _serialNumberWarning, value))
                {
                    OnPropertyChanged(nameof(HasSerialNumberWarning));
                }
            }
        }

        public bool HasSerialNumberWarning => !string.IsNullOrEmpty(SerialNumberWarning);

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

        public bool IsEditMode
        {
            get => _isEditMode;
            set
            {
                if (SetProperty(ref _isEditMode, value))
                {
                    OnPropertyChanged(nameof(FormTitle));
                    OnPropertyChanged(nameof(FormSubtitle));
                }
            }
        }

        public string FormTitle => IsEditMode ? "تعديل سجل معايرة" : "سجل معايرة جهاز";

        public string FormSubtitle => IsEditMode ? "تعديل بيانات المعايرة" : "إدخال بيانات المعايرة";

        /// <summary>
        /// وجود شهادة غير محذوفة لهذا السجل. الفهرس الفريد المشروط على
        /// CalibrationRecordId يمنع الثانية على مستوى القاعدة، فالمنع هنا واجهة لا حماية.
        /// </summary>
        private bool _hasCertificate;
        public bool HasCertificate
        {
            get => _hasCertificate;
            set
            {
                if (SetProperty(ref _hasCertificate, value))
                {
                    OnPropertyChanged(nameof(CanIssueCertificate));
                    RaiseCertificateNumberStateChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        /// <summary>
        /// الإصدار يقتضي سجلاً محفوظاً: الشهادة تحمل CalibrationRecordId غير قابل للإلغاء،
        /// وفي وضع الإضافة لا وجود للسجل بعد.
        /// </summary>
        public bool CanIssueCertificate => IsEditMode && _calibrationRecordId > 0 && !HasCertificate;

        /// <summary>
        /// ثلاث حالات لحقل رقم الشهادة، لا حالة واحدة:
        ///   ١. سجل جديد بلا شهادة  ⇒ للقراءة فقط، الرقم يُولَّد عند الإصدار.
        ///   ٢. سجل له شهادة صادرة  ⇒ مقفل، الرقم من CertificateRepository.AddAsync.
        ///   ٣. سجل مُرحَّل بترقيم ورقي سابق ⇒ قابل للتحرير كما كان.
        ///
        /// HasCertificate تسبق في الأولوية: سجل مُرحَّل صدرت له شهادة لاحقاً يُقفل
        /// رقمه، وإلا لأمكن دهس رقم مولَّد بكتابة يدوية.
        /// </summary>
        public bool IsCertificateNumberReadOnly => HasCertificate || !_hasLegacyCertificateNumber;

        /// <summary>الإلزام للمُرحَّل وحده — الرقم المولَّد لا يُطالَب به المستخدم.</summary>
        public bool RequiresCertificateNumber => !HasCertificate && _hasLegacyCertificateNumber;

        /// <summary>
        /// نصّ نائب للعرض وحده. يُمرَّر إلى HintAssist لا إلى Text، فلا يُكتب في
        /// CertificateNumber: الخاصّية تبقى فارغة حرفياً حتى يُخصَّص رقم حقيقي.
        /// </summary>
        public string CertificateNumberHint =>
            IsCertificateNumberReadOnly && !HasCertificate ? "يُولَّد آلياً عند الإصدار" : string.Empty;

        /// <summary>النجمة تتبع الإلزام الفعلي بدل أن تكون ساكنة تكذب على المستخدم.</summary>
        public string CertificateNumberLabel =>
            RequiresCertificateNumber ? "رقم الشهادة *" : "رقم الشهادة";

        private void RaiseCertificateNumberStateChanged()
        {
            OnPropertyChanged(nameof(IsCertificateNumberReadOnly));
            OnPropertyChanged(nameof(RequiresCertificateNumber));
            OnPropertyChanged(nameof(CertificateNumberHint));
            OnPropertyChanged(nameof(CertificateNumberLabel));
        }
        #endregion

        #region Commands
        public ICommand SaveCommand { get; }
        public ICommand AddAttachmentCommand { get; }
        public ICommand RemoveAttachmentCommand { get; }
        public ICommand IssueCertificateCommand { get; }
        #endregion

        private void IssueCertificate()
        {
            if (!CanIssueCertificate) return;

            try
            {
                var dialog = _certificateFormDialogFactory();
                dialog.ViewModel.CertificateIssued += OnCertificateIssued;
                try
                {
                    dialog.LoadForRecord(_calibrationRecordId);
                    dialog.Owner = Application.Current?.Windows
                        .OfType<Window>()
                        .FirstOrDefault(w => w.IsActive);
                    dialog.ShowDialog();
                }
                finally
                {
                    dialog.ViewModel.CertificateIssued -= OnCertificateIssued;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في فتح نموذج الشهادة: {ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnCertificateIssued(object? sender, CertificateIssuedEventArgs e)
        {
            CertificateNumber = e.CertificateNumber;
            HasCertificate = true;
            Saved?.Invoke(this, EventArgs.Empty);
            CloseWindowAction?.Invoke();
        }

        private void LoadFormSources()
        {
            try
            {
                var owners = Task.Run(async () => await _ownerRepository.GetAllAsync()).Result;
                Owners = new ObservableCollection<Owner>(owners);

                var types = Task.Run(async () => await _deviceTypeRepository.GetAllAsync()).Result;
                DeviceTypes = new ObservableCollection<DeviceType>(types);

                using (var context = _contextFactory.CreateDbContext())
                {
                    var engineers = context.CalibrationRecords
                        .AsNoTracking()
                        .Select(r => r.EngineerName)
                        .Distinct()
                        .Where(name => !string.IsNullOrEmpty(name))
                        .ToList();
                    Engineers = new ObservableCollection<string>(engineers);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحميل مصادر النموذج: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CheckSerialNumberDuplicate()
        {
            SerialNumberWarning = string.Empty;
            if (string.IsNullOrWhiteSpace(SerialNumber) || _isEditMode) return;

            Task.Run(async () =>
            {
                var existing = await _deviceRepository.GetBySerialNumberAsync(SerialNumber.Trim());
                if (existing != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        SerialNumberWarning = $"تنبيه: هذا الرقم التسلسلي مسجل مسبقاً لجهاز {existing.Model} تابع لجهة {existing.Owner?.Name}. سيتم ربط المعايرة به.";
                        _deviceId = existing.Id;
                        SelectedOwner = Owners.FirstOrDefault(o => o.Id == existing.OwnerId);
                        SelectedDeviceType = DeviceTypes.FirstOrDefault(t => t.Id == existing.DeviceTypeId);
                        Model = existing.Model;
                    });
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _deviceId = 0;
                    });
                }
            });
        }

        public void LoadForEdit(int recordId)
        {
            IsEditMode = true;
            _calibrationRecordId = recordId;

            try
            {
                using (var context = _contextFactory.CreateDbContext())
                {
                    var record = context.CalibrationRecords
                        .Include(r => r.Device)
                            .ThenInclude(d => d!.Owner)
                        .Include(r => r.Device)
                            .ThenInclude(d => d!.DeviceType)
                        .Include(r => r.Attachments)
                        .FirstOrDefault(r => r.Id == recordId);

                    if (record != null && record.Device != null)
                    {
                        _deviceId = record.DeviceId;
                        SelectedOwner = Owners.FirstOrDefault(o => o.Id == record.Device.OwnerId);
                        SelectedDeviceType = DeviceTypes.FirstOrDefault(t => t.Id == record.Device.DeviceTypeId);
                        Model = record.Device.Model;
                        SerialNumber = record.Device.SerialNumber;
                        
                        CertificateNumber = record.CertificateNumber;

                        // الالتقاط من القيمة المخزَّنة وقت التحميل لا من حالة تُضبط
                        // لاحقاً: رقم مكتوب في القاعدة ⇒ سجل مُرحَّل ⇒ الحقل يبقى
                        // قابلاً للتحرير وإلزامياً كما كان قبل ميزة الشهادة.
                        _hasLegacyCertificateNumber = !string.IsNullOrWhiteSpace(record.CertificateNumber);

                        CalibrationDate = record.CalibrationDate;
                        ExpiryDate = record.ExpiryDate;
                        EngineerName = record.EngineerName;
                        SelectedResult = record.Result;
                        Description = record.CalibrationDescription ?? string.Empty;

                        Attachments.Clear();
                        foreach (var att in record.Attachments)
                        {
                            Attachments.Add(new AttachmentItem
                            {
                                Id = att.Id,
                                FileName = att.FileName,
                                FullPath = att.FilePath,
                                IsSaved = true
                            });
                        }
                    }

                    // استعلام وجود مباشر: يبقى سطح ICertificateRepository بلا توسيع،
                    // ويطابق أسلوب هذا الملف في القراءات المحلية.
                    HasCertificate = context.Certificates
                        .AsNoTracking()
                        .Any(c => c.CalibrationRecordId == recordId && !c.IsDeleted);
                }

                OnPropertyChanged(nameof(CanIssueCertificate));
                RaiseCertificateNumberStateChanged();
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحميل سجل المعايرة للتعديل: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void LoadForDeviceOnly(int deviceId)
        {
            IsEditMode = false;
            _deviceId = deviceId;
            _calibrationRecordId = 0;

            try
            {
                var device = Task.Run(async () => await _deviceRepository.GetByIdAsync(deviceId)).Result;
                if (device != null)
                {
                    SelectedOwner = Owners.FirstOrDefault(o => o.Id == device.OwnerId);
                    SelectedDeviceType = DeviceTypes.FirstOrDefault(t => t.Id == device.DeviceTypeId);
                    Model = device.Model;
                    SerialNumber = device.SerialNumber;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحميل بيانات الجهاز: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanSave()
        {
            if (_isSaving) return false;

            return (!string.IsNullOrWhiteSpace(OwnerText) || SelectedOwner != null) &&
                   (!string.IsNullOrWhiteSpace(DeviceTypeText) || SelectedDeviceType != null) &&
                   !string.IsNullOrWhiteSpace(Model) &&
                   !string.IsNullOrWhiteSpace(SerialNumber) &&
                   (!RequiresCertificateNumber || !string.IsNullOrWhiteSpace(CertificateNumber)) &&
                   !string.IsNullOrWhiteSpace(EngineerName);
        }

        private async Task SaveAsync()
        {
            if (_isSaving) return;
            _isSaving = true;
            CommandManager.InvalidateRequerySuggested();

            // يُلتقط قبل أي ضبط للحالة: طور ما بعد الـCommit يضبط IsEditMode = true،
            // فقراءته بعد ذلك كانت ستُعيد true دائماً ويصير الشرط عاطلاً.
            bool wasNewRecord = !IsEditMode;

            try
            {
                ValidationErrors = string.Empty;

            if (ExpiryDate <= CalibrationDate)
            {
                ValidationErrors = "تنبيه: يجب أن يكون تاريخ الانتهاء بعد تاريخ المعايرة.";
                return;
            }

            if (string.IsNullOrWhiteSpace(OwnerText) && SelectedOwner == null)
            {
                ValidationErrors = "تنبيه: يجب إدخال اسم الجهة المالكة.";
                return;
            }

            if (string.IsNullOrWhiteSpace(DeviceTypeText) && SelectedDeviceType == null)
            {
                ValidationErrors = "تنبيه: يجب إدخال نوع الجهاز.";
                return;
            }

            // الفراغ وحده يتخطّى فحص التفرّد — نظير الفلتر على الفهرس تماماً. أي رقم
            // غير فارغ يبقى محروساً كما كان. بلا هذا الشرط كان ثاني سجل جديد يُرفض
            // برسالة «رقم الشهادة مسجل مسبقاً» لأن فراغه يطابق فراغ سابقه.
            if (!string.IsNullOrWhiteSpace(CertificateNumber))
            {
                using (var context = await _contextFactory.CreateDbContextAsync())
                {
                    var dupCert = await context.CalibrationRecords
                        .AnyAsync(r => r.CertificateNumber == CertificateNumber.Trim() && r.Id != _calibrationRecordId && !r.IsDeleted);
                    if (dupCert)
                    {
                        ValidationErrors = "تنبيه: رقم الشهادة مسجل مسبقاً بسجل آخر. يرجى إدخال رقم شهادة فريد.";
                        return;
                    }
                }
            }

            try
            {
                Owner finalOwner = null!;
                DeviceType finalType = null!;
                bool ownerAdded = false;
                bool typeAdded = false;
                string realSignature = string.Empty;

                using (var context = await _contextFactory.CreateDbContextAsync())
                using (var transaction = await context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        // 1. Process Owner
                        if (SelectedOwner != null)
                        {
                            finalOwner = await context.Owners.FindAsync(SelectedOwner.Id) 
                                         ?? throw new InvalidOperationException("الجهة المالكة المحددة غير موجودة.");
                        }
                        else
                        {
                            string ownerNameTrim = OwnerText.Trim();
                            var existing = await context.Owners.FirstOrDefaultAsync(o => o.Name.ToLower() == ownerNameTrim.ToLower());
                            if (existing != null)
                            {
                                if (existing.IsDeleted)
                                {
                                    // Reincarnate
                                    existing.IsDeleted = false;
                                    context.Owners.Update(existing);
                                    await context.SaveChangesAsync();
                                    ownerAdded = true;
                                }
                                finalOwner = existing;
                            }
                            else
                            {
                                finalOwner = new Owner
                                {
                                    Name = ownerNameTrim,
                                    CreatedAt = DateTime.UtcNow,
                                    IsDeleted = false
                                };
                                context.Owners.Add(finalOwner);
                                await context.SaveChangesAsync();
                                ownerAdded = true;
                            }
                        }

                        // 2. Process DeviceType
                        if (SelectedDeviceType != null)
                        {
                            finalType = await context.DeviceTypes.FindAsync(SelectedDeviceType.Id)
                                        ?? throw new InvalidOperationException("نوع الجهاز المحدد غير موجود.");
                        }
                        else
                        {
                            string typeNameTrim = DeviceTypeText.Trim();
                            var existing = await context.DeviceTypes.FirstOrDefaultAsync(t => t.Name.ToLower() == typeNameTrim.ToLower());
                            if (existing != null)
                            {
                                if (existing.IsDeleted)
                                {
                                    // Reincarnate
                                    existing.IsDeleted = false;
                                    existing.CreatedAt = DateTime.UtcNow;
                                    context.DeviceTypes.Update(existing);
                                    await context.SaveChangesAsync();
                                    typeAdded = true;
                                }
                                finalType = existing;
                            }
                            else
                            {
                                finalType = new DeviceType
                                {
                                    Name = typeNameTrim,
                                    CreatedAt = DateTime.UtcNow,
                                    IsDeleted = false
                                };
                                context.DeviceTypes.Add(finalType);
                                await context.SaveChangesAsync();
                                typeAdded = true;
                            }
                        }

                        // 3. Process Device
                        Device device;
                        if (_deviceId > 0)
                        {
                            device = await context.Devices.FirstOrDefaultAsync(d => d.Id == _deviceId && !d.IsDeleted)
                                     ?? throw new InvalidOperationException("الجهاز المحدد غير موجود.");
                            device.OwnerId = finalOwner.Id;
                            device.DeviceTypeId = finalType.Id;
                            device.Model = Model.Trim();
                            device.SerialNumber = SerialNumber.Trim();
                            context.Devices.Update(device);
                        }
                        else
                        {
                            var existingDevice = await context.Devices.FirstOrDefaultAsync(d => d.SerialNumber.ToLower() == SerialNumber.Trim().ToLower() && !d.IsDeleted);
                            if (existingDevice != null)
                            {
                                device = existingDevice;
                                device.OwnerId = finalOwner.Id;
                                device.DeviceTypeId = finalType.Id;
                                device.Model = Model.Trim();
                                context.Devices.Update(device);
                            }
                            else
                            {
                                device = new Device
                                {
                                    OwnerId = finalOwner.Id,
                                    DeviceTypeId = finalType.Id,
                                    Model = Model.Trim(),
                                    SerialNumber = SerialNumber.Trim(),
                                    CreatedAt = DateTime.UtcNow,
                                    IsDeleted = false
                                };
                                context.Devices.Add(device);
                            }
                        }
                        await context.SaveChangesAsync();
                        _deviceId = device.Id;

                        // 4. Process Calibration Record
                        //
                        // لا توقيع HMAC على مستوى السجل. التوقيع مسؤولية الشهادة وحدها
                        // (SignaturePayloadBuilderV1 عبر CertificateRepository.AddAsync)،
                        // وكل سجل جديد يتطلّب شهادة بالقرار المحوريّ.
                        //
                        // realSignature يبقى "" من تهيئته أعلاه. العمود HmacSignature
                        // مُعرَّف IsRequired أي NOT NULL لا «غير فارغ»، فالنصّ الفارغ
                        // مقبول بلا تغيير مخطط ولا ترقية.
                        CalibrationRecord record;
                        if (IsEditMode)
                        {
                            record = await context.CalibrationRecords.FindAsync(_calibrationRecordId)
                                     ?? throw new InvalidOperationException("سجل المعايرة غير موجود.");
                            record.DeviceId = _deviceId;
                            record.CertificateNumber = CertificateNumber.Trim();
                            record.CalibrationDate = CalibrationDate;
                            record.ExpiryDate = ExpiryDate;
                            record.EngineerName = EngineerName.Trim();
                            record.CalibrationDescription = Description.Trim();
                            record.Result = SelectedResult;
                            record.HmacSignature = realSignature;
                            record.UpdatedAt = DateTime.UtcNow;
                            context.CalibrationRecords.Update(record);
                            await context.SaveChangesAsync();
                        }
                        else
                        {
                            // CertificateNumber وHmacSignature يُحفَظان فارغين قصداً عند
                            // الإنشاء: الشهادة صاحبة الرقم والتوقيع وحدها (القرار المحوريّ)،
                            // وحقل الشاشة في وضع الإضافة مقفل بلا قيمة مُدخَلة أصلاً.
                            record = new CalibrationRecord
                            {
                                DeviceId = _deviceId,
                                CertificateNumber = string.Empty,
                                CalibrationDate = CalibrationDate,
                                ExpiryDate = ExpiryDate,
                                EngineerName = EngineerName.Trim(),
                                CalibrationDescription = Description.Trim(),
                                Result = SelectedResult,
                                HmacSignature = string.Empty,
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow,
                                IsDeleted = false
                            };
                            context.CalibrationRecords.Add(record);
                            await context.SaveChangesAsync();
                            _calibrationRecordId = record.Id;
                        }

                        // Commit transaction
                        await transaction.CommitAsync();
                    }
                    catch (Exception)
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                }

            // 5. Post-Commit Phase
            bool postCommitFailed = false;
            string postCommitError = string.Empty;

            // 1. Audit Log Logging (safe outside main DbContext)
            try
            {
                if (IsEditMode)
                {
                    await _auditLogRepository.LogAsync("تعديل معايرة", "CalibrationRecord", _calibrationRecordId.ToString(), $"تعديل سجل المعايرة ذو الشهادة {CertificateNumber.Trim()}");
                }
                else
                {
                    await _auditLogRepository.LogAsync("إضافة معايرة", "CalibrationRecord", _calibrationRecordId.ToString(), $"إضافة سجل معايرة جديد ذو الشهادة {CertificateNumber.Trim()}");
                }
            }
            catch (Exception ex)
            {
                postCommitFailed = true;
                postCommitError += $"\n- خطأ تسجيل العمليات (Audit Log): {ex.Message}";
            }

            // لا توليد QR على مستوى السجل. رمز الاستجابة السريعة يخصّ الشهادة،
            // ويُبنى عند إصدارها. كان توليده هنا يكتب الملف باسم
            // "{CertificateNumber}.png" ورقمُ السجل فارغ دائماً، فينتج ملف ".png"
            // واحد يدهسه كل حفظ جديد.

            try
            {
                // Save attachments (executed outside main DbContext to prevent locks)
                await SaveAttachmentsAsync();
            }
            catch (Exception ex)
            {
                postCommitFailed = true;
                postCommitError += $"\n- خطأ حفظ المرفقات: {ex.Message}";
            }

            // Fire master data events if new entities were added (since the core record is successfully committed)
            if (ownerAdded) MasterDataEvents.RaiseOwnerAdded();
            if (typeAdded) MasterDataEvents.RaiseDeviceTypeAdded();

            // السجل صار محفوظاً وله معرّف ⇒ صار قابلاً لأن تُنسب إليه شهادة.
            // لا مسّ لمنطق الحفظ: ضبط حالة عرض فقط، في طور ما بعد الـCommit.
            IsEditMode = true;
            HasCertificate = false;
            OnPropertyChanged(nameof(CanIssueCertificate));
            CommandManager.InvalidateRequerySuggested();

            Saved?.Invoke(this, EventArgs.Empty);
            CalibrationEvents.RaiseCalibrationChanged();

            // سجل جديد صار جاهزاً لإصدار شهادته ⇒ تبقى النافذة مفتوحة ليضغط المستخدم
            // «إنشاء شهادة». لا نافذة سؤال عن الطباعة — تلك زرّ ظاهر في المرحلة ٥.
            bool stayOpenForCertificate = wasNewRecord && CanIssueCertificate;

            if (postCommitFailed)
            {
                MessageBox.Show($"تم حفظ سجل المعايرة بنجاح في قاعدة البيانات، ولكن حدث خطأ أثناء حفظ المرفقات أو تسجيل العملية:{postCommitError}\n\n(لن تحتاج لإعادة حفظ السجل — السجل الرئيسي مسجل بنجاح).",
                    "تحذير - فشل جزئي في الحفظ", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else if (stayOpenForCertificate)
            {
                MessageBox.Show("تم حفظ سجل المعايرة بنجاح.\n\nيمكنك الآن إصدار شهادته بالضغط على «إنشاء شهادة»، أو إغلاق النافذة.",
                    "تم الحفظ", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("تم حفظ سجل المعايرة بنجاح.", "تم الحفظ", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            if (!stayOpenForCertificate)
            {
                CloseWindowAction?.Invoke();
            }
        }
        catch (Exception ex)
        {
            string errorMsg = ex.Message;
            if (ex.InnerException != null)
            {
                errorMsg += $"\nتفاصيل إضافية: {ex.InnerException.Message}";
            }
            MessageBox.Show($"خطأ أثناء حفظ البيانات: {errorMsg}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        }
        finally
        {
            _isSaving = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

        private void AddAttachment()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "All Files (*.*)|*.*",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                foreach (string filePath in openFileDialog.FileNames)
                {
                    Attachments.Add(new AttachmentItem
                    {
                        FileName = Path.GetFileName(filePath),
                        FullPath = filePath,
                        IsSaved = false
                    });
                }
            }
        }

        private void RemoveAttachment(object? parameter)
        {
            if (parameter is not AttachmentItem item) return;

            var result = MessageBox.Show($"هل أنت متأكد من حذف المرفق '{item.FileName}'؟", "تأكيد حذف المرفق", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                Attachments.Remove(item);
                if (item.IsSaved && item.Id > 0)
                {
                    Task.Run(async () => await _attachmentRepository.DeleteAsync(item.Id));
                }
            }
        }

        private async Task SaveAttachmentsAsync()
        {
            string attachmentsRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Attachments");
            using (var context = await _contextFactory.CreateDbContextAsync())
            {
                var setting = await context.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "AttachmentsPath");
                if (setting != null && !string.IsNullOrWhiteSpace(setting.Value))
                {
                    attachmentsRoot = setting.Value;
                }
            }
            string baseFolder = Path.Combine(attachmentsRoot, CertificateNumber.Trim());
            FileHelper.EnsureDirectoryExists(baseFolder);

            foreach (var att in Attachments)
            {
                if (!att.IsSaved)
                {
                    string destPath = Path.Combine(baseFolder, att.FileName);
                    
                    try
                    {
                        if (File.Exists(att.FullPath) && att.FullPath != destPath)
                        {
                            File.Copy(att.FullPath, destPath, true);
                        }

                        var attachment = new Attachment
                        {
                            CalibrationRecordId = _calibrationRecordId,
                            FileName = att.FileName,
                            FilePath = destPath,
                            FileExtension = Path.GetExtension(att.FileName)
                        };
                        await _attachmentRepository.AddAsync(attachment);
                        att.IsSaved = true;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"خطأ أثناء حفظ المرفق {att.FileName}: {ex.Message}");
                    }
                }
            }
        }

        public Action? CloseWindowAction { get; set; }
    }

    public class AttachmentItem
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public bool IsSaved { get; set; }
    }
}
