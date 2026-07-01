namespace CAL_QR.Services
{
    public interface IHmacService
    {
        string BuildConcatenatedString(
            string certNo,
            string model,
            string serial,
            string ownerName,
            string calDate,
            string expDate,
            string result,
            string engineerName);

        string ComputeSignature(
            string certNo,
            string model,
            string serial,
            string ownerName,
            string calDate,
            string expDate,
            string result,
            string engineerName);

        bool VerifySignature(
            string certNo,
            string model,
            string serial,
            string ownerName,
            string calDate,
            string expDate,
            string result,
            string engineerName,
            string signature);
    }
}
