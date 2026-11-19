namespace CAL_QR.Validation
{
    /// <summary>
    /// صفّ «Radiation Source» في قسم CALIBRATION METHODOLOGY يحمل في قوالب م. رضا الجملة
    /// «The calibration was performed using a Cs-137 reference gamma source.». المصدر ليس قالباً
    /// يُبذَر (يتغيّر بكل معايرة، SeedContent-DeviceTypes.md) فيكتبه المعايِر، والـPDF يحذف الصفّ
    /// الفارغ كبقيّة الحقول، فتُطبع الشهادة بلا الصفّ وتخالف القالب بصمت. تحذير لا منع، وفق
    /// قرار الوحدة أنّ لا حقل يمنع الحفظ.
    /// </summary>
    public static class RadiationSourceRules
    {
        /// <summary>الصيغة المرجعيّة الظاهرة تلميحاً في الحقل (النويدة مثال لا قيمة مزروعة).</summary>
        public const string ReferenceSentence =
            "The calibration was performed using a Cs-137 reference gamma source.";

        /// <summary>
        /// رسالة التحذير، أو null إن لم يكن العرض مبسّطاً، أو قسم المنهجية مخفياً (لا يُطبع أصلاً)،
        /// أو كان المصدر مكتوباً.
        /// </summary>
        public static string? Warning(bool simplifiedLayout, bool methodologyEnabled, string? radiationSource)
        {
            if (!simplifiedLayout || !methodologyEnabled) return null;
            if (!string.IsNullOrWhiteSpace(radiationSource)) return null;

            return "حقل Radiation Source فارغ — سيُطبع قسم المنهجية بلا صفّ المصدر الإشعاعيّ الموجود في قالب رضا " +
                   $"(مثال: {ReferenceSentence})";
        }
    }
}
