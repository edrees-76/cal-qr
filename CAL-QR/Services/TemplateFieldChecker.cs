using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CAL_QR.Services
{
    public static class TemplateFieldChecker
    {
        private static readonly Regex RadionuclidePattern = new Regex(
            @"[A-Z][a-z]?-\d{1,3}", RegexOptions.IgnoreCase);

        private static readonly Regex DistanceInTextPattern = new Regex(
            @"Distance\s*=\s*(\d+\.?\d*)", RegexOptions.IgnoreCase);

        private static readonly Regex FirstNumberPattern = new Regex(@"(\d+\.?\d*)");

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

        public static string? ExtractDistanceNumber(string? referenceGeometry)
        {
            if (string.IsNullOrWhiteSpace(referenceGeometry)) return null;

            var match = DistanceInTextPattern.Match(referenceGeometry);
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }

        public static string? ExtractDistanceNumberFromField(string? distanceField)
        {
            if (string.IsNullOrWhiteSpace(distanceField)) return null;

            var match = FirstNumberPattern.Match(distanceField);
            return match.Success ? match.Groups[1].Value : null;
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
