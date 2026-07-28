using System;
using System.Globalization;
using System.Text;

namespace CAL_QR.Services.Payloads
{
    /// <summary>
    /// تطبيع القيم قبل التوقيع.
    ///
    /// ⚠ مجمَّد مع SIG1. أي تغيير في سلوك أي دالّة هنا يُبطل التحقق من كل شهادة
    /// موقّعة بـ SIG1 تماماً كتغيير ترتيب الحقول. التغيير يكون بإصدار جديد لا بتعديل.
    ///
    /// المبدأ: يُوقَّع النص المطبوع نفسه — لا تُعاد صياغته رقمياً. لا Parse ولا
    /// ToString("F4"): القيم قد تكون غير رقمية ("&lt; 0.1"، "N/A")، وإعادة التنسيق
    /// تُتلف الأرقام المعنوية ("5.40" تصير "5.4000" أو "5.4") فيغطي التوقيع قيمة
    /// لم تُطبع قط. التواريخ وحدها تُنسَّق، لأنها DateTime لا نص.
    ///
    /// خطر متبقٍ مقبول ومسجَّل: التطبيع يعتمد Unicode NFC، وجداول التطبيع قد تتغير
    /// بين إصدارات ICU المشحونة مع ويندوز. تحديث نظام قد ينقل محرفاً نادراً من صورة
    /// إلى أخرى فيُبطل توقيعاً سليماً. الاحتمال ضئيل جداً مع النص العربي واللاتيني
    /// المستعمل في هذه الشهادات، والخطر مقبول — على غرار الخطر المتبقي الموثّق في
    /// HmacService.VerifySignature.
    /// </summary>
    public static class PayloadNormalizer
    {
        /// <summary>رمز القيمة الخالية. null و"" و"   " كلها تُنتجه — لا تمييز بينها.</summary>
        public const string EmptyMarker = "~";

        /// <summary>تطبيع نص: Trim ← NFC ← طيّ الأرقام العربية-الهندية ← طيّ المسافات ← هروب.</summary>
        public static string Text(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return EmptyMarker;
            }

            string normalized = value.Trim().Normalize(NormalizationForm.FormC);
            normalized = FoldArabicIndicDigits(normalized);
            normalized = CollapseInnerWhitespace(normalized);

            return normalized.Length == 0 ? EmptyMarker : Escape(normalized);
        }

        /// <summary>تاريخ بصيغة YYYYMMDD تحت InvariantCulture. الجزء الزمني يُهمل عمداً.</summary>
        public static string Date(DateTime value) =>
            value.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        /// <summary>تاريخ اختياري — الخالي يُنتج رمز الخلوّ.</summary>
        public static string Date(DateTime? value) =>
            value.HasValue ? Date(value.Value) : EmptyMarker;

        /// <summary>منطقي بصيغة 0/1.</summary>
        public static string Flag(bool value) => value ? "1" : "0";

        /// <summary>
        /// الهروب — يُطبَّق على كل قيمة قبل الدمج، لا على قيم الصفوف وحدها.
        /// بدونه يستطيع اسم موقّع يحوي ";" أن يُزيح حقول الصف.
        /// الترتيب إلزامي: الشرطة المائلة أولاً، وإلا هُرِّبت مرتين.
        ///
        /// ملاحظة دقّة: فرعا '\n' و'\r' **غير قابلين للوصول** عملياً، لأن
        /// CollapseInnerWhitespace يسبق هذه الدالّة ويحوّل كل محارف المسافات —
        /// ومنها سطرا الإدخال — إلى مسافة واحدة. الحماية من تلفيق سطر حقل كامل
        /// مصدرها ذلك الطيّ لا هذان الفرعان. يبقيان دفاعاً في العمق لو تغيّر
        /// ترتيب الاستدعاء، وحذفهما ممنوع لأنه تغيير في صنف مجمَّد.
        /// </summary>
        private static string Escape(string value)
        {
            var builder = new StringBuilder(value.Length + 8);
            foreach (char c in value)
            {
                switch (c)
                {
                    case '\\': builder.Append("\\\\"); break;
                    case ';': builder.Append("\\;"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    default: builder.Append(c); break;
                }
            }
            return builder.ToString();
        }

        /// <summary>
        /// طيّ الأرقام العربية-الهندية (٠-٩) والفارسية (۰-۹) إلى ASCII، حتى لا
        /// ينتج المُدخَل نفسه توقيعين مختلفين حسب لوحة المفاتيح المستعملة.
        /// </summary>
        private static string FoldArabicIndicDigits(string value)
        {
            var builder = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (c >= '٠' && c <= '٩')
                {
                    builder.Append((char)('0' + (c - '٠')));
                }
                else if (c >= '۰' && c <= '۹')
                {
                    builder.Append((char)('0' + (c - '۰')));
                }
                else
                {
                    builder.Append(c);
                }
            }
            return builder.ToString();
        }

        /// <summary>طيّ كل تتابع مسافات داخلي إلى مسافة واحدة (بما فيه Tab وسطر جديد).</summary>
        private static string CollapseInnerWhitespace(string value)
        {
            var builder = new StringBuilder(value.Length);
            bool previousWasWhitespace = false;

            foreach (char c in value)
            {
                if (char.IsWhiteSpace(c))
                {
                    if (!previousWasWhitespace)
                    {
                        builder.Append(' ');
                        previousWasWhitespace = true;
                    }
                }
                else
                {
                    builder.Append(c);
                    previousWasWhitespace = false;
                }
            }

            return builder.ToString().Trim();
        }
    }
}
