using CAL_QR.Models;

namespace CAL_QR.Services
{
    public interface ICurrentUserService
    {
        User? CurrentUser { get; }
        void SetCurrentUser(User user);
        void ClearCurrentUser();
    }
}
