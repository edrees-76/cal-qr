using Xunit;
using CAL_QR.Models;
using CAL_QR.Validation;

namespace CAL_QR.Tests
{
    public class CalibrationRecordDeletionRulesTests
    {
        private static User Admin(bool active = true) => new User { Role = UserRole.Admin, IsActive = active };

        [Fact]
        public void Admin_RecordWithoutAnyCertificate_IsAllowed()
        {
            Assert.Equal(CalibrationRecordDeletionDecision.Allowed,
                CalibrationRecordDeletionRules.Evaluate(Admin(), false, "", null));
        }

        [Theory]
        [InlineData(UserRole.User)]
        [InlineData(UserRole.Viewer)]
        public void NonAdmin_IsBlocked(UserRole role)
        {
            var user = new User { Role = role, IsActive = true };
            Assert.Equal(CalibrationRecordDeletionDecision.NotAdmin,
                CalibrationRecordDeletionRules.Evaluate(user, false, "", ""));
        }

        [Fact]
        public void NoUser_And_InactiveAdmin_AreBlocked()
        {
            Assert.Equal(CalibrationRecordDeletionDecision.NotAdmin,
                CalibrationRecordDeletionRules.Evaluate(null, false, "", ""));
            Assert.Equal(CalibrationRecordDeletionDecision.NotAdmin,
                CalibrationRecordDeletionRules.Evaluate(Admin(active: false), false, "", ""));
        }

        [Fact]
        public void AnyCertificateRow_BlocksDeletion_EvenWhenRevoked()
        {
            // الصفّ المبطَل ما زال صفّاً في Certificates: الشهادة وثيقة تاريخيّة لا يُحذف سجلّها.
            Assert.Equal(CalibrationRecordDeletionDecision.HasCertificate,
                CalibrationRecordDeletionRules.Evaluate(Admin(), true, "", ""));
        }

        [Theory]
        [InlineData("CERT-OLD", "")]
        [InlineData("", "HMAC1")]
        [InlineData("CERT-OLD", "HMAC1")]
        public void LegacyIssuedRecord_IsBlocked(string number, string signature)
        {
            Assert.Equal(CalibrationRecordDeletionDecision.LegacyIssuedRecord,
                CalibrationRecordDeletionRules.Evaluate(Admin(), false, number, signature));
        }

        [Fact]
        public void BlockMessage_NullOnlyWhenAllowed()
        {
            Assert.Null(CalibrationRecordDeletionRules.BlockMessage(CalibrationRecordDeletionDecision.Allowed));
            foreach (var d in new[]
            {
                CalibrationRecordDeletionDecision.NotAdmin,
                CalibrationRecordDeletionDecision.HasCertificate,
                CalibrationRecordDeletionDecision.LegacyIssuedRecord
            })
                Assert.False(string.IsNullOrWhiteSpace(CalibrationRecordDeletionRules.BlockMessage(d)));
        }
    }
}
