using System;

namespace CAL_QR.Models
{
    public class PaperTemplate
    {
        public int Id { get; set; }
        public string TemplateName { get; set; } = string.Empty;
        public string PaperType { get; set; } = string.Empty; // Roll, A4, Custom
        public decimal PaperWidthMm { get; set; }
        public decimal PaperHeightMm { get; set; }
        public int Columns { get; set; }
        public int Rows { get; set; }
        public decimal LabelWidthMm { get; set; }
        public decimal LabelHeightMm { get; set; }
        public decimal MarginTopMm { get; set; }
        public decimal MarginLeftMm { get; set; }
        public decimal MarginRightMm { get; set; }
        public decimal MarginBottomMm { get; set; }
        public decimal HorizontalGapMm { get; set; }
        public decimal VerticalGapMm { get; set; }
        public bool IsDefault { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
