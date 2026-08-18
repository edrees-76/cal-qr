using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using CAL_QR.Data;
using CAL_QR.Enums;
using CAL_QR.Models;

namespace CAL_QR.Services.Documents
{
    /// <summary>
    /// قالب شهادة المعايرة. صنف نقيّ: يستقبل Certificate و CertificateDocumentAssets
    /// فقط، ولا يقرأ ملفاً ولا قاعدة بيانات ولا يلمس WPF.
    /// </summary>
    public sealed class CertificateDocument : IDocument
    {
        private const string NavyColor = "#1A3A6B";
        private const string GoldColor = "#C9A227";
        private const string HeaderBgColor = "#F0F4F8";

        private const float HeaderLogoSize = 62f;
        private const float HeaderLine12FontSize = 10f;
        private const float HeaderLine3FontSize = 12f;
        private const float HeaderLine45FontSize = 9f;

        // الجسم إنجليزي بالكامل بخط Arial. العربية محصورة في ست كتل ثابتة
        // فقط (أربعة أسطر ترويسة + بيان المطابقة العربي + سطرا التذييل)،
        // وتلك وحدها تُبدَّل إلى ArabicFont. اسم عائلة الخط العربي هنا هو
        // الاسم الصريح المسجَّل به الملفان الثابتان عبر RegisterFontWithCustomName
        // في CertificatePdfEnvironment — لا الاسم الداخلي الفعلي لأي من الملفين
        // (والاسمان الداخليان مختلفان أصلاً بين Regular وExtraBold).
        private const string LatinFont = "Arial";
        private const string ArabicFont = CertificatePdfEnvironment.ArabicFontFamily;

        private readonly record struct Field(string Label, string? Value, bool FullWidth = false);

        private readonly Certificate _certificate;
        private readonly CertificateDocumentAssets _assets;
        private readonly bool _isStatusReport;

        public CertificateDocument(Certificate certificate, CertificateDocumentAssets assets)
        {
            _certificate = certificate ?? throw new ArgumentNullException(nameof(certificate));
            _assets = assets ?? throw new ArgumentNullException(nameof(assets));
            _isStatusReport = _certificate.DocumentType == CertificateDocumentType.CalibrationStatusReport;
        }

        // IDocument يتطلب ثلاثة أعضاء لا اثنين كما في المسوَّدة المقترحة:
        // GetMetadata و GetSettings و Compose. تحقّقتُ من ذلك بالانعكاس على
        // QuestPDF.dll 2024.3.10 قبل الكتابة (توقيع Document نفسها في QuestPDF.Fluent
        // ينفّذ الأعضاء الثلاثة بنفس القيم الافتراضية أدناه).
        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

        public DocumentSettings GetSettings() => DocumentSettings.Default;

        public void Compose(IDocumentContainer container)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.2f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontFamily(LatinFont).FontSize(9));

                page.Header().Element(ComposeHeader);
                page.Content().Element(ComposeContent);
                page.Footer().Element(ComposeFooter);
            });
        }

        private void ComposeHeader(IContainer container)
        {
            container.Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.ConstantItem(HeaderLogoSize).Element(e =>
                    {
                        if (_assets.EstablishmentLogoPng != null) e.Image(_assets.EstablishmentLogoPng);
                    });

                    row.RelativeItem().Column(c =>
                    {
                        c.Item().AlignCenter().Text("دولة ليبيا").FontFamily(ArabicFont).DirectionFromRightToLeft().FontSize(HeaderLine12FontSize);
                        c.Item().AlignCenter().Text("مؤسسة الطاقة الذرية").FontFamily(ArabicFont).DirectionFromRightToLeft().FontSize(HeaderLine12FontSize);
                        c.Item().AlignCenter().Text("مركز البحوث النووية - تاجوراء").FontFamily(ArabicFont).DirectionFromRightToLeft().Bold().FontSize(HeaderLine3FontSize).FontColor(NavyColor);
                        c.Item().AlignCenter().Text("Secondary Standard Dosimetry Laboratory (SSDL)").FontFamily(LatinFont).FontSize(HeaderLine45FontSize);
                        c.Item().AlignCenter().Text("وحدة المعايرة").FontFamily(ArabicFont).DirectionFromRightToLeft().FontSize(HeaderLine45FontSize);
                    });

                    row.ConstantItem(HeaderLogoSize).Element(e =>
                    {
                        if (_assets.CenterLogoPng != null) e.Image(_assets.CenterLogoPng);
                    });
                });

                column.Item().PaddingTop(6).BorderBottom(1).BorderColor(NavyColor);

                if (!string.IsNullOrWhiteSpace(_certificate.ReferenceNo))
                    column.Item().PaddingTop(3).AlignLeft()
                        .Text($"Ref: {_certificate.ReferenceNo}")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        }

        private void ComposeContent(IContainer container)
        {
            container.PaddingVertical(10).Column(column =>
            {
                column.Spacing(8);

                ComposeTitleBlock(column);
                ComposeClientInstrumentSection(column);
                ComposeEnvironmentalSection(column);
                ComposeTechnicalSection(column);
                ComposeMethodologySection(column);
                ComposeResultsSection(column);
                ComposeUncertaintySection(column);
                ComposeFunctionalChecksSection(column);
                ComposeRemarksSection(column);
                ComposeCalibrationStatusSection(column);
                ComposeAdditionalInfoSection(column);
                ComposeComplianceBox(column);
                ComposeApprovalBlock(column);
                ComposeFinalBlock(column);
            });
        }

        private void ComposeTitleBlock(ColumnDescriptor column)
        {
            column.Item().AlignCenter()
                .Text(_isStatusReport ? "CALIBRATION STATUS REPORT" : "CALIBRATION CERTIFICATE")
                .Bold().FontSize(14).FontColor(NavyColor);

            if (!string.IsNullOrWhiteSpace(_certificate.CertificateTemplateType))
                column.Item().AlignCenter()
                    .Text(_isStatusReport
                        ? $"FOR {_certificate.CertificateTemplateType}"
                        : $"CERTIFICATE FOR {_certificate.CertificateTemplateType}")
                    .FontSize(10);

            column.Item().AlignCenter().Text($"CERTIFICATE NO. {_certificate.CertificateNumber}").Bold().FontSize(10);
        }

        private void ComposeClientInstrumentSection(ColumnDescriptor column)
        {
            var def = DeviceTypeCatalog.Resolve(_certificate.CertificateTemplateType);

            var readoutLabel       = def?.ReadoutUnitLabel;
            var readoutSerialLabel = def?.ReadoutUnitSerialLabel;
            var primaryLabel       = def?.PrimaryInstrumentLabel       ?? "DEVICE MODEL";
            var primarySerialLabel = def?.PrimaryInstrumentSerialLabel ?? "DEVICE SERIAL NUMBER";
            var sectionTitle       = def?.ClientSectionTitle           ?? "CLIENT & INSTRUMENT SPECIFICATIONS";
            var showStandardBox    = def?.ClientBoxShowsStandardTraceabilityStatus ?? false;

            var fields = new List<Field>
            {
                new Field("CLIENT NAME", _certificate.ClientName, FullWidth: true),
                new Field("CLIENT ADDRESS", _certificate.ClientAddress, FullWidth: true),
            };

            if (!string.IsNullOrWhiteSpace(readoutLabel))
                fields.Add(new Field(readoutLabel, _certificate.SurveyMeterModel));
            if (!string.IsNullOrWhiteSpace(readoutSerialLabel))
                fields.Add(new Field(readoutSerialLabel, _certificate.SurveyMeterSerialNumber));

            fields.Add(new Field(primaryLabel, _certificate.DeviceModel));
            fields.Add(new Field(primarySerialLabel, _certificate.DeviceSerialNumber));
            fields.Add(new Field("MANUFACTURER", _certificate.DeviceManufacturer));

            fields.Add(new Field(
                _isStatusReport ? "FUNCTIONAL INSPECTION DATE" : "CALIBRATION DATE",
                _certificate.CalibrationDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)));
            fields.Add(new Field(
                _isStatusReport ? "RECALIBRATION AFTER REPAIR" : "DUE DATE",
                _isStatusReport
                    ? CertificateTexts.StatusReportRecalibrationValue
                    : _certificate.DueDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)));

            if (showStandardBox)
            {
                fields.Add(new Field("CALIBRATION STANDARD", _certificate.CalibrationStandard));
                fields.Add(new Field(
                    _isStatusReport ? "STATUS / VERDICT" : "COMPLIANCE VERDICT",
                    _certificate.ComplianceVerdict));
            }

            if (!HasAnyValue(fields)) return;

            SectionTitle(column, sectionTitle);
            RenderFieldTable(column, fields);
        }

        private void ComposeEnvironmentalSection(ColumnDescriptor column)
        {
            var fields = new List<Field>
            {
                new Field("TEMPERATURE", _certificate.Temperature),
                new Field("RELATIVE HUMIDITY", _certificate.RelativeHumidity),
                new Field("ATMOSPHERIC PRESSURE", _certificate.AtmosphericPressure),
            };

            if (!HasAnyValue(fields)) return;

            SectionTitle(column, "ENVIRONMENTAL CONDITIONS");
            RenderFieldTable(column, fields);
        }

        private void ComposeTechnicalSection(ColumnDescriptor column)
        {
            var fields = new List<Field>
            {
                new Field("COUNTING TIME", _certificate.CountingTime),
                new Field("COUNTING UNIT", _certificate.CountingUnit),
                new Field("CALIBRATION MODE", _certificate.CalibrationMode),
            };

            if (!HasAnyValue(fields)) return;

            SectionTitle(column, "TECHNICAL INFORMATION");
            RenderFieldTable(column, fields);
        }

        private void ComposeMethodologySection(ColumnDescriptor column)
        {
            if (!_certificate.MethodologyEnabled) return;

            var fields = new List<Field>
            {
                new Field("RADIATION SOURCE", _certificate.RadiationSource),
                new Field("REFERENCE GEOMETRY", _certificate.ReferenceGeometry),
                new Field("METHODOLOGY", _certificate.MethodologyText, FullWidth: true),
                new Field("TRACEABILITY REFERENCE", _certificate.TraceabilityReference),
            };

            if (!HasAnyValue(fields)) return;

            SectionTitle(column, "CALIBRATION METHODOLOGY");
            RenderFieldTable(column, fields);
        }

        private void ComposeResultsSection(ColumnDescriptor column)
        {
            if (_isStatusReport)
            {
                SectionTitle(column, "CALIBRATION RESULTS");
                column.Item().Text(CertificateTexts.StatusReportNoResultsTitle).Bold().FontSize(9).FontColor(NavyColor);
                column.Item().Text(CertificateTexts.StatusReportNoResultsBody).FontSize(8);
                return;
            }

            var rows = (_certificate.CalibrationResults ?? new List<CertificateCalibrationResult>())
                .OrderBy(r => r.SortOrder).ThenBy(r => r.Id).ToList();

            if (rows.Count == 0) return;

            var resultColumns = CertificateResultColumns.Select(rows);
            if (resultColumns.Count == 0) return;

            SectionTitle(column, "CALIBRATION RESULTS");

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columnsDef =>
                {
                    foreach (var _ in resultColumns) columnsDef.RelativeColumn();
                });

                table.Header(header =>
                {
                    foreach (var col in resultColumns)
                    {
                        header.Cell().Background(NavyColor).Padding(4).AlignCenter()
                            .Text(CertificateResultColumns.GetHeader(col)).Bold().FontSize(7.5f).FontColor(Colors.White);
                    }
                });

                foreach (var row in rows)
                {
                    foreach (var col in resultColumns)
                    {
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(3).AlignCenter()
                            .Text(CertificateResultColumns.GetValue(col, row)).FontSize(7.5f);
                    }
                }
            });

            var summaries = (_certificate.NuclideSummaries ?? new List<CertificateNuclideSummary>())
                .OrderBy(s => s.SortOrder).ThenBy(s => s.Id);

            foreach (var summary in summaries)
            {
                if (string.IsNullOrWhiteSpace(summary.AverageCorrectionFactor)) continue;
                column.Item().Text($"Average Correction Factor (CFavg) — {summary.Radionuclide} = {summary.AverageCorrectionFactor}").Bold().FontSize(8);
            }

            if (!string.IsNullOrWhiteSpace(_certificate.CorrectedReadingFormula))
                column.Item().Text(_certificate.CorrectedReadingFormula!).FontSize(8);
        }

        private void ComposeUncertaintySection(ColumnDescriptor column)
        {
            if (!_certificate.UncertaintyEnabled) return;

            var components = (_certificate.UncertaintyComponents ?? new List<CertificateUncertaintyComponent>())
                .OrderBy(c => c.SortOrder).ThenBy(c => c.Id).ToList();

            bool hasSummary = !string.IsNullOrWhiteSpace(_certificate.CombinedUncertainty)
                || !string.IsNullOrWhiteSpace(_certificate.ExpandedUncertainty);

            if (components.Count == 0 && !hasSummary) return;

            SectionTitle(column, "UNCERTAINTY BUDGET");

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2f);
                    columns.RelativeColumn(1f);
                    columns.RelativeColumn(1f);
                    columns.RelativeColumn(1f);
                    columns.RelativeColumn(1f);
                });

                table.Header(header =>
                {
                    void AddHeaderCell(string text) =>
                        header.Cell().Background(NavyColor).Padding(4).AlignCenter().Text(text).Bold().FontSize(7.5f).FontColor(Colors.White);

                    AddHeaderCell("Component");
                    AddHeaderCell("Evaluation Type");
                    AddHeaderCell("Distribution");
                    AddHeaderCell("Standard Uncertainty");
                    AddHeaderCell("Contribution %");
                });

                foreach (var comp in components)
                {
                    void AddCell(string? text) =>
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(3).AlignCenter().Text(text ?? string.Empty).FontSize(7.5f);

                    AddCell(comp.ComponentName);
                    AddCell(comp.EvaluationType);
                    AddCell(comp.Distribution);
                    AddCell(comp.StandardUncertainty);
                    AddCell(comp.ContributionPercent);
                }

                string coverageFactor = string.IsNullOrWhiteSpace(_certificate.CoverageFactor) ? "2" : _certificate.CoverageFactor!;

                table.Cell().ColumnSpan(4).Background(HeaderBgColor).Padding(4)
                    .Text("Combined Standard Uncertainty (uc)").Bold().FontSize(8).FontColor(NavyColor);
                table.Cell().Background(HeaderBgColor).Padding(4).AlignCenter()
                    .Text(_certificate.CombinedUncertainty ?? string.Empty).Bold().FontSize(8);

                table.Cell().ColumnSpan(4).Background(HeaderBgColor).Padding(4)
                    .Text($"Expanded Uncertainty (U) (k = {coverageFactor})").Bold().FontSize(8).FontColor(NavyColor);
                table.Cell().Background(HeaderBgColor).Padding(4).AlignCenter()
                    .Text(_certificate.ExpandedUncertainty ?? string.Empty).Bold().FontSize(8);
            });
        }

        private void ComposeFunctionalChecksSection(ColumnDescriptor column)
        {
            var checks = (_certificate.FunctionalChecks ?? new List<CertificateFunctionalCheck>())
                .OrderBy(c => c.SortOrder).ThenBy(c => c.Id).ToList();

            if (checks.Count == 0) return;

            bool hasRemarks = checks.Any(c => !string.IsNullOrWhiteSpace(c.Remarks));

            SectionTitle(column, "FUNCTIONAL CHECKS");

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(25);
                    columns.RelativeColumn(2f);
                    columns.RelativeColumn(2f);
                    columns.RelativeColumn(1f);
                    if (hasRemarks) columns.RelativeColumn(2f);
                });

                table.Header(header =>
                {
                    void AddHeaderCell(string text) =>
                        header.Cell().Background(NavyColor).Padding(4).AlignCenter().Text(text).Bold().FontSize(8).FontColor(Colors.White);

                    AddHeaderCell("#");
                    AddHeaderCell("Check Name");
                    AddHeaderCell("Requirement");
                    AddHeaderCell("Result");
                    if (hasRemarks) AddHeaderCell("Remarks");
                });

                int idx = 1;
                foreach (var check in checks)
                {
                    void AddCell(string text) =>
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(3).AlignCenter().Text(text).FontSize(8);

                    AddCell(idx.ToString(CultureInfo.InvariantCulture));
                    AddCell(check.CheckName);
                    AddCell(check.Requirement ?? string.Empty);

                    // خليّة النتيجة — حمراء غامقة للفاشل/غير المنفَّذ
                    var resultText = check.Result ?? string.Empty;
                    if (IsFailedResult(resultText))
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(3).AlignCenter()
                            .Text(resultText).Bold().FontSize(8).FontColor("#C62828");
                    else
                        AddCell(resultText);

                    if (hasRemarks) AddCell(check.Remarks ?? string.Empty);
                    idx++;
                }
            });
        }

        private void ComposeRemarksSection(ColumnDescriptor column)
        {
            if (!_isStatusReport || string.IsNullOrWhiteSpace(_certificate.Remarks)) return;

            SectionTitle(column, "REMARKS");
            column.Item().Text(_certificate.Remarks!).FontSize(8);
        }

        private void ComposeCalibrationStatusSection(ColumnDescriptor column)
        {
            if (!_isStatusReport) return;

            bool hasVerdict = !string.IsNullOrWhiteSpace(_certificate.ComplianceVerdict);
            bool hasReason = !string.IsNullOrWhiteSpace(_certificate.StatusReason);
            if (!hasVerdict && !hasReason) return;

            SectionTitle(column, "CALIBRATION STATUS");
            if (hasVerdict)
                column.Item().Text(_certificate.ComplianceVerdict!).Bold().FontSize(9).FontColor(NavyColor);
            if (hasReason)
                column.Item().Text($"Reason: {_certificate.StatusReason}").FontSize(8);
        }

        private void ComposeAdditionalInfoSection(ColumnDescriptor column)
        {
            bool hasInfo = !string.IsNullOrWhiteSpace(_certificate.AdditionalInformation);
            bool hasNotes = !string.IsNullOrWhiteSpace(_certificate.Notes);

            if (!hasInfo && !hasNotes) return;

            SectionTitle(column, "ADDITIONAL INFORMATION & NOTES");

            if (hasInfo) column.Item().Text(_certificate.AdditionalInformation!).FontSize(8);
            if (hasNotes) column.Item().Text(_certificate.Notes!).FontSize(8);
        }

        private void ComposeComplianceBox(ColumnDescriptor column)
        {
            if (_isStatusReport)
            {
                column.Item().ShowEntire().Border(0.5f).BorderColor(GoldColor).Padding(6).Column(box =>
                {
                    box.Item().Text(CertificateTexts.StatusReportNotPerformedTitle).Bold().FontSize(9).FontColor("#C62828");
                    box.Item().PaddingTop(3).Text(CertificateTexts.StatusReportNotPerformedLine1).Bold().FontSize(8);
                    box.Item().PaddingTop(2).Text(CertificateTexts.StatusReportNotPerformedLine2).Bold().FontSize(8);
                    box.Item().PaddingTop(2).Text(CertificateTexts.StatusReportNotPerformedLine3).Bold().FontSize(8);
                });
                return;
            }

            column.Item().ShowEntire().Border(0.5f).BorderColor(GoldColor).Padding(6).Column(box =>
            {
                box.Item().Text(CertificateTexts.ComplianceStatementEn).FontSize(8);
                // ContentFromRightToLeft على الحاوية يرتّب عناصرها لا اتجاه الفقرة
                // نفسها — لهذا انقلبت الجملة رغم استعماله سابقاً. الإصلاح على
                // مستوى TextStyle عبر DirectionFromRightToLeft على النص ذاته، مع عزل
                // المقاطع اللاتينية (IAEA وISO/IEC 17025:2017) بمحارف LRI/PDI كي لا
                // ينكسر ترتيب bidi للجملة العربية حولها.
                box.Item().PaddingTop(4).AlignRight()
                    .Text(IsolateLatinRuns(CertificateTexts.ComplianceStatementAr, '⁦', '⁩'))
                    .FontFamily(ArabicFont).DirectionFromRightToLeft().FontSize(8);
            });
        }

        private static string IsolateLatinRuns(string text, char open, char close) =>
            System.Text.RegularExpressions.Regex.Replace(
                text,
                @"[A-Za-z0-9/:().\-]+(?:\s[A-Za-z0-9/:().\-]+)*",
                m => System.Text.RegularExpressions.Regex.IsMatch(m.Value, "[A-Za-z0-9]")
                     ? open + m.Value + close : m.Value);

        private void ComposeApprovalBlock(ColumnDescriptor column)
        {
            column.Item().ShowEntire().PaddingTop(10).Row(row =>
            {
                void AddBox(string title, string? name, string? position, DateTime? date)
                {
                    row.RelativeItem().Padding(4).Column(col =>
                    {
                        col.Item().Text(title).Bold().FontSize(8).FontColor(NavyColor);
                        if (!string.IsNullOrWhiteSpace(name)) col.Item().Text(name!).FontSize(8);
                        if (!string.IsNullOrWhiteSpace(position)) col.Item().Text(position!).FontSize(7).FontColor(Colors.Grey.Darken1);
                        if (date.HasValue) col.Item().Text($"Date: {date.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}").FontSize(7);
                        col.Item().PaddingTop(15).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2);
                    });
                }

                AddBox("Calibrated By", _certificate.CalibratedByName, _certificate.CalibratedByTitle, _certificate.CalibratedByDate);
                AddBox("Reviewed By", _certificate.ReviewedByName, _certificate.ReviewedByTitle, _certificate.ReviewedByDate);
                AddBox("Approved By", _certificate.ApprovedByName, _certificate.ApprovedByTitle, _certificate.ApprovedByDate);
                AddBox("Authorized By", _certificate.AuthorizedByName, _certificate.AuthorizedByTitle, _certificate.AuthorizedByDate);

                // خانة ختم رسمي فارغة إلى اليسار
                row.RelativeItem(1.5f).Padding(4).Column(stamp =>
                {
                    stamp.Item().Text("Official Stamp").Bold().FontSize(8).FontColor(NavyColor);
                    stamp.Item().PaddingTop(2).Border(0.5f).BorderColor(Colors.Grey.Lighten2).Height(60);
                });
            });
        }

        private void ComposeFinalBlock(ColumnDescriptor column)
        {
            column.Item().ShowEntire().PaddingTop(10).Row(row =>
            {
                row.ConstantItem(60).Column(qr =>
                {
                    if (_assets.QrPng != null)
                    {
                        qr.Item().Width(50).Image(_assets.QrPng);
                        qr.Item().AlignCenter().Text(_certificate.CertificateNumber).FontSize(6);
                        qr.Item().AlignCenter().Text("Verify Certificate").FontSize(6);
                        qr.Item().AlignCenter().Text("تحقق من الشهادة").FontFamily(ArabicFont).DirectionFromRightToLeft().FontSize(6);
                    }
                });

                row.RelativeItem().Column(info =>
                {
                    info.Item().Text($"Verify Code: {_certificate.VerifyCode}").FontSize(7);

                    if (_certificate.AmendedAt.HasValue)
                    {
                        info.Item().Text($"⚠ This certificate was amended on {_certificate.AmendedAt.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}")
                            .Bold().FontSize(8).FontColor("#C62828");
                        info.Item().Text("عُدّلت هذه الشهادة")
                            .FontFamily(ArabicFont).DirectionFromRightToLeft().Bold().FontSize(8).FontColor("#C62828");
                    }
                });
            });
        }

        private void ComposeFooter(IContainer container)
        {
            container.AlignRight().Text(x =>
            {
                x.Span("Page ").FontSize(7);
                x.CurrentPageNumber().FontSize(7);
                x.Span(" of ").FontSize(7);
                x.TotalPages().FontSize(7);
            });
        }

        private static void SectionTitle(ColumnDescriptor column, string text) =>
            column.Item().PaddingTop(4).Text(text).Bold().FontSize(11).FontColor(NavyColor);

        private static bool HasAnyValue(IEnumerable<Field> fields) =>
            fields.Any(f => !string.IsNullOrWhiteSpace(f.Value));

        private static bool IsFailedResult(string? result)
        {
            if (string.IsNullOrWhiteSpace(result)) return false;
            var normalized = System.Text.RegularExpressions.Regex.Replace(result.Trim(), @"\s+", " ");
            return normalized.Equals("Failed", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("Not Performed", StringComparison.OrdinalIgnoreCase);
        }

        private static void RenderFieldTable(ColumnDescriptor column, IEnumerable<Field> fields)
        {
            var visible = fields.Where(f => !string.IsNullOrWhiteSpace(f.Value)).ToList();
            if (visible.Count == 0) return;

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                int col = 0;

                foreach (var f in visible)
                {
                    if (f.FullWidth)
                    {
                        while (col != 0)
                        {
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2);
                            col = (col + 1) % 3;
                        }

                        table.Cell().ColumnSpan(3)
                            .Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Column(c =>
                            {
                                c.Item().Text(f.Label).Bold().FontSize(7.5f).FontColor(NavyColor);
                                c.Item().Text(f.Value!).FontSize(8);
                            });

                        continue;
                    }

                    table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Column(c =>
                    {
                        c.Item().Text(f.Label).Bold().FontSize(7.5f).FontColor(NavyColor);
                        c.Item().Text(f.Value!).FontSize(8);
                    });

                    col = (col + 1) % 3;
                }
            });
        }
    }
}
