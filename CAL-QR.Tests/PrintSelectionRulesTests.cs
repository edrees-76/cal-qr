using System.Collections.Generic;
using CAL_QR.Models;
using CAL_QR.Validation;
using Xunit;

namespace CAL_QR.Tests
{
    public class PrintSelectionRulesTests
    {
        private static List<PaperTemplate> Templates() => new()
        {
            new PaperTemplate { Id = 1, TemplateName = "A" },
            new PaperTemplate { Id = 2, TemplateName = "B", IsDefault = true },
            new PaperTemplate { Id = 3, TemplateName = "C" }
        };

        [Fact]
        public void PickTemplate_PreferredWinsOverLastAndDefault()
        {
            var picked = PrintSelectionRules.PickTemplate(Templates(), preferredId: 3, lastSavedId: 1);
            Assert.Equal(3, picked!.Id);
        }

        [Fact]
        public void PickTemplate_NoPreferred_UsesLastSaved()
        {
            Assert.Equal(1, PrintSelectionRules.PickTemplate(Templates(), null, 1)!.Id);
        }

        [Fact]
        public void PickTemplate_PreferredMissing_FallsBackToLastThenDefault()
        {
            Assert.Equal(1, PrintSelectionRules.PickTemplate(Templates(), 99, 1)!.Id);
            Assert.Equal(2, PrintSelectionRules.PickTemplate(Templates(), 99, 98)!.Id);
        }

        [Fact]
        public void PickTemplate_NoDefault_ReturnsFirst_AndEmptyReturnsNull()
        {
            var list = new List<PaperTemplate> { new() { Id = 5 }, new() { Id = 6 } };
            Assert.Equal(5, PrintSelectionRules.PickTemplate(list, null, null)!.Id);
            Assert.Null(PrintSelectionRules.PickTemplate(new List<PaperTemplate>(), 1, 1));
        }

        [Fact]
        public void PickPrinter_CurrentKeptOverSaved()
        {
            var printers = new List<string> { "P1", "P2", "P3" };
            Assert.Equal("P3", PrintSelectionRules.PickPrinter(printers, "P3", "P1"));
        }

        [Fact]
        public void PickPrinter_CurrentGone_FallsBackToSavedThenFirstThenEmpty()
        {
            var printers = new List<string> { "P1", "P2" };
            Assert.Equal("P2", PrintSelectionRules.PickPrinter(printers, "Gone", "P2"));
            Assert.Equal("P1", PrintSelectionRules.PickPrinter(printers, "", "Gone"));
            Assert.Equal("", PrintSelectionRules.PickPrinter(new List<string>(), "x", "y"));
        }
    }
}
