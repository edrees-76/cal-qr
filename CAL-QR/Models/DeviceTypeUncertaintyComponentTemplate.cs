namespace CAL_QR.Models
{
    /// <summary>
    /// قالب مكوّن واحد من ميزانية عدم اليقين على مستوى نوع الجهاز. يُنسخ نصاً
    /// إلى CertificateUncertaintyComponent وقت إصدار الشهادة.
    ///
    /// أسماء الأعمدة مطابقة عمداً لأعمدة الكيان الهدف — النسخ خريطة حقل-بحقل.
    ///
    /// StandardUncertainty و ContributionPercent يبقيان فارغين في القوالب المزروعة:
    /// اسم المكوّن ثابت لنوع الجهاز، وقيمته تتغير بكل معايرة. زرع قيمة هنا كان
    /// سيُنتج ميزانية عدم يقين تبدو محسوبة وهي منسوخة.
    /// </summary>
    public class DeviceTypeUncertaintyComponentTemplate
    {
        public int Id { get; set; }
        public int DeviceTypeId { get; set; }
        public int SortOrder { get; set; }

        public string ComponentName { get; set; } = string.Empty;
        public string? EvaluationType { get; set; }
        public string? StandardUncertainty { get; set; }
        public string? ContributionPercent { get; set; }
        public string? Distribution { get; set; }

        public virtual DeviceType? DeviceType { get; set; }
    }
}
