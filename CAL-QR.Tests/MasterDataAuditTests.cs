using System;
using System.Threading.Tasks;
using Moq;
using Xunit;
using CAL_QR.Repositories;
using CAL_QR.Services;
using CAL_QR.Validation;

namespace CAL_QR.Tests
{
    public class MasterDataAuditTests
    {
        [Fact]
        public void Describe_IncludesActionNameAndId()
        {
            string text = MasterDataAuditRules.Describe(MasterDataAuditRules.OwnerAdded, "  مختبر الأمل ", 7);

            Assert.Equal("إضافة جهة مالكة: \"مختبر الأمل\" (المعرّف 7)", text);
        }

        [Fact]
        public void Describe_AppendsTheNote()
        {
            string text = MasterDataAuditRules.Describe(
                MasterDataAuditRules.DeviceTypeAdded, "Pancake", 3, MasterDataAuditRules.NoteFromCalibrationForm);

            Assert.Equal("إضافة نوع جهاز: \"Pancake\" (المعرّف 3) — من نموذج المعايرة", text);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Describe_BlankName_ShowsPlaceholder(string? name)
        {
            Assert.Contains("(بلا اسم)", MasterDataAuditRules.Describe(MasterDataAuditRules.OwnerDeleted, name, 1));
        }

        [Fact]
        public void Describe_NoIdYet_OmitsTheIdPart()
        {
            string text = MasterDataAuditRules.Describe(MasterDataAuditRules.OwnerEdited, "X", 0);

            Assert.DoesNotContain("المعرّف", text);
        }

        [Fact]
        public async Task TryLogAsync_WritesOneEntryWithTheDescribedDetails()
        {
            var audit = new Mock<IAuditLogRepository>();
            string expectedDetails = MasterDataAuditRules.Describe(MasterDataAuditRules.DeviceTypeEdited, "Gamma", 12);

            await MasterDataAuditWriter.TryLogAsync(
                audit.Object, MasterDataAuditRules.DeviceTypeEdited,
                MasterDataAuditRules.DeviceTypeEntity, 12, "Gamma");

            audit.Verify(a => a.LogAsync(
                MasterDataAuditRules.DeviceTypeEdited,
                MasterDataAuditRules.DeviceTypeEntity,
                "12",
                expectedDetails,
                It.IsAny<int?>(),
                It.IsAny<string?>()), Times.Once);
        }

        [Fact]
        public async Task TryLogAsync_AuditFailure_DoesNotPropagate()
        {
            var audit = new Mock<IAuditLogRepository>();
            audit.Setup(a => a.LogAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<int?>(), It.IsAny<string?>()))
                .ThrowsAsync(new InvalidOperationException("db down"));

            var ex = await Record.ExceptionAsync(() => MasterDataAuditWriter.TryLogAsync(
                audit.Object, MasterDataAuditRules.OwnerDeleted, MasterDataAuditRules.OwnerEntity, 5, "A"));

            Assert.Null(ex);
        }
    }
}
