using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.EntityFrameworkCore;
using CAL_QR.ViewModels.Base;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Repositories;
using CAL_QR.Services;

namespace CAL_QR.ViewModels
{
    public class PrintPreviewViewModel : BaseViewModel
    {
        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;
        private readonly IPaperTemplateRepository _templateRepository;
        private readonly IQrService _qrService;
        private readonly IPrintService _printService;
        private readonly IAuditLogRepository _auditLogRepository;

        private CalibrationRecord? _calibrationRecord;
        private ObservableCollection<string> _printers = new();
        private ObservableCollection<PaperTemplate> _templates = new();
        private string _selectedPrinter = string.Empty;
        private PaperTemplate? _selectedTemplate;
        private string _selectedQrSaveSize = "Large";
        private BitmapSource? _qrImagePreview;

        private int _startColumn = 1;
        private int _startRow = 1;

        public event EventHandler? RedrawGridRequested;

        public PrintPreviewViewModel(
            IDbContextFactory<CalQrDbContext> contextFactory,
            IPaperTemplateRepository templateRepository,
            IQrService qrService,
            IPrintService printService,
            IAuditLogRepository auditLogRepository)
        {
            _contextFactory = contextFactory;
            _templateRepository = templateRepository;
            _qrService = qrService;
            _printService = printService;
            _auditLogRepository = auditLogRepository;

            Printers = new ObservableCollection<string>(_printService.GetAvailablePrinters());
            
            PrintCommand = new RelayCommand(Print);
            SaveQrImageCommand = new RelayCommand(SaveQrImage);
        }

        #region Properties
        public CalibrationRecord? CalibrationRecord
        {
            get => _calibrationRecord;
            set => SetProperty(ref _calibrationRecord, value);
        }

        public ObservableCollection<string> Printers
        {
            get => _printers;
            set => SetProperty(ref _printers, value);
        }

        public ObservableCollection<PaperTemplate> Templates
        {
            get => _templates;
            set => SetProperty(ref _templates, value);
        }

        public string SelectedPrinter
        {
            get => _selectedPrinter;
            set => SetProperty(ref _selectedPrinter, value);
        }

        public PaperTemplate? SelectedTemplate
        {
            get => _selectedTemplate;
            set
            {
                if (SetProperty(ref _selectedTemplate, value))
                {
                    StartColumn = 1;
                    StartRow = 1;
                    RedrawGridRequested?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public string SelectedQrSaveSize
        {
            get => _selectedQrSaveSize;
            set => SetProperty(ref _selectedQrSaveSize, value);
        }

        public BitmapSource? QrImagePreview
        {
            get => _qrImagePreview;
            set => SetProperty(ref _qrImagePreview, value);
        }

        public int StartColumn
        {
            get => _startColumn;
            set
            {
                if (SetProperty(ref _startColumn, value))
                {
                    RedrawGridRequested?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public int StartRow
        {
            get => _startRow;
            set
            {
                if (SetProperty(ref _startRow, value))
                {
                    RedrawGridRequested?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        #endregion

        public ICommand PrintCommand { get; }
        public ICommand SaveQrImageCommand { get; }

        public async Task LoadDataAsync(int calibrationRecordId)
        {
            using (var context = await _contextFactory.CreateDbContextAsync())
            {
                CalibrationRecord = await context.CalibrationRecords
                    .AsNoTracking()
                    .Include(r => r.Device!)
                        .ThenInclude(d => d.Owner)
                    .Include(r => r.Device!)
                        .ThenInclude(d => d.DeviceType)
                    .FirstOrDefaultAsync(r => r.Id == calibrationRecordId && !r.IsDeleted);
            }

            if (CalibrationRecord != null)
            {
                string qrContent = _qrService.GenerateVerificationText(
                    ownerName: CalibrationRecord.Device?.Owner?.Name ?? "",
                    deviceType: CalibrationRecord.Device?.DeviceType?.Name ?? "",
                    model: CalibrationRecord.Device?.Model ?? "",
                    serial: CalibrationRecord.Device?.SerialNumber ?? "",
                    certNo: CalibrationRecord.CertificateNumber,
                    calDate: CalibrationRecord.CalibrationDate.ToString("yyyy-MM-dd"),
                    expDate: CalibrationRecord.ExpiryDate.ToString("yyyy-MM-dd"),
                    engineerName: CalibrationRecord.EngineerName,
                    description: CalibrationRecord.CalibrationDescription ?? "",
                    result: CalibrationRecord.Result,
                    verifyCode: CalibrationRecord.HmacSignature
                );

                QrImagePreview = _qrService.GenerateQrCodeImage(qrContent, 200);
            }

            var list = await _templateRepository.GetAllAsync();
            Templates = new ObservableCollection<PaperTemplate>(list);

            using (var context = await _contextFactory.CreateDbContextAsync())
            {
                var lastPrinter = await context.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "LastPrinterName");
                if (lastPrinter != null && !string.IsNullOrWhiteSpace(lastPrinter.Value) && Printers.Contains(lastPrinter.Value))
                {
                    SelectedPrinter = lastPrinter.Value;
                }
                else if (Printers.Count > 0)
                {
                    SelectedPrinter = Printers[0];
                }

                var lastTemplateIdSetting = await context.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "LastTemplateId");
                if (lastTemplateIdSetting != null && int.TryParse(lastTemplateIdSetting.Value, out int tid) && tid > 0)
                {
                    SelectedTemplate = Templates.FirstOrDefault(t => t.Id == tid);
                }

                if (SelectedTemplate == null)
                {
                    SelectedTemplate = Templates.FirstOrDefault(t => t.IsDefault) ?? Templates.FirstOrDefault();
                }
            }
        }

        private async void Print()
        {
            if (CalibrationRecord == null || SelectedTemplate == null) return;

            try
            {
                string infoText = $"{CalibrationRecord.Device?.DeviceType?.Name}\nModel: {CalibrationRecord.Device?.Model}\nS/N: {CalibrationRecord.Device?.SerialNumber}";
                
                string qrContent = _qrService.GenerateVerificationText(
                    ownerName: CalibrationRecord.Device?.Owner?.Name ?? "",
                    deviceType: CalibrationRecord.Device?.DeviceType?.Name ?? "",
                    model: CalibrationRecord.Device?.Model ?? "",
                    serial: CalibrationRecord.Device?.SerialNumber ?? "",
                    certNo: CalibrationRecord.CertificateNumber,
                    calDate: CalibrationRecord.CalibrationDate.ToString("yyyy-MM-dd"),
                    expDate: CalibrationRecord.ExpiryDate.ToString("yyyy-MM-dd"),
                    engineerName: CalibrationRecord.EngineerName,
                    description: CalibrationRecord.CalibrationDescription ?? "",
                    result: CalibrationRecord.Result,
                    verifyCode: CalibrationRecord.HmacSignature
                );

                var qrPrintImage = _qrService.GenerateQrCodeImage(qrContent, 600);

                var job = new QrPrintJob
                {
                    QrImage = qrPrintImage,
                    Template = SelectedTemplate,
                    PrinterName = SelectedPrinter,
                    StartColumn = StartColumn,
                    StartRow = StartRow,
                    CertificateNumber = CalibrationRecord.CertificateNumber,
                    DeviceInfoText = infoText
                };

                _printService.PrintQrLabel(job);
                await _auditLogRepository.LogAsync("طباعة ملصق QR", "CalibrationRecord", CalibrationRecord.Id.ToString(), $"طباعة رمز الاستجابة السريعة للشهادة رقم {CalibrationRecord.CertificateNumber}");

                SaveSettings();

                MessageBox.Show("تم إرسال أمر الطباعة بنجاح.", "تمت الطباعة", MessageBoxButton.OK, MessageBoxImage.Information);
                CloseWindowAction?.Invoke();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ أثناء الطباعة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveQrImage()
        {
            if (CalibrationRecord == null) return;

            try
            {
                _qrService.GenerateAndSaveQrForRecord(
                    ownerName: CalibrationRecord.Device?.Owner?.Name ?? "",
                    deviceType: CalibrationRecord.Device?.DeviceType?.Name ?? "",
                    model: CalibrationRecord.Device?.Model ?? "",
                    serial: CalibrationRecord.Device?.SerialNumber ?? "",
                    certNo: CalibrationRecord.CertificateNumber,
                    calDate: CalibrationRecord.CalibrationDate.ToString("yyyy-MM-dd"),
                    expDate: CalibrationRecord.ExpiryDate.ToString("yyyy-MM-dd"),
                    engineerName: CalibrationRecord.EngineerName,
                    description: CalibrationRecord.CalibrationDescription ?? "",
                    result: CalibrationRecord.Result,
                    verifyCode: CalibrationRecord.HmacSignature
                );

                MessageBox.Show("تم حفظ ملف صورة كود QR بنجاح في مجلد التطبيق.", "تم الحفظ", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ أثناء حفظ الصورة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveSettings()
        {
            Task.Run(async () =>
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var printerSetting = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == "LastPrinterName");
                if (printerSetting != null)
                {
                    printerSetting.Value = SelectedPrinter;
                }
                var templateSetting = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == "LastTemplateId");
                if (templateSetting != null && SelectedTemplate != null)
                {
                    templateSetting.Value = SelectedTemplate.Id.ToString();
                }
                await context.SaveChangesAsync();
            });
        }

        public Action? CloseWindowAction { get; set; }
    }
}
