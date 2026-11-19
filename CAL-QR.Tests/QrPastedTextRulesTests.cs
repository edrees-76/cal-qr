using CAL_QR.Models;
using CAL_QR.Validation;
using Xunit;

namespace CAL_QR.Tests
{
    /// <summary>قواعد نصّ QR الملصوق: الاستخراج والمقارنة بسجلّ الشهادة (دوالّ نقيّة).</summary>
    public class QrPastedTextRulesTests
    {
        private static Certificate Cert() => new Certificate
        {
            ClientName = "مركز البحوث النووية",
            DeviceSerialNumber = "PR-777",
            CertificateNumber = "TNRC-SSDL-2026-0001"
        };

        [Fact]
        public void Parse_ExtractsAllKnownFields()
        {
            var parsed = QrPastedTextRules.Parse("L:TNRC-SSDL\r\nC:مركز البحوث النووية\nSN:PR-777\nCN:TNRC-SSDL-2026-0001\nV:ABCD1234");

            Assert.Equal("TNRC-SSDL", parsed["L"]);
            Assert.Equal("مركز البحوث النووية", parsed["C"]);
            Assert.Equal("PR-777", parsed["SN"]);
            Assert.Equal("TNRC-SSDL-2026-0001", parsed["CN"]);
            Assert.Equal("ABCD1234", parsed["V"]);
        }

        [Fact]
        public void Parse_ToleratesLeadingSpacesAndKeepsFirstOccurrence()
        {
            var parsed = QrPastedTextRules.Parse("   V:FIRST\nV:SECOND");

            Assert.Equal("FIRST", parsed["V"]);
        }

        [Fact]
        public void Parse_CnLineIsNotMistakenForC()
        {
            var parsed = QrPastedTextRules.Parse("CN:TNRC-SSDL-2026-0001");

            Assert.False(parsed.ContainsKey("C"));
            Assert.Equal("TNRC-SSDL-2026-0001", parsed["CN"]);
        }

        [Fact]
        public void Parse_EmptyOrNull_ReturnsEmpty()
        {
            Assert.Empty(QrPastedTextRules.Parse(null));
            Assert.Empty(QrPastedTextRules.Parse("   "));
        }

        [Fact]
        public void FindMismatches_MatchingFields_ReturnsNone()
        {
            var parsed = QrPastedTextRules.Parse("C:مركز البحوث النووية\nSN:PR-777\nCN:TNRC-SSDL-2026-0001");

            Assert.Empty(QrPastedTextRules.FindMismatches(parsed, Cert()));
        }

        [Fact]
        public void FindMismatches_IgnoresWhitespaceAndCaseDifferences()
        {
            var parsed = QrPastedTextRules.Parse("C:  مركز   البحوث النووية \nSN:pr-777");

            Assert.Empty(QrPastedTextRules.FindMismatches(parsed, Cert()));
        }

        [Fact]
        public void FindMismatches_AbsentFieldsAreNotMismatches()
        {
            var parsed = QrPastedTextRules.Parse("V:ABCD1234");

            Assert.Empty(QrPastedTextRules.FindMismatches(parsed, Cert()));
        }

        [Fact]
        public void FindMismatches_ReportsEachDifferingFieldWithBothValues()
        {
            var parsed = QrPastedTextRules.Parse("C:جهة أخرى\nSN:PR-777\nCN:TNRC-SSDL-2026-0999");

            var mismatches = QrPastedTextRules.FindMismatches(parsed, Cert());

            Assert.Equal(2, mismatches.Count);
            Assert.Contains(mismatches, m => m.Label == "اسم العميل" && m.Pasted == "جهة أخرى" && m.Recorded == "مركز البحوث النووية");
            Assert.Contains(mismatches, m => m.Label == "رقم الشهادة" && m.Pasted == "TNRC-SSDL-2026-0999");
        }
    }
}
