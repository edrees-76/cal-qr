namespace CAL_QR.Validation
{
    /// <summary>
    /// نصوص أحداث سجلّ التدقيق لعمليّات الجهات المالكة وأنواع الأجهزة (دوالّ نقيّة).
    /// التفصيل بالاسم والمعرّف دون مقارنة قديم/جديد: الشهادات الصادرة منسوخة النصّ ومجمّدة،
    /// فتعديل الجهة أو النوع لا يغيّر شهادة صادرة.
    /// </summary>
    public static class MasterDataAuditRules
    {
        public const string OwnerEntity = "Owner";
        public const string DeviceTypeEntity = "DeviceType";

        public const string OwnerAdded = "إضافة جهة مالكة";
        public const string OwnerEdited = "تعديل جهة مالكة";
        public const string OwnerDeleted = "حذف جهة مالكة";

        public const string DeviceTypeAdded = "إضافة نوع جهاز";
        public const string DeviceTypeEdited = "تعديل قالب نوع جهاز";
        public const string DeviceTypeDeleted = "حذف نوع جهاز";

        /// <summary>ملاحظة تُلحق حين تُنشأ الجهة أو النوع من داخل نموذج المعايرة.</summary>
        public const string NoteFromCalibrationForm = "من نموذج المعايرة";

        /// <summary>ملاحظة تُلحق حين يُعاد تفعيل سجلّ محذوف سابقاً بالاسم نفسه.</summary>
        public const string NoteReactivated = "إعادة تفعيل سجلّ محذوف سابقاً، من نموذج المعايرة";

        /// <summary>تفصيل الحدث: «الإجراء: "الاسم" (معرّف N) [— ملاحظة]».</summary>
        public static string Describe(string action, string? name, int id, string? note = null)
        {
            string shownName = string.IsNullOrWhiteSpace(name) ? "(بلا اسم)" : name.Trim();
            string idPart = id > 0 ? $" (المعرّف {id})" : string.Empty;
            string notePart = string.IsNullOrWhiteSpace(note) ? string.Empty : $" — {note.Trim()}";
            return $"{action}: \"{shownName}\"{idPart}{notePart}";
        }
    }
}
