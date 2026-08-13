using System;
using System.Windows.Input;
using CAL_QR.Models;
using CAL_QR.Repositories;
using CAL_QR.ViewModels.Base;

namespace CAL_QR.ViewModels
{
    public enum VerificationDisplayState
    {
        None,
        Authentic,
        Amended,
        NotFound,
        Unverifiable
    }

    public class QrVerifyViewModel : BaseViewModel
    {
        private readonly ICertificateRepository _certificateRepository;

        private string _concatenatedText = string.Empty;
        private string _quickVerifyCode = string.Empty;

        private string _owner = string.Empty;
        private string _model = string.Empty;
        private string _serial = string.Empty;
        private string _certNo = string.Empty;
        private string _calDate = string.Empty;
        private string _expDate = string.Empty;
        private string _result = string.Empty;
        private string _amendedAt = string.Empty;

        private bool _isValidated;
        private VerificationDisplayState _displayState = VerificationDisplayState.None;
        private string _message = string.Empty;
        private string _verificationSource = "لم يتم التحقق بعد";

        public QrVerifyViewModel(ICertificateRepository certificateRepository)
        {
            _certificateRepository = certificateRepository;

            VerifyPastedTextCommand = new RelayCommand(async () => await VerifyPastedTextAsync());
            ClearCommand = new RelayCommand(Clear);
            QuickVerifyCommand = new RelayCommand(async () => await QuickVerifyAsync());
        }

        #region Properties
        public string ConcatenatedText
        {
            get => _concatenatedText;
            set => SetProperty(ref _concatenatedText, value);
        }

        public string QuickVerifyCode
        {
            get => _quickVerifyCode;
            set => SetProperty(ref _quickVerifyCode, value);
        }

        public string Owner { get => _owner; set => SetProperty(ref _owner, value); }
        public string Model { get => _model; set => SetProperty(ref _model, value); }
        public string Serial { get => _serial; set => SetProperty(ref _serial, value); }
        public string CertNo { get => _certNo; set => SetProperty(ref _certNo, value); }
        public string CalDate { get => _calDate; set => SetProperty(ref _calDate, value); }
        public string ExpDate { get => _expDate; set => SetProperty(ref _expDate, value); }
        public string Result { get => _result; set => SetProperty(ref _result, value); }
        public string AmendedAt { get => _amendedAt; set => SetProperty(ref _amendedAt, value); }

        public string VerificationSource
        {
            get => _verificationSource;
            set => SetProperty(ref _verificationSource, value);
        }

        public bool IsValidated { get => _isValidated; set => SetProperty(ref _isValidated, value); }

        public VerificationDisplayState DisplayState
        {
            get => _displayState;
            set
            {
                if (SetProperty(ref _displayState, value))
                {
                    OnPropertyChanged(nameof(IsAuthentic));
                    OnPropertyChanged(nameof(IsAmended));
                    OnPropertyChanged(nameof(IsNotFound));
                    OnPropertyChanged(nameof(IsUnverifiable));
                    OnPropertyChanged(nameof(IsSuccess));
                }
            }
        }

        public bool IsAuthentic => DisplayState == VerificationDisplayState.Authentic;
        public bool IsAmended => DisplayState == VerificationDisplayState.Amended;
        public bool IsNotFound => DisplayState == VerificationDisplayState.NotFound;
        public bool IsUnverifiable => DisplayState == VerificationDisplayState.Unverifiable;
        public bool IsSuccess => IsAuthentic || IsAmended;

        public string Message { get => _message; set => SetProperty(ref _message, value); }
        #endregion

        #region Commands
        public ICommand VerifyPastedTextCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand QuickVerifyCommand { get; }
        #endregion

        internal async System.Threading.Tasks.Task VerifyPastedTextAsync()
        {
            VerificationSource = "عبر لصق نص QR";

            if (string.IsNullOrWhiteSpace(ConcatenatedText))
            {
                SetUnverifiable("تنبيه: يرجى لصق نص كود QR أولاً.");
                return;
            }

            string? code = null;
            var lines = ConcatenatedText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.StartsWith("V:"))
                {
                    code = line.Substring("V:".Length).Trim();
                    break;
                }
            }

            if (code == null)
            {
                SetUnverifiable("لم يُعثر على كود تحقق في النص الملصوق.");
                return;
            }

            await VerifyAsync(code);
        }

        internal async System.Threading.Tasks.Task QuickVerifyAsync()
        {
            VerificationSource = "عبر الكود السريع";

            if (string.IsNullOrWhiteSpace(QuickVerifyCode))
            {
                SetUnverifiable("تنبيه: يرجى إدخال كود التحقق أولاً.");
                return;
            }

            await VerifyAsync(QuickVerifyCode.Trim());
        }

        private async System.Threading.Tasks.Task VerifyAsync(string code)
        {
            var result = await _certificateRepository.VerifyByCodeAsync(code);

            switch (result.Status)
            {
                case CertificateVerificationStatus.Authentic:
                    FillCertificateDisplay(result.Certificate);
                    DisplayState = VerificationDisplayState.Authentic;
                    Message = "✅ شهادة أصلية ومطابقة لمركز البحوث النووية.";
                    break;

                case CertificateVerificationStatus.AuthenticAmended:
                    FillCertificateDisplay(result.Certificate);
                    AmendedAt = result.AmendedAt?.ToString("yyyy-MM-dd") ?? string.Empty;
                    DisplayState = VerificationDisplayState.Amended;
                    Message = $"⚠️ شهادة أصلية، لكنها عُدِّلت بعد إصدارها بتاريخ {AmendedAt}.";
                    break;

                case CertificateVerificationStatus.NotFound:
                    ClearCertificateDisplay();
                    DisplayState = VerificationDisplayState.NotFound;
                    Message = "❌ الكود غير موجود — الشهادة قد تكون مزوّرة أو الكود مُدخل خطأً.";
                    break;

                case CertificateVerificationStatus.UnsupportedVersion:
                    ClearCertificateDisplay();
                    DisplayState = VerificationDisplayState.Unverifiable;
                    Message = "ℹ️ تعذّر التحقّق: إصدار توقيع غير مدعوم. يرجى مراجعة المنظومة.";
                    break;
            }

            IsValidated = true;
        }

        private void SetUnverifiable(string message)
        {
            ClearCertificateDisplay();
            DisplayState = VerificationDisplayState.Unverifiable;
            Message = message;
            IsValidated = true;
        }

        private void FillCertificateDisplay(Certificate? certificate)
        {
            if (certificate == null)
            {
                ClearCertificateDisplay();
                return;
            }

            Owner = certificate.ClientName;
            Model = certificate.DeviceModel;
            Serial = certificate.DeviceSerialNumber;
            CertNo = certificate.CertificateNumber;
            CalDate = certificate.CalibrationDate.ToString("yyyy-MM-dd");
            ExpDate = certificate.DueDate.ToString("yyyy-MM-dd");
            Result = certificate.ComplianceVerdict ?? string.Empty;
        }

        private void ClearCertificateDisplay()
        {
            Owner = string.Empty;
            Model = string.Empty;
            Serial = string.Empty;
            CertNo = string.Empty;
            CalDate = string.Empty;
            ExpDate = string.Empty;
            Result = string.Empty;
            AmendedAt = string.Empty;
        }

        private void Clear()
        {
            ConcatenatedText = string.Empty;
            QuickVerifyCode = string.Empty;
            ClearCertificateDisplay();

            IsValidated = false;
            DisplayState = VerificationDisplayState.None;
            Message = string.Empty;
            VerificationSource = "لم يتم التحقق بعد";
        }
    }
}
