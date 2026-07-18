using CAL_QR.Models;

namespace CAL_QR.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        public User? CurrentUser { get; private set; }

        public void SetCurrentUser(User user)
        {
            CurrentUser = user;
        }

        public void ClearCurrentUser()
        {
            CurrentUser = null;
        }
    }
}
