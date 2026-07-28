using System;
using System.Linq;
using Xunit;
using CAL_QR.Models;
using CAL_QR.Services.Payloads;

namespace CAL_QR.Tests
{
    /// <summary>
    /// الاختبار الذهبي لنص التوقيع SIG1 — تثبيت البايتات.
    ///
    /// ⚠ هذا الاختبار هو ما يجعل تجميد SIG1 حقيقة لا تعليقاً.
    /// فشله لا يعني «اختبار يحتاج تحديثاً»؛ يعني أن تعديلاً على البانِي أبطل
    /// التحقق من كل شهادة موقّعة بـ SIG1. العلاج هو التراجع عن التعديل،
    /// أو إضافة SignaturePayloadBuilderV2 — لا تعديل السلسلة المتوقعة أدناه.
    ///
    /// السلسلة تُبنى بـ string.Join("\n", ...) عمداً: لو كُتبت كنص حرفي
    /// متعدد الأسطر لحملت CRLF من نظام الملفات على ويندوز وأخفت خطأ
    /// فاصل الأسطر بدل أن تكشفه.
    /// </summary>
    public class SignaturePayloadGoldenTests
    {
        /// <summary>
        /// شهادة مرجعية ثابتة. القيم مختارة لتغطية: قيمة خالية (~)، منطقي 0 و1،
        /// مجموعة فارغة (FC:0)، مجموعة بصف واحد، مجموعة بصفين، خانة موقّع غير مملوءة،
        /// وقيمة سالبة في RelativeError مع نظيرتها المطلقة.
        /// </summary>
        private static Certificate BuildReferenceCertificate()
        {
            var certificate = new Certificate
            {
                Id = 1,
                CalibrationRecordId = 7,
                CertificateNumber = "TNRC-SSDL-2026-0018",
                CertificateTemplateType = "Pancake Probe",

                ClientName = "مستشفى بنغازي الطبي",
                ClientAddress = null,
                DeviceModel = "Ludlum 44-9",
                DeviceSerialNumber = "PR-105",
                DeviceManufacturer = null,
                SurveyMeterModel = "Ludlum Model 3",
                SurveyMeterSerialNumber = "SM-77",

                MeasurementType = null,
                Distance = null,
                CountingTime = "60 Sec",
                CountingUnit = "kCPM",

                Temperature = "22.5",
                RelativeHumidity = "45",
                AtmosphericPressure = "1013",

                CalibrationDate = new DateTime(2026, 1, 15),
                IssueDate = new DateTime(2026, 1, 20),
                DueDate = new DateTime(2027, 1, 15),

                AverageCorrectionFactor = "1.038",
                CorrectedReadingFormula = "Measured × CFavg",
                ComplianceVerdict = "Complies",
                CalibrationStandard = "SSDL-CP-01",

                MethodologyEnabled = false,
                RadiationSource = null,
                ReferenceGeometry = null,
                MethodologyText = null,
                TraceabilityReference = null,

                UncertaintyEnabled = true,
                CombinedUncertainty = "5.40",
                ExpandedUncertainty = "10.80",
                CoverageFactor = "2",

                // خارج التوقيع عمداً — وجودها هنا يثبت أنها لا تُسرّب إليه
                ReferenceNo = "OUT-2026-441",
                Notes = "ملاحظة إدارية قابلة للتعديل",
                AdditionalInformation = "معلومات إضافية",
                AmendedAt = new DateTime(2026, 3, 1),
                FirstPrintedAt = new DateTime(2026, 1, 21),

                CalibratedByName = "م. أحمد الشريف",
                CalibratedByTitle = "SSDL - TNRC",
                CalibratedByDate = new DateTime(2026, 1, 20),
                ReviewedByName = "م. سالم الورفلي",
                ReviewedByTitle = "Calibration Unit Head — SSDL - TNRC",
                ReviewedByDate = new DateTime(2026, 1, 20),
                ApprovedByName = "د. خالد المصراتي",
                ApprovedByTitle = "Head of Department — SSDL - TNRC",
                ApprovedByDate = new DateTime(2026, 1, 21),
                AuthorizedByName = null,
                AuthorizedByTitle = null,
                AuthorizedByDate = null
            };

            certificate.CalibrationResults.Add(new CertificateCalibrationResult
            {
                Id = 11,
                SortOrder = 1,
                SourceId = "S-01",
                Radionuclide = "Cs-137",
                Scale = "×1",
                ReferenceDoseLevel = null,
                ReferenceValue = "5.40",
                MeasuredReading = "5.20",
                CorrectionFactor = "1.038",
                RelativeError = "-3.70",
                AbsoluteRelativeError = "3.70",
                Unit = "kCPM",
                Remarks = "ضمن الحدود"
            });
            certificate.CalibrationResults.Add(new CertificateCalibrationResult
            {
                Id = 12,
                SortOrder = 2,
                SourceId = "S-02",
                Radionuclide = "Co-60",
                Scale = "×1",
                ReferenceDoseLevel = null,
                ReferenceValue = "8.00",
                MeasuredReading = "8.10",
                CorrectionFactor = "0.988",
                RelativeError = "1.25",
                AbsoluteRelativeError = "1.25",
                Unit = "kCPM",
                Remarks = null
            });

            certificate.UncertaintyComponents.Add(new CertificateUncertaintyComponent
            {
                Id = 21,
                SortOrder = 1,
                ComponentName = "Repeatability",
                EvaluationType = "A",
                StandardUncertainty = "1.20",
                ContributionPercent = "4.90",
                Distribution = "Normal"
            });

            // لا فحوص وظيفية — لإثبات أن سطر العدّ يُكتب FC:0 ولا يُحذف
            return certificate;
        }

        /// <summary>السلسلة المجمّدة. لا تُعدَّل — انظر تحذير الصنف أعلاه.</summary>
        private static readonly string GoldenPayload = string.Join("\n", new[]
        {
            "CALQR-SIG-V1",
            "CN:TNRC-SSDL-2026-0018",
            "TT:Pancake Probe",
            "CL:مستشفى بنغازي الطبي",
            "CA:~",
            "DM:Ludlum 44-9",
            "DS:PR-105",
            "DF:~",
            "SM:Ludlum Model 3",
            "SS:SM-77",
            "MT:~",
            "DI:~",
            "CT:60 Sec",
            "CU:kCPM",
            "TP:22.5",
            "RH:45",
            "AP:1013",
            "CD:20260115",
            "ID:20260120",
            "DD:20270115",
            "AF:1.038",
            "RF:Measured × CFavg",
            "VD:Complies",
            "ST:SSDL-CP-01",
            "ME:0",
            "MS:~",
            "MG:~",
            "MX:~",
            "MR:~",
            "UE:1",
            "UC:5.40",
            "UX:10.80",
            "UK:2",
            "RC:2",
            "R1:S-01;Cs-137;×1;~;5.40;5.20;1.038;-3.70;3.70;kCPM;ضمن الحدود",
            "R2:S-02;Co-60;×1;~;8.00;8.10;0.988;1.25;1.25;kCPM;~",
            "UCC:1",
            "U1:Repeatability;A;1.20;4.90;Normal",
            "FC:0",
            "G1:م. أحمد الشريف;SSDL - TNRC;20260120",
            "G2:م. سالم الورفلي;Calibration Unit Head — SSDL - TNRC;20260120",
            "G3:د. خالد المصراتي;Head of Department — SSDL - TNRC;20260121",
            "G4:~;~;~"
        });

        [Fact]
        public void SignaturePayloadV1_MatchesFrozenBytes()
        {
            var builder = new SignaturePayloadBuilderV1();

            string actual = builder.Build(BuildReferenceCertificate());

            Assert.Equal(GoldenPayload, actual);
        }

        [Fact]
        public void SignaturePayloadV1_UsesLineFeedOnly_NeverCarriageReturn()
        {
            var builder = new SignaturePayloadBuilderV1();

            string actual = builder.Build(BuildReferenceCertificate());

            Assert.DoesNotContain("\r", actual);
            Assert.False(actual.EndsWith("\n", StringComparison.Ordinal),
                "نص التوقيع لا ينتهي بسطر جديد — إضافته لاحقاً تُبطل كل التواقيع السابقة.");
        }

        [Fact]
        public void SignaturePayloadV1_DeclaresVersionOnFirstLine()
        {
            var builder = new SignaturePayloadBuilderV1();

            string actual = builder.Build(BuildReferenceCertificate());

            Assert.StartsWith("CALQR-SIG-V1\n", actual, StringComparison.Ordinal);
            Assert.Equal("SIG1", builder.Version);
        }

        [Fact]
        public void SignaturePayloadV1_IgnoresFieldsOutsideTheSignature()
        {
            var builder = new SignaturePayloadBuilderV1();
            string baseline = builder.Build(BuildReferenceCertificate());

            var mutated = BuildReferenceCertificate();
            mutated.ReferenceNo = "OUT-2026-999";
            mutated.Notes = "ملاحظة أخرى تماماً";
            mutated.AdditionalInformation = "نص مختلف";
            mutated.QrPayload = "أي شيء";
            mutated.VerifyCode = "0000000000000000";
            mutated.AmendedAt = new DateTime(2030, 12, 31);
            mutated.FirstPrintedAt = new DateTime(2030, 12, 31);
            mutated.IssuedAt = new DateTime(2030, 12, 31);
            mutated.UpdatedAt = new DateTime(2030, 12, 31);
            mutated.IsDeleted = true;

            Assert.Equal(baseline, builder.Build(mutated));
        }

        [Fact]
        public void SignaturePayloadV1_EmptyGroups_EmitZeroCountLine_NotAbsentLine()
        {
            var builder = new SignaturePayloadBuilderV1();
            var certificate = BuildReferenceCertificate();
            certificate.CalibrationResults.Clear();
            certificate.UncertaintyComponents.Clear();

            string actual = builder.Build(certificate);

            Assert.Contains("\nRC:0\n", actual);
            Assert.Contains("\nUCC:0\n", actual);
            Assert.Contains("\nFC:0\n", actual);
            Assert.DoesNotContain("\nR1:", actual);
            Assert.DoesNotContain("\nU1:", actual);
            Assert.DoesNotContain("\nF1:", actual);
        }

        [Theory]
        [InlineData("CertificateTemplateType")]
        [InlineData("RelativeError")]
        [InlineData("ResultRemarks")]
        [InlineData("CheckRemarks")]
        public void SignaturePayloadV1_CoversFieldsAddedBeforeFreeze(string field)
        {
            var builder = new SignaturePayloadBuilderV1();
            string baseline = builder.Build(BuildReferenceCertificate());

            var mutated = BuildReferenceCertificate();
            switch (field)
            {
                case "CertificateTemplateType":
                    mutated.CertificateTemplateType = "Dose Rate Meter";
                    break;
                case "RelativeError":
                    mutated.CalibrationResults.First().RelativeError = "-9.99";
                    break;
                case "ResultRemarks":
                    mutated.CalibrationResults.First().Remarks = "خارج الحدود";
                    break;
                case "CheckRemarks":
                    mutated.FunctionalChecks.Add(new CertificateFunctionalCheck
                    {
                        Id = 31,
                        SortOrder = 1,
                        CheckName = "Battery check",
                        Requirement = "> 90%",
                        Result = "Pass",
                        Remarks = "ملاحظة داخل جدول مطبوع"
                    });
                    break;
            }

            Assert.NotEqual(baseline, builder.Build(mutated));
        }
    }
}
