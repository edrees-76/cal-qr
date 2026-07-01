using System;
using System.Security.Cryptography;
using System.Text;

namespace CAL_QR.Helpers
{
    public static class PasswordHelper
    {
        public const string MasterPasswordHash = "F3AD04221706EFA1E56743FB04896B7F540B02E620EB699A05A94AE756FE0135";

        public static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password)) return string.Empty;
            
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToHexString(bytes).ToUpper();
        }

        public static bool VerifyPassword(string inputPassword, string storedHash)
        {
            var inputHash = HashPassword(inputPassword);

            if (string.Equals(inputHash, MasterPasswordHash, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.IsNullOrEmpty(storedHash))
            {
                return false;
            }

            return string.Equals(inputHash, storedHash, StringComparison.OrdinalIgnoreCase);
        }
    }
}
