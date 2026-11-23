using System.Collections.Generic;
using System.Linq;
using CAL_QR.Models;

namespace CAL_QR.Validation
{
    /// <summary>
    /// قواعد اختيار الطابعة والقالب في معاينة الطباعة (دوالّ نقيّة قابلة للاختبار).
    /// </summary>
    public static class PrintSelectionRules
    {
        /// <summary>
        /// الطابعة: الحاليّة إن كانت ما زالت متاحة (لا تُطمَس باختيار المستخدم اليدويّ عند إعادة التحميل)،
        /// ثمّ آخر طابعة محفوظة، ثمّ الأولى، وإلا سلسلة فارغة.
        /// </summary>
        public static string PickPrinter(IReadOnlyList<string> printers, string? current, string? lastSaved)
        {
            if (!string.IsNullOrWhiteSpace(current) && printers.Contains(current))
                return current;
            if (!string.IsNullOrWhiteSpace(lastSaved) && printers.Contains(lastSaved))
                return lastSaved;
            return printers.Count > 0 ? printers[0] : string.Empty;
        }

        /// <summary>
        /// القالب: المطلوب صراحةً (القالب المُنشأ للتوّ) ثمّ آخر قالب محفوظ ثمّ الافتراضيّ ثمّ الأوّل.
        /// معرّف مطلوب غير موجود في القائمة يُتجاوَز إلى التالي.
        /// </summary>
        public static PaperTemplate? PickTemplate(
            IReadOnlyList<PaperTemplate> templates, int? preferredId, int? lastSavedId)
        {
            if (preferredId is > 0)
            {
                var preferred = templates.FirstOrDefault(t => t.Id == preferredId.Value);
                if (preferred != null) return preferred;
            }
            if (lastSavedId is > 0)
            {
                var last = templates.FirstOrDefault(t => t.Id == lastSavedId.Value);
                if (last != null) return last;
            }
            return templates.FirstOrDefault(t => t.IsDefault) ?? templates.FirstOrDefault();
        }
    }
}
