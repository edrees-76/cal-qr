using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Data;
using CAL_QR.Models;

namespace CAL_QR.Validation
{
    /// <summary>
    /// مصدر الحقيقة الواحد لعرض رقم الشهادة.
    ///
    /// القرار المعماريّ: <b>الشهادة صاحبة الكلمة.</b> رقم الشهادة يُقرأ من جدول
    /// <c>Certificates</c> لا من <c>CalibrationRecord.CertificateNumber</c>.
    /// العمود الأخير يبقى على المخطط للسجلات المُرحَّلة القديمة وحدها، ويُترك
    /// فارغاً لكل سجل جديد لأن <c>CertificateRepository.AddAsync</c> هو الجهة
    /// الوحيدة التي تُخصّص رقماً.
    ///
    /// لماذا هنا لا في كل شاشة؟ ستّ شاشات تعرض الرقم (القائمة، البحث السريع،
    /// البحث الشامل، جدول التفاصيل، الخط الزمني، التقارير). ستّ نسخ من نفس
    /// المنطق كانت ستتباعد بأول تعديل، فيظهر الرقم في شاشة ويغيب في أخرى.
    ///
    /// لماذا لا تنقّل عكسيّ على المخطط (<c>CalibrationRecord.Certificate</c>)؟
    /// لأن المخطط مجمَّد بقرار صريح، ولأن <c>Include</c> لا يقبل مرشّحاً على
    /// تنقّل مرجعيّ — فكان شرط <c>!IsDeleted</c> سيسقط، وتظهر شهادة مُبطَلة
    /// كأنها سارية.
    /// </summary>
    public static class CertificateNumberDisplayRules
    {
        /// <summary>ما يُعرض حين لا شهادة. شرطة طويلة لا نصّ ولا فراغ.</summary>
        public const string None = "—";

        /// <summary>
        /// تعبير العرض. دالّة نقية: رقم الشهادة إن وُجدت، وإلا <see cref="None"/>.
        /// </summary>
        public static string Display(string? issuedNumber) =>
            string.IsNullOrWhiteSpace(issuedNumber) ? None : issuedNumber.Trim();

        /// <summary>
        /// أرقام الشهادات السارية لمجموعة سجلات، مفهرسةً بمعرّف سجل المعايرة.
        /// السجل بلا شهادة لا يظهر في القاموس إطلاقاً.
        ///
        /// مرشّح <c>!IsDeleted</c> مقصود: الشهادة المُبطَلة ناعماً لا تُعرض
        /// كرقم سارٍ. والفهرس الفريد المشروط على <c>CalibrationRecordId</c>
        /// يضمن ألّا تزيد الشهادات غير المحذوفة عن واحدة لكل سجل.
        /// </summary>
        public static Dictionary<int, string> Load(CalQrDbContext context, IEnumerable<int> recordIds)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (recordIds == null) return new Dictionary<int, string>();

            var ids = recordIds.Distinct().ToList();
            if (ids.Count == 0) return new Dictionary<int, string>();

            return context.Certificates
                .AsNoTracking()
                .Where(c => ids.Contains(c.CalibrationRecordId) && !c.IsDeleted)
                .Select(c => new { c.CalibrationRecordId, c.CertificateNumber })
                .ToDictionary(x => x.CalibrationRecordId, x => x.CertificateNumber);
        }

        /// <inheritdoc cref="Load"/>
        public static async Task<Dictionary<int, string>> LoadAsync(CalQrDbContext context, IEnumerable<int> recordIds)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (recordIds == null) return new Dictionary<int, string>();

            var ids = recordIds.Distinct().ToList();
            if (ids.Count == 0) return new Dictionary<int, string>();

            return await context.Certificates
                .AsNoTracking()
                .Where(c => ids.Contains(c.CalibrationRecordId) && !c.IsDeleted)
                .Select(c => new { c.CalibrationRecordId, c.CertificateNumber })
                .ToDictionaryAsync(x => x.CalibrationRecordId, x => x.CertificateNumber);
        }

        /// <summary>
        /// يملأ <see cref="CalibrationRecord.DisplayCertificateNumber"/> لكل سجل
        /// من قاموس سبق تحميله. دالّة نقية بلا وصول لقاعدة البيانات، فتُستدعى
        /// من مسار متزامن أو غير متزامن سواءً.
        /// </summary>
        public static void Populate(IEnumerable<CalibrationRecord> records, IReadOnlyDictionary<int, string> issuedNumbers)
        {
            if (records == null || issuedNumbers == null) return;

            foreach (var record in records)
            {
                record.DisplayCertificateNumber =
                    Display(issuedNumbers.TryGetValue(record.Id, out var number) ? number : null);
            }
        }
    }
}
