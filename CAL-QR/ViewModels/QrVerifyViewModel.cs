using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Data;
using CAL_QR.ViewModels.Base;
using CAL_QR.Services;

namespace CAL_QR.ViewModels
{
    public class QrVerifyViewModel : BaseViewModel
    {
        private readonly IHmacService _hmacService;
        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;

        private string _concatenatedText = string.Empty;
        private string _quickVerifyCode = string.Empty;
        
        private string _owner = string.Empty;
        private string _deviceType = string.Empty;
        private string _model = string.Empty;
        private string _serial = string.Empty;
        private string _certNo = string.Empty;
        private string _calDate = string.Empty;
        private string _expDate = string.Empty;
        private string _engineer = string.Empty;
        private string _calType = string.Empty;
        private string _result = string.Empty;
        private string _readVerifyCode = string.Empty;

        private string _manualOwner = string.Empty;
        private string _manualDeviceType = string.Empty;
        private string _manualModel = string.Empty;
        private string _manualSerial = string.Empty;
        private string _manualCertNo = string.Empty;
        private DateTime? _manualCalDate = DateTime.Today;
        private DateTime? _manualExpDate = DateTime.Today.AddYears(1);
        private string _manualEngineer = string.Empty;
        private string _manualResult = "Passed";
        private string _manualVerifyCode = string.Empty;

        private bool _isValidated;
        private bool _isSuccess;
        private string _message = string.Empty;
        private string _computedVerifyCode = string.Empty;
        private string _verificationSource = "لم يتم التحقق بعد";

        public QrVerifyViewModel(IHmacService hmacService, IDbContextFactory<CalQrDbContext> contextFactory)
        {
            _hmacService = hmacService;
            _contextFactory = contextFactory;

            VerifyPastedTextCommand = new RelayCommand(VerifyPastedText);
            VerifyManualCommand = new RelayCommand(VerifyManual);
            ClearCommand = new RelayCommand(Clear);
            QuickVerifyCommand = new RelayCommand(async () => await QuickVerifyByCodeAsync());
        }

        #region Properties
        public string ConcatenatedText
        {
            get => _concatenatedText;
            set => SetProperty(ref _concatenatedText, value);
        }

        public string Owner { get => _owner; set => SetProperty(ref _owner, value); }
        public string DeviceType { get => _deviceType; set => SetProperty(ref _deviceType, value); }
        public string Model { get => _model; set => SetProperty(ref _model, value); }
        public string Serial { get => _serial; set => SetProperty(ref _serial, value); }
        public string CertNo { get => _certNo; set => SetProperty(ref _certNo, value); }
        public string CalDate { get => _calDate; set => SetProperty(ref _calDate, value); }
        public string ExpDate { get => _expDate; set => SetProperty(ref _expDate, value); }
        public string Engineer { get => _engineer; set => SetProperty(ref _engineer, value); }
        public string CalType { get => _calType; set => SetProperty(ref _calType, value); }
        public string Result { get => _result; set => SetProperty(ref _result, value); }
        public string ReadVerifyCode { get => _readVerifyCode; set => SetProperty(ref _readVerifyCode, value); }

        public string ManualOwner { get => _manualOwner; set => SetProperty(ref _manualOwner, value); }
        public string ManualDeviceType { get => _manualDeviceType; set => SetProperty(ref _manualDeviceType, value); }
        public string ManualModel { get => _manualModel; set => SetProperty(ref _manualModel, value); }
        public string ManualSerial { get => _manualSerial; set => SetProperty(ref _manualSerial, value); }
        public string ManualCertNo { get => _manualCertNo; set => SetProperty(ref _manualCertNo, value); }
        public DateTime? ManualCalDate { get => _manualCalDate; set => SetProperty(ref _manualCalDate, value); }
        public DateTime? ManualExpDate { get => _manualExpDate; set => SetProperty(ref _manualExpDate, value); }
        public string ManualEngineer { get => _manualEngineer; set => SetProperty(ref _manualEngineer, value); }
        public string ManualResult { get => _manualResult; set => SetProperty(ref _manualResult, value); }
        public string ManualVerifyCode { get => _manualVerifyCode; set => SetProperty(ref _manualVerifyCode, value); }

        public string QuickVerifyCode
        {
            get => _quickVerifyCode;
            set => SetProperty(ref _quickVerifyCode, value);
        }

        public string VerificationSource
        {
            get => _verificationSource;
            set => SetProperty(ref _verificationSource, value);
        }

        public bool IsValidated { get => _isValidated; set => SetProperty(ref _isValidated, value); }
        public bool IsSuccess { get => _isSuccess; set => SetProperty(ref _isSuccess, value); }
        public string Message { get => _message; set => SetProperty(ref _message, value); }
        public string ComputedVerifyCode { get => _computedVerifyCode; set => SetProperty(ref _computedVerifyCode, value); }
        #endregion

        #region Commands
        public ICommand VerifyPastedTextCommand { get; }
        public ICommand VerifyManualCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand QuickVerifyCommand { get; }
        #endregion

        private void VerifyPastedText()
        {
            IsValidated = false;
            Message = string.Empty;
            VerificationSource = "عبر لصق نص QR";

            if (string.IsNullOrWhiteSpace(ConcatenatedText))
            {
                Message = "تنبيه: يرجى لصق نص كود QR أولاً.";
                IsSuccess = false;
                IsValidated = true;
                return;
            }

            try
            {
                var lines = ConcatenatedText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.StartsWith("الجهة / Owner: ")) Owner = line.Substring("الجهة / Owner: ".Length).Trim();
                    else if (line.StartsWith("النوع / Type: ")) DeviceType = line.Substring("النوع / Type: ".Length).Trim();
                    else if (line.StartsWith("الموديل / Model: ")) Model = line.Substring("الموديل / Model: ".Length).Trim();
                    else if (line.StartsWith("الرقم التسلسلي / S/N: ")) Serial = line.Substring("الرقم التسلسلي / S/N: ".Length).Trim();
                    else if (line.StartsWith("رقم الشهادة / Cert No: ")) CertNo = line.Substring("رقم الشهادة / Cert No: ".Length).Trim();
                    else if (line.StartsWith("تاريخ المعايرة / Cal. Date: ")) CalDate = line.Substring("تاريخ المعايرة / Cal. Date: ".Length).Trim();
                    else if (line.StartsWith("تاريخ الانتهاء / Exp. Date: ")) ExpDate = line.Substring("تاريخ الانتهاء / Exp. Date: ".Length).Trim();
                    else if (line.StartsWith("المهندس / Engineer: ")) Engineer = line.Substring("المهندس / Engineer: ".Length).Trim();
                    else if (line.StartsWith("نوع المعايرة / Cal. Type: ")) CalType = line.Substring("نوع المعايرة / Cal. Type: ".Length).Trim();
                    else if (line.StartsWith("النتيجة / Result: "))
                    {
                        var resText = line.Substring("النتيجة / Result: ".Length).Trim();
                        if (resText.Contains("Passed") || resText.Contains("ناجح")) Result = "Passed";
                        else if (resText.Contains("Failed") || resText.Contains("راسب")) Result = "Failed";
                        else if (resText.Contains("Conditional") || resText.Contains("مشروط")) Result = "Conditional";
                        else Result = resText;
                    }
                    else if (line.StartsWith("كود التحقق / Verify Code: ")) ReadVerifyCode = line.Substring("كود التحقق / Verify Code: ".Length).Trim();
                }

                ComputedVerifyCode = _hmacService.ComputeSignature(
                    certNo: CertNo,
                    model: Model,
                    serial: Serial,
                    ownerName: Owner,
                    calDate: CalDate,
                    expDate: ExpDate,
                    result: Result,
                    engineerName: Engineer
                );

                IsSuccess = _hmacService.VerifySignature(
                    certNo: CertNo,
                    model: Model,
                    serial: Serial,
                    ownerName: Owner,
                    calDate: CalDate,
                    expDate: ExpDate,
                    result: Result,
                    engineerName: Engineer,
                    signature: ReadVerifyCode
                );

                if (IsSuccess)
                {
                    Message = "✅ شهادة أصلية ومطابقة لمركز البحوث النووية.";
                }
                else
                {
                    Message = "❌ تحذير: التوقيع الرقمي غير مطابق! الشهادة معدّلة أو مزوّرة!";
                }

                IsValidated = true;
            }
            catch (Exception ex)
            {
                Message = $"خطأ أثناء تحليل النص: {ex.Message}";
                IsSuccess = false;
                IsValidated = true;
            }
        }

        private void VerifyManual()
        {
            IsValidated = false;
            Message = string.Empty;
            VerificationSource = "عبر الإدخال اليدوي";

            if (string.IsNullOrWhiteSpace(ManualCertNo) ||
                string.IsNullOrWhiteSpace(ManualModel) ||
                string.IsNullOrWhiteSpace(ManualSerial) ||
                string.IsNullOrWhiteSpace(ManualOwner) ||
                !ManualCalDate.HasValue ||
                !ManualExpDate.HasValue ||
                string.IsNullOrWhiteSpace(ManualEngineer) ||
                string.IsNullOrWhiteSpace(ManualVerifyCode))
            {
                Message = "تنبيه: يرجى ملء كافة حقول التحقق اليدوي مع كود التحقق.";
                IsSuccess = false;
                IsValidated = true;
                return;
            }

            try
            {
                string calDateStr = ManualCalDate.Value.ToString("yyyy-MM-dd");
                string expDateStr = ManualExpDate.Value.ToString("yyyy-MM-dd");

                ComputedVerifyCode = _hmacService.ComputeSignature(
                    certNo: ManualCertNo.Trim(),
                    model: ManualModel.Trim(),
                    serial: ManualSerial.Trim(),
                    ownerName: ManualOwner.Trim(),
                    calDate: calDateStr,
                    expDate: expDateStr,
                    result: ManualResult,
                    engineerName: ManualEngineer.Trim()
                );

                IsSuccess = _hmacService.VerifySignature(
                    certNo: ManualCertNo.Trim(),
                    model: ManualModel.Trim(),
                    serial: ManualSerial.Trim(),
                    ownerName: ManualOwner.Trim(),
                    calDate: calDateStr,
                    expDate: expDateStr,
                    result: ManualResult,
                    engineerName: ManualEngineer.Trim(),
                    signature: ManualVerifyCode.Trim()
                );

                if (IsSuccess)
                {
                    Message = "✅ شهادة أصلية ومطابقة لمركز البحوث النووية (التحقق اليدوي).";
                }
                else
                {
                    Message = "❌ تحذير: التوقيع الرقمي غير مطابق! البيانات المُدخلة معدّلة أو مزوّرة!";
                }

                IsValidated = true;
            }
            catch (Exception ex)
            {
                Message = $"خطأ أثناء التحقق اليدوي: {ex.Message}";
                IsSuccess = false;
                IsValidated = true;
            }
        }

        private void Clear()
        {
            ConcatenatedText = string.Empty;
            QuickVerifyCode = string.Empty;
            Owner = string.Empty;
            DeviceType = string.Empty;
            Model = string.Empty;
            Serial = string.Empty;
            CertNo = string.Empty;
            CalDate = string.Empty;
            ExpDate = string.Empty;
            Engineer = string.Empty;
            CalType = string.Empty;
            Result = string.Empty;
            ReadVerifyCode = string.Empty;

            ManualOwner = string.Empty;
            ManualDeviceType = string.Empty;
            ManualModel = string.Empty;
            ManualSerial = string.Empty;
            ManualCertNo = string.Empty;
            ManualCalDate = DateTime.Today;
            ManualExpDate = DateTime.Today.AddYears(1);
            ManualEngineer = string.Empty;
            ManualResult = "Passed";
            ManualVerifyCode = string.Empty;

            IsValidated = false;
            IsSuccess = false;
            Message = string.Empty;
            ComputedVerifyCode = string.Empty;
            VerificationSource = "لم يتم التحقق بعد";
        }

        private async Task QuickVerifyByCodeAsync()
        {
            IsValidated = false;
            Message = string.Empty;
            VerificationSource = "عبر الكود السريع";

            if (string.IsNullOrWhiteSpace(QuickVerifyCode))
            {
                Message = "تنبيه: يرجى إدخال كود التحقق أولاً.";
                IsSuccess = false;
                IsValidated = true;
                return;
            }

            try
            {
                string searchCode = QuickVerifyCode.Trim().ToUpper();

                using var context = await _contextFactory.CreateDbContextAsync();

                var record = await context.CalibrationRecords
                    .AsNoTracking()
                    .Include(r => r.Device)
                        .ThenInclude(d => d!.Owner)
                    .Include(r => r.Device)
                        .ThenInclude(d => d!.DeviceType)
                    .FirstOrDefaultAsync(r => r.HmacSignature == searchCode && !r.IsDeleted);

                if (record != null)
                {
                    Owner = record.Device?.Owner?.Name ?? "غير محدد";
                    DeviceType = record.Device?.DeviceType?.Name ?? "غير محدد";
                    Model = record.Device?.Model ?? "غير محدد";
                    Serial = record.Device?.SerialNumber ?? "غير محدد";
                    CertNo = record.CertificateNumber ?? "غير محدد";
                    CalDate = record.CalibrationDate.ToString("yyyy-MM-dd");
                    ExpDate = record.ExpiryDate.ToString("yyyy-MM-dd");
                    Engineer = record.EngineerName ?? "غير محدد";
                    CalType = record.CalibrationDescription ?? "غير محدد";
                    
                    Result = record.Result switch
                    {
                        "Passed" => "Passed",
                        "Failed" => "Failed",
                        "Conditional" => "Conditional",
                        _ => record.Result
                    };

                    ReadVerifyCode = record.HmacSignature ?? string.Empty;
                    ComputedVerifyCode = record.HmacSignature ?? string.Empty;
                    
                    IsSuccess = true;
                    Message = "✅ شهادة أصلية ومطابقة لمركز البحوث النووية.";
                }
                else
                {
                    Owner = string.Empty;
                    DeviceType = string.Empty;
                    Model = string.Empty;
                    Serial = string.Empty;
                    CertNo = string.Empty;
                    CalDate = string.Empty;
                    ExpDate = string.Empty;
                    Engineer = string.Empty;
                    CalType = string.Empty;
                    Result = string.Empty;
                    ReadVerifyCode = string.Empty;
                    ComputedVerifyCode = string.Empty;

                    IsSuccess = false;
                    Message = "❌ كود التحقق غير موجود في قاعدة البيانات - الشهادة قد تكون مزوّرة أو الكود مُدخل بشكل خاطئ.";
                }

                IsValidated = true;
            }
            catch (Exception ex)
            {
                Message = $"خطأ أثناء التحقق السريع: {ex.Message}";
                IsSuccess = false;
                IsValidated = true;
            }
        }
    }
}
