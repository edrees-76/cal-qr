using System.Globalization;
using System.Text;

namespace CAL_QR.Validation
{
    /// <summary>
    /// عبارة «95% confidence level» تصحّ علميّاً عند k = 2 فقط. اشتقاق وقت العرض من
    /// بيانات مجمّدة: لا يغيّر القيمة المخزَّنة ولا حمولة التوقيع.
    /// </summary>
    public static class CoverageFactorRules
    {
        /// <summary>
        /// true إن كانت القيمة النصّيّة تساوي 2 عدديّاً ("2" و"2.0" و"2,00" و"٢").
        /// القيمة الفارغة أو غير الرقميّة (مثل "N/A") ليست 2.
        /// </summary>
        public static bool IsTwo(string? coverageFactor)
        {
            if (string.IsNullOrWhiteSpace(coverageFactor)) return false;

            var normalized = new StringBuilder();
            foreach (char c in coverageFactor.Trim())
            {
                if (char.IsDigit(c))
                {
                    normalized.Append((char)('0' + (int)char.GetNumericValue(c)));
                }
                else if (c == ',' || c == '٫')
                {
                    normalized.Append('.');
                }
                else
                {
                    normalized.Append(c);
                }
            }

            return double.TryParse(
                       normalized.ToString(),
                       NumberStyles.AllowDecimalPoint,
                       CultureInfo.InvariantCulture,
                       out double value)
                   && value == 2.0;
        }

        /// <summary>اللاحقة التي تُلحق برأس عمود U: العبارة عند k = 2 فقط.</summary>
        public static string ConfidenceSuffix(string? coverageFactor) =>
            IsTwo(coverageFactor) ? " (95% confidence level)" : string.Empty;
    }
}
