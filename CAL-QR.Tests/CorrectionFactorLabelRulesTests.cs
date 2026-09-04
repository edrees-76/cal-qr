using System.Collections.Generic;
using Xunit;
using CAL_QR.Models;
using CAL_QR.Validation;

namespace CAL_QR.Tests
{
    /// <summary>
    /// اختبارات وحدة على CorrectionFactorLabelRules.
    /// </summary>
    public class CorrectionFactorLabelRulesTests
    {
        [Fact]
        public void IsAveraged_TwoRowsSameNuclide_ReturnsTrue()
        {
            var rows = new List<CertificateCalibrationResult>
            {
                new() { Radionuclide = "Cs-137" },
                new() { Radionuclide = "Cs-137" },
            };

            Assert.True(CorrectionFactorLabelRules.IsAveraged("Cs-137", rows));
        }

        [Fact]
        public void IsAveraged_OneRow_ReturnsFalse()
        {
            var rows = new List<CertificateCalibrationResult>
            {
                new() { Radionuclide = "Cs-137" },
            };

            Assert.False(CorrectionFactorLabelRules.IsAveraged("Cs-137", rows));
        }

        [Fact]
        public void IsAveraged_ZeroRows_ReturnsFalse()
        {
            Assert.False(CorrectionFactorLabelRules.IsAveraged("Cs-137", new List<CertificateCalibrationResult>()));
        }

        [Fact]
        public void IsAveraged_MixedNuclides_EachClassifiedIndependently()
        {
            var rows = new List<CertificateCalibrationResult>
            {
                new() { Radionuclide = "Cs-137" },
                new() { Radionuclide = "Cs-137" },
                new() { Radionuclide = "Co-60" },
            };

            Assert.True(CorrectionFactorLabelRules.IsAveraged("Cs-137", rows));
            Assert.False(CorrectionFactorLabelRules.IsAveraged("Co-60", rows));
        }

        [Fact]
        public void IsAveraged_SpacingDifference_StillMatches()
        {
            // قالب B401 يكتب "Sr-90 / Y-90" بمسافات، والمستخدم قد يكتبها "Sr-90/Y-90".
            var rows = new List<CertificateCalibrationResult>
            {
                new() { Radionuclide = "Sr-90/Y-90" },
                new() { Radionuclide = "Sr-90/Y-90" },
            };

            Assert.True(CorrectionFactorLabelRules.IsAveraged("Sr-90 / Y-90", rows));
        }

        [Fact]
        public void IsAveraged_CaseDifference_StillMatches()
        {
            var rows = new List<CertificateCalibrationResult>
            {
                new() { Radionuclide = "cs-137" },
                new() { Radionuclide = "Cs-137" },
            };

            Assert.True(CorrectionFactorLabelRules.IsAveraged("Cs-137", rows));
        }

        [Fact]
        public void IsAveraged_TrulyDifferentNuclides_DoNotMatch()
        {
            var rows = new List<CertificateCalibrationResult>
            {
                new() { Radionuclide = "Cs-137" },
                new() { Radionuclide = "Co-60" },
            };

            Assert.False(CorrectionFactorLabelRules.IsAveraged("Cs-137", rows));
        }

        [Fact]
        public void LabelFor_Averaged_ReturnsCFavg()
        {
            Assert.Equal("CFavg", CorrectionFactorLabelRules.LabelFor(true));
        }

        [Fact]
        public void LabelFor_NotAveraged_ReturnsCF()
        {
            Assert.Equal("CF", CorrectionFactorLabelRules.LabelFor(false));
        }

        [Fact]
        public void HeaderFor_AllAveraged_ReturnsAverageHeader()
        {
            Assert.Equal("Average Correction Factor (CFavg)",
                CorrectionFactorLabelRules.HeaderFor(new[] { true, true }));
        }

        [Fact]
        public void HeaderFor_AllSingle_ReturnsPlainHeader()
        {
            Assert.Equal("Correction Factor (CF)",
                CorrectionFactorLabelRules.HeaderFor(new[] { false, false }));
        }

        [Fact]
        public void HeaderFor_Mixed_ReturnsCombinedHeader()
        {
            Assert.Equal("Correction Factor (CF / CFavg)",
                CorrectionFactorLabelRules.HeaderFor(new[] { true, false }));
        }

        [Fact]
        public void HeaderFor_NoNuclides_ReturnsPlainHeader()
        {
            Assert.Equal("Correction Factor (CF)",
                CorrectionFactorLabelRules.HeaderFor(System.Array.Empty<bool>()));
        }
    }
}
