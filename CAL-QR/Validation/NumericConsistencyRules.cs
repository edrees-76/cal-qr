using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CAL_QR.Models;

namespace CAL_QR.Validation
{
    /// <summary>
    /// CALC-02 (المرحلة 1): فحوص عدديّة <b>غير مانعة</b> على القيم النصّيّة في نموذج الشهادة.
    /// القيم تُخزَّن نصّاً للحفاظ على الأرقام المعنويّة (ISO/IEC 17025)، فلا شيء هنا يعدّل
    /// قيمة ولا يمسّ حمولة التوقيع: الدوالّ تُرجع نصّ تنبيه أو null، والعرض في TemplateWarnings.
    ///
    /// القيمة غير الرقميّة كاملةً («N/A» و«&lt; 0.1» و«5 %» وفارغ) تُتخطّى بصمت: الفحص غير منطبق.
    /// التسامح يُشتقّ من آخر خانة مكتوبة في كلّ قيمة، فلا إنذار كاذباً من التقريب.
    /// </summary>
    public static class NumericConsistencyRules
    {
        private const double Epsilon = 1e-9;

        // ── التحليل ──────────────────────────────────────────────────────────

        private readonly record struct Parsed(double Value, int Decimals);

        /// <summary>
        /// يقبل رقماً كاملاً فقط (إشارة بادئة اختياريّة، أرقاماً عربيّة، فاصلة «,» أو «٫» عشريّة).
        /// أيّ نصّ آخر ⇒ false.
        /// </summary>
        private static bool TryParse(string? text, out Parsed parsed)
        {
            parsed = default;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var sb = new StringBuilder();
            foreach (char c in text.Trim())
            {
                if (char.IsDigit(c)) sb.Append((char)('0' + (int)char.GetNumericValue(c)));
                else if (c == ',' || c == '٫') sb.Append('.');
                else if (c == '−') sb.Append('-');
                else sb.Append(c);
            }

            string normalized = sb.ToString();
            if (!double.TryParse(normalized, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture, out double value))
            {
                return false;
            }

            int dot = normalized.IndexOf('.');
            int decimals = dot < 0 ? 0 : normalized.Length - dot - 1;
            parsed = new Parsed(value, decimals);
            return true;
        }

        /// <summary>نصف وحدة آخر خانة مكتوبة.</summary>
        private static double HalfUnit(int decimals) => 0.5 * Math.Pow(10, -decimals);

        private static string Show(string? s) => (s ?? string.Empty).Trim();

        // ── 1: U ≈ k × uc ───────────────────────────────────────────────────

        /// <summary>
        /// تنبيه إن لم تساوِ U المكتوبة k × uc ضمن تسامح التقريب. يُتخطّى إن كانت أيّ قيمة
        /// من الثلاث غير رقميّة أو كانت k ≤ 0 أو uc سالبة.
        /// </summary>
        public static string? ExpandedUncertaintyWarning(string? combined, string? coverageFactor, string? expanded)
        {
            if (!TryParse(combined, out var uc) || !TryParse(coverageFactor, out var k) ||
                !TryParse(expanded, out var u))
            {
                return null;
            }
            if (k.Value <= 0 || uc.Value < 0) return null;

            double expected = k.Value * uc.Value;
            // k قيمة معلنة دقيقة (2 أو 1.96 أو 2.58) لا ناتج تقريب، فلا تدخل في التسامح؛
            // التسامح من تقريب uc (مضروباً في k) وتقريب U وحدهما.
            double tolerance = HalfUnit(u.Decimals)
                               + k.Value * HalfUnit(uc.Decimals)
                               + Epsilon;

            if (Math.Abs(u.Value - expected) <= tolerance) return null;

            string expectedText = expected.ToString("F" + u.Decimals, CultureInfo.InvariantCulture);
            return $"U المكتوبة ({Show(expanded)}) لا تساوي k × uc (≈ {expectedText}) " +
                   $"بحسب k = {Show(coverageFactor)} وuc = {Show(combined)}؛ راجع الأرقام.";
        }

        // ── 2: مجموع المساهمات ≈ 100 ───────────────────────────────────────

        /// <summary>
        /// تنبيه إن لم يجمع ContributionPercent إلى 100 ضمن التقريب (الجدول يطبع صفّ «100 (%)» ثابتاً).
        /// الصفوف الفارغة كلّياً تُهمَل؛ وإن كان في صفّ ذي محتوى نسبة فارغة أو غير رقميّة يُتخطّى الفحص.
        /// </summary>
        public static string? ContributionSumWarning(IEnumerable<CertificateUncertaintyComponent>? components)
        {
            if (components == null) return null;

            var values = new List<Parsed>();
            foreach (var c in components)
            {
                if (c == null) continue;
                bool hasContent = !string.IsNullOrWhiteSpace(c.ComponentName)
                                  || !string.IsNullOrWhiteSpace(c.StandardUncertainty)
                                  || !string.IsNullOrWhiteSpace(c.ContributionPercent);
                if (!hasContent) continue;

                if (!TryParse(c.ContributionPercent, out var p)) return null;
                values.Add(p);
            }

            if (values.Count == 0) return null;

            double sum = values.Sum(v => v.Value);
            double tolerance = Math.Max(0.5, values.Sum(v => HalfUnit(v.Decimals))) + Epsilon;
            if (Math.Abs(sum - 100.0) <= tolerance) return null;

            string sumText = sum.ToString("0.##", CultureInfo.InvariantCulture);
            return $"مجموع نسب المساهمة {sumText} لا يساوي 100، والجدول يطبع إجمالياً ثابتاً «100 (%)»؛ راجع النسب.";
        }

        // ── 3: k معقول ──────────────────────────────────────────────────────

        /// <summary>تنبيه إن كانت k رقميّة وخارج المدى [1, 3]. k ≠ 2 ليس خطأً بذاته.</summary>
        public static string? CoverageFactorRangeWarning(string? coverageFactor)
        {
            if (!TryParse(coverageFactor, out var k)) return null;
            if (k.Value >= 1.0 - Epsilon && k.Value <= 3.0 + Epsilon) return null;

            return $"معامل التغطية k = {Show(coverageFactor)} خارج المدى المعتاد (1 إلى 3)؛ تأكّد من القيمة.";
        }

        // ── 4: حدود منطقيّة ─────────────────────────────────────────────────

        /// <summary>
        /// uc وU وكلّ StandardUncertainty ≥ 0، وكلّ ContributionPercent بين 0 و100.
        /// تُرجع رسالة لكلّ مخالفة (فارغة إن لم توجد).
        /// </summary>
        public static IReadOnlyList<string> UncertaintyBoundsWarnings(
            string? combined, string? expanded, IEnumerable<CertificateUncertaintyComponent>? components)
        {
            var warnings = new List<string>();

            if (TryParse(combined, out var uc) && uc.Value < 0)
                warnings.Add($"uc المركّبة ({Show(combined)}) سالبة؛ عدم اليقين لا يكون سالباً.");
            if (TryParse(expanded, out var u) && u.Value < 0)
                warnings.Add($"U الموسّعة ({Show(expanded)}) سالبة؛ عدم اليقين لا يكون سالباً.");

            if (components != null)
            {
                foreach (var c in components)
                {
                    if (c == null) continue;
                    string name = string.IsNullOrWhiteSpace(c.ComponentName) ? "مكوّن بلا اسم" : c.ComponentName.Trim();

                    if (TryParse(c.StandardUncertainty, out var s) && s.Value < 0)
                        warnings.Add($"عدم اليقين القياسيّ للمكوّن «{name}» ({Show(c.StandardUncertainty)}) سالب.");
                    if (TryParse(c.ContributionPercent, out var p) && (p.Value < 0 || p.Value > 100 + Epsilon))
                        warnings.Add($"نسبة مساهمة المكوّن «{name}» ({Show(c.ContributionPercent)}) خارج المدى 0 إلى 100.");
                }
            }

            return warnings;
        }

        /// <summary>
        /// ReferenceValue يجب أن تكون &gt; 0 وMeasuredReading ≥ 0 حين تكونان رقميّتين كاملتين.
        /// </summary>
        public static IReadOnlyList<string> ResultBoundsWarnings(IEnumerable<CertificateCalibrationResult>? rows)
        {
            var warnings = new List<string>();
            if (rows == null) return warnings;

            int index = 0;
            foreach (var r in rows)
            {
                index++;
                if (r == null) continue;

                if (TryParse(r.ReferenceValue, out var reference) && reference.Value <= 0)
                    warnings.Add($"القيمة المرجعيّة في صفّ النتائج {index} ({Show(r.ReferenceValue)}) صفر أو سالبة.");
                if (TryParse(r.MeasuredReading, out var reading) && reading.Value < 0)
                    warnings.Add($"القراءة المقاسة في صفّ النتائج {index} ({Show(r.MeasuredReading)}) سالبة.");
            }

            return warnings;
        }

        // ── تجميع ───────────────────────────────────────────────────────────

        /// <summary>
        /// كلّ تنبيهات المرحلة 1. فحوص بلوك عدم اليقين تعمل فقط حين يكون مفعَّلاً (لا يُطبع بدونه)،
        /// وحدود صفوف النتائج تعمل دائماً.
        /// </summary>
        public static IReadOnlyList<string> Warnings(
            bool uncertaintyEnabled,
            string? combined,
            string? coverageFactor,
            string? expanded,
            IEnumerable<CertificateUncertaintyComponent>? components,
            IEnumerable<CertificateCalibrationResult>? results)
        {
            var list = new List<string>();

            if (uncertaintyEnabled)
            {
                var componentList = components?.ToList();

                AddIfNotNull(list, ExpandedUncertaintyWarning(combined, coverageFactor, expanded));
                AddIfNotNull(list, ContributionSumWarning(componentList));
                AddIfNotNull(list, CoverageFactorRangeWarning(coverageFactor));
                list.AddRange(UncertaintyBoundsWarnings(combined, expanded, componentList));
            }

            list.AddRange(ResultBoundsWarnings(results));
            return list;
        }

        private static void AddIfNotNull(List<string> list, string? message)
        {
            if (message != null) list.Add(message);
        }
    }
}
