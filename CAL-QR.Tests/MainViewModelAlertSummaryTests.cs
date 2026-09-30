using Xunit;
using CAL_QR.ViewModels;

namespace CAL_QR.Tests
{
    public class MainViewModelAlertSummaryTests
    {
        [Fact]
        public void BuildAlertSummaryText_BothCounts_StatesBoth()
        {
            Assert.Equal("3 جهاز منتهي الصلاحية، 2 جهاز يقترب انتهاؤه",
                MainViewModel.BuildAlertSummaryText(3, 2));
        }

        [Fact]
        public void BuildAlertSummaryText_OnlyExpired_OmitsExpiringPart()
        {
            Assert.Equal("4 جهاز منتهي الصلاحية", MainViewModel.BuildAlertSummaryText(4, 0));
        }

        [Fact]
        public void BuildAlertSummaryText_OnlyExpiring_OmitsExpiredPart()
        {
            Assert.Equal("5 جهاز يقترب انتهاؤه", MainViewModel.BuildAlertSummaryText(0, 5));
        }

        [Fact]
        public void BuildAlertSummaryText_Zero_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, MainViewModel.BuildAlertSummaryText(0, 0));
        }
    }
}
