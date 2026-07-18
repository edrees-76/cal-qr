using CAL_QR.Models;
using CAL_QR.Services;

namespace CAL_QR.Tests
{
    public class TestCurrentUserService : ICurrentUserService
    {
        public User? CurrentUser { get; set; }

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
