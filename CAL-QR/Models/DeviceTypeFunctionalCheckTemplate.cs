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
    /// المعايِر لا القالب — والقالب يقترح قيمة ابتدائية فقط ("Acceptable" في
    /// الأنواع الستّة). كان مزروعًا "Yes" في Pancake و Beta بتعليق يزعم أنّ
    /// الفارق مقصود ومنقول من النماذج، وهو غير صحيح: قوالب م. رضا الستّة كلّها
    /// تستعمل "Acceptable"، بما فيها B401 وهو قالب Pancake نفسه.
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
