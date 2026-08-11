using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using CAL_QR.Models;
using CAL_QR.Services;
using CAL_QR.Services.Documents;

namespace CAL_QR.Tests
{
    public class CertificatePdfServiceTests
    {
        private readonly ICertificatePdfService _service;

        public CertificatePdfServiceTests()
        {
            CertificatePdfEnvironment.EnsureInitialized();
            _service = new CertificatePdfService(new QrService(null!));
        }

        private static void AssertLooksLikePdf(byte[] bytes)
        {
            Assert.True(bytes.Length > 1000, $"Expected PDF bytes > 1000, got {bytes.Length}");
            string header = Encoding.ASCII.GetString(bytes, 0, 5);
            Assert.Equal("%PDF-", header);
        }

        private static Certificate BuildBaseCertificate()
        {
            return new Certificate
            {
                CertificateNumber = "TNRC-SSDL-2026-0001",
                CertificateTemplateType = "Beta Probe",
                ClientName = "جهة فحص تجريبية",
                DeviceModel = "X-Model",
                DeviceSerialNumber = "SN-001",
                CalibrationDate = new DateTime(2026, 1, 1),
                IssueDate = new DateTime(2026, 1, 2),
                DueDate = new DateTime(2027, 1, 1),
                QrPayload = "PAYLOAD-TEST",
                VerifyCode = "ABCD1234ABCD1234",
            };
        }

        [Fact]
        public void GenerateBytes_FullFamily_ProducesValidPdf()
        {
            var certificate = BuildBaseCertificate();
            certificate.ClientAddress = "طرابلس";
            certificate.DeviceManufacturer = "Manufacturer";
            certificate.DetectorType = "GM Tube";
            certificate.ProcedureNo = "SSDL-CP-01";
            certificate.CalibrationLocation = "SSDL Lab";
            certificate.Instrumentation = "Reference chamber";
            certificate.MeasurementType = "Dose Rate Measurement (µSv/h)";
            certificate.Distance = "1.0 meter";
            certificate.CalibrationStandard = "ISO/IEC 17025:2017";
            certificate.ComplianceVerdict = "Passed";
            certificate.Temperature = "22 C";
            certificate.RelativeHumidity = "45%";
            certificate.AtmosphericPressure = "1013 hPa";
            certificate.CountingTime = "60 Sec";
            certificate.CountingUnit = "kCPM";
            certificate.CalibrationMode = "Direct Contact Geometry";

            certificate.MethodologyEnabled = true;
            certificate.RadiationSource = "Cs-137";
            certificate.ReferenceGeometry = "Direct";
            certificate.MethodologyText = "نص المنهجية";
            certificate.TraceabilityReference = "NIST";

            certificate.UncertaintyEnabled = true;
            certificate.CombinedUncertainty = "0.05";
            certificate.ExpandedUncertainty = "0.10";
            certificate.CoverageFactor = "2";

            certificate.CalibrationResults.Add(new CertificateCalibrationResult
            {
                SortOrder = 1,
                SourceId = "SRC-1",
                Radionuclide = "Cs-137",
                MeasuredReading = "10.1",
                CorrectionFactor = "1.02",
                Unit = "µSv/h"
            });
            certificate.CalibrationResults.Add(new CertificateCalibrationResult
            {
                SortOrder = 2,
                SourceId = "SRC-2",
                Radionuclide = "Co-60",
                MeasuredReading = "20.3",
                CorrectionFactor = "0.98",
                Unit = "µSv/h"
            });

            certificate.NuclideSummaries.Add(new CertificateNuclideSummary
            {
                SortOrder = 1,
                Radionuclide = "Cs-137",
                AverageCorrectionFactor = "1.02"
            });
            certificate.NuclideSummaries.Add(new CertificateNuclideSummary
            {
                SortOrder = 2,
                Radionuclide = "Co-60",
                AverageCorrectionFactor = "0.98"
            });

            certificate.UncertaintyComponents.Add(new CertificateUncertaintyComponent
            {
                SortOrder = 1,
                ComponentName = "Repeatability",
                EvaluationType = "A",
                Distribution = "Normal",
                StandardUncertainty = "0.01",
                ContributionPercent = "10"
            });

            certificate.FunctionalChecks.Add(new CertificateFunctionalCheck
            {
                SortOrder = 1,
                CheckName = "Battery Check",
                Requirement = "> 20%",
                Result = "Pass"
            });

            byte[] bytes = _service.GenerateBytes(certificate);

            AssertLooksLikePdf(bytes);
        }

        [Fact]
        public void GenerateBytes_SimplifiedFamily_UncertaintyAndMethodologyDisabled_ProducesValidPdf()
        {
            var certificate = BuildBaseCertificate();
            certificate.CertificateTemplateType = "PED";
            certificate.UncertaintyEnabled = false;
            certificate.MethodologyEnabled = false;

            byte[] bytes = _service.GenerateBytes(certificate);

            AssertLooksLikePdf(bytes);
        }

        [Fact]
        public void GenerateBytes_AllFourChildTablesEmpty_ProducesValidPdf()
        {
            var certificate = BuildBaseCertificate();

            Assert.Empty(certificate.CalibrationResults);
            Assert.Empty(certificate.NuclideSummaries);
            Assert.Empty(certificate.UncertaintyComponents);
            Assert.Empty(certificate.FunctionalChecks);

            byte[] bytes = _service.GenerateBytes(certificate);

            AssertLooksLikePdf(bytes);
        }

        [Fact]
        public void GenerateBytes_NullQrPayload_FoldsQrBlock_ProducesValidPdf()
        {
            var certificate = BuildBaseCertificate();
            certificate.QrPayload = null;

            byte[] bytes = _service.GenerateBytes(certificate);

            AssertLooksLikePdf(bytes);
        }

        [Fact]
        public void GenerateBytes_AmendedCertificate_ProducesValidPdf()
        {
            var certificate = BuildBaseCertificate();
            certificate.AmendedAt = DateTime.UtcNow;

            byte[] bytes = _service.GenerateBytes(certificate);

            AssertLooksLikePdf(bytes);
        }

        [Fact]
        public void GenerateBytes_ArabicTextInNotesAndMethodology_ProducesValidPdf()
        {
            var certificate = BuildBaseCertificate();
            certificate.Notes = "ملاحظات باللغة العربية للتأكد من تسجيل الخط";
            certificate.MethodologyEnabled = true;
            certificate.MethodologyText = "نص منهجية المعايرة باللغة العربية";

            byte[] bytes = _service.GenerateBytes(certificate);

            AssertLooksLikePdf(bytes);
        }

        [Fact]
        public void GenerateBytes_AllElevenResultColumnsPopulated_ProducesValidPdf()
        {
            var certificate = BuildBaseCertificate();

            for (int i = 1; i <= 3; i++)
            {
                certificate.CalibrationResults.Add(new CertificateCalibrationResult
                {
                    SortOrder = i,
                    SourceId = $"SRC-{i:00}",
                    Radionuclide = "Cs-137",
                    Scale = "x1.0",
                    ReferenceDoseLevel = "10.0 mSv",
                    ReferenceValue = "10.00 mSv",
                    MeasuredReading = "10.15 mSv",
                    CorrectionFactor = "0.985",
                    RelativeError = "+1.5 %",
                    AbsoluteRelativeError = "3.92 %",
                    Unit = "mSv",
                    Remarks = "Within acceptable limits"
                });
            }

            certificate.UncertaintyEnabled = true;
            certificate.CombinedUncertainty = "0.05";
            certificate.ExpandedUncertainty = "0.10";
            certificate.CoverageFactor = "2";

            for (int i = 1; i <= 5; i++)
            {
                certificate.UncertaintyComponents.Add(new CertificateUncertaintyComponent
                {
                    SortOrder = i,
                    ComponentName = $"Component {i}",
                    EvaluationType = i % 2 == 0 ? "A" : "B",
                    Distribution = "Normal",
                    StandardUncertainty = "0.01",
                    ContributionPercent = "10"
                });
            }

            for (int i = 1; i <= 5; i++)
            {
                certificate.FunctionalChecks.Add(new CertificateFunctionalCheck
                {
                    SortOrder = i,
                    CheckName = $"Functional Check {i}",
                    Requirement = "> 20%",
                    Result = "Pass",
                    Remarks = "No issues observed during the functional check"
                });
            }

            byte[] bytes = _service.GenerateBytes(certificate);

            AssertLooksLikePdf(bytes);
        }

        [Fact]
        public void GenerateBytes_NullCertificate_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _service.GenerateBytes(null!));
        }

        [Fact]
        public void EnsureInitialized_CalledTwice_DoesNotThrow()
        {
            CertificatePdfEnvironment.EnsureInitialized();
            CertificatePdfEnvironment.EnsureInitialized();
        }
    }
}
