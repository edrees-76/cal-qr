using System;
using System.Collections.Generic;

namespace CAL_QR.Helpers
{
    public static class ArabicFixer
    {
        private struct ArabicChar
        {
            public char Normal;
            public char Isolated;
            public char Final;
            public char Medial;
            public char Initial;
            public bool ConnectsPrevious;
            public bool ConnectsNext;

            public ArabicChar(char normal, char isolated, char final, char medial, char initial, bool connectsPrev, bool connectsNext)
            {
                Normal = normal;
                Isolated = isolated;
                Final = final;
                Medial = medial;
                Initial = initial;
                ConnectsPrevious = connectsPrev;
                ConnectsNext = connectsNext;
            }
        }

        private static readonly Dictionary<char, ArabicChar> CharMap = new()
        {
            { 'آ', new ArabicChar('آ', '\uFE81', '\uFE82', '\uFE82', '\uFE81', true, false) },
            { 'أ', new ArabicChar('أ', '\uFE83', '\uFE84', '\uFE84', '\uFE83', true, false) },
            { 'ؤ', new ArabicChar('ؤ', '\uFE85', '\uFE86', '\uFE86', '\uFE85', true, false) },
            { 'إ', new ArabicChar('إ', '\uFE87', '\uFE88', '\uFE88', '\uFE87', true, false) },
            { 'ئ', new ArabicChar('ئ', '\uFE89', '\uFE8A', '\uFE8C', '\uFE8B', true, true) },
            { 'ا', new ArabicChar('ا', '\uFE8D', '\uFE8E', '\uFE8E', '\uFE8D', true, false) },
            { 'ب', new ArabicChar('ب', '\uFE8F', '\uFE90', '\uFE92', '\uFE91', true, true) },
            { 'ة', new ArabicChar('ة', '\uFE93', '\uFE94', '\uFE94', '\uFE93', true, false) },
            { 'ت', new ArabicChar('ت', '\uFE95', '\uFE96', '\uFE98', '\uFE97', true, true) },
            { 'ث', new ArabicChar('ث', '\uFE99', '\uFE9A', '\uFE9C', '\uFE9B', true, true) },
            { 'ج', new ArabicChar('ج', '\uFE9D', '\uFE9E', '\uFEA0', '\uFE9F', true, true) },
            { 'ح', new ArabicChar('ح', '\uFEA1', '\uFEA2', '\uFEA4', '\uFEA3', true, true) },
            { 'خ', new ArabicChar('خ', '\uFEA5', '\uFEA6', '\uFEA8', '\uFEA7', true, true) },
            { 'د', new ArabicChar('د', '\uFEA9', '\uFEAA', '\uFEAA', '\uFEA9', true, false) },
            { 'ذ', new ArabicChar('ذ', '\uFEAB', '\uFEAC', '\uFEAC', '\uFEAB', true, false) },
            { 'ر', new ArabicChar('ر', '\uFEAD', '\uFEAE', '\uFEAE', '\uFEAD', true, false) },
            { 'ز', new ArabicChar('ز', '\uFEAF', '\uFEB0', '\uFEB0', '\uFEAF', true, false) },
            { 'س', new ArabicChar('س', '\uFEB1', '\uFEB2', '\uFEB4', '\uFEB3', true, true) },
            { 'ش', new ArabicChar('ش', '\uFEB5', '\uFEB6', '\uFEB8', '\uFEB7', true, true) },
            { 'ص', new ArabicChar('ص', '\uFEB9', '\uFEBA', '\uFEBC', '\uFEBB', true, true) },
            { 'ض', new ArabicChar('ض', '\uFEBD', '\uFEBE', '\uFEC0', '\uFEBF', true, true) },
            { 'ط', new ArabicChar('ط', '\uFEC1', '\uFEC2', '\uFEC4', '\uFEC3', true, true) },
            { 'ظ', new ArabicChar('ظ', '\uFEC5', '\uFEC6', '\uFEC8', '\uFEC7', true, true) },
            { 'ع', new ArabicChar('ع', '\uFEC9', '\uFECA', '\uFECC', '\uFECB', true, true) },
            { 'غ', new ArabicChar('غ', '\uFECD', '\uFECE', '\uFED0', '\uFECF', true, true) },
            { 'ف', new ArabicChar('ف', '\uFED1', '\uFED2', '\uFED4', '\uFED3', true, true) },
            { 'ق', new ArabicChar('ق', '\uFED5', '\uFED6', '\uFED8', '\uFED7', true, true) },
            { 'ك', new ArabicChar('ك', '\uFED9', '\uFEDA', '\uFEDC', '\uFEDB', true, true) },
            { 'ل', new ArabicChar('ل', '\uFEDD', '\uFEDE', '\uFEE0', '\uFEDF', true, true) },
            { 'م', new ArabicChar('م', '\uFEE1', '\uFEE2', '\uFEE4', '\uFEE3', true, true) },
            { 'ن', new ArabicChar('ن', '\uFEE5', '\uFEE6', '\uFEE8', '\uFEE7', true, true) },
            { 'ه', new ArabicChar('ه', '\uFEE9', '\uFEEA', '\uFEEC', '\uFEEB', true, true) },
            { 'و', new ArabicChar('و', '\uFEED', '\uFEEE', '\uFEEE', '\uFEED', true, false) },
            { 'ى', new ArabicChar('ى', '\uFEEF', '\uFEF0', '\uFEF0', '\uFEEF', true, false) },
            { 'ي', new ArabicChar('ي', '\uFEF1', '\uFEF2', '\uFEF4', '\uFEF3', true, true) },
            { 'ء', new ArabicChar('ء', '\uFE80', '\uFE80', '\uFE80', '\uFE80', false, false) },

            // Lam-Alef Ligature placeholders
            { '\uE000', new ArabicChar('\uE000', '\uFEF5', '\uFEF6', '\uFEF6', '\uFEF5', true, false) }, // لآ
            { '\uE001', new ArabicChar('\uE001', '\uFEF7', '\uFEF8', '\uFEF8', '\uFEF7', true, false) }, // لأ
            { '\uE002', new ArabicChar('\uE002', '\uFEF9', '\uFEFA', '\uFEFA', '\uFEF9', true, false) }, // لإ
            { '\uE003', new ArabicChar('\uE003', '\uFEFB', '\uFEFC', '\uFEFC', '\uFEFB', true, false) }  // لا
        };

        public static string Fix(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            // Preprocess Lam-Alef ligatures
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] == 'ل' && i + 1 < input.Length)
                {
                    char next = input[i + 1];
                    if (next == 'آ') { sb.Append('\uE000'); i++; }
                    else if (next == 'أ') { sb.Append('\uE001'); i++; }
                    else if (next == 'إ') { sb.Append('\uE002'); i++; }
                    else if (next == 'ا') { sb.Append('\uE003'); i++; }
                    else sb.Append('ل');
                }
                else
                {
                    sb.Append(input[i]);
                }
            }
            string preprocessed = sb.ToString();

            char[] chars = preprocessed.ToCharArray();
            char[] shaped = new char[chars.Length];

            for (int i = 0; i < chars.Length; i++)
            {
                char current = chars[i];
                if (!CharMap.ContainsKey(current))
                {
                    shaped[i] = current;
                    continue;
                }

                bool prevConnects = false;
                if (i > 0 && CharMap.TryGetValue(chars[i - 1], out var prevChar))
                {
                    prevConnects = prevChar.ConnectsNext;
                }

                bool nextConnects = false;
                if (i < chars.Length - 1 && CharMap.TryGetValue(chars[i + 1], out var nextChar))
                {
                    nextConnects = nextChar.ConnectsPrevious;
                }

                var curMap = CharMap[current];
                if (prevConnects && nextConnects)
                {
                    shaped[i] = curMap.Medial;
                }
                else if (prevConnects)
                {
                    shaped[i] = curMap.Final;
                }
                else if (nextConnects)
                {
                    shaped[i] = curMap.Initial;
                }
                else
                {
                    shaped[i] = curMap.Isolated;
                }
            }

            return ReverseArabicAndKeepLtr(new string(shaped));
        }

        private static string ReverseArabicAndKeepLtr(string input)
        {
            var runs = new List<string>();
            var isArabicRun = new List<bool>();

            int start = 0;
            bool currentIsArabic = IsArabicChar(input[0]);

            for (int i = 1; i < input.Length; i++)
            {
                bool isArabic = IsArabicChar(input[i]);
                if (isArabic != currentIsArabic)
                {
                    runs.Add(input.Substring(start, i - start));
                    isArabicRun.Add(currentIsArabic);
                    start = i;
                    currentIsArabic = isArabic;
                }
            }
            runs.Add(input.Substring(start));
            isArabicRun.Add(currentIsArabic);

            var result = new System.Text.StringBuilder();
            for (int i = runs.Count - 1; i >= 0; i--)
            {
                if (isArabicRun[i])
                {
                    char[] arr = runs[i].ToCharArray();
                    Array.Reverse(arr);
                    result.Append(arr);
                }
                else
                {
                    result.Append(runs[i]);
                }
            }

            return result.ToString();
        }

        private static bool IsArabicChar(char c)
        {
            return (c >= 0x0600 && c <= 0x06FF) || (c >= 0xFE70 && c <= 0xFEFF) || c == ' ' || c == ':' || c == '.' || c == '،' || c == '؛' || c == '؟';
        }
    }
}
