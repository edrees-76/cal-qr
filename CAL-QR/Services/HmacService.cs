using System;
using System.Security.Cryptography;
using System.Text;

namespace CAL_QR.Services
{
    public class HmacService : IHmacService
    {
        private const string SecretKey = "CalQR-Nuclear-Center-2026-SecretKey";

        public string BuildConcatenatedString(
            string certNo,
            string model,
            string serial,
            string ownerName,
            string calDate,
            string expDate,
            string result,
            string engineerName)
        {
            return $"{certNo?.Trim()}|{model?.Trim()}|{serial?.Trim()}|{ownerName?.Trim()}|{calDate?.Trim()}|{expDate?.Trim()}|{result?.Trim()}|{engineerName?.Trim()}";
        }

        public string ComputeSignature(
            string certNo,
            string model,
            string serial,
            string ownerName,
            string calDate,
            string expDate,
            string result,
            string engineerName)
        {
            string rawData = BuildConcatenatedString(
                certNo: certNo,
                model: model,
                serial: serial,
                ownerName: ownerName,
                calDate: calDate,
                expDate: expDate,
                result: result,
                engineerName: engineerName
            );

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SecretKey));
            byte[] hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawData));
            string hexHash = Convert.ToHexString(hashBytes);
            
            return hexHash.Substring(0, 8).ToUpper();
        }

        public bool VerifySignature(
            string certNo,
            string model,
            string serial,
            string ownerName,
            string calDate,
            string expDate,
            string result,
            string engineerName,
            string signature)
        {
            if (string.IsNullOrWhiteSpace(signature)) return false;

            string computed = ComputeSignature(
                certNo: certNo,
                model: model,
                serial: serial,
                ownerName: ownerName,
                calDate: calDate,
                expDate: expDate,
                result: result,
                engineerName: engineerName
            );

            return string.Equals(computed, signature.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
