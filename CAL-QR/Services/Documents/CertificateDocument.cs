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

                // Ref يسارًا، Financial Receipt No يمينًا، بينهما فاصل مطّاط يدفعهما
                // للحافّتين. AutoItem يتقلّص حول نصّه، وRelativeItem الأوسط الفارغ
                // يمتصّ العرض المتبقّي فيفترق الطرفان. يظهران دائمًا (استثناء من إخفاء
                // الفارغ، بقرار Edrees): القيمة إن وُجدت، وإلا خطّ سفليّ للكتابة اليدويّة.
                column.Item().PaddingTop(14).Row(row =>
                {
                    row.AutoItem()
                        .Text($"Ref: {(string.IsNullOrWhiteSpace(_certificate.ReferenceNo) ? "________" : _certificate.ReferenceNo)}")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);

                    row.RelativeItem();

                    row.AutoItem()
                        .Text($"Financial Receipt No: {(string.IsNullOrWhiteSpace(_certificate.FinancialReceiptNo) ? "________" : _certificate.FinancialReceiptNo)}")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                });
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
                ComposeImportantNotesSection(column);
                ComposeAdditionalInformationSection(column);
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
            var primaryLabel       = def?.PrimaryInstrumentLabel       ?? "Device Model";
            var primarySerialLabel = def?.PrimaryInstrumentSerialLabel ?? "Device Serial Number";
            var sectionTitle       = def?.ClientSectionTitle           ?? "CLIENT & INSTRUMENT SPECIFICATIONS";
            var showStandardBox    = def?.ClientBoxShowsStandardTraceabilityStatus ?? false;

            var fields = new List<Field>
            {
                new Field("Client Name", _certificate.ClientName, FullWidth: true),
                new Field("Client Address", _certificate.ClientAddress, FullWidth: true),
            };

            if (!string.IsNullOrWhiteSpace(readoutLabel))
                fields.Add(new Field(readoutLabel, _certificate.SurveyMeterModel));
            if (!string.IsNullOrWhiteSpace(readoutSerialLabel))
                fields.Add(new Field(readoutSerialLabel, _certificate.SurveyMeterSerialNumber));

            fields.Add(new Field(primaryLabel, _certificate.DeviceModel));
            fields.Add(new Field(primarySerialLabel, _certificate.DeviceSerialNumber));
            fields.Add(new Field("Manufacturer", _certificate.DeviceManufacturer));

            fields.Add(new Field(
                _isStatusReport ? "Functional Inspection Date" : "Calibration Date",
                _certificate.CalibrationDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)));
            fields.Add(new Field(
                _isStatusReport ? "Recalibration After Repair" : "Calibration Due Date",
                _isStatusReport
                    ? CertificateTexts.StatusReportRecalibrationValue
                    : _certificate.DueDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)));

            if (showStandardBox)
            {
                fields.Add(new Field("CALIBRATION STANDARD", _certificate.CalibrationStandard));
                fields.Add(new Field("Traceability Reference", _certificate.TraceabilityReference, FullWidth: true));
                fields.Add(new Field("STATUS / VERDICT", _certificate.ComplianceVerdict));
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

            // خليّة Radiation Traceability للعائلة الكاملة فقط (شهادة معايرة، لا تقرير
            // حالة، لا نوع مبسّط) — قرار رضا ب٣. تُركَّب من بيانات جدول النتائج.
            var def = DeviceTypeCatalog.Resolve(_certificate.CertificateTemplateType);
            if (!_isStatusReport && def?.SimplifiedResults != true)
            {
                var radiationTraceability = BuildRadiationTraceability();
                if (!string.IsNullOrWhiteSpace(radiationTraceability))
                    fields.Add(new Field("Radiation Traceability", radiationTraceability, FullWidth: true));
            }

            if (!HasAnyValue(fields)) return;

            SectionTitle(column, "TECHNICAL INFORMATION");
            RenderFieldTable(column, fields);
        }

        // يُركّب سطر Radiation Traceability من جدول النتائج: أرقام المصادر المميَّزة،
        // ثمّ النويدات المميَّزة، ثمّ جهة التتبّع الثابتة SSDL (نصّ قالب رضا). يعيد ""
        // إن لم يوجد مصدر ولا نويدة، فلا تظهر الخليّة على شهادة بلا بيانات مصدر.
        private string BuildRadiationTraceability()
        {
            var results = _certificate.CalibrationResults ?? new List<CertificateCalibrationResult>();

            var sourceIds = results
                .Select(r => r.SourceId)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var nuclides = results
                .Select(r => r.Radionuclide)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (sourceIds.Count == 0 && nuclides.Count == 0) return string.Empty;

            var parts = new List<string>();
            if (sourceIds.Count > 0) parts.Add($"Reference Source ID: {string.Join(", ", sourceIds)}");
            if (nuclides.Count > 0) parts.Add($"Radionuclide: {string.Join(", ", nuclides)}");
            parts.Add("Traceability: SSDL");

            return string.Join("    ", parts);
        }

        private void ComposeMethodologySection(ColumnDescriptor column)
        {
            // قسم المنهجيّة للأنواع المبسّطة فقط (قرار رضا ب٤). العائلة الكاملة وتقرير
            // الحالة: لا قسم منفصل، والتتبّع يُعرض داخل صندوق العميل (ب٩).
            var def = DeviceTypeCatalog.Resolve(_certificate.CertificateTemplateType);
            if (def?.SimplifiedResults != true) return;

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

            var def = DeviceTypeCatalog.Resolve(_certificate.CertificateTemplateType);

            var rows = (_certificate.CalibrationResults ?? new List<CertificateCalibrationResult>())
                .OrderBy(r => r.SortOrder).ThenBy(r => r.Id).ToList();

            if (def?.SimplifiedResults == true)
            {
                ComposeSimplifiedResults(column, rows);
                return;
            }

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

            if (!string.IsNullOrWhiteSpace(_certificate.CorrectedReadingFormula))
                column.Item().Text(_certificate.CorrectedReadingFormula!).FontSize(8);

            ComposeResultsSummaryStrip(column);
        }

        // شريط ملخّص بعد جدول النتائج (العائلة الكاملة) مطابق لقالب رضا: ثلاثة أعمدة
        // رأس + قيمة — CFavg (من ملخّصات النويدات) · uc · U. القيم uc/U مكرّرة عمداً
        // هنا وفي جدول الميزانية، كما في B401. يعيد بلا رسم إن لا بيانات.
        private void ComposeResultsSummaryStrip(ColumnDescriptor column)
        {
            var cfavg = string.Join(", ",
                (_certificate.NuclideSummaries ?? new List<CertificateNuclideSummary>())
                    .OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
                    .Where(s => !string.IsNullOrWhiteSpace(s.AverageCorrectionFactor))
                    .Select(s => s.AverageCorrectionFactor!.Trim()));

            var uc = _certificate.CombinedUncertainty;
            var u = _certificate.ExpandedUncertainty;

            if (string.IsNullOrWhiteSpace(cfavg) && string.IsNullOrWhiteSpace(uc) && string.IsNullOrWhiteSpace(u))
                return;

            string coverageFactor = string.IsNullOrWhiteSpace(_certificate.CoverageFactor) ? "2" : _certificate.CoverageFactor!;

            column.Item().PaddingTop(2).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                void HeaderCell(string text) =>
                    table.Cell().Background(NavyColor).Padding(4).AlignCenter().Text(text).Bold().FontSize(7.5f).FontColor(Colors.White);
                void ValueCell(string? text) =>
                    table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignCenter().Text(text ?? string.Empty).Bold().FontSize(8);

                HeaderCell("Average Correction Factor (CFavg)");
                HeaderCell("Combined Standard Uncertainty (uc)");
                HeaderCell($"Expanded Uncertainty (U) (k = {coverageFactor}) (95% confidence level)");

                ValueCell(cfavg);
                ValueCell(uc);
                ValueCell(u);
            });
        }

        // العرض المبسّط (Gamma · Teletector · PED · Dose Rate): كتلة رأسيّة مطابقة
        // لقوالب رضا — CF ثمّ AE% لكل صفّ نتيجة، ثمّ الحكم النهائيّ، ثمّ الصيغة.
        // الحكم يظهر هنا لهذه الأنواع لأنّ صندوق العميل لا يعرضه (showStandardBox=false).
        private void ComposeSimplifiedResults(ColumnDescriptor column, List<CertificateCalibrationResult> rows)
        {
            bool hasResults = rows.Any(r =>
                !string.IsNullOrWhiteSpace(r.CorrectionFactor) ||
                !string.IsNullOrWhiteSpace(r.AbsoluteRelativeError));
            bool hasVerdict = !string.IsNullOrWhiteSpace(_certificate.ComplianceVerdict);
            if (!hasResults && !hasVerdict) return;

            SectionTitle(column, "CALIBRATION RESULTS");

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2f);
                    columns.RelativeColumn(3f);
                });

                void Row(string label, string? value)
                {
                    if (string.IsNullOrWhiteSpace(value)) return;
                    table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4)
                        .Text(label).Bold().FontSize(8).FontColor(NavyColor);
                    table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4)
                        .Text(value!).Bold().FontSize(8);
                }

                foreach (var r in rows)
                {
                    Row("Calibration Factor (CF)", r.CorrectionFactor);
                    Row("Absolute Relative Error (AE)(%)", r.AbsoluteRelativeError);
                }

                Row("Final Compliance Verdict", _certificate.ComplianceVerdict);
            });

            if (!string.IsNullOrWhiteSpace(_certificate.CorrectedReadingFormula))
                column.Item().PaddingTop(2).Text(_certificate.CorrectedReadingFormula!).FontSize(8);
        }

        private void ComposeUncertaintySection(ColumnDescriptor column)
        {
            if (!_certificate.UncertaintyEnabled) return;

            var components = (_certificate.UncertaintyComponents ?? new List<CertificateUncertaintyComponent>())
                .OrderBy(c => c.SortOrder).ThenBy(c => c.Id).ToList();

            bool hasSummary = !string.IsNullOrWhiteSpace(_certificate.CombinedUncertainty)
                || !string.IsNullOrWhiteSpace(_certificate.ExpandedUncertainty);

            if (components.Count == 0 && !hasSummary) return;

            SectionTitle(column, "Uncertainty Budget (per GUM — Type A / Type B Evaluation)");

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(25);   // No.
                    columns.RelativeColumn(2.5f); // Component
                    columns.RelativeColumn(1f);   // Evaluation Type
                    columns.RelativeColumn(1f);   // Standard Uncertainty (%)
                    columns.RelativeColumn(1f);   // Contribution (%)
                    columns.RelativeColumn(1f);   // Distribution
                });

                table.Header(header =>
                {
                    void AddHeaderCell(string text) =>
                        header.Cell().Background(NavyColor).Padding(4).AlignCenter().Text(text).Bold().FontSize(7.5f).FontColor(Colors.White);

                    AddHeaderCell("No.");
                    AddHeaderCell("Component");
                    AddHeaderCell("Evaluation Type");
                    AddHeaderCell("Standard Uncertainty (%)");
                    AddHeaderCell("Contribution (%)");
                    AddHeaderCell("Distribution");
                });

                int idx = 1;
                foreach (var comp in components)
                {
                    void AddCell(string? text) =>
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(3).AlignCenter().Text(text ?? string.Empty).FontSize(7.5f);

                    AddCell(idx.ToString(CultureInfo.InvariantCulture));
                    AddCell(comp.ComponentName);
                    AddCell(comp.EvaluationType);
                    AddCell(comp.StandardUncertainty);
                    AddCell(comp.ContributionPercent);
                    AddCell(comp.Distribution);
                    idx++;
                }

                string coverageFactor = string.IsNullOrWhiteSpace(_certificate.CoverageFactor) ? "2" : _certificate.CoverageFactor!;

                // صفّ uc: التسمية تمتدّ على (No · Component · Evaluation)، القيمة تحت
                // Standard Uncertainty، إجمالي المساهمة 100 (%)، وخانة Distribution فارغة.
                table.Cell().ColumnSpan(3).Background(HeaderBgColor).Padding(4)
                    .Text("Combined Standard Uncertainty (uc)").Bold().FontSize(8).FontColor(NavyColor);
                table.Cell().Background(HeaderBgColor).Padding(4).AlignCenter()
                    .Text(_certificate.CombinedUncertainty ?? string.Empty).Bold().FontSize(8);
                table.Cell().Background(HeaderBgColor).Padding(4).AlignCenter()
                    .Text("100 (%)").Bold().FontSize(8);
                table.Cell().Background(HeaderBgColor);

                // صفّ U: التسمية تمتدّ على (No · Component · Evaluation)، القيمة تحت
                // Standard Uncertainty، وخانتا Contribution و Distribution فارغتان.
                table.Cell().ColumnSpan(3).Background(HeaderBgColor).Padding(4)
                    .Text($"Expanded Uncertainty (U) (k = {coverageFactor})").Bold().FontSize(8).FontColor(NavyColor);
                table.Cell().Background(HeaderBgColor).Padding(4).AlignCenter()
                    .Text(_certificate.ExpandedUncertainty ?? string.Empty).Bold().FontSize(8);
                table.Cell().Background(HeaderBgColor);
                table.Cell().Background(HeaderBgColor);
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

        // منفصلان بقرار رضا (ب١١): «IMPORTANT NOTES & CONDITIONS» (الملاحظات) ثمّ
        // «ADDITIONAL INFORMATION» (المعلومات) — كلٌّ بحارس وجود تحت عنوانه. الموضع
        // النهائيّ لكلٍّ حسب العائلة يُحدَّد في ترتيب ComposeContent (PORD).
        private void ComposeImportantNotesSection(ColumnDescriptor column)
        {
            if (string.IsNullOrWhiteSpace(_certificate.Notes)) return;

            SectionTitle(column, "IMPORTANT NOTES & CONDITIONS");
            column.Item().Text(_certificate.Notes!).FontSize(8);
        }

        private void ComposeAdditionalInformationSection(ColumnDescriptor column)
        {
            if (string.IsNullOrWhiteSpace(_certificate.AdditionalInformation)) return;

            SectionTitle(column, "ADDITIONAL INFORMATION");
            column.Item().Text(_certificate.AdditionalInformation!).FontSize(8);
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
            container.Column(footer =>
            {
                // سطر جهة الاتصال المؤسّسي — ثابت في كل شهادة، فوق فاصل رفيع.
                footer.Item().PaddingTop(3).BorderTop(0.5f).BorderColor(NavyColor);
                footer.Item().PaddingTop(2).AlignCenter()
                    .Text(CertificateTexts.FooterContact).FontSize(6.5f).FontColor(Colors.Grey.Darken1);

                footer.Item().AlignRight().Text(x =>
                {
                    x.Span("Page ").FontSize(7);
                    x.CurrentPageNumber().FontSize(7);
                    x.Span(" of ").FontSize(7);
                    x.TotalPages().FontSize(7);
                });
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
