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
using CAL_QR.Validation;

namespace CAL_QR.ViewModels
{
    public class PrintPreviewViewModel : BaseViewModel
    {
        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;
        private readonly IPaperTemplateRepository _templateRepository;
        private readonly IPrintService _printService;
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly ICertificateRepository _certificateRepository;

        private CalibrationRecord? _calibrationRecord;
        private ObservableCollection<string> _printers = new();
        private ObservableCollection<PaperTemplate> _templates = new();
        private string _selectedPrinter = string.Empty;
        private PaperTemplate? _selectedTemplate;
        private BitmapSource? _qrImagePreview;

        private int _startColumn = 1;
        private int _startRow = 1;

        // ── كاش بيانات مهامّ المعاينة (بلا Template)، يُبنى مرّةً ويُصيَّر عند كلّ تغيير قالب ──
        private readonly List<QrPrintJob> _previewJobs = new();

        public event EventHandler? RedrawGridRequested;

        public PrintPreviewViewModel(
            IDbContextFactory<CalQrDbContext> contextFactory,
            IPaperTemplateRepository templateRepository,
            IPrintService printService,
            IAuditLogRepository auditLogRepository,
            ICertificateRepository certificateRepository)
        {
            _contextFactory = contextFactory;
            _templateRepository = templateRepository;
            _printService = printService;
            _auditLogRepository = auditLogRepository;
            _certificateRepository = certificateRepository;

            Printers = new ObservableCollection<string>(_printService.GetAvailablePrinters());
            
            PrintCommand = new AsyncRelayCommand(PrintAsync, onError: AsyncRelayCommand.ShowUnexpectedError);
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
                    RefreshLabelPreviews();
                }
            }
        }

        public BitmapSource? QrImagePreview
        {
            get => _qrImagePreview;
            set => SetProperty(ref _qrImagePreview, value);
        }

        private ObservableCollection<BatchPrintItem> _batchItems = new();
        public ObservableCollection<BatchPrintItem> BatchItems
        {
            get => _batchItems;
            set => SetProperty(ref _batchItems, value);
        }

        public string BatchPrintingStatus => $"سيتم طباعة {CalibrationRecords.Count} ملصقات ابتداءً من الصف {StartRow} العمود {StartColumn} بالتسلسل";

        public int StartColumn
        {
            get => _startColumn;
            set
            {
                if (SetProperty(ref _startColumn, value))
                {
                    RedrawGridRequested?.Invoke(this, EventArgs.Empty);
                    OnPropertyChanged(nameof(BatchPrintingStatus));
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
                    OnPropertyChanged(nameof(BatchPrintingStatus));
                }
            }
        }
        #endregion

        public ICommand PrintCommand { get; }

        public List<CalibrationRecord> CalibrationRecords { get; private set; } = new();

        public bool IsBatchMode => CalibrationRecords.Count > 1;

        public string BatchStatusText => IsBatchMode ? $"معاينة دفعة طباعة: {CalibrationRecords.Count} ملصقات" : "معاينة ملصق فردي";

        public async Task LoadDataAsync(int calibrationRecordId, int? preferredTemplateId = null)
        {
            await LoadDataAsync(new List<int> { calibrationRecordId }, preferredTemplateId);
        }

        /// <param name="preferredTemplateId">قالب يُختار صراحةً (القالب المُنشأ للتوّ)؛ الطابعة الحاليّة تبقى مختارة.</param>
        public async Task LoadDataAsync(List<int> calibrationRecordIds, int? preferredTemplateId = null)
        {
            using (var context = await _contextFactory.CreateDbContextAsync())
            {
                CalibrationRecords = await context.CalibrationRecords
                    .AsNoTracking()
                    .Include(r => r.Device!)
                        .ThenInclude(d => d.Owner)
                    .Include(r => r.Device!)
                        .ThenInclude(d => d.DeviceType)
                    .Where(r => calibrationRecordIds.Contains(r.Id) && !r.IsDeleted)
                    .ToListAsync();
            }

            OnPropertyChanged(nameof(IsBatchMode));
            OnPropertyChanged(nameof(BatchStatusText));

            BatchItems.Clear();
            _previewJobs.Clear();
            int idx = 1;
            foreach (var record in CalibrationRecords)
            {
                // ── بيانات الملصق من المصدر الواحد (الشهادة المجمّدة) — تخطّي ما لا يُطبع ──
                var (job, _) = await BuildJobDataAsync(record);
                if (job == null)
                    continue;

                _previewJobs.Add(job);

                string infoText = $"{job.DeviceType}\nModel: {job.Model}\nS/N: {job.SerialNumber}\nتاريخ المعايرة: {job.CalibrationDate}\nتاريخ الانتهاء: {job.ExpiryDate}\nكود التحقق: {job.VerifyCode}";
                BatchItems.Add(new BatchPrintItem
                {
                    Index = idx++,
                    CertificateNumber = job.CertificateNumber,
                    DeviceInfo = infoText
                    // ── QrImage يُملأ في RefreshLabelPreviews بصورة الملصق النصّيّ (WYSIWYG) ──
                });
            }

            if (CalibrationRecords.Count > 0)
            {
                CalibrationRecord = CalibrationRecords[0];
            }

            OnPropertyChanged(nameof(BatchPrintingStatus));

            var list = await _templateRepository.GetAllAsync();
            Templates = new ObservableCollection<PaperTemplate>(list);

            using (var context = await _contextFactory.CreateDbContextAsync())
            {
                var lastPrinter = await context.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "LastPrinterName");
                // الطابعة الحاليّة (اختيار المستخدم) تبقى إن كانت متاحة، وإلا المحفوظة ثمّ الأولى.
                SelectedPrinter = PrintSelectionRules.PickPrinter(Printers, SelectedPrinter, lastPrinter?.Value);

                var lastTemplateIdSetting = await context.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "LastTemplateId");
                int? lastTemplateId = lastTemplateIdSetting != null && int.TryParse(lastTemplateIdSetting.Value, out int tid) && tid > 0
                    ? tid
                    : null;

                SelectedTemplate = PrintSelectionRules.PickTemplate(Templates, preferredTemplateId, lastTemplateId);
            }
        }

        // ── المصدر الواحد لبناء بيانات المهمّة من الشهادة المجمّدة (طباعة ومعاينة) ──
        // ── يملأ حقول البيانات فقط؛ Template/PrinterName/StartColumn/StartRow يضبطها المستدعي ──
        private async Task<(QrPrintJob? Job, string? BlockReason)> BuildJobDataAsync(CalibrationRecord record)
        {
            // الشهادة الفعليّة لهذا السجلّ بالرابط الموثوق CalibrationRecordId، لا بالعمود القديم
            // CalibrationRecord.CertificateNumber القابل للتحرير: رقم فيه قد يكون خُصِّص لاحقاً
            // لشهادة سجلّ آخر، فيُطبع ملصق جهاز بكود تحقّق جهاز آخر.
            var certificate = await _certificateRepository
                .GetByCalibrationRecordIdAsync(record.Id);

            // بلا هذا الحارس كانت certificate! تتّكئ على سلوك دالّة في ملفّ آخر
            // لا يراه المترجم، فلا يستطيع التحقّق من عدم الفراغ بنفسه.
            if (certificate == null)
                return (null, CalibrationLabelPolicy.NoCertificateReason);

            var blockReason = CalibrationLabelPolicy.GetBlockReason(certificate);
            if (blockReason != null)
                return (null, blockReason);

            // التسمية (CF/CFavg) مشتقّة من عدد صفوف كلّ نظير — نفس قاعدة شريط ملخّص الشهادة.
            // اشتقاق وقت الطباعة من بيانات مجمّدة: لا يمسّ الشهادة ولا حمولة توقيعها.
            var nuclideLines = CorrectionFactorLabelRules.NuclideLines(
                certificate.NuclideSummaries, certificate.CalibrationResults);
            string correctionFactorLabel = CorrectionFactorLabelRules.ShortLabelFor(
                CorrectionFactorLabelRules.PrintedFlags(certificate.NuclideSummaries, certificate.CalibrationResults));

            return (new QrPrintJob
            {
                CertificateNumber = certificate.CertificateNumber,
                ClientName = certificate.ClientName,
                DeviceType = certificate.CertificateTemplateType ?? "",
                Model = certificate.DeviceModel,
                SerialNumber = certificate.DeviceSerialNumber,
                CalibrationDate = certificate.CalibrationDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                ExpiryDate = certificate.DueDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                VerifyCode = certificate.VerifyCode ?? "",
                NuclideLines = nuclideLines,
                CorrectionFactorLabel = correctionFactorLabel
            }, null);
        }

        // ── تصيير صور المعاينة (الفردي + الدفعيّ) بالملصق النصّيّ نفسه عبر PrintService — WYSIWYG ──
        private void RefreshLabelPreviews()
        {
            if (SelectedTemplate == null
                || SelectedTemplate.LabelWidthMm <= 0
                || SelectedTemplate.LabelHeightMm <= 0)
            {
                QrImagePreview = null;
                foreach (var item in BatchItems)
                    item.QrImage = null;
                return;
            }

            QrImagePreview = _previewJobs.Count > 0
                ? _printService.RenderLabelPreview(_previewJobs[0], SelectedTemplate)
                : null;

            for (int i = 0; i < BatchItems.Count && i < _previewJobs.Count; i++)
                BatchItems[i].QrImage = _printService.RenderLabelPreview(_previewJobs[i], SelectedTemplate);
        }

        private async Task PrintAsync()
        {
            if (CalibrationRecords.Count == 0 || SelectedTemplate == null) return;

            try
            {
                int currentColumn = StartColumn;
                int currentRow = StartRow;

                var jobs = new List<QrPrintJob>();
                var blockReasons = new List<string>();
                foreach (var record in CalibrationRecords)
                {
                    // ── بيانات المهمّة من المصدر الواحد (الشهادة المجمّدة) ──
                    var (job, blockReason) = await BuildJobDataAsync(record);
                    if (job == null)
                    {
                        if (blockReason != null && !blockReasons.Contains(blockReason))
                            blockReasons.Add(blockReason);
                        continue;
                    }

                    job.Template = SelectedTemplate;
                    job.PrinterName = SelectedPrinter;
                    job.StartColumn = currentColumn;
                    job.StartRow = currentRow;
                    jobs.Add(job);

                    if (SelectedTemplate.PaperType != "Roll")
                    {
                        currentColumn++;
                        if (currentColumn > SelectedTemplate.Columns)
                        {
                            currentColumn = 1;
                            currentRow++;
                        }
                    }
                }

                // بلا هذا الحارس كان فرع else يستدعي PrintMultipleQrLabels بقائمة
                // فارغة ثمّ يسجّل في سجلّ التدقيق حدث طباعة لم يقع.
                if (jobs.Count == 0)
                {
                    string message = blockReasons.Count > 0
                        ? string.Join("\n", blockReasons)
                        : "السجلّات المحدّدة لا ملصق لها.";
                    MessageBox.Show(message, "لا يوجد ما يُطبع", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (jobs.Count == 1)
                {
                    _printService.PrintQrLabel(jobs[0]);
                    await _auditLogRepository.LogAsync("طباعة ملصق QR", "CalibrationRecord", CalibrationRecords[0].Id.ToString(), $"طباعة رمز الاستجابة السريعة للشهادة رقم {CalibrationRecords[0].CertificateNumber}");
                }
                else
                {
                    _printService.PrintMultipleQrLabels(jobs);
                    string certNumbers = string.Join(", ", CalibrationRecords.Select(r => r.CertificateNumber));
                    await _auditLogRepository.LogAsync("طباعة متعددة ملصقات QR", "Devices", "", $"طباعة رمز الاستجابة السريعة للشهادات: {certNumbers}");
                }

                // الطباعة تمّت: فشل حفظ آخر طابعة/قالب يُسجَّل ولا يُعرض «خطأ أثناء الطباعة».
                await TrySaveLastPrintSettingsAsync(_contextFactory, SelectedPrinter, SelectedTemplate?.Id);

                MessageBox.Show("تم إرسال أمر الطباعة بنجاح.", "تمت الطباعة", MessageBoxButton.OK, MessageBoxImage.Information);
                CloseWindowAction?.Invoke();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ أثناء الطباعة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// يحفظ آخر طابعة وقالب لتُختارا في الطباعة التالية. كان إطلاقاً بلا انتظار (Task.Run)
        /// فيضيع فشله بلا أثر، ويقرأ الخصائص من خيط آخر بعد إغلاق النافذة. القيم تُمرَّر الآن من
        /// خيط الواجهة، ويُنتظر الحفظ، وفشله يُكتب في AppLog ولا يُلقى: الطباعة نفسها نجحت.
        /// </summary>
        internal static async Task TrySaveLastPrintSettingsAsync(
            IDbContextFactory<CalQrDbContext> contextFactory, string printerName, int? templateId)
        {
            try
            {
                using var context = await contextFactory.CreateDbContextAsync();
                var printerSetting = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == "LastPrinterName");
                if (printerSetting != null)
                {
                    printerSetting.Value = printerName;
                }
                var templateSetting = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == "LastTemplateId");
                if (templateSetting != null && templateId.HasValue)
                {
                    templateSetting.Value = templateId.Value.ToString();
                }
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                AppLog.Error("PrintPreview: saving last printer/template failed after a successful print", ex);
            }
        }

        public Action? CloseWindowAction { get; set; }
    }

    public class BatchPrintItem : BaseViewModel
    {
        public int Index { get; set; }
        public string CertificateNumber { get; set; } = string.Empty;
        public string DeviceInfo { get; set; } = string.Empty;

        private BitmapSource? _qrImage;
        public BitmapSource? QrImage
        {
            get => _qrImage;
            set => SetProperty(ref _qrImage, value);
        }
    }
}
