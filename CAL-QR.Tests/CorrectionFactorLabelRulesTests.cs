using System.Linq;
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
        // ===== ShortLabelFor =====

        [Theory]
        [InlineData(new bool[0], "CF")]
        [InlineData(new[] { false }, "CF")]
        [InlineData(new[] { true, true }, "CFavg")]
        [InlineData(new[] { true, false }, "CF / CFavg")]
        public void ShortLabelFor_FollowsFlags(bool[] flags, string expected)
        {
            Assert.Equal(expected, CorrectionFactorLabelRules.ShortLabelFor(flags));
        }

        // ===== FormulaFor: معادلة القراءة المصحّحة =====

        [Fact]
        public void FormulaFor_TemplateCFavg_SingleReading_BecomesCF()
        {
            Assert.Equal(CorrectionFactorLabelRules.CfFormula,
                CorrectionFactorLabelRules.FormulaFor(CorrectionFactorLabelRules.CfAvgFormula, new[] { false }));
        }

        [Fact]
        public void FormulaFor_TemplateCF_AllAveraged_BecomesCFavg()
        {
            Assert.Equal(CorrectionFactorLabelRules.CfAvgFormula,
                CorrectionFactorLabelRules.FormulaFor(CorrectionFactorLabelRules.CfFormula, new[] { true, true }));
        }

        [Fact]
        public void FormulaFor_Mixed_LeavesTextUnchanged()
        {
            // القالب لا يحوي معادلة لحالة مختلطة؛ لا يُختلق نصّ.
            Assert.Equal(CorrectionFactorLabelRules.CfAvgFormula,
                CorrectionFactorLabelRules.FormulaFor(CorrectionFactorLabelRules.CfAvgFormula, new[] { true, false }));
        }

        [Fact]
        public void FormulaFor_NoNuclides_LeavesTextUnchanged()
        {
            // العائلة المبسّطة وتقرير الحالة: لا نظائر، فتبقى معادلة القالب.
            Assert.Equal(CorrectionFactorLabelRules.CfAvgFormula,
                CorrectionFactorLabelRules.FormulaFor(CorrectionFactorLabelRules.CfAvgFormula, new bool[0]));
        }

        [Theory]
        [InlineData("Corrected Reading = Measured Reading × CFavg × k")]
        [InlineData("Reading × CFavg")]
        [InlineData("")]
        [InlineData(null)]
        public void FormulaFor_CustomOrEmptyText_IsNeverRewritten(string? formula)
        {
            Assert.Equal(formula, CorrectionFactorLabelRules.FormulaFor(formula, new[] { false }));
        }

        [Fact]
        public void FormulaFor_TemplateTextWithDifferentSpacing_IsRecognized()
        {
            Assert.Equal(CorrectionFactorLabelRules.CfFormula,
                CorrectionFactorLabelRules.FormulaFor("Corrected Reading =  Measured Reading ×CFavg ", new[] { false }));
        }

        [Fact]
        public void FormulaFor_AlreadyCorrect_ReturnsOriginalTextUnchanged()
        {
            // لا تبديل لنصّ صحيح بمسافات مختلفة: كلّ تغيير في RF يغيّر حمولة التوقيع.
            const string spaced = "Corrected Reading = Measured Reading ×  CF";
            Assert.Same(spaced, CorrectionFactorLabelRules.FormulaFor(spaced, new[] { false }));
        }

        // ===== NuclideLines / PrintedFlags: سطور الملصق وشريط PDF =====

        [Fact]
        public void NuclideLines_LabelEachNuclideByItsOwnRowCount()
        {
            var summaries = new List<CertificateNuclideSummary>
            {
                new() { SortOrder = 2, Radionuclide = "Co-60", AverageCorrectionFactor = "0.98" },
                new() { SortOrder = 1, Radionuclide = "Cs-137", AverageCorrectionFactor = " 1.02 " },
                new() { SortOrder = 3, Radionuclide = "Am-241", AverageCorrectionFactor = "" },
            };
            var rows = new List<CertificateCalibrationResult>
            {
                new() { Radionuclide = "Cs-137" },
                new() { Radionuclide = "Cs-137" },
                new() { Radionuclide = "Co-60" },
            };

            Assert.Equal(new[] { "CFavg Cs-137 = 1.02", "CF Co-60 = 0.98" },
                CorrectionFactorLabelRules.NuclideLines(summaries, rows));
            Assert.Equal(new[] { true, false },
                CorrectionFactorLabelRules.PrintedFlags(summaries, rows));
        }

        [Fact]
        public void NuclideLines_NullInputs_ReturnEmpty()
        {
            Assert.Empty(CorrectionFactorLabelRules.NuclideLines(null, null));
            Assert.Empty(CorrectionFactorLabelRules.PrintedFlags(null, null));
        }

        // نصّا المعادلة في الكتالوج هما النصّان اللذان تتعرّف عليهما القاعدة؛
        // تباعدهما يُعطّل التصحيح بصمت.
        [Fact]
        public void CatalogFormulas_AreTheTemplateTextsTheRuleRecognizes()
        {
            var formulas = CAL_QR.Data.DeviceTypeCatalog.All
                .Select(d => d.CorrectedReadingFormula)
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .ToList();

            Assert.NotEmpty(formulas);
            Assert.All(formulas, f => Assert.Contains(f!, new[] { CorrectionFactorLabelRules.CfFormula, CorrectionFactorLabelRules.CfAvgFormula }));
        }
    }
}
