namespace CAL_QR.Models
{
    /// <summary>
    /// مكوّن واحد من مكوّنات ميزانية عدم اليقين داخل الشهادة. كل القيم نصية لا رقمية.
    /// </summary>
    public class CertificateUncertaintyComponent
    {
        public int Id { get; set; }
        public int CertificateId { get; set; }
        public int SortOrder { get; set; }

        public string ComponentName { get; set; } = string.Empty;
        public string? EvaluationType { get; set; } // "A" | "B"
        public string? StandardUncertainty { get; set; }
        public string? ContributionPercent { get; set; }
        public string? Distribution { get; set; }

        public virtual Certificate? Certificate { get; set; }
    }
}
