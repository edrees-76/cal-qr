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
