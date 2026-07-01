using System.Collections.Generic;
using System.Threading.Tasks;
using CAL_QR.Models;

namespace CAL_QR.Repositories
{
    public interface IPaperTemplateRepository
    {
        Task<IEnumerable<PaperTemplate>> GetAllAsync();
        Task<PaperTemplate?> GetByIdAsync(int id);
        Task<PaperTemplate?> GetDefaultAsync();
        Task AddAsync(PaperTemplate template);
        Task UpdateAsync(PaperTemplate template);
        Task DeleteAsync(int id);
        Task SetDefaultAsync(int id);
    }
}
