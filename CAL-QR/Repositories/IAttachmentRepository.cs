using System.Collections.Generic;
using System.Threading.Tasks;
using CAL_QR.Models;

namespace CAL_QR.Repositories
{
    public interface IAttachmentRepository
    {
        Task<IEnumerable<Attachment>> GetByRecordIdAsync(int recordId);
        Task<IEnumerable<Attachment>> GetByCertificateIdAsync(int certificateId);
        Task AddAsync(Attachment attachment);
        Task DeleteAsync(int id);
    }
}
