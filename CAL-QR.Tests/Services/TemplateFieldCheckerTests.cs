using Xunit;
using CAL_QR.Services;

namespace CAL_QR.Tests.Services
{
    public class TemplateFieldCheckerTests
    {
        // ── ExtractRadionuclides ──

        [Fact]
        public void ExtractRadionuclides_SingleNuclide_ReturnsSingle()
        {
            var result = TemplateFieldChecker.ExtractRadionuclides(
                "using a Co-60 point gamma source");
            Assert.Single(result);
            Assert.Contains("Co-60", result);
        }

        [Fact]
        public void ExtractRadionuclides_TwoNuclides_ReturnsBoth()
        {
            var result = TemplateFieldChecker.ExtractRadionuclides(
                "Sr-90/Y-90 reference Beta sources");
            Assert.Equal(2, result.Count);
            Assert.Contains("Sr-90", result);
            Assert.Contains("Y-90", result);
        }

        [Fact]
        public void ExtractRadionuclides_NoNuclide_ReturnsEmpty()
        {
            var result = TemplateFieldChecker.ExtractRadionuclides(
                "No nuclide mentioned here");
            Assert.Empty(result);
        }

        [Fact]
        public void ExtractRadionuclides_NullOrEmpty_ReturnsEmpty()
        {
            Assert.Empty(TemplateFieldChecker.ExtractRadionuclides(null));
            Assert.Empty(TemplateFieldChecker.ExtractRadionuclides(""));
            Assert.Empty(TemplateFieldChecker.ExtractRadionuclides("   "));
        }

        [Fact]
        public void ExtractRadionuclides_CaseInsensitive_NormalizesOutput()
        {
            var result = TemplateFieldChecker.ExtractRadionuclides(
                "using a co-60 source");
            Assert.Single(result);
            Assert.Contains("Co-60", result);
        }

        [Fact]
        public void ExtractRadionuclides_Cs137InDoseRateMeter_ReturnsSingle()
        {
            var result = TemplateFieldChecker.ExtractRadionuclides(
                "The calibration was carried out using a Cs-137 point gamma source.");
            Assert.Single(result);
            Assert.Contains("Cs-137", result);
        }

        // ── ExtractDistanceNumber ──

        [Fact]
        public void ExtractDistanceNumber_StandardFormat_ReturnsNumber()
        {
            var result = TemplateFieldChecker.ExtractDistanceNumber(
                "Distance = 1.0 meter (Axis configuration)");
            Assert.Equal("1.0", result);
        }

        [Fact]
        public void ExtractDistanceNumber_IntegerDistance_ReturnsNumber()
        {
            var result = TemplateFieldChecker.ExtractDistanceNumber(
                "Distance = 2 meter (Axis configuration)");
            Assert.Equal("2", result);
        }

        [Fact]
        public void ExtractDistanceNumber_DirectContact_ReturnsNull()
        {
            var result = TemplateFieldChecker.ExtractDistanceNumber(
                "Direct Contact Geometry");
            Assert.Null(result);
        }

        [Fact]
        public void ExtractDistanceNumber_NullOrEmpty_ReturnsNull()
        {
            Assert.Null(TemplateFieldChecker.ExtractDistanceNumber(null));
            Assert.Null(TemplateFieldChecker.ExtractDistanceNumber(""));
        }

        // ── ExtractDistanceNumberFromField ──

        [Fact]
        public void ExtractDistanceNumberFromField_NumberOnly_ReturnsNumber()
        {
            Assert.Equal("2.0", TemplateFieldChecker.ExtractDistanceNumberFromField("2.0"));
        }

        [Fact]
        public void ExtractDistanceNumberFromField_NumberWithUnit_ReturnsNumber()
        {
            Assert.Equal("2.0", TemplateFieldChecker.ExtractDistanceNumberFromField("2.0 meter"));
        }

        [Fact]
        public void ExtractDistanceNumberFromField_NullOrEmpty_ReturnsNull()
        {
            Assert.Null(TemplateFieldChecker.ExtractDistanceNumberFromField(null));
            Assert.Null(TemplateFieldChecker.ExtractDistanceNumberFromField(""));
        }

        // ── CALC-03: مقارنة المسافة دلاليّاً ──

        private static DistanceComparison Compare(string geometry, string field)
        {
            var g = TemplateFieldChecker.ParseGeometryDistance(geometry);
            var f = TemplateFieldChecker.ParseDistanceField(field);
            Assert.NotNull(g);
            Assert.NotNull(f);
            return TemplateFieldChecker.CompareDistances(g!, f!);
        }

        [Theory]
        [InlineData("Distance = 1.0 meter (Axis)", "1 meter")]
        [InlineData("Distance = 1 meter (Axis)", "1.00 meter")]
        [InlineData("Distance = 1.0 meter (Axis)", "100 cm")]
        [InlineData("Distance = 1.0 meter (Axis)", "1000 mm")]
        [InlineData("Distance = 1.5 meter (Axis)", "1,5 m")]
        [InlineData("Distance = 1.5 meter (Axis)", "١٫٥ م")]
        [InlineData("Distance = 1.0 meter (Axis)", "١.٠")]
        [InlineData("Distance = 1.0 meter (Axis)", "1.0")]
        public void CompareDistances_SameDistance_IsConsistent(string geometry, string field)
        {
            Assert.True(Compare(geometry, field).AreConsistent);
        }

        [Fact]
        public void CompareDistances_DifferentNumberSameUnit_OffersAutoFix()
        {
            var result = Compare("Distance = 1.0 meter (Axis)", "2.0 meter");

            Assert.False(result.AreConsistent);
            Assert.True(result.CanAutoFix);
            Assert.False(result.UnitsDiffer);
        }

        [Fact]
        public void CompareDistances_UnitlessField_OffersAutoFixWhenValuesDiffer()
        {
            var result = Compare("Distance = 1.0 meter (Axis)", "2.0");

            Assert.False(result.AreConsistent);
            Assert.True(result.CanAutoFix);
        }

        [Fact]
        public void CompareDistances_DifferentUnitAndValue_NeverOffersAutoFix()
        {
            // 1.0 meter مقابل 50 cm: القيمتان مختلفتان بعد توحيد الوحدة، والوحدتان مختلفتان.
            var result = Compare("Distance = 1.0 meter (Axis)", "50 cm");

            Assert.False(result.AreConsistent);
            Assert.False(result.CanAutoFix);
            Assert.True(result.UnitsDiffer);
        }

        [Fact]
        public void ParseDistanceField_ArabicDigitsAndDecimalSeparator_Normalized()
        {
            var reading = TemplateFieldChecker.ParseDistanceField("١٫٥ متر");

            Assert.NotNull(reading);
            Assert.Equal("1.5", reading!.NormalizedNumber);
            Assert.Equal(1.5m, reading.Value);
            Assert.Equal(DistanceUnit.Meter, reading.Unit);
        }

        [Theory]
        [InlineData("2 mm", DistanceUnit.Millimeter)]
        [InlineData("2 millimeters", DistanceUnit.Millimeter)]
        [InlineData("2 cm", DistanceUnit.Centimeter)]
        [InlineData("2 سم", DistanceUnit.Centimeter)]
        [InlineData("2 m", DistanceUnit.Meter)]
        [InlineData("2 metre", DistanceUnit.Meter)]
        [InlineData("2", DistanceUnit.Unspecified)]
        [InlineData("2 minutes", DistanceUnit.Unspecified)]
        public void ParseDistanceField_RecognisesTheUnit(string field, DistanceUnit expected)
        {
            Assert.Equal(expected, TemplateFieldChecker.ParseDistanceField(field)!.Unit);
        }

        [Fact]
        public void ParseDistanceField_NoNumber_ReturnsNull()
        {
            Assert.Null(TemplateFieldChecker.ParseDistanceField("Direct contact"));
        }

        [Fact]
        public void ReplaceDistanceNumber_WithArabicRawNumber_UsesTheNormalizedReplacement()
        {
            var geometry = TemplateFieldChecker.ParseGeometryDistance("Distance = ١٫٠ meter (Axis)")!;
            var field = TemplateFieldChecker.ParseDistanceField("2,5 meter")!;

            string fixedText = TemplateFieldChecker.ReplaceDistanceNumber(
                "Distance = ١٫٠ meter (Axis)", geometry.RawNumber, field.NormalizedNumber);

            Assert.Equal("Distance = 2.5 meter (Axis)", fixedText);
        }

        // ── ReplaceRadionuclide ──

        [Fact]
        public void ReplaceRadionuclide_SingleOccurrence_Replaces()
        {
            var result = TemplateFieldChecker.ReplaceRadionuclide(
                "using a Co-60 point gamma source", "Co-60", "Cs-137");
            Assert.Contains("Cs-137", result);
            Assert.DoesNotContain("Co-60", result);
        }

        [Fact]
        public void ReplaceRadionuclide_CaseInsensitive_Replaces()
        {
            var result = TemplateFieldChecker.ReplaceRadionuclide(
                "using a co-60 source", "Co-60", "Cs-137");
            Assert.Contains("Cs-137", result);
        }

        [Fact]
        public void ReplaceRadionuclide_MultipleOccurrences_ReplacesAll()
        {
            var result = TemplateFieldChecker.ReplaceRadionuclide(
                "Co-60 source calibrated with Co-60", "Co-60", "Cs-137");
            Assert.DoesNotContain("Co-60", result);
            Assert.Equal("Cs-137 source calibrated with Cs-137", result);
        }

        // ── ReplaceDistanceNumber ──

        [Fact]
        public void ReplaceDistanceNumber_StandardFormat_ReplacesNumberOnly()
        {
            var result = TemplateFieldChecker.ReplaceDistanceNumber(
                "Distance = 1.0 meter (Axis configuration)", "1.0", "2.0");
            Assert.Equal("Distance = 2.0 meter (Axis configuration)", result);
        }

        [Fact]
        public void ReplaceDistanceNumber_PreservesUnit_AndParenthetical()
        {
            var result = TemplateFieldChecker.ReplaceDistanceNumber(
                "Distance = 1.0 meter (Axis configuration)", "1.0", "3.5");
            Assert.Contains("meter", result);
            Assert.Contains("Axis configuration", result);
            Assert.Contains("3.5", result);
        }
    }
}
