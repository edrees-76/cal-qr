using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CAL_QR.ViewModels.Base;
using CAL_QR.Models;
using CAL_QR.Repositories;

namespace CAL_QR.ViewModels
{
    public class PaperTemplateViewModel : BaseViewModel
    {
        private readonly IPaperTemplateRepository _templateRepository;

        private int _templateId;
        private string _templateName = string.Empty;
        private string _paperType = "A4";
        private double _paperWidth = 210;
        private double _paperHeight = 297;
        private int _columns = 3;
        private int _rows = 8;
        private double _labelWidth = 70;
        private double _labelHeight = 35;
        private double _marginLeft = 0;
        private double _marginTop = 0;
        private double _gapHorizontal = 0;
        private double _gapVertical = 0;
        private bool _isDefault;

        public event EventHandler? Saved;

        public PaperTemplateViewModel(IPaperTemplateRepository templateRepository)
        {
            _templateRepository = templateRepository;

            SaveCommand = new RelayCommand(async () => await SaveAsync(), CanSave);
        }

        #region Properties
        public string TemplateName { get => _templateName; set => SetProperty(ref _templateName, value); }
        public string PaperType
        {
            get => _paperType;
            set
            {
                if (SetProperty(ref _paperType, value))
                {
                    UpdateDefaultDimensions();
                }
            }
        }
        public double PaperWidth { get => _paperWidth; set => SetProperty(ref _paperWidth, value); }
        public double PaperHeight { get => _paperHeight; set => SetProperty(ref _paperHeight, value); }
        public int Columns { get => _columns; set => SetProperty(ref _columns, value); }
        public int Rows { get => _rows; set => SetProperty(ref _rows, value); }
        public double LabelWidth { get => _labelWidth; set => SetProperty(ref _labelWidth, value); }
        public double LabelHeight { get => _labelHeight; set => SetProperty(ref _labelHeight, value); }
        public double MarginLeft { get => _marginLeft; set => SetProperty(ref _marginLeft, value); }
        public double MarginTop { get => _marginTop; set => SetProperty(ref _marginTop, value); }
        public double GapHorizontal { get => _gapHorizontal; set => SetProperty(ref _gapHorizontal, value); }
        public double GapVertical { get => _gapVertical; set => SetProperty(ref _gapVertical, value); }
        public bool IsDefault { get => _isDefault; set => SetProperty(ref _isDefault, value); }
        #endregion

        public ICommand SaveCommand { get; }

        private void UpdateDefaultDimensions()
        {
            if (PaperType == "A4")
            {
                PaperWidth = 210;
                PaperHeight = 297;
                Columns = 3;
                Rows = 8;
                LabelWidth = 70;
                LabelHeight = 35;
            }
            else if (PaperType == "Roll")
            {
                PaperWidth = 80;
                PaperHeight = 40;
                Columns = 1;
                Rows = 1;
                LabelWidth = 80;
                LabelHeight = 40;
                MarginLeft = 0;
                MarginTop = 0;
                GapHorizontal = 0;
                GapVertical = 0;
            }
        }

        public void LoadTemplate(int id)
        {
            _templateId = id;
            var t = Task.Run(async () => await _templateRepository.GetByIdAsync(id)).Result;
            if (t != null)
            {
                TemplateName = t.TemplateName;
                PaperType = t.PaperType;
                PaperWidth = (double)t.PaperWidthMm;
                PaperHeight = (double)t.PaperHeightMm;
                Columns = t.Columns;
                Rows = t.Rows;
                LabelWidth = (double)t.LabelWidthMm;
                LabelHeight = (double)t.LabelHeightMm;
                MarginLeft = (double)t.MarginLeftMm;
                MarginTop = (double)t.MarginTopMm;
                GapHorizontal = (double)t.HorizontalGapMm;
                GapVertical = (double)t.VerticalGapMm;
                IsDefault = t.IsDefault;
            }
        }

        private bool CanSave()
        {
            return !string.IsNullOrWhiteSpace(TemplateName) &&
                   PaperWidth > 0 && PaperHeight > 0 &&
                   Columns > 0 && Rows > 0 &&
                   LabelWidth > 0 && LabelHeight > 0;
        }

        private async Task SaveAsync()
        {
            try
            {
                // Validate if labels fit within paper size
                double totalRequiredWidth = MarginLeft + (Columns * LabelWidth) + ((Columns - 1) * GapHorizontal);
                double totalRequiredHeight = MarginTop + (Rows * LabelHeight) + ((Rows - 1) * GapVertical);

                if (totalRequiredWidth > PaperWidth)
                {
                    MessageBox.Show(
                        $"تنبيه: العرض الإجمالي المطلوب للملصقات ({totalRequiredWidth} مم) يتجاوز عرض الورقة المحدد ({PaperWidth} مم).\n\n" +
                        "يرجى تقليل عدد الأعمدة، أو تصغير عرض الملصق أو الفجوات الأفقية.",
                        "تنبيه أبعاد القالب",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                    return;
                }

                if (totalRequiredHeight > PaperHeight)
                {
                    MessageBox.Show(
                        $"تنبيه: الارتفاع الإجمالي المطلوب للملصقات ({totalRequiredHeight} مم) يتجاوز ارتفاع الورقة المحدد ({PaperHeight} مم).\n\n" +
                        "يرجى تقليل عدد الصفوف، أو تصغير ارتفاع الملصق أو الفجوات العمودية.",
                        "تنبيه أبعاد القالب",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                    return;
                }

                var template = new PaperTemplate
                {
                    Id = _templateId,
                    TemplateName = TemplateName.Trim(),
                    PaperType = PaperType,
                    PaperWidthMm = (decimal)PaperWidth,
                    PaperHeightMm = (decimal)PaperHeight,
                    Columns = Columns,
                    Rows = Rows,
                    LabelWidthMm = (decimal)LabelWidth,
                    LabelHeightMm = (decimal)LabelHeight,
                    MarginLeftMm = (decimal)MarginLeft,
                    MarginTopMm = (decimal)MarginTop,
                    HorizontalGapMm = (decimal)GapHorizontal,
                    VerticalGapMm = (decimal)GapVertical,
                    IsDefault = IsDefault
                };

                if (_templateId > 0)
                {
                    await _templateRepository.UpdateAsync(template);
                }
                else
                {
                    await _templateRepository.AddAsync(template);
                }

                if (IsDefault)
                {
                    await _templateRepository.SetDefaultAsync(template.Id);
                }

                MessageBox.Show("تم حفظ قالب الطباعة بنجاح.", "تم الحفظ", MessageBoxButton.OK, MessageBoxImage.Information);
                Saved?.Invoke(this, EventArgs.Empty);
                CloseWindowAction?.Invoke();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ أثناء حفظ القالب: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public Action? CloseWindowAction { get; set; }
    }
}
