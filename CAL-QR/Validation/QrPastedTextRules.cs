using System;
using System.Collections.Generic;
using CAL_QR.Models;

namespace CAL_QR.Validation
{
    /// <summary>حقل في نصّ QR الملصوق يخالف سجلّ الشهادة المسترجَع بالكود.</summary>
    public sealed record QrFieldMismatch(string Label, string Pasted, string Recorded);

    /// <summary>
    /// قواعد نقيّة لنصّ QR الملصوق (أسطر <c>L:</c> و<c>C:</c> و<c>SN:</c> و<c>CN:</c> و<c>V:</c>، كما
    /// يبنيها <c>CertificateSignatureService</c>). الكود <c>V:</c> وحده يحدّد السجلّ؛ فورقة معدَّلة
    /// الاسم أو الرقم التسلسليّ أو رقم الشهادة مع كود منسوخ من شهادة صالحة تبقى مقبولة ما لم تُقارَن
    /// الحقول الأخرى بالسجلّ.
    /// </summary>
    public static class QrPastedTextRules
    {
        // الترتيب مهمّ: "SN:" و"CN:" قبل "C:" حتى لا يُلتبس البادئ (لا يتقاطعان فعلاً، لكنّه أوضح).
        private static readonly string[] Keys = { "SN", "CN", "L", "C", "V" };

        /// <summary>يستخرج الحقول المعروفة (أوّل ظهور لكلّ مفتاح). المفتاح بحروف كبيرة، القيمة مقصوصة.</summary>
        public static IReadOnlyDictionary<string, string> Parse(string? text)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(text)) return result;

            foreach (string rawLine in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string line = rawLine.TrimStart();
                foreach (string key in Keys)
                {
                    string prefix = key + ":";
                    if (line.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        if (!result.ContainsKey(key))
                        {
                            result[key] = line.Substring(prefix.Length).Trim();
                        }
                        break;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// يقارن الحقول الحاضرة في النصّ الملصوق (C وSN وCN) بسجلّ الشهادة. حقل غائب عن النصّ لا
        /// يُعدّ اختلافاً. L (رمز المختبر) ثابت فلا يُقارَن.
        /// </summary>
        public static IReadOnlyList<QrFieldMismatch> FindMismatches(
            IReadOnlyDictionary<string, string> pasted, Certificate certificate)
        {
            var mismatches = new List<QrFieldMismatch>();
            Check(pasted, "C", "اسم العميل", certificate.ClientName, mismatches);
            Check(pasted, "SN", "الرقم التسلسلي", certificate.DeviceSerialNumber, mismatches);
            Check(pasted, "CN", "رقم الشهادة", certificate.CertificateNumber, mismatches);
            return mismatches;
        }

        private static void Check(
            IReadOnlyDictionary<string, string> pasted, string key, string label,
            string? recorded, List<QrFieldMismatch> into)
        {
            if (!pasted.TryGetValue(key, out string? value)) return;

            if (!string.Equals(Normalize(value), Normalize(recorded), StringComparison.OrdinalIgnoreCase))
            {
                into.Add(new QrFieldMismatch(label, value, (recorded ?? string.Empty).Trim()));
            }
        }

        /// <summary>قصّ وطيّ تتابع الفراغات (اللصق قد يغيّر الفراغات دون أن يغيّر المعنى).</summary>
        private static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            return string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
