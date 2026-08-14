using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using CAL_QR.ViewModels.Base;
using CAL_QR.Models;
using CAL_QR.Repositories;
using CAL_QR.Data;
using CAL_QR.Services;
using CAL_QR.Validation;

namespace CAL_QR.ViewModels
{
    public class DeviceDetailViewModel : BaseViewModel
    {
        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;
        private readonly ICalibrationRepository _calibrationRepository;
        private readonly IDeviceRepository _deviceRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly ICertificateRepository _certificateRepository;
        private readonly ICertificatePdfService _certificatePdfService;

        private Device? _device;
        private ObservableCollection<CalibrationRecord> _calibrations = new();
        private ObservableCollection<TimelineNode> _calibrationsTimeline = new();
        private ObservableCollection<Attachment> _selectedRecordAttachments = new();
        private CalibrationRecord? _selectedRecord;

        public event EventHandler? Saved;
        public static Func<string, string, MessageBoxButton, MessageBoxImage, MessageBoxResult>? MessageBoxShowMock { get; set; }

        public DeviceDetailViewModel(
            IDbContextFactory<CalQrDbContext> contextFactory,
            ICalibrationRepository calibrationRepository,
            IDeviceRepository deviceRepository,
            ICurrentUserService currentUserService,
            IAuditLogRepository auditLogRepository,
            ICertificateRepository certificateRepository,
            ICertificatePdfService certificatePdfService)
        {
            _contextFactory = contextFactory;
            _calibrationRepository = calibrationRepository;
            _deviceRepository = deviceRepository;
            _currentUserService = currentUserService;
            _auditLogRepository = auditLogRepository;
            _certificateRepository = certificateRepository;
            _certificatePdfService = certificatePdfService;

            OpenAttachmentCommand = new RelayCommand(OpenAttachment);
            PrintRecordCommand = new RelayCommand(PrintRecord, CanPrintRecord);
            ExportPdfCommand = new RelayCommand(async () => await ExportPdfAsync(), CanExportPdf);
            DeleteRecordCommand = new RelayCommand(DeleteRecord, CanDeleteRecord);
        }

        public ICommand PrintRecordCommand { get; }
        public ICommand ExportPdfCommand { get; }
        public ICommand DeleteRecordCommand { get; }

        #region Properties
        public Device? Device
        {
            get => _device;
            set => SetProperty(ref _device, value);
        }

        public ObservableCollection<CalibrationRecord> Calibrations
        {
            get => _calibrations;
            set => SetProperty(ref _calibrations, value);
        }

        public ObservableCollection<TimelineNode> CalibrationsTimeline
        {
            get => _calibrationsTimeline;
            set => SetProperty(ref _calibrationsTimeline, value);
        }

        public ObservableCollection<Attachment> SelectedRecordAttachments
        {
            get => _selectedRecordAttachments;
            set => SetProperty(ref _selectedRecordAttachments, value);
        }

        public CalibrationRecord? SelectedRecord
        {
            get => _selectedRecord;
            set
            {
                if (SetProperty(ref _selectedRecord, value))
                {
                    LoadAttachmentsForRecord(value);
                    (PrintRecordCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    OnPropertyChanged(nameof(ShowLegacySignature));
                }
            }
        }

        /// <summary>
        /// التوقيع الرقميّ القديم وملصق المعايرة: يظهران للسجل المُرحَّل وحده.
        ///
        /// الشرط على <b>خلوّ التوقيع</b> لا على غياب الشهادة، وهو أصدق وأبسط:
        /// السجل الجديد يُحفظ بتوقيع فارغ وبلا ملف QR بقرار معماريّ، والملصق
        /// يُبنى في PrintPreviewViewModel من CertificateNumber و HmacSignature —
        /// وكلاهما فارغ، فكان سيُطبع ملصق برقم فارغ ورمز تحقّق فارغ.
        ///
        /// الإخفاء لا التعطيل: الميزة غير موجودة لهذه السجلات، لا موجودة ومعطّلة.
        /// </summary>
        public bool ShowLegacySignature =>
            SelectedRecord != null && !string.IsNullOrWhiteSpace(SelectedRecord.HmacSignature);

        public bool IsUserAdmin => _currentUserService.CurrentUser?.Role == UserRole.Admin;
        #endregion

        #region Commands
        public ICommand OpenAttachmentCommand { get; }
        #endregion

        public void LoadDeviceDetails(int deviceId, int? preferredRecordId = null)
        {
            try
            {
                Device = Task.Run(async () => await _deviceRepository.GetByIdAsync(deviceId)).Result;

                var records = Task.Run(async () => await _calibrationRepository.GetByDeviceIdAsync(deviceId)).Result.ToList();
                for (int i = 0; i < records.Count; i++)
                {
                    records[i].SequenceNumber = i + 1;
                }

                // رقم الشهادة يُقرأ من Certificates لا من العمود المهجور على السجل.
                // استعلام واحد لكل سجلات الجهاز، ثم ملء خاصّية العرض — نفس نمط
                // SequenceNumber أعلاه: خاصّية [NotMapped] تُملأ هنا.
                using (var context = _contextFactory.CreateDbContext())
                {
                    var issuedNumbers = CertificateNumberDisplayRules.Load(context, records.Select(r => r.Id));
                    CertificateNumberDisplayRules.Populate(records, issuedNumbers);
                }

                Calibrations = new ObservableCollection<CalibrationRecord>(records);

                if (preferredRecordId.HasValue && preferredRecordId.Value > 0)
                {
                    SelectedRecord = records.FirstOrDefault(r => r.Id == preferredRecordId.Value) ?? records.FirstOrDefault();
                }
                else
                {
                    SelectedRecord = records.FirstOrDefault();
                }

                var timelineRecords = records.OrderBy(r => r.CalibrationDate).ToList();
                var nodes = new List<TimelineNode>();
                for (int i = 0; i < timelineRecords.Count; i++)
                {
                    var r = timelineRecords[i];
                    nodes.Add(new TimelineNode
                    {
                        CertificateNumber = r.DisplayCertificateNumber,
                        DateString = r.CalibrationDate.ToString("yyyy-MM-dd"),
                        ResultColor = r.Result == "Passed" ? "#2E7D32" : r.Result == "Failed" ? "#C62828" : "#F9A825",
                        IsPassed = r.Result == "Passed",
                        IsFailed = r.Result == "Failed",
                        IsConditional = r.Result == "Conditional",
                        ShowLine = i > 0
                    });
                }
                CalibrationsTimeline = new ObservableCollection<TimelineNode>(nodes);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحميل تفاصيل الجهاز: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadAttachmentsForRecord(CalibrationRecord? record)
        {
            SelectedRecordAttachments.Clear();
            if (record == null) return;

            try
            {
                using (var context = _contextFactory.CreateDbContext())
                {
                    var list = context.Attachments
                        .AsNoTracking()
                        .Where(a => a.CalibrationRecordId == record.Id)
                        .ToList();
                    SelectedRecordAttachments = new ObservableCollection<Attachment>(list);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحميل المرفقات: {ex.Message}");
            }
        }

        private void OpenAttachment(object? parameter)
        {
            if (parameter is not Attachment att) return;

            try
            {
                if (File.Exists(att.FilePath))
                {
                    Process.Start(new ProcessStartInfo(att.FilePath) { UseShellExecute = true });
                }
                else
                {
                    MessageBox.Show("الملف المرفق غير موجود على المسار المحدد.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"فشل فتح الملف المرفق: {ex.Message}");
            }
        }

        private void PrintRecord()
        {
            if (SelectedRecord == null) return;
            var dialog = new Views.Dialogs.PrintPreviewDialog(SelectedRecord.Id);
            dialog.Owner = Application.Current.Windows.OfType<Views.Dialogs.DeviceDetailDialog>().FirstOrDefault() ?? Application.Current.MainWindow;
            dialog.ShowDialog();
        }

        private bool CanPrintRecord()
        {
            return SelectedRecord != null
                && !string.IsNullOrWhiteSpace(SelectedRecord.CertificateNumber);
        }

        private bool CanExportPdf()
        {
            return SelectedRecord != null
                && !string.IsNullOrWhiteSpace(SelectedRecord.CertificateNumber);
        }

        private async Task ExportPdfAsync()
        {
            if (SelectedRecord == null || string.IsNullOrWhiteSpace(SelectedRecord.CertificateNumber))
                return;

            try
            {
                var certificate = await _certificateRepository
                    .GetByCertificateNumberAsync(SelectedRecord.CertificateNumber);

                if (certificate == null)
                {
                    ShowMessageBox("لم يُعثر على شهادة مرتبطة بهذا السجلّ.", "تصدير PDF",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var folder = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "CAL-QR Certificates");
                System.IO.Directory.CreateDirectory(folder);

                var safeName = certificate.CertificateNumber;
                foreach (var ch in System.IO.Path.GetInvalidFileNameChars())
                    safeName = safeName.Replace(ch, '-');

                var filePath = System.IO.Path.Combine(folder, safeName + ".pdf");

                await _certificatePdfService.GenerateFileAsync(certificate, filePath);

                // Phase 5-b: تسجيل أوّل طباعة/إصدار. MarkPrintedAsync idempotent — لا تدهس أوّل تاريخ.
                await _certificateRepository.MarkPrintedAsync(certificate.Id);

                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ShowMessageBox($"فشل تصدير الشهادة: {ex.Message}", "تصدير PDF",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private MessageBoxResult ShowMessageBox(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon)
        {
            if (MessageBoxShowMock != null)
            {
                return MessageBoxShowMock(messageBoxText, caption, button, icon);
            }
            return MessageBox.Show(messageBoxText, caption, button, icon);
        }

        private bool CanDeleteRecord(object? parameter)
        {
            return _currentUserService.CurrentUser?.Role == UserRole.Admin;
        }

        private async void DeleteRecord(object? parameter)
        {
            if (parameter is not CalibrationRecord record) return;

            // Check how many non-deleted calibration records exist for this device
            using (var context = await _contextFactory.CreateDbContextAsync())
            {
                var dbCount = await context.CalibrationRecords.CountAsync(r => r.DeviceId == record.DeviceId && !r.IsDeleted);
                if (dbCount <= 1)
                {
                    ShowMessageBox(
                        "لا يمكن حذف شهادة المعايرة هذه لأنها الشهادة الوحيدة المتبقية للجهاز.\nيجب أن يحتفظ كل جهاز بسجل معايرة واحد على الأقل.\n\nتنويه: لحذف هذا السجل بالكامل، يجب حذف الجهاز نفسه (خيار حذف الجهاز غير متوفر حالياً بالواجهة الرئيسية).",
                        "تنبيه الحماية",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
            }

            var confirmResult = ShowMessageBox(
                $"هل أنت متأكد من رغبتك في حذف شهادة المعايرة ذات الرقم ({record.CertificateNumber})؟\nهذا الإجراء سيقوم بحذف الشهادة ومرفقاتها نهائياً من النظام.",
                "تأكيد الحذف",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmResult != MessageBoxResult.Yes) return;

            try
            {
                // Soft delete Calibration Record
                await _calibrationRepository.SoftDeleteAsync(record.Id);

                // Audit Log
                await _auditLogRepository.LogAsync(
                    "حذف شهادة معايرة",
                    "CalibrationRecord",
                    record.Id.ToString(),
                    $"حذف ناعم لشهادة المعايرة رقم {record.CertificateNumber} للجهاز موديل {Device?.Model} رقم تسلسلي {Device?.SerialNumber}");

                // Raise calibration changed event
                CAL_QR.Helpers.CalibrationEvents.RaiseCalibrationChanged();

                // Trigger Saved event to tell DevicesViewModel to reload
                Saved?.Invoke(this, EventArgs.Empty);

                // Reload local dialog data
                LoadDeviceDetails(record.DeviceId);
            }
            catch (Exception ex)
            {
                ShowMessageBox($"خطأ أثناء حذف الشهادة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class TimelineNode
    {
        public string CertificateNumber { get; set; } = string.Empty;
        public string DateString { get; set; } = string.Empty;
        public string ResultColor { get; set; } = string.Empty;
        public bool IsPassed { get; set; }
        public bool IsFailed { get; set; }
        public bool IsConditional { get; set; }
        public bool ShowLine { get; set; }
    }
}
