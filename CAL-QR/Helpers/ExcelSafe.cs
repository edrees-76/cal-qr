namespace CAL_QR.Helpers
{
    /// <summary>
    /// يمنع «حقن الصيغ» في ملفّات Excel المصدَّرة: نصّ حرّ (اسم جهة، وصف…) يبدأ بـ = + - @ أو
    /// Tab/CR قد يُفسَّر صيغةً عند فتح الملفّ. تُسبَق القيمة بفاصلة عليا فتُعرض نصّاً كما هي.
    /// لا تُستعمل على الأرقام ولا على التواريخ المنسَّقة.
    /// </summary>
    public static class ExcelSafe
    {
        public static string Text(string? value)
        {
            if (string.IsNullOrEmpty(value)) return value ?? string.Empty;
            char c = value[0];
            return c is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + value : value;
        }
    }
}
