using System;
using System.Text.RegularExpressions;

namespace CAL_QR.Validation
{
    /// <summary>
    /// موضوع سطر العنوان «CALIBRATION CERTIFICATE FOR …» في الشهادة. قوالب م. رضا تضع فيه
    /// الجهاز بموديله (G501: «Gamma Scintillation Probe (Model 44-2)»). يُشتقّ وقت العرض من
    /// حقلَين مجمّدين في الشهادة — اسم النوع وDeviceModel — فلا يغيّر قيمة مخزّنة ولا حمولة التوقيع.
    ///
    /// القاعدة (بقرار إدريس بعد تجربة بيانات بموديلات قصيرة مثل «DRM-620»):
    ///   • الموديل يذكر اسم النوع ⟵ الموديل كما هو (لا تكرار).
    ///   • وإلّا ⟵ «النوع (الموديل)».
    ///   • أحدهما فارغ ⟵ الآخر وحده؛ كلاهما فارغ ⟵ null (لا سطر).
    /// المقارنة تتجاهل حالة الأحرف والمسافات المكرّرة (قالب G501 نفسه فيه «Scintillation  Probe»).
    /// </summary>
    public static class CertificateTitleRules
    {
        public static string? Subject(string? deviceModel, string? templateType)
        {
            string model = Normalize(deviceModel);
            string type = Normalize(templateType);

            if (model.Length == 0) return type.Length == 0 ? null : type;
            if (type.Length == 0) return model;

            return model.Contains(type, StringComparison.OrdinalIgnoreCase)
                ? model
                : $"{type} ({model})";
        }

        private static string Normalize(string? value) =>
            string.IsNullOrWhiteSpace(value) ? string.Empty : Regex.Replace(value.Trim(), @"\s+", " ");
    }
}
