namespace CAL_QR.Models
{
    /// <summary>
    /// فحص وظيفي واحد من جدول الفحوص الوظيفية داخل الشهادة. كل القيم نصية لا رقمية.
    /// </summary>
    public class CertificateFunctionalCheck
    {
        public int Id { get; set; }
        public int CertificateId { get; set; }
        public int SortOrder { get; set; }

        public string CheckName { get; set; } = string.Empty;
        public string? Requirement { get; set; }
        public string? Result { get; set; }
        public string? Remarks { get; set; }

        public virtual Certificate? Certificate { get; set; }
    }
}
