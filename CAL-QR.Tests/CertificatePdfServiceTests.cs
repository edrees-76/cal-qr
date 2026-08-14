using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using CAL_QR.Enums;
using CAL_QR.Models;
using CAL_QR.Services;
using CAL_QR.Services.Documents;
using UglyToad.PdfPig;

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

        private static Certificate BuildStatusReportCertificate()
        {
            var certificate = BuildBaseCertificate();
            certificate.DocumentType = CertificateDocumentType.CalibrationStatusReport;
            certificate.ComplianceVerdict = "NOT PERFORMED";
            certificate.StatusReason = "Device failed initial functional inspection; calibration could not proceed.";
            certificate.Remarks = "Contamination detected on the probe window. Returned to client for repair.";

            certificate.FunctionalChecks.Add(new CertificateFunctionalCheck
            {
                SortOrder = 1, CheckName = "Battery Check", Requirement = "> 20%", Result = "Pass"
            });
            certificate.FunctionalChecks.Add(new CertificateFunctionalCheck
            {
                SortOrder = 2, CheckName = "Response Test", Requirement = "Within ±10%", Result = "Failed",
                Remarks = "No response to reference source"
            });
            certificate.FunctionalChecks.Add(new CertificateFunctionalCheck
            {
                SortOrder = 3, CheckName = "Contamination Check", Requirement = "Clean", Result = "Not Performed"
            });

            return certificate;
        }

        private static Certificate BuildSimplifiedStabilityCertificate()
        {
            var certificate = BuildBaseCertificate();
            certificate.UncertaintyEnabled = false;
            certificate.MethodologyEnabled = false;
            certificate.ComplianceVerdict = "Passed";
            return certificate;
        }

        private static Certificate BuildEmptyOptionalFieldsCertificate()
        {
            // كلّ الحقول الاختياريّة فارغة/فاذّة — نتأكّد أنّ القالب لا ينهار على الحدّ الأدنى
            var certificate = BuildBaseCertificate();
            certificate.UncertaintyEnabled = false;
            certificate.MethodologyEnabled = false;
            return certificate;
        }

        private static Certificate BuildLongArabicCertificate()
        {
            var certificate = BuildBaseCertificate();
            certificate.ClientName = "شركة الواحة للنفط — قسم الوقاية من الإشعاع والسلامة النوويّة";
            certificate.ClientAddress = "طرابلس، ليبيا — المنطقة الصناعيّة، مبنى رقم ٤٧، الطابق الثالث";
            certificate.MethodologyEnabled = true;
            certificate.MethodologyText = string.Concat(System.Linq.Enumerable.Repeat("نصّ منهجيّة مطوّل للتأكّد من عدم انهيار التخطيط عند إدخال عربيّ كثيف. ", 12));
            certificate.ComplianceVerdict = "Passed";
            return certificate;
        }

        public static System.Collections.Generic.IEnumerable<object[]> StabilityFamilies()
        {
            yield return new object[] { "FullFamily", (Func<Certificate>)(() =>
            {
                var c = BuildBaseCertificate();
                c.UncertaintyEnabled = true;
                c.MethodologyEnabled = true;
                c.CombinedUncertainty = "0.05";
                c.ExpandedUncertainty = "0.10";
                c.CoverageFactor = "2";
                c.ComplianceVerdict = "Passed";
                c.CalibrationResults.Add(new CertificateCalibrationResult { SortOrder = 1, SourceId = "SRC-1", Radionuclide = "Cs-137", MeasuredReading = "10.1", CorrectionFactor = "1.02", Unit = "µSv/h" });
                c.NuclideSummaries.Add(new CertificateNuclideSummary { SortOrder = 1, Radionuclide = "Cs-137", AverageCorrectionFactor = "1.02" });
                c.UncertaintyComponents.Add(new CertificateUncertaintyComponent { SortOrder = 1, ComponentName = "Repeatability", EvaluationType = "A", Distribution = "Normal", StandardUncertainty = "0.01", ContributionPercent = "10" });
                c.FunctionalChecks.Add(new CertificateFunctionalCheck { SortOrder = 1, CheckName = "Battery Check", Requirement = "> 20%", Result = "Pass" });
                return c;
            }), 2 };
            yield return new object[] { "Simplified", (Func<Certificate>)BuildSimplifiedStabilityCertificate, 1 };
            yield return new object[] { "StatusReport", (Func<Certificate>)BuildStatusReportCertificate, 2 };
            yield return new object[] { "EmptyOptionalFields", (Func<Certificate>)BuildEmptyOptionalFieldsCertificate, 1 };
            yield return new object[] { "LongArabic", (Func<Certificate>)BuildLongArabicCertificate, 1 };
        }

        [Theory]
        [MemberData(nameof(StabilityFamilies))]
        public void GenerateBytes_StabilityAcrossFamilies_ProducesValidPdf(string familyName, Func<Certificate> build, int expectedPages)
        {
            var certificate = build();

            byte[] bytes = _service.GenerateBytes(certificate);

            AssertLooksLikePdf(bytes);
            Assert.True(bytes.Length > 1000, $"[{familyName}] الناتج أصغر من المتوقّع: {bytes.Length} بايت");

            using var ms = new System.IO.MemoryStream(bytes);
            using var doc = UglyToad.PdfPig.PdfDocument.Open(ms);
            Assert.True(doc.NumberOfPages == expectedPages,
                $"[{familyName}] متوقّع {expectedPages} صفحة، فعليّ {doc.NumberOfPages}");
        }

        private static Certificate BuildOversizedCertificate()
        {
            var certificate = BuildBaseCertificate();
            certificate.ComplianceVerdict = "Passed";
            certificate.UncertaintyEnabled = true;
            certificate.MethodologyEnabled = true;
            certificate.CombinedUncertainty = "0.05";
            certificate.ExpandedUncertainty = "0.10";
            certificate.CoverageFactor = "2";
            certificate.MethodologyText = string.Concat(System.Linq.Enumerable.Repeat("نصّ منهجيّة مطوّل جدًّا لإجبار التمدّد. ", 20));

            for (int i = 1; i <= 40; i++)
                certificate.CalibrationResults.Add(new CertificateCalibrationResult
                {
                    SortOrder = i, SourceId = $"SRC-{i:00}", Radionuclide = "Cs-137",
                    Scale = "x1.0", ReferenceValue = "10.00 mSv", MeasuredReading = "10.15 mSv",
                    CorrectionFactor = "0.985", Unit = "mSv", Remarks = "Within acceptable limits"
                });
            for (int i = 1; i <= 15; i++)
                certificate.UncertaintyComponents.Add(new CertificateUncertaintyComponent
                {
                    SortOrder = i, ComponentName = $"Component {i}", EvaluationType = "A",
                    Distribution = "Normal", StandardUncertainty = "0.01", ContributionPercent = "5"
                });
            for (int i = 1; i <= 15; i++)
                certificate.FunctionalChecks.Add(new CertificateFunctionalCheck
                {
                    SortOrder = i, CheckName = $"Functional Check {i}",
                    Requirement = "> 20%", Result = "Pass", Remarks = "No issues observed"
                });

            return certificate;
        }

        [Fact]
        public void GenerateBytes_OversizedCertificate_SpillsToMultiplePages_WithRepeatedHeaderFooter()
        {
            var certificate = BuildOversizedCertificate();

            byte[] bytes = _service.GenerateBytes(certificate);

            AssertLooksLikePdf(bytes);

            using var ms = new System.IO.MemoryStream(bytes);
            using var doc = UglyToad.PdfPig.PdfDocument.Open(ms);

            // 1) التمدّد المشروع مسموح: بيانات ضخمة تتجاوز صفحتين دون اقتصاص
            Assert.True(doc.NumberOfPages >= 3,
                $"متوقّع 3 صفحات فأكثر لبيانات ضخمة، فعليّ {doc.NumberOfPages}");

            // 2) الترويسة/التذييل يتكرّران: رقم الشهادة يظهر في نصّ الصفحة الأخيرة كما الأولى
            var pages = System.Linq.Enumerable.ToList(doc.GetPages());
            string firstPageText = pages[0].Text;
            string lastPageText = pages[pages.Count - 1].Text;
            string certNo = certificate.CertificateNumber;

            Assert.True(firstPageText.Contains(certNo),
                $"رقم الشهادة غير موجود في نصّ الصفحة الأولى. نصّ الصفحة:\n{firstPageText}");
            Assert.True(lastPageText.Contains(certNo),
                $"رقم الشهادة غير موجود في نصّ الصفحة الأخيرة (الترويسة لم تتكرّر؟). نصّ الصفحة:\n{lastPageText}");
        }
    }
}
