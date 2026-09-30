using Xunit;
using CAL_QR.Helpers;

namespace CAL_QR.Tests
{
    public class WindowSizingTests
    {
        [Theory]
        [InlineData(750, 728, 712)]   // أطول من مساحة العمل: يُصغَّر مع هامش
        [InlineData(620, 728, 620)]   // يناسب: لا تغيير
        [InlineData(1200, 1366, 1200)]
        [InlineData(1200, 1100, 1084)]
        public void Fit_ShrinksOnlyWhenTooLarge(double requested, double available, double expected)
        {
            Assert.Equal(expected, WindowSizing.Fit(requested, available));
        }

        [Fact]
        public void Fit_IgnoresNonPositiveAvailable()
        {
            Assert.Equal(700, WindowSizing.Fit(700, 0));
        }
    }
}
