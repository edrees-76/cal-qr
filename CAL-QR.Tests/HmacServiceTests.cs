using Xunit;
using System;
using CAL_QR.Services;

namespace CAL_QR.Tests
{
    public class HmacServiceTests
    {
        private readonly IHmacService _hmacService;

        public HmacServiceTests()
        {
            _hmacService = new HmacService();
        }

        [Fact]
        public void HmacService_SameData_ProducesSameSignature()
        {
            string signature1 = _hmacService.ComputeSignature(
                certNo: "CERT-100",
                model: "Geiger-A",
                serial: "SN-9999",
                ownerName: "T.N.R.C",
                calDate: "2026-06-30",
                expDate: "2027-06-30",
                result: "Passed",
                engineerName: "Edrees"
            );

            string signature2 = _hmacService.ComputeSignature(
                certNo: "CERT-100",
                model: "Geiger-A",
                serial: "SN-9999",
                ownerName: "T.N.R.C",
                calDate: "2026-06-30",
                expDate: "2027-06-30",
                result: "Passed",
                engineerName: "Edrees"
            );

            Assert.Equal(signature1, signature2);
        }

        [Fact]
        public void HmacService_DifferentData_ProducesDifferentSignature()
        {
            string signature1 = _hmacService.ComputeSignature(
                certNo: "CERT-100",
                model: "Geiger-A",
                serial: "SN-9999",
                ownerName: "T.N.R.C",
                calDate: "2026-06-30",
                expDate: "2027-06-30",
                result: "Passed",
                engineerName: "Edrees"
            );

            string signature2 = _hmacService.ComputeSignature(
                certNo: "CERT-101",
                model: "Geiger-A",
                serial: "SN-9999",
                ownerName: "T.N.R.C",
                calDate: "2026-06-30",
                expDate: "2027-06-30",
                result: "Passed",
                engineerName: "Edrees"
            );

            Assert.NotEqual(signature1, signature2);
        }

        [Fact]
        public void HmacService_ModifiedData_VerificationFails()
        {
            string certNo = "CERT-100";
            string model = "Geiger-A";
            string serial = "SN-9999";
            string ownerName = "T.N.R.C";
            string calDate = "2026-06-30";
            string expDate = "2027-06-30";
            string result = "Passed";
            string engineerName = "Edrees";

            string signature = _hmacService.ComputeSignature(
                certNo: certNo,
                model: model,
                serial: serial,
                ownerName: ownerName,
                calDate: calDate,
                expDate: expDate,
                result: result,
                engineerName: engineerName
            );

            bool verified = _hmacService.VerifySignature(
                certNo: certNo,
                model: model,
                serial: serial,
                ownerName: ownerName,
                calDate: calDate,
                expDate: expDate,
                result: result,
                engineerName: engineerName,
                signature: signature
            );
            Assert.True(verified);

            bool verifiedModified = _hmacService.VerifySignature(
                certNo: certNo,
                model: model,
                serial: serial,
                ownerName: ownerName,
                calDate: "2026-06-29",
                expDate: expDate,
                result: result,
                engineerName: engineerName,
                signature: signature
            );
            Assert.False(verifiedModified);
        }
    }
}
