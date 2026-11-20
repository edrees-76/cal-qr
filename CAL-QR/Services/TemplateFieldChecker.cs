using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CAL_QR.Services
{
    public enum DistanceUnit
    {
        Unspecified,
        Millimeter,
        Centimeter,
        Meter
    }

    /// <summary>مسافة مقروءة: الرقم كما كُتب، وصيغته الموحَّدة (ASCII ونقطة عشريّة)، وقيمته ووحدته.</summary>
    public sealed class DistanceReading
    {
        public DistanceReading(string rawNumber, string normalizedNumber, decimal value, string rawUnit, DistanceUnit unit)
        {
            RawNumber = rawNumber;
            NormalizedNumber = normalizedNumber;
            Value = value;
            RawUnit = rawUnit;
            Unit = unit;
        }

        public string RawNumber { get; }
        public string NormalizedNumber { get; }
        public decimal Value { get; }
        public string RawUnit { get; }
        public DistanceUnit Unit { get; }

        /// <summary>كما كُتب للعرض في رسالة التحذير: «1.0 meter».</summary>
        public string Display => RawUnit.Length > 0 ? RawNumber + " " + RawUnit : RawNumber;
    }

    public sealed class DistanceComparison
    {
        public DistanceComparison(bool areConsistent, bool canAutoFix, bool unitsDiffer)
        {
            AreConsistent = areConsistent;
            CanAutoFix = canAutoFix;
            UnitsDiffer = unitsDiffer;
        }

        public bool AreConsistent { get; }
        public bool CanAutoFix { get; }
        public bool UnitsDiffer { get; }
    }

    public static class TemplateFieldChecker
    {
        private static readonly Regex RadionuclidePattern = new Regex(
            @"[A-Z][a-z]?-\d{1,3}", RegexOptions.IgnoreCase);

        public static HashSet<string> ExtractRadionuclides(string? text)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(text)) return result;

            foreach (Match match in RadionuclidePattern.Matches(text))
            {
                result.Add(NormalizeNuclide(match.Value));
            }

            return result;
        }

        private static string NormalizeNuclide(string value)
        {
            int dashIndex = value.IndexOf('-');
            string element = value.Substring(0, dashIndex);
            string number = value.Substring(dashIndex);

            string normalizedElement = element.Length switch
            {
                1 => char.ToUpperInvariant(element[0]).ToString(),
                _ => char.ToUpperInvariant(element[0]) + char.ToLowerInvariant(element[1]).ToString()
            };

            return normalizedElement + number;
        }

        public static string? ExtractDistanceNumber(string? referenceGeometry) =>
            ParseGeometryDistance(referenceGeometry)?.RawNumber;

        public static string? ExtractDistanceNumberFromField(string? distanceField) =>
            ParseDistanceField(distanceField)?.RawNumber;

        // ── CALC-03: مقارنة المسافة دلاليّاً (رقم + وحدة) ───────────────────────────────

        // رقم بأيّ أرقام عشريّة يونيكود (\d)، وفاصل عشريّ: "." أو "," أو "٫" (U+066B).
        // فاصل الآلاف لا يُدعم عمداً: مسافات المعايرة بالأمتار والسنتيمترات.
        private const string NumberPattern = @"(?<num>\d+(?:[.,٫]\d+)?)";

        private const string UnitPattern =
            @"(?<unit>millimet(?:er|re)s?|centimet(?:er|re)s?|met(?:er|re)s?|mm|cm|m|ملم|مم|سم|متر)(?![A-Za-z])";

        private static readonly Regex GeometryDistancePattern = new Regex(
            @"Distance\s*=\s*" + NumberPattern + @"(?:\s*" + UnitPattern + ")?", RegexOptions.IgnoreCase);

        private static readonly Regex FieldDistancePattern = new Regex(
            NumberPattern + @"(?:\s*" + UnitPattern + ")?", RegexOptions.IgnoreCase);

        /// <summary>قراءة «Distance = N unit» من نصّ الهندسة المرجعيّة، أو null.</summary>
        public static DistanceReading? ParseGeometryDistance(string? referenceGeometry)
        {
            if (string.IsNullOrWhiteSpace(referenceGeometry)) return null;
            return ToReading(GeometryDistancePattern.Match(referenceGeometry));
        }

        /// <summary>قراءة أوّل رقم (ووحدته إن وُجدت) من حقل المسافة، أو null.</summary>
        public static DistanceReading? ParseDistanceField(string? distanceField)
        {
            if (string.IsNullOrWhiteSpace(distanceField)) return null;
            return ToReading(FieldDistancePattern.Match(distanceField));
        }

        private static DistanceReading? ToReading(Match match)
        {
            if (!match.Success) return null;

            string raw = match.Groups["num"].Value;
            string normalized = NormalizeNumber(raw);
            if (!decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal value))
            {
                return null;
            }

            string rawUnit = match.Groups["unit"].Success ? match.Groups["unit"].Value : string.Empty;
            return new DistanceReading(raw, normalized, value, rawUnit, UnitOf(rawUnit));
        }

        // أرقام يونيكود العشريّة ← ASCII، و"," و"٫" ← ".".
        private static string NormalizeNumber(string raw)
        {
            var sb = new StringBuilder(raw.Length);
            foreach (char c in raw)
            {
                if (char.IsDigit(c))
                {
                    sb.Append((char)('0' + (int)char.GetNumericValue(c)));
                }
                else if (c == '.' || c == ',' || c == '٫')
                {
                    sb.Append('.');
                }
            }
            return sb.ToString();
        }

        private static DistanceUnit UnitOf(string rawUnit)
        {
            if (rawUnit.Length == 0) return DistanceUnit.Unspecified;

            string u = rawUnit.ToLowerInvariant();
            if (u.StartsWith("millimet", StringComparison.Ordinal) || u == "mm" || u == "ملم" || u == "مم")
                return DistanceUnit.Millimeter;
            if (u.StartsWith("centimet", StringComparison.Ordinal) || u == "cm" || u == "سم")
                return DistanceUnit.Centimeter;
            return DistanceUnit.Meter;
        }

        private static decimal MetersPerUnit(DistanceUnit unit) => unit switch
        {
            DistanceUnit.Millimeter => 0.001m,
            DistanceUnit.Centimeter => 0.01m,
            _ => 1m
        };

        /// <summary>
        /// يقارن مسافة الهندسة المرجعيّة بحقل المسافة عدديّاً (1.0 = 1) وبعد توحيد الوحدة
        /// حين تُذكر في الطرفين. الإصلاح التلقائيّ (استبدال الرقم وحده) لا يُعرض إلا حين
        /// تتطابق الوحدتان أو تغيب إحداهما، فلا يُكتب «100 meter» مكان «100 cm».
        /// </summary>
        public static DistanceComparison CompareDistances(DistanceReading geometry, DistanceReading field)
        {
            bool bothSpecified = geometry.Unit != DistanceUnit.Unspecified
                                 && field.Unit != DistanceUnit.Unspecified;

            if (!bothSpecified)
            {
                bool same = geometry.Value == field.Value;
                return new DistanceComparison(same, canAutoFix: !same, unitsDiffer: false);
            }

            bool consistent = geometry.Value * MetersPerUnit(geometry.Unit)
                              == field.Value * MetersPerUnit(field.Unit);
            bool unitsDiffer = geometry.Unit != field.Unit;

            return new DistanceComparison(
                consistent,
                canAutoFix: !consistent && !unitsDiffer,
                unitsDiffer: !consistent && unitsDiffer);
        }

        public static string ReplaceRadionuclide(string text, string oldNuclide, string newNuclide)
        {
            return Regex.Replace(text, Regex.Escape(oldNuclide), newNuclide, RegexOptions.IgnoreCase);
        }

        public static string ReplaceDistanceNumber(string referenceGeometry, string oldNumber, string newNumber)
        {
            string pattern = @"(Distance\s*=\s*)" + Regex.Escape(oldNumber);
            return Regex.Replace(referenceGeometry, pattern, "${1}" + newNumber, RegexOptions.IgnoreCase);
        }
    }
}
