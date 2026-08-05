using System.Collections.Generic;
using System.Threading.Tasks;
using CAL_QR.Models;

namespace CAL_QR.Repositories
{
    public interface IDeviceTypeRepository
    {
        Task<IEnumerable<DeviceType>> GetAllAsync();
        Task<DeviceType?> GetByIdAsync(int id);

        /// <summary>
        /// النوع بقالبه كاملاً: الحقول النصية والعَلَمان وقوالب الفحوص ومكوّنات عدم اليقين.
        /// مفصولة عن GetByIdAsync عمداً: مسار إصدار الشهادة وحده يحتاج القوالب،
        /// وضمّها في الدالّة العامّة كان سيحمّل كل مستدعٍ استعلامَي Include لا يستعملهما.
        /// الفرز بـSortOrder ليس هنا بل في بانِي المسوّدة، ليبقى ترتيب الصفوف
        /// قابلاً للاختبار في موضع واحد.
        /// </summary>
        Task<DeviceType?> GetByIdWithTemplatesAsync(int id);
        Task AddAsync(DeviceType type);

        /// <summary>
        /// إعادة تسمية نوع قائم — الاسم وحده، ولا شيء غيره.
        ///
        /// بديلة عن UpdateAsync(DeviceType) التي كانت تأخذ كائناً مفصولاً وتضبط
        /// EntityState.Modified، فتكتب **كل** الأعمدة. ومستدعيها الوحيد كان يبني
        /// `new DeviceType { Id, Name }`، فكانت كل إعادة تسمية تمسح الحقول القالبية
        /// السبعة عشر وتُصفّر UncertaintyEnabled و MethodologyEnabled وتدهس CreatedAt.
        ///
        /// التوقيع هنا يصف ما تفعله الدالّة فعلاً، فيستحيل على مستدعٍ لاحق أن يظنّها
        /// تحديثاً كاملاً فيفقد القالب من حيث لا يدري.
        /// </summary>
        Task RenameAsync(int id, string name);
        Task SoftDeleteAsync(int id);
    }
}
