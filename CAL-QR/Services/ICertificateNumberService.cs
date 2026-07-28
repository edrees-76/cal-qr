using System;
using System.Threading.Tasks;
using CAL_QR.Data;

namespace CAL_QR.Services
{
    public interface ICertificateNumberService
    {
        /// <summary>
        /// يخصّص الرقم التالي بصيغة TNRC-SSDL-YYYY-XXXX، والسنة من
        /// <paramref name="issueDate"/> لا من تاريخ المعايرة.
        ///
        /// ⚠ تُستدعى حصراً من داخل معاملة مفتوحة يملكها المستدعي، وتُلقي
        /// InvalidOperationException خارجها. الزيادة يجب أن تقع في نفس معاملة
        /// حفظ الشهادة، وإلا وقع التخصيص والإدراج في معاملتين ونشأ تسابق.
        ///
        /// ⚠ يجب أن تكون **أول** جملة قاعدة بيانات في المعاملة. جملتها كتابة،
        /// فتأخذ قفل الكتابة فوراً؛ أما قراءة تسبقها فتُنشئ قفل قراءة يحتاج ترقية،
        /// وترقية القفل في SQLite تُعيد SQLITE_BUSY فوراً دون احترام busy_timeout.
        /// </summary>
        Task<string> AllocateAsync(CalQrDbContext context, DateTime issueDate);

        /// <summary>
        /// مزامنة العدّاد مع أكبر رقم موجود فعلاً — للاستخدام مرة واحدة بعد ترحيل
        /// الشهادات الورقية السابقة، لا في كل إقلاع (وإلا أعادت إحياء رقم بعد تصفير
        /// مقصود). لا تُنقص العدّاد أبداً. تُعيد عدد السنوات التي تغيّرت.
        /// </summary>
        Task<int> SyncCounterWithExistingAsync();
    }
}
