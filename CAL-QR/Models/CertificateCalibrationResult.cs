namespace CAL_QR.Models
{
    /// <summary>
    /// سطر واحد من جدول نتائج المعايرة داخل الشهادة. كل القيم نصية لا رقمية.
    /// </summary>
    public class CertificateCalibrationResult
    {
        public int Id { get; set; }
        public int CertificateId { get; set; }
        public int SortOrder { get; set; }

        public string? SourceId { get; set; }
        public string? Radionuclide { get; set; }
        public string? Scale { get; set; }
        public string? ReferenceValue { get; set; }
        public string? MeasuredReading { get; set; }

        // معامل CF: يقابل "Correction Factor" في نماذج Pancake،
        // و"Calibration Factor" في نماذج جاما — نفس الاختصار بمصطلحين حسب نوع الجهاز.
        public string? CorrectionFactor { get; set; }

        public string? AbsoluteRelativeError { get; set; }
        public string? Unit { get; set; }
        public string? Remarks { get; set; }

        public virtual Certificate? Certificate { get; set; }
    }
}
