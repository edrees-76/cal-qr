using Xunit;
using CAL_QR.Validation;

namespace CAL_QR.Tests
{
    public class AttachmentRulesTests
    {
        [Theory]
        [InlineData("scan.pdf")]
        [InlineData("SCAN.PDF")]
        [InlineData("photo.JPG")]
        [InlineData(@"C:\a\b\report.xlsx")]
        [InlineData("notes.txt")]
        public void Allowed(string name) => Assert.True(AttachmentRules.IsAllowed(name));

        [Theory]
        [InlineData("setup.exe")]
        [InlineData("run.bat")]
        [InlineData("link.lnk")]
        [InlineData("x.js")]
        [InlineData("macro.xlsm")]
        [InlineData("doc.docm")]
        [InlineData("noext")]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("file.pdf.exe")]
        public void Rejected(string? name) => Assert.False(AttachmentRules.IsAllowed(name));

        [Fact]
        public void DialogFilter_ListsPdfAndPng()
        {
            Assert.Contains("*.pdf", AttachmentRules.DialogFilter);
            Assert.Contains("*.png", AttachmentRules.DialogFilter);
            Assert.DoesNotContain("*.exe", AttachmentRules.DialogFilter);
        }
    }
}
