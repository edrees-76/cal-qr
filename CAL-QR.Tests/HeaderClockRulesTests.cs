using System;
using Xunit;
using CAL_QR.Validation;

namespace CAL_QR.Tests
{
    public class HeaderClockRulesTests
    {
        [Fact]
        public void Format_Evening_UsesTwelveHourClockAndPm() =>
            Assert.Equal("الثلاثاء 17/11/2026 06:56 م", HeaderClockRules.Format(new DateTime(2026, 11, 17, 18, 56, 37)));

        [Fact]
        public void Format_Morning_UsesAm() =>
            Assert.Equal("الأربعاء 18/11/2026 09:05 ص", HeaderClockRules.Format(new DateTime(2026, 11, 18, 9, 5, 0)));

        [Fact]
        public void Format_Midnight_ShowsTwelveAm() =>
            Assert.Equal("الجمعة 20/11/2026 12:00 ص", HeaderClockRules.Format(new DateTime(2026, 11, 20, 0, 0, 0)));

        [Fact]
        public void Format_Noon_ShowsTwelvePm() =>
            Assert.Equal("السبت 21/11/2026 12:00 م", HeaderClockRules.Format(new DateTime(2026, 11, 21, 12, 0, 0)));

        [Theory]
        [InlineData(15, "الأحد")]
        [InlineData(16, "الاثنين")]
        [InlineData(19, "الخميس")]
        public void Format_UsesArabicDayName(int day, string expectedDayName) =>
            Assert.StartsWith(expectedDayName + " ", HeaderClockRules.Format(new DateTime(2026, 11, day, 10, 0, 0)));
    }
}
