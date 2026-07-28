namespace CAL_QR.Models
{
    /// <summary>
    /// عدّاد أرقام الشهادات، صفّ واحد لكل سنة ميلادية. عدّاد مشترك بين كل أنواع
    /// الأجهزة، والسنة مشتقة من IssueDate لا CalibrationDate.
    ///
    /// ⚠ هذا الجدول هو المصدر الوحيد للأرقام. لا يجوز إطلاقاً اشتقاق الرقم التالي
    /// من MAX(seq)+1 على جدول الشهادات: رقم أُلغيت شهادته لا يُعاد استخدامه أبداً
    /// (ISO/IEC 17025)، وMAX يُعيد إحياءه بصمت.
    /// </summary>
    public class CertificateSequence
    {
        /// <summary>السنة الميلادية — مفتاح أساسي، فيستحيل صفّان لسنة واحدة.</summary>
        public int Year { get; set; }

        /// <summary>آخر رقم مُخصَّص في هذه السنة. يزيد ولا ينقص أبداً.</summary>
        public int LastNumber { get; set; }
    }
}
