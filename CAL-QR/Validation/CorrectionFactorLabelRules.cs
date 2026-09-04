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
            var flags = isAveragedFlags as IReadOnlyCollection<bool> ?? isAveragedFlags.ToList();

            bool anyAveraged = flags.Any(f => f);
            bool anySingle = flags.Any(f => !f);

            if (anyAveraged && anySingle)
            {
                return "Correction Factor (CF / CFavg)";
            }

            return anyAveraged ? "Average Correction Factor (CFavg)" : "Correction Factor (CF)";
        }

        private static string Normalize(string? value) =>
            value == null ? string.Empty : new string(value.Where(c => !char.IsWhiteSpace(c)).ToArray());
    }
}
