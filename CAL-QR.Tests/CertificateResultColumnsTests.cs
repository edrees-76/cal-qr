using System.Collections.Generic;
using System.Linq;
using Xunit;
using CAL_QR.Models;
using CAL_QR.Services.Documents;

namespace CAL_QR.Tests
{
    public class CertificateResultColumnsTests
    {
        [Fact]
        public void Select_AllRowsHaveOnlySourceId_ReturnsSingleColumn()
        {
            var rows = new List<CertificateCalibrationResult>
            {
                new CertificateCalibrationResult { SourceId = "S1" },
                new CertificateCalibrationResult { SourceId = "S2" },
            };

            var columns = CertificateResultColumns.Select(rows);

            Assert.Single(columns);
            Assert.Equal(CertificateResultColumn.SourceId, columns[0]);
        }

        [Fact]
        public void Select_ColumnHeldByOneOfThreeRows_IsIncluded()
        {
            var rows = new List<CertificateCalibrationResult>
            {
                new CertificateCalibrationResult { SourceId = "S1" },
                new CertificateCalibrationResult { SourceId = "S2", Remarks = "Note" },
                new CertificateCalibrationResult { SourceId = "S3" },
            };

            var columns = CertificateResultColumns.Select(rows);

            Assert.Contains(CertificateResultColumn.Remarks, columns);
        }

        [Fact]
        public void Select_ColumnWithOnlyBlankAndWhitespaceAndNullValues_IsExcluded()
        {
            var rows = new List<CertificateCalibrationResult>
            {
                new CertificateCalibrationResult { SourceId = "S1", Unit = "" },
                new CertificateCalibrationResult { SourceId = "S2", Unit = "   " },
                new CertificateCalibrationResult { SourceId = "S3", Unit = null },
            };

            var columns = CertificateResultColumns.Select(rows);

            Assert.DoesNotContain(CertificateResultColumn.Unit, columns);
        }

        [Fact]
        public void Select_ReturnedOrder_MatchesFixedOrderRegardlessOfFillOrder()
        {
            var rows = new List<CertificateCalibrationResult>
            {
                new CertificateCalibrationResult
                {
                    Remarks = "R",
                    Unit = "U",
                    SourceId = "S",
                    Radionuclide = "N",
                }
            };

            var columns = CertificateResultColumns.Select(rows);

            var expectedOrder = new[]
            {
                CertificateResultColumn.SourceId,
                CertificateResultColumn.Radionuclide,
                CertificateResultColumn.Unit,
                CertificateResultColumn.Remarks
            };

            Assert.Equal(expectedOrder, columns.ToArray());
        }

        [Fact]
        public void Select_EmptyList_ReturnsNoColumns()
        {
            var columns = CertificateResultColumns.Select(new List<CertificateCalibrationResult>());

            Assert.Empty(columns);
        }
    }
}
