namespace CAL_QR.Services
{
    public interface IHmacService
    {
        string BuildConcatenatedString(
            string certNo,
            string model,
            string serial,
            string ownerName,
            string calDate,
            string expDate,
            string result,
            string engineerName);

        string ComputeSignature(
            string certNo,
            string model,
            string serial,
            string ownerName,
            string calDate,
            string expDate,
            string result,
            string engineerName);

        void Initialize();

        // ملاحظة أمنية معروفة: التحقق الأوفلاين (بدون قاعدة بيانات) عبر تاريخ المعايرة calDate كبديل، يسمح نظرياً بتزوير شهادة بتاريخ معايرة قديم (قبل تاريخ القطع) عبر القوة الغاشمة على توقيع 8 أحرف دون التحقق من فرادة رقم الشهادة بقاعدة البيانات. هذا خطر متبقٍ مقبول حالياً بناءً على الاستخدام النادر لهذا المسار، وقد تم توثيق ظهور المفتاح القديم (Legacy Key) في محادثة تطوير سابقة، وبالتالي فإن نطاق أي استغلال محتمل لهذا التسريب يقتصر فعلياً على تزوير شهادات "قديمة التاريخ" (قبل تاريخ القطع 14 يوليو 2026) فقط، وليس أي شهادة مستقبلية تعتمد على المفتاح النشط الجديد.
        bool VerifySignature(
            string certNo,
            string model,
            string serial,
            string ownerName,
            string calDate,
            string expDate,
            string result,
            string engineerName,
            string signature,
            System.DateTime? recordCreatedAt = null);
    }
}
