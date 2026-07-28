using System;
using System.Globalization;
using System.Threading;
using Xunit;
using CAL_QR.Models;
using CAL_QR.Services.Payloads;
using CAL_QR.Validation;

namespace CAL_QR.Tests
{
    /// <summary>
    /// التطبيع والهروب والترتيب — الثلاثة التي تُنتج «فشل تحقق عشوائي» إن اختلّت.
    /// </summary>
    public class SignaturePayloadNormalizationTests
    {
        private static Certificate MinimalCertificate() => new Certificate
        {
            Id = 1,
            CertificateNumber = "TNRC-SSDL-2026-0001",
            ClientName = "جهة",
            DeviceModel = "Model",
            DeviceSerialNumber = "SN-1",
            CalibrationDate = new DateTime(2026, 1, 15),
            IssueDate = new DateTime(2026, 1, 20),
            DueDate = new DateTime(2027, 1, 15)
        };

        [Theory]
        [InlineData("ar-LY")]
        [InlineData("de-DE")]
        [InlineData("en-US")]
        public void Payload_IsIdenticalAcrossCultures(string cultureName)
        {
            var builder = new SignaturePayloadBuilderV1();
            var original = CultureInfo.CurrentCulture;

            try
            {
                // ثقافة de-DE تستعمل الفاصلة عشرية وتُنسّق التواريخ بصيغة مغايرة:
                // أي تسرّب لثقافة النظام إلى نص التوقيع يظهر هنا.
                Thread.CurrentThread.CurrentCulture = new CultureInfo(cultureName);

                var certificate = MinimalCertificate();
                certificate.CombinedUncertainty = "5.40";
                certificate.CalibrationResults.Add(new CertificateCalibrationResult
                {
                    Id = 1,
                    SortOrder = 1,
                    ReferenceValue = "5.40",
                    RelativeError = "-3.70",
                    AbsoluteRelativeError = "3.70"
                });

                string payload = builder.Build(certificate);

                Assert.Contains("CD:20260115", payload);
                Assert.Contains("UC:5.40", payload);
                Assert.Contains(";-3.70;3.70;", payload);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }

        [Fact]
        public void Normalizer_FoldsArabicIndicDigitsToAscii()
        {
            Assert.Equal("2026", PayloadNormalizer.Text("٢٠٢٦"));
            Assert.Equal("5.40", PayloadNormalizer.Text("٥.٤٠"));
        }

        [Fact]
        public void Normalizer_TrimsAndCollapsesWhitespace()
        {
            Assert.Equal("Ludlum 44-9", PayloadNormalizer.Text("  Ludlum   44-9  "));
            Assert.Equal("Ludlum 44-9", PayloadNormalizer.Text("Ludlum\t\t44-9"));
        }

        [Fact]
        public void Normalizer_TreatsNullAndBlankAlike()
        {
            Assert.Equal("~", PayloadNormalizer.Text(null));
            Assert.Equal("~", PayloadNormalizer.Text(""));
            Assert.Equal("~", PayloadNormalizer.Text("    "));
        }

        [Fact]
        public void Normalizer_EscapesSeparatorAndBackslash()
        {
            Assert.Equal("\\;", PayloadNormalizer.Text(";"));
            Assert.Equal("\\\\", PayloadNormalizer.Text("\\"));
        }

        [Fact]
        public void SignatoryNameContainingSeparator_DoesNotShiftFields()
        {
            var builder = new SignaturePayloadBuilderV1();

            var injected = MinimalCertificate();
            injected.CalibratedByName = "فلان;SSDL - TNRC;20990101";
            injected.CalibratedByTitle = "لقب";
            injected.CalibratedByDate = new DateTime(2026, 1, 20);

            var honest = MinimalCertificate();
            honest.CalibratedByName = "فلان";
            honest.CalibratedByTitle = "SSDL - TNRC";
            honest.CalibratedByDate = new DateTime(2099, 1, 1);

            string injectedPayload = builder.Build(injected);

            // الفواصل المحقونة مهرَّبة، فالصف يبقى ثلاثة حقول لا خمسة
            Assert.Contains("G1:فلان\\;SSDL - TNRC\\;20990101;لقب;20260120", injectedPayload);
            Assert.NotEqual(builder.Build(honest), injectedPayload);
        }

        [Fact]
        public void DroppingARow_ChangesTheCountLine_AndTherebyThePayload()
        {
            var builder = new SignaturePayloadBuilderV1();

            var full = MinimalCertificate();
            full.CalibrationResults.Add(new CertificateCalibrationResult { Id = 1, SortOrder = 1, Radionuclide = "Cs-137" });
            full.CalibrationResults.Add(new CertificateCalibrationResult { Id = 2, SortOrder = 2, Radionuclide = "Co-60" });

            var truncated = MinimalCertificate();
            truncated.CalibrationResults.Add(new CertificateCalibrationResult { Id = 1, SortOrder = 1, Radionuclide = "Cs-137" });

            string fullPayload = builder.Build(full);
            string truncatedPayload = builder.Build(truncated);

            Assert.Contains("RC:2", fullPayload);
            Assert.Contains("RC:1", truncatedPayload);
            Assert.NotEqual(fullPayload, truncatedPayload);
        }

        [Fact]
        public void TiedSortOrder_IsBrokenByIdDeterministically()
        {
            // الحالة التي كشفت وجوب حساب التوقيع بعد SaveChanges الأولى:
            // ثلاثة صفوف بنفس SortOrder، تُضاف بترتيبين مختلفين.
            var builder = new SignaturePayloadBuilderV1();

            var ascending = MinimalCertificate();
            ascending.CalibrationResults.Add(new CertificateCalibrationResult { Id = 11, SortOrder = 1, Radionuclide = "Cs-137" });
            ascending.CalibrationResults.Add(new CertificateCalibrationResult { Id = 12, SortOrder = 1, Radionuclide = "Co-60" });
            ascending.CalibrationResults.Add(new CertificateCalibrationResult { Id = 13, SortOrder = 1, Radionuclide = "Am-241" });

            var shuffled = MinimalCertificate();
            shuffled.CalibrationResults.Add(new CertificateCalibrationResult { Id = 13, SortOrder = 1, Radionuclide = "Am-241" });
            shuffled.CalibrationResults.Add(new CertificateCalibrationResult { Id = 11, SortOrder = 1, Radionuclide = "Cs-137" });
            shuffled.CalibrationResults.Add(new CertificateCalibrationResult { Id = 12, SortOrder = 1, Radionuclide = "Co-60" });

            Assert.Equal(builder.Build(ascending), builder.Build(shuffled));
        }

        [Theory]
        [InlineData("-5.40", "5.40")]
        [InlineData("−5.40", "5.40")]
        [InlineData("5.40", "5.40")]
        [InlineData("< 0.1", "< 0.1")]
        [InlineData("N/A", "N/A")]
        [InlineData("-0.00", "0.00")]
        [InlineData("", "")]
        public void AbsoluteOf_StripsTheSignWithoutReformattingTheNumber(string input, string expected)
        {
            Assert.Equal(expected, MeasurementValueRules.AbsoluteOf(input)!);
        }

        [Fact]
        public void AbsoluteOf_PreservesTrailingSignificantZeros()
        {
            // الفارق الحاسم عن Parse/Abs/ToString: هذا كان سيُعيد "5.4".
            Assert.Equal("5.40", MeasurementValueRules.AbsoluteOf("-5.40")!);
            Assert.Equal("1.000", MeasurementValueRules.AbsoluteOf("-1.000")!);
        }

        [Fact]
        public void AbsoluteOf_NullStaysNull()
        {
            Assert.Null(MeasurementValueRules.AbsoluteOf(null));
        }
    }
}
