using System.Collections.Generic;
using CAL_QR.Models;

namespace CAL_QR.Validation
{
    /// <summary>
    /// تنبيه المدير بحصيلة بذر أنواع الأجهزة (التبعية الحاجزة رقم 7 في القسم 10.0 من ملفّ القرارات).
    ///
    /// الحصيلة يكتبها DeviceTypeSeeder في AppSettings عند الإقلاع، ولم يكن أحد يقرؤها. هنا يُقرَّر
    /// من يراها وكيف تُصاغ؛ دوالّ نقيّة لتُختبر بلا واجهة ولا قاعدة.
    /// </summary>
    public static class DeviceTypeSeedNoticeRules
    {
        public const string Title = "تنبيه: أنواع الأجهزة وقوالبها";

        /// <summary>التنبيه للمدير وحده، كما ينصّ القرار.</summary>
        public static bool CanSeeNotice(User? user) =>
            user != null && user.IsActive && user.Role == UserRole.Admin;

        /// <summary>
        /// في الحصيلة تعارض أو نوع مخفي يُقرّ به المدير فيُمحى. رسائل الفشل لا تُمحى بالإقرار:
        /// يمحوها البذر نفسه عند نجاح المحاولة التالية.
        /// </summary>
        public static bool HasAcknowledgeableConflicts(string? conflicts) =>
            !string.IsNullOrWhiteSpace(conflicts);

        /// <summary>
        /// نصّ التنبيه، أو null إن لم يكن ثمّة ما يُعرض. النصوص المخزَّنة تُعرض كما هي.
        /// </summary>
        public static string? BuildMessage(string? conflicts, string? seedFailure, string? repairFailure)
        {
            var sections = new List<string>();

            if (!string.IsNullOrWhiteSpace(seedFailure))
                sections.Add(seedFailure.Trim());

            if (!string.IsNullOrWhiteSpace(repairFailure))
                sections.Add(repairFailure.Trim());

            if (HasAcknowledgeableConflicts(conflicts))
                sections.Add("حصيلة فحص أنواع الأجهزة عند الإقلاع:\n" + conflicts!.Trim());

            if (sections.Count == 0)
                return null;

            string message = string.Join("\n\n", sections);

            if (HasAcknowledgeableConflicts(conflicts))
            {
                message += "\n\nهل اطّلعت على حصيلة الفحص وتريد إخفاءها؟\n" +
                           "«نعم» تمحوها وتُسجَّل في سجلّ العمليّات؛ «لا» تُبقيها لتظهر عند الدخول التالي.";
            }

            return message;
        }
    }
}
