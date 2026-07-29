namespace CAL_QR.Models
{
    /// <summary>
    /// صفّ ملخّص لنويدة واحدة داخل الشهادة: الطبقة الوسطى بين قراءات المصادر الخام
    /// والملصق.
    ///
    ///     قراءات المصادر (CertificateCalibrationResult)
    ///             ↓
    ///     CFavg لكل نويدة (هذا الجدول)  ← الملصق يقرأ من هنا
    ///             ↓
    ///     الشهادة تعرض الطبقتين
    ///
    /// مفتاح التجميع **النويدة** لا نوع الإشعاع: مصدران لنفس النويدة بنشاطين أو
    /// مقياسين مختلفين ⇒ صفّ واحد بمتوسطهما. نويدتان مختلفتان ⇒ صفّان.
    ///
    /// ⚠ AverageCorrectionFactor يُدخَل نصاً كما يكتبه المختبر، و**النظام لا يحسبه**.
    /// هذا التزام بمبدأ التخزين النصي الموثّق في Certificate: القيم قد تكون غير رقمية
    /// ("< 0.1"، "N/A")، وعدد الأرقام المعنوية معلومة قياسية جزء من محتوى الوثيقة.
    /// حساب المتوسط آلياً كان سيُتلف كليهما.
    ///
    /// هذا الجدول استبدل Certificate.AverageCorrectionFactor المفرد، الذي كان يعجز
    /// عن تمثيل أكثر من نويدة واحدة.
    /// </summary>
    public class CertificateNuclideSummary
    {
        public int Id { get; set; }
        public int CertificateId { get; set; }
        public int SortOrder { get; set; }

        public string Radionuclide { get; set; } = string.Empty;
        public string? AverageCorrectionFactor { get; set; }

        public virtual Certificate? Certificate { get; set; }
    }
}
