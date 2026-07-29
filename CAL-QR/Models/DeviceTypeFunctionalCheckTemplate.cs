namespace CAL_QR.Models
{
    /// <summary>
    /// قالب فحص وظيفي واحد على مستوى نوع الجهاز. يُنسخ نصاً إلى
    /// CertificateFunctionalCheck وقت إصدار الشهادة، ثم يُجمَّد فيها.
    ///
    /// أسماء الأعمدة مطابقة عمداً لأعمدة الكيان الهدف، فيصير النسخ خريطة
    /// ميكانيكية حقلاً بحقل وقابلة للاختبار.
    ///
    /// الاستثناء الوحيد DefaultResult: نظيره في الشهادة اسمه Result ويملؤه
    /// المعايِر لا القالب — والقالب يقترح قيمة ابتدائية فقط ("Yes" في نماذج
    /// Pancake، "Acceptable" في البقية). الاختلاف مقصود ومنقول من النماذج
    /// الفعلية، ولا يُوحَّد: النص يُطبع كما هو على الشهادة.
    /// </summary>
    public class DeviceTypeFunctionalCheckTemplate
    {
        public int Id { get; set; }
        public int DeviceTypeId { get; set; }
        public int SortOrder { get; set; }

        public string CheckName { get; set; } = string.Empty;
        public string? Requirement { get; set; }
        public string? DefaultResult { get; set; }

        public virtual DeviceType? DeviceType { get; set; }
    }
}
