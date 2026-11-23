using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Repositories;
using CAL_QR.Validation;

namespace CAL_QR.Services
{
    /// <summary>
    /// تطبيق الحدّ الأدنى على القاعدة المفتوحة (CertificateCounterFloorRules). يُستدعى عند كلّ
    /// إقلاع بعد الترحيلات وتهيئة مفتاح التوقيع — والاستعادة تُعيد التشغيل، فيمرّ بها حتماً.
    /// </summary>
    public static class CertificateCounterFloor
    {
        public const string HmacKeySetting = "HmacSecretKey";

        public static string? LedgerIdOf(CalQrDbContext context) =>
            CertificateCounterFloorRules.LedgerId(
                context.AppSettings.Where(s => s.Key == HmacKeySetting).Select(s => s.Value).FirstOrDefault());

        /// <summary>
        /// في اتّجاهين، وكلاهما رفع لا خفض:
        /// ١) عدّاد القاعدة يُرفع إلى الحدّ إن كان أدنى منه (قاعدة مستعادة من نسخة أقدم).
        /// ٢) الحدّ يُرفع إلى عدّاد القاعدة إن كان أدنى (أوّل تشغيل بعد التحديث، أو كتابة فاتت).
        /// يُرجع السنوات التي رُفع عدّادها في القاعدة.
        /// </summary>
        public static List<(int Year, int From, int To)> Apply(CalQrDbContext context, ICertificateCounterFloorStore store)
        {
            string? ledgerId = LedgerIdOf(context);
            if (ledgerId == null) return new List<(int, int, int)>();

            var rows = context.CertificateSequence.ToList();
            var current = rows.ToDictionary(r => r.Year, r => r.LastNumber);
            var floor = store.Read(ledgerId);

            var raises = CertificateCounterFloorRules.Raises(current, floor);
            foreach (var (year, _, to) in raises)
            {
                var row = rows.FirstOrDefault(r => r.Year == year);
                if (row == null)
                    context.CertificateSequence.Add(new CertificateSequence { Year = year, LastNumber = to });
                else
                    row.LastNumber = to;
            }

            if (raises.Count > 0) context.SaveChanges();

            store.Raise(ledgerId, CertificateCounterFloorRules.Merge(current, floor));
            return raises;
        }

        /// <summary>
        /// يكتب حدث رفع العدّاد في سجلّ التدقيق بعد أن طُبِّق الرفع. فشل الكتابة يُسجَّل في AppLog
        /// ويُرجع false ولا يُلقى: كان يقع في catch تطبيق الحدّ فيُظهر «تعذّر التحقّق من عدّاد
        /// أرقام الشهادات» مع أنّ الحماية طُبِّقت — إنذار كاذب.
        /// </summary>
        public static bool TryLogRaise(IAuditLogRepository audit, string details)
        {
            try
            {
                // يُستدعى من OnStartup المتزامن على خيط الواجهة. داخل Task.Run لا SynchronizationContext،
                // فمتابعات LogAsync تجري على مجمّع الخيوط ولا جمود في الانتظار المتزامن. والانتظار
                // مقصود: الإقلاع يكمل بعد أن يُكتب الحدث، ومدّته لحظة واحدة قبل شاشة البداية.
                Task.Run(() => audit.LogAsync("رفع عدّاد الشهادات", "نظام", "CertificateSequence", details))
                    .GetAwaiter().GetResult();
                return true;
            }
            catch (Exception ex)
            {
                AppLog.Error("CertificateCounterFloor: audit log failed after the counter was raised", ex);
                return false;
            }
        }
    }
}
