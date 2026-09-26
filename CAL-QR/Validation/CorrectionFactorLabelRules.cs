using System;
using System.Collections.Generic;
using System.Linq;
using CAL_QR.Models;

namespace CAL_QR.Validation
{
    /// <summary>
    /// قواعد اشتقاق تسمية معامل التصحيح (CF/CFavg).
    /// </summary>
    public static class CorrectionFactorLabelRules
    {
        /// <summary>
        /// هل نظير معطى ممثَّل بأكثر من صفّ نتيجة (⇒ CFavg) أم بصفّ واحد أو بلا صفّ (⇒ CF).
        ///
        /// المطابقة: حذف **كلّ** المسافات من الطرفين (لا Trim فقط) + OrdinalIgnoreCase.
        /// السبب: قالب B401 يكتب "Sr-90 / Y-90" بمسافات حول الشرطة المائلة، والمستخدم
        /// يكتبها "Sr-90/Y-90" — وهما نفس النظير. مطابقة ساذجة تفشل هنا فتطبع CF على
        /// متوسّط حقيقيّ. (هذا يعالج اختلاف الكتابة لا اختلاف النظير.)
        /// </summary>
        public static bool IsAveraged(string? radionuclide, IEnumerable<CertificateCalibrationResult> rows)
        {
            if (rows == null)
            {
                return false;
            }

            string normalizedTarget = Normalize(radionuclide);

            int count = rows.Count(row => string.Equals(Normalize(row?.Radionuclide), normalizedTarget, StringComparison.OrdinalIgnoreCase));
            return count > 1;
        }

        public static string LabelFor(bool isAveraged) => isAveraged ? "CFavg" : "CF";

        /// <summary>
        /// رأس العمود/الشريط الذي يجمع نويدات متعدّدة: يتبع محتواها الفعليّ لا نصًّا
        /// ثابتًا. مصدر واحد يقرأ منه موضعان — شريط ملخّص PDF ورأس عمود الشاشة —
        /// كي لا يتباعد منطقان متوازيان بمرور الوقت.
        ///
        ///   كلّ النظائر متوسّطة  ⇒ "Average Correction Factor (CFavg)"
        ///   كلّها مفردة          ⇒ "Correction Factor (CF)"
        ///   مختلطة               ⇒ "Correction Factor (CF / CFavg)"
        ///   بلا نظائر            ⇒ "Correction Factor (CF)" (لا متوسّط لغياب البيانات)
        /// </summary>
        public static string HeaderFor(IEnumerable<bool> isAveragedFlags)
        {
            string shortLabel = ShortLabelFor(isAveragedFlags);

            return shortLabel == "CFavg"
                ? $"Average Correction Factor ({shortLabel})"
                : $"Correction Factor ({shortLabel})";
        }

        /// <summary>
        /// التسمية المختصرة لمجموعة نظائر: "CFavg" (كلّها متوسّطة) · "CF" (كلّها مفردة
        /// أو لا نظائر) · "CF / CFavg" (مختلطة). مصدر HeaderFor ونصّ احتياط الملصق.
        /// </summary>
        public static string ShortLabelFor(IEnumerable<bool> isAveragedFlags)
        {
            var flags = isAveragedFlags as IReadOnlyCollection<bool> ?? isAveragedFlags.ToList();

            bool anyAveraged = flags.Any(f => f);
            bool anySingle = flags.Any(f => !f);

            if (anyAveraged && anySingle)
            {
                return "CF / CFavg";
            }

            return anyAveraged ? "CFavg" : "CF";
        }

        /// <summary>
        /// النظائر التي تُطبع قيمتها: ملخّصات بقيمة CF غير فارغة، بترتيبها المعروض.
        /// نفس المجموعة في شريط ملخّص PDF وسطور الملصق ومعادلة القراءة المصحّحة.
        /// </summary>
        public static List<CertificateNuclideSummary> PrintedSummaries(IEnumerable<CertificateNuclideSummary>? summaries) =>
            (summaries ?? Enumerable.Empty<CertificateNuclideSummary>())
                .OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
                .Where(s => !string.IsNullOrWhiteSpace(s.AverageCorrectionFactor))
                .ToList();

        /// <summary>
        /// أعلام التوسيط للنظائر المطبوعة (PrintedSummaries) بالترتيب نفسه.
        /// </summary>
        public static List<bool> PrintedFlags(
            IEnumerable<CertificateNuclideSummary>? summaries,
            IEnumerable<CertificateCalibrationResult>? rows)
        {
            var results = rows?.ToList() ?? new List<CertificateCalibrationResult>();
            return PrintedSummaries(summaries).Select(s => IsAveraged(s.Radionuclide, results)).ToList();
        }

        /// <summary>
        /// سطر لكلّ نظير مطبوع: "التسمية النظير = القيمة" (مثل "CFavg Sr-90/Y-90 = 1.02").
        /// مصدر واحد لشريط ملخّص PDF (أكثر من نظير) وسطور الملصق.
        /// </summary>
        public static List<string> NuclideLines(
            IEnumerable<CertificateNuclideSummary>? summaries,
            IEnumerable<CertificateCalibrationResult>? rows)
        {
            var results = rows?.ToList() ?? new List<CertificateCalibrationResult>();
            return PrintedSummaries(summaries)
                .Select(s => $"{LabelFor(IsAveraged(s.Radionuclide, results))} {s.Radionuclide} = {s.AverageCorrectionFactor!.Trim()}")
                .ToList();
        }

        // نصّا المعادلة كما في قوالب رضا (DeviceTypeCatalog) حرفيًّا.
        public const string CfFormula = "Corrected Reading = Measured Reading × CF";
        public const string CfAvgFormula = "Corrected Reading = Measured Reading × CFavg";

        /// <summary>
        /// معادلة القراءة المصحّحة تتبع تسمية النظائر المطبوعة. تُستبدل فقط إن كان النصّ
        /// الحاليّ أحد نصّي القالب (مقارنة بلا مسافات ولا حالة أحرف)؛ النصّ الذي كتبه
        /// المشغّل بنفسه يُعاد كما هو.
        ///
        ///   كلّها متوسّطة ⇒ CfAvgFormula
        ///   كلّها مفردة   ⇒ CfFormula
        ///   مختلطة أو بلا نظائر ⇒ بلا تغيير — القالب لا يحوي معادلة مختلطة، والعائلة
        ///   المبسّطة وتقرير الحالة لا نظائر لهما فتبقى معادلتهما كما هي.
        /// </summary>
        public static string? FormulaFor(string? currentFormula, IEnumerable<bool> isAveragedFlags)
        {
            string normalized = Normalize(currentFormula);
            bool isTemplateText =
                string.Equals(normalized, Normalize(CfFormula), StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, Normalize(CfAvgFormula), StringComparison.OrdinalIgnoreCase);
            if (!isTemplateText)
            {
                return currentFormula;
            }

            var flags = isAveragedFlags as IReadOnlyCollection<bool> ?? isAveragedFlags.ToList();
            if (flags.Count == 0)
            {
                return currentFormula;
            }

            bool anyAveraged = flags.Any(f => f);
            bool anySingle = flags.Any(f => !f);
            if (anyAveraged && anySingle)
            {
                return currentFormula;
            }

            string target = anyAveraged ? CfAvgFormula : CfFormula;

            // النصّ صحيح أصلاً (ولو بمسافات مختلفة): لا تغيير، كي لا يتبدّل نصّ مخزَّن بلا داعٍ.
            return string.Equals(normalized, Normalize(target), StringComparison.OrdinalIgnoreCase)
                ? currentFormula
                : target;
        }

        private static string Normalize(string? value) =>
            value == null ? string.Empty : new string(value.Where(c => !char.IsWhiteSpace(c)).ToArray());
    }
}
