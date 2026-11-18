using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace CAL_QR.Validation
{
    /// <summary>
    /// حدّ أدنى لعدّاد أرقام الشهادات يعيش خارج القاعدة، كي لا تُرجعه الاستعادة.
    ///
    /// المشكلة: نسخة احتياطيّة من الاثنين تحمل عدّاد الاثنين. شهادات الثلاثاء طُبعت وسُلّمت،
    /// فإن استُعيدت نسخة الاثنين يوم الأربعاء أُعطي رقمها لشهادة جديدة — رقمان لشهادتين
    /// (ISO/IEC 17025 يمنع ذلك). SyncCounterWithExistingAsync لا يرى شهادات الثلاثاء لأنّها
    /// ليست في القاعدة المستعادة أصلاً.
    ///
    /// الحدّ مربوط بهويّة القاعدة (بصمة مفتاح التوقيع) لا بالجهاز: القاعدة التجريبيّة لها
    /// مفتاح آخر، فإصدارها لا يرفع حدّ القاعدة الحقيقيّة فيُحدث فجوات في ترقيمها.
    /// </summary>
    public static class CertificateCounterFloorRules
    {
        public const int MaxNumberPerYear = 9999;

        /// <summary>
        /// هويّة سجلّ الترقيم: أوّل 16 خانة من SHA-256 لمفتاح التوقيع. لا يُكشف المفتاح نفسه،
        /// ونسخة القاعدة نفسها (على أيّ جهاز) تحمل المفتاح نفسه فالهويّة نفسها.
        /// </summary>
        public static string? LedgerId(string? hmacSecretKey)
        {
            if (string.IsNullOrWhiteSpace(hmacSecretKey)) return null;

            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(hmacSecretKey));
            return Convert.ToHexString(hash, 0, 8).ToLowerInvariant();
        }

        /// <summary>الأكبر لكلّ سنة من مصادر متعدّدة. القيم غير الموجبة تُهمل.</summary>
        public static Dictionary<int, int> Merge(params IReadOnlyDictionary<int, int>?[] sources)
        {
            var result = new Dictionary<int, int>();
            foreach (var source in sources)
            {
                if (source == null) continue;
                foreach (var (year, number) in source)
                {
                    if (number <= 0) continue;
                    if (!result.TryGetValue(year, out int current) || number > current)
                        result[year] = number;
                }
            }
            return result;
        }

        /// <summary>
        /// السنوات التي يجب رفع عدّادها في القاعدة إلى الحدّ: الحدّ أكبر ممّا فيها.
        /// لا خفض أبداً — رقم صدر لا يعود.
        /// </summary>
        public static List<(int Year, int From, int To)> Raises(
            IReadOnlyDictionary<int, int> current, IReadOnlyDictionary<int, int> floor)
        {
            var raises = new List<(int, int, int)>();
            foreach (var (year, target) in floor.OrderBy(p => p.Key))
            {
                int existing = current.TryGetValue(year, out int value) ? value : 0;
                if (target > existing) raises.Add((year, existing, target));
            }
            return raises;
        }

        /// <summary>
        /// ما يكتبه المدير بعد الاستعادة: آخر رقم صدر فعلاً في السنة. يُقبل المقترح أو أكبر
        /// منه حتّى 9999؛ الأصغر مرفوض لأنّه يُعيد رقماً ربّما صدر.
        /// يُرجع (القيمة، null) أو (null، رسالة الخطأ).
        /// </summary>
        public static (int? Value, string? Error) ParseConfirmed(string? input, int proposed)
        {
            string text = (input ?? string.Empty).Trim();
            if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int value))
                return (null, "اكتب رقماً صحيحاً بلا رموز (مثل 45).");

            if (value < proposed)
                return (null, $"لا يمكن أن يكون أصغر من {proposed}: هذا آخر رقم معروف أنّه صدر، والأصغر يُعيد إصدار رقم مستخدَم.");

            if (value > MaxNumberPerYear)
                return (null, $"الحدّ الأقصى لأرقام السنة {MaxNumberPerYear}.");

            return (value, null);
        }
    }
}
