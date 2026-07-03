using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CAL_QR.Models;

namespace CAL_QR.Services
{
    public interface ISearchService
    {
        Task<List<SearchResultItem>> SearchAsync(string query, CancellationToken cancellationToken);
    }
}
