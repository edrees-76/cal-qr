using System.Collections.Generic;
using System.Linq;
using CAL_QR.Models;
using CAL_QR.Validation;
using Xunit;

namespace CAL_QR.Tests
{
    public class NumericConsistencyRulesTests
    {
        private static CertificateUncertaintyComponent Comp(string? name, string? contribution, string? u = null) =>
            new() { ComponentName = name ?? string.Empty, ContributionPercent = contribution, StandardUncertainty = u };

        // ── 1: U ≈ k × uc ───────────────────────────────────────────────────

        [Theory]
        [InlineData("0.52", "2", "1.04")]
        [InlineData("0.52", "2", "1.05")]   // حدّ التسامح: خانة U الأخيرة + تقريب uc
        [InlineData("0.52", "2", "1.03")]
        [InlineData("0.52", "2.0", "1.04")]
        [InlineData("0,52", "2,00", "1,04")]
        [InlineData("٠٫٥٢", "٢", "١٫٠٤")]  // أرقام عربيّة
        [InlineData("1.00", "2", "2.00")]
        public void ExpandedUncertainty_ConsistentValues_NoWarning(string uc, string k, string u)
        {
            Assert.Null(NumericConsistencyRules.ExpandedUncertaintyWarning(uc, k, u));
        }

        [Theory]
        [InlineData("0.52", "2", "1.30")]
        [InlineData("0.52", "2", "1.06")]   // خارج التسامح بقليل
        [InlineData("0.52", "2", "0.52")]   // نسي الضرب في k
        [InlineData("0.52", "3", "1.04")]   // k خاطئة
        public void ExpandedUncertainty_Mismatch_Warns(string uc, string k, string u)
        {
            string? warning = NumericConsistencyRules.ExpandedUncertaintyWarning(uc, k, u);

            Assert.NotNull(warning);
            Assert.Contains("k × uc", warning);
        }

        [Fact]
        public void ExpandedUncertainty_WarningShowsExpectedValueWithUPrecision()
        {
            string? warning = NumericConsistencyRules.ExpandedUncertaintyWarning("0.52", "2", "1.30");

            Assert.Contains("1.04", warning);
            Assert.Contains("1.30", warning);
        }

        [Theory]
        [InlineData(null, "2", "1.04")]
        [InlineData("0.52", null, "1.04")]
        [InlineData("0.52", "2", null)]
        [InlineData("", "2", "1.04")]
        [InlineData("N/A", "2", "1.04")]
        [InlineData("0.52", "N/A", "1.04")]
        [InlineData("< 0.1", "2", "0.2")]
        [InlineData("0.52 %", "2", "1.04 %")]   // قيمة بوحدة: غير رقميّة كاملة فتُتخطّى
        [InlineData("0.52", "0", "9.99")]       // k غير صالحة للفحص
        [InlineData("-0.52", "2", "9.99")]      // uc سالبة تتولّاها فحوص الحدود
        public void ExpandedUncertainty_NotApplicable_NoWarning(string? uc, string? k, string? u)
        {
            Assert.Null(NumericConsistencyRules.ExpandedUncertaintyWarning(uc, k, u));
        }

        // ── 2: مجموع المساهمات ≈ 100 ───────────────────────────────────────

        [Fact]
        public void ContributionSum_ExactlyHundred_NoWarning()
        {
            var rows = new[] { Comp("a", "60.0"), Comp("b", "30.0"), Comp("c", "10.0") };
            Assert.Null(NumericConsistencyRules.ContributionSumWarning(rows));
        }

        [Theory]
        [InlineData("33.3", "33.3", "33.3")]    // 99.9: تقريب ثلاثة أثلاث
        [InlineData("33.4", "33.3", "33.4")]    // 100.1
        [InlineData("33", "33", "33")]          // أعداد صحيحة: 99، ضمن اتّساع التسامح
        public void ContributionSum_RoundingNoise_NoWarning(string a, string b, string c)
        {
            var rows = new[] { Comp("a", a), Comp("b", b), Comp("c", c) };
            Assert.Null(NumericConsistencyRules.ContributionSumWarning(rows));
        }

        [Theory]
        [InlineData("60.0", "30.0", "5.0")]     // 95
        [InlineData("60.0", "30.0", "15.0")]    // 105
        public void ContributionSum_NotHundred_Warns(string a, string b, string c)
        {
            var rows = new[] { Comp("a", a), Comp("b", b), Comp("c", c) };

            string? warning = NumericConsistencyRules.ContributionSumWarning(rows);

            Assert.NotNull(warning);
            Assert.Contains("100", warning);
        }

        [Fact]
        public void ContributionSum_SingleComponentNotHundred_Warns()
        {
            Assert.NotNull(NumericConsistencyRules.ContributionSumWarning(new[] { Comp("a", "50.0") }));
        }

        [Fact]
        public void ContributionSum_FullyEmptyRowsAreIgnored()
        {
            var rows = new[] { Comp("a", "100.0"), Comp(null, null) };
            Assert.Null(NumericConsistencyRules.ContributionSumWarning(rows));
        }

        [Fact]
        public void ContributionSum_RowWithBlankOrNonNumericContribution_SkipsCheck()
        {
            Assert.Null(NumericConsistencyRules.ContributionSumWarning(new[] { Comp("a", "50.0"), Comp("b", "") }));
            Assert.Null(NumericConsistencyRules.ContributionSumWarning(new[] { Comp("a", "50.0"), Comp("b", "N/A") }));
        }

        [Fact]
        public void ContributionSum_NoRowsOrNull_NoWarning()
        {
            Assert.Null(NumericConsistencyRules.ContributionSumWarning(null));
            Assert.Null(NumericConsistencyRules.ContributionSumWarning(new List<CertificateUncertaintyComponent>()));
            Assert.Null(NumericConsistencyRules.ContributionSumWarning(new[] { Comp(null, null) }));
        }

        // ── 3: k معقول ──────────────────────────────────────────────────────

        [Theory]
        [InlineData("1")]
        [InlineData("2")]
        [InlineData("2.00")]
        [InlineData("2.58")]
        [InlineData("3")]
        [InlineData("3.0")]
        [InlineData("١٫٩٦")]
        [InlineData("N/A")]
        [InlineData("")]
        [InlineData(null)]
        public void CoverageFactorRange_InRangeOrNotApplicable_NoWarning(string? k)
        {
            Assert.Null(NumericConsistencyRules.CoverageFactorRangeWarning(k));
        }

        [Theory]
        [InlineData("0.5")]
        [InlineData("0")]
        [InlineData("-2")]
        [InlineData("3.01")]
        [InlineData("20")]
        public void CoverageFactorRange_OutOfRange_Warns(string k)
        {
            Assert.NotNull(NumericConsistencyRules.CoverageFactorRangeWarning(k));
        }

        // ── 4: حدود منطقيّة ─────────────────────────────────────────────────

        [Fact]
        public void UncertaintyBounds_ValidValues_NoWarnings()
        {
            var rows = new[] { Comp("a", "50", "0.3"), Comp("b", "50", "0") };
            Assert.Empty(NumericConsistencyRules.UncertaintyBoundsWarnings("0.5", "1.0", rows));
        }

        [Fact]
        public void UncertaintyBounds_NegativeAndOutOfRange_WarnsEach()
        {
            var rows = new[] { Comp("A", "120", "-0.3"), Comp("B", "-5", "0.1") };

            var warnings = NumericConsistencyRules.UncertaintyBoundsWarnings("-0.5", "-1.0", rows);

            // uc وU سالبتان (2) + مكوّن A: سالب ونسبة خارج المدى (2) + مكوّن B: نسبة خارج المدى (1).
            Assert.Equal(5, warnings.Count);
            Assert.Contains(warnings, w => w.Contains("uc المركّبة"));
            Assert.Contains(warnings, w => w.Contains("U الموسّعة"));
            Assert.Contains(warnings, w => w.Contains("«A»") && w.Contains("سالب"));
            Assert.Contains(warnings, w => w.Contains("«A»") && w.Contains("0 إلى 100"));
            Assert.Contains(warnings, w => w.Contains("«B»") && w.Contains("0 إلى 100"));
        }

        [Fact]
        public void UncertaintyBounds_ContributionExactlyHundredOrZero_NoWarning()
        {
            var rows = new[] { Comp("a", "100"), Comp("b", "0") };
            Assert.Empty(NumericConsistencyRules.UncertaintyBoundsWarnings(null, null, rows));
        }

        [Fact]
        public void UncertaintyBounds_NonNumericValues_Skipped()
        {
            var rows = new[] { Comp("a", "N/A", "< 0.1") };
            Assert.Empty(NumericConsistencyRules.UncertaintyBoundsWarnings("N/A", "-", rows));
        }

        [Fact]
        public void ResultBounds_ZeroOrNegativeReferenceAndNegativeReading_Warn()
        {
            var rows = new[]
            {
                new CertificateCalibrationResult { ReferenceValue = "0", MeasuredReading = "1.2" },
                new CertificateCalibrationResult { ReferenceValue = "-3", MeasuredReading = "-0.1" },
                new CertificateCalibrationResult { ReferenceValue = "10", MeasuredReading = "0" },
            };

            var warnings = NumericConsistencyRules.ResultBoundsWarnings(rows);

            Assert.Equal(3, warnings.Count);
            Assert.Contains(warnings, w => w.Contains("صفّ النتائج 1") && w.Contains("المرجعيّة"));
            Assert.Contains(warnings, w => w.Contains("صفّ النتائج 2") && w.Contains("المرجعيّة"));
            Assert.Contains(warnings, w => w.Contains("صفّ النتائج 2") && w.Contains("القراءة"));
        }

        [Fact]
        public void ResultBounds_NonNumericOrEmpty_Skipped()
        {
            var rows = new[]
            {
                new CertificateCalibrationResult { ReferenceValue = "1.0 mSv", MeasuredReading = "N/A" },
                new CertificateCalibrationResult(),
            };

            Assert.Empty(NumericConsistencyRules.ResultBoundsWarnings(rows));
            Assert.Empty(NumericConsistencyRules.ResultBoundsWarnings(null));
        }

        // ── تجميع ───────────────────────────────────────────────────────────

        [Fact]
        public void Warnings_UncertaintyDisabled_OnlyResultBoundsRun()
        {
            var results = new[] { new CertificateCalibrationResult { ReferenceValue = "0" } };
            var comps = new[] { Comp("a", "50") };

            var warnings = NumericConsistencyRules.Warnings(false, "0.52", "20", "9.99", comps, results);

            Assert.Single(warnings);
            Assert.Contains("القيمة المرجعيّة", warnings[0]);
        }

        [Fact]
        public void Warnings_UncertaintyEnabled_RunsAllPhaseOneChecks()
        {
            var comps = new[] { Comp("a", "50") };

            var warnings = NumericConsistencyRules.Warnings(true, "0.52", "20", "9.99", comps, null);

            Assert.Contains(warnings, w => w.Contains("k × uc"));
            Assert.Contains(warnings, w => w.Contains("مجموع نسب المساهمة"));
            Assert.Contains(warnings, w => w.Contains("خارج المدى المعتاد"));
        }

        [Fact]
        public void Warnings_AllConsistent_Empty()
        {
            var comps = new[] { Comp("a", "70.0", "0.4"), Comp("b", "30.0", "0.3") };

            var warnings = NumericConsistencyRules.Warnings(true, "0.52", "2", "1.04", comps, null);

            Assert.Empty(warnings);
        }

        [Fact]
        public void Rules_NeverMutateInputs()
        {
            var comp = Comp("a", "50", "0.3");
            string before = comp.ContributionPercent!;

            _ = NumericConsistencyRules.Warnings(true, "0.52", "2", "1.30", new[] { comp }, null);

            Assert.Equal(before, comp.ContributionPercent);
        }
    }
}
