using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CAL_QR.Validation;

namespace CAL_QR.Services
{
    /// <summary>
    /// ملفّ الحدّ الأدنى لعدّادات الشهادات خارج القاعدة (CertificateCounterFloorRules).
    /// لكلّ سجلّ ترقيم (بصمة مفتاح التوقيع) آخر رقم صدر في كلّ سنة. يزيد ولا ينقص.
    /// </summary>
    public interface ICertificateCounterFloorStore
    {
        /// <summary>آخر الأرقام المعروفة لهذا السجلّ، أو قاموس فارغ.</summary>
        IReadOnlyDictionary<int, int> Read(string ledgerId);

        /// <summary>يرفع الحدّ إلى القيم المعطاة حيث هي أكبر؛ لا يخفض شيئاً.</summary>
        void Raise(string ledgerId, IReadOnlyDictionary<int, int> lastNumbers);
    }

    public sealed class CertificateCounterFloorStore : ICertificateCounterFloorStore
    {
        public const string FileName = "certificate-counter-floor.json";

        private static readonly object Gate = new();
        private readonly string _filePath;

        public CertificateCounterFloorStore(string directory)
        {
            _filePath = Path.Combine(directory, FileName);
        }

        public string FilePath => _filePath;

        public IReadOnlyDictionary<int, int> Read(string ledgerId)
        {
            lock (Gate)
            {
                return Load().TryGetValue(ledgerId, out var years)
                    ? new Dictionary<int, int>(years)
                    : new Dictionary<int, int>();
            }
        }

        public void Raise(string ledgerId, IReadOnlyDictionary<int, int> lastNumbers)
        {
            if (string.IsNullOrWhiteSpace(ledgerId)) throw new ArgumentException("هويّة سجلّ الترقيم مطلوبة.", nameof(ledgerId));

            lock (Gate)
            {
                var all = Load();
                all.TryGetValue(ledgerId, out var existing);
                var merged = CertificateCounterFloorRules.Merge(existing, lastNumbers);

                if (existing != null && merged.Count == existing.Count
                    && merged.All(p => existing.TryGetValue(p.Key, out int v) && v == p.Value))
                {
                    return; // لا جديد: لا كتابة
                }

                all[ledgerId] = merged;
                Save(all);
            }
        }

        private Dictionary<string, Dictionary<int, int>> Load()
        {
            if (!File.Exists(_filePath)) return new Dictionary<string, Dictionary<int, int>>();

            try
            {
                string json = File.ReadAllText(_filePath);
                var raw = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(json)
                          ?? new Dictionary<string, Dictionary<string, int>>();

                return raw.ToDictionary(
                    ledger => ledger.Key,
                    ledger => ledger.Value
                        .Where(y => int.TryParse(y.Key, out _))
                        .ToDictionary(y => int.Parse(y.Key), y => y.Value));
            }
            catch (JsonException ex)
            {
                // ملفّ تالف: يُحفظ جانباً للفحص ولا يُكتب فوقه بصمت، ويبدأ ملفّ جديد.
                // الحماية لا تضيع: الاستعادة تأخذ أيضاً عدّاد القاعدة الحيّة قبل استبدالها.
                string aside = _filePath + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmss");
                File.Move(_filePath, aside, overwrite: true);
                AppLog.Error($"ملفّ حدّ عدّاد الشهادات تالف؛ نُقل إلى {aside}", ex);
                return new Dictionary<string, Dictionary<int, int>>();
            }
        }

        private void Save(Dictionary<string, Dictionary<int, int>> all)
        {
            string? directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var raw = all.ToDictionary(
                ledger => ledger.Key,
                ledger => ledger.Value.OrderBy(y => y.Key).ToDictionary(y => y.Key.ToString(), y => y.Value));

            // كتابة ذرّيّة: ملفّ مؤقّت ثمّ استبدال، فانقطاع الكهرباء لا يترك نصف ملفّ.
            string temp = _filePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(raw, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, _filePath, overwrite: true);
        }
    }
}
