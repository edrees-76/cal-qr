using System;
using Xunit;
using CAL_QR.Validation;

namespace CAL_QR.Tests
{
    /// <summary>
    /// قواعد التواريخ. دوالّ نقية، فلا حاجة لقاعدة بيانات هنا —
    /// اختبار تطبيقها على مستوى المستودع في CertificateAmendmentTests.
    /// </summary>
    public class CertificateDateRulesTests
    {
        private static readonly DateTime Today = new DateTime(2026, 7, 29);

        [Fact]
        public void IssueDateBeforeCalibrationDate_IsRejected()
        {
            var result = CertificateDateRules.Validate(
                calibrationDate: new DateTime(2026, 7, 20),
                issueDate: new DateTime(2026, 7, 19),
                today: Today);

            Assert.False(result.IsValid);
            Assert.Contains("يسبق تاريخ المعايرة", result.ErrorMessage!);
        }

        [Fact]
        public void IssueDateEqualToCalibrationDate_IsAccepted()
        {
            var result = CertificateDateRules.Validate(
                calibrationDate: new DateTime(2026, 7, 20),
                issueDate: new DateTime(2026, 7, 20),
                today: Today);

            Assert.True(result.IsValid);
        }

        [Fact]
        public void IssueDateToday_IsAccepted_BoundaryCase()
        {
            var result = CertificateDateRules.Validate(
                calibrationDate: new DateTime(2026, 7, 20),
                issueDate: Today,
                today: Today);

            Assert.True(result.IsValid);
        }

        [Fact]
        public void IssueDateTomorrow_IsRejected_BoundaryCase()
        {
            var result = CertificateDateRules.Validate(
                calibrationDate: new DateTime(2026, 7, 20),
                issueDate: Today.AddDays(1),
                today: Today);

            Assert.False(result.IsValid);
            Assert.Contains("المستقبل", result.ErrorMessage!);
        }

        [Fact]
        public void DueDate_IsOneYearFromCalibrationDate_NotFromIssueDate()
        {
            Assert.Equal(
                new DateTime(2027, 1, 15),
                CertificateDateRules.ComputeDueDate(new DateTime(2026, 1, 15)));
        }

        [Fact]
        public void DueDate_OnLeapDay_LandsOnFebruary28()
        {
            // 2024-02-29 + سنة: .NET يُرجع 2025-02-28 لعدم وجود 29 فبراير.
            // مثبَّت صراحةً حتى لا يتغير السلوك بصمت.
            Assert.Equal(
                new DateTime(2025, 2, 28),
                CertificateDateRules.ComputeDueDate(new DateTime(2024, 2, 29)));
        }

        [Fact]
        public void AfterNumberIssued_IssueDateMayMoveWithinSameYear()
        {
            var result = CertificateDateRules.Validate(
                calibrationDate: new DateTime(2026, 1, 15),
                issueDate: new DateTime(2026, 7, 1),
                today: Today,
                existingCertificateNumber: "TNRC-SSDL-2026-0018");

            Assert.True(result.IsValid);
        }

        [Fact]
        public void AfterNumberIssued_IssueDateMayNotLeaveTheNumbersYear()
        {
            var result = CertificateDateRules.Validate(
                calibrationDate: new DateTime(2025, 12, 20),
                issueDate: new DateTime(2025, 12, 28),
                today: Today,
                existingCertificateNumber: "TNRC-SSDL-2026-0018");

            Assert.False(result.IsValid);
            Assert.Contains("2026", result.ErrorMessage!);
        }

        [Fact]
        public void MigratedPaperNumber_WithForeignShape_SkipsTheYearLock()
        {
            // رقم ورقي مرحَّل لا يتبع الصيغة: لا تُفرض عليه قاعدة حبس السنة،
            // وإلا رُفض تعديل مشروع على سجل قديم.
            var result = CertificateDateRules.Validate(
                calibrationDate: new DateTime(2025, 12, 20),
                issueDate: new DateTime(2025, 12, 28),
                today: Today,
                existingCertificateNumber: "SSDL/441/2025");

            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData("TNRC-SSDL-2026-0018", 2026)]
        [InlineData("TNRC-SSDL-2027-0001", 2027)]
        [InlineData("SSDL/441/2025", null)]
        [InlineData("TNRC-SSDL-26-0018", null)]
        [InlineData("", null)]
        [InlineData(null, null)]
        public void ExtractYear_ReadsTheYearSegmentOnly(string? certificateNumber, int? expected)
        {
            Assert.Equal(expected, CertificateDateRules.ExtractYear(certificateNumber));
        }

        [Theory]
        [InlineData("TNRC-SSDL-2026-0018", 18)]
        [InlineData("TNRC-SSDL-2026-0001", 1)]
        [InlineData("TNRC-SSDL-2026-9999", 9999)]
        [InlineData("SSDL/441/2025", null)]
        [InlineData("TNRC-SSDL-2026-18", null)]
        [InlineData("", null)]
        [InlineData(null, null)]
        public void ExtractSequence_ReadsTheSequenceSegmentOnly(string? certificateNumber, int? expected)
        {
            Assert.Equal(expected, CertificateDateRules.ExtractSequence(certificateNumber));
        }

        [Fact]
        public void ExtractYearAndSequence_ReadDistinctSegments_NotOffsetByOne()
        {
            // الحارس المباشر على العطل: انزياح بخانة كان يجعل السنة تُقرأ "026-"
            // فتعطي 26 بدل 2026. القيمتان مختلفتان عمداً حتى لا تتسترا على بعضهما.
            const string number = "TNRC-SSDL-2026-0018";

            Assert.Equal(2026, CertificateDateRules.ExtractYear(number));
            Assert.Equal(18, CertificateDateRules.ExtractSequence(number));
        }
    }
}
