using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using CAL_QR.Models;
using CAL_QR.Data;

namespace CAL_QR.Services
{
    public class ExportService : IExportService
    {
        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;

        public ExportService(IDbContextFactory<CalQrDbContext> contextFactory)
        {
            _contextFactory = contextFactory;

            try
            {
                QuestPDF.Settings.License = LicenseType.Community;
            }
            catch
            {
                // Already registered
            }
        }

        private int GetAlertThresholdDays()
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                var setting = context.AppSettings.AsNoTracking().FirstOrDefault(s => s.Key == "AlertDaysThreshold");
                if (setting != null && int.TryParse(setting.Value, out int days))
                {
                    return days;
                }
            }
            catch
            {
                // Fallback
            }
            return 30;
        }

        private string GetStatusText(CalibrationRecord record, int alertDays)
        {
            DateTime today = DateTime.Today;
            if (record.ExpiryDate < today)
            {
                return "منتهية الصلاحية";
            }
            if (record.ExpiryDate <= today.AddDays(alertDays))
            {
                return "قريبة الانتهاء";
            }
            return "سارية";
        }

        public Task ExportToExcelAsync(IEnumerable<CalibrationRecord> records, string reportType, string filePath)
        {
            return Task.Run(() =>
            {
                int alertDays = GetAlertThresholdDays();

                using (var workbook = new XLWorkbook())
                {
                    var ws = workbook.Worksheets.Add("سجلات المعايرة");
                    ws.RightToLeft = true;

                    // Fixed Header
                    ws.Cell(1, 1).Value = "مركز البحوث النووية";
                    ws.Cell(2, 1).Value = "إدارة الوقاية من الإشعاع";
                    ws.Cell(3, 1).Value = "قسم قياس وتقدير الجرعات الشخصية والمعايرة | وحدة المعايرة";
                    ws.Cell(4, 1).Value = $"نوع التقرير: {(reportType == "Detailed" ? "مفصل" : "مختصر")} | تاريخ التوليد: {DateTime.Today:yyyy-MM-dd}";

                    for (int i = 1; i <= 4; i++)
                    {
                        ws.Row(i).Style.Font.FontName = "Cairo";
                        ws.Row(i).Style.Font.Bold = true;
                    }
                    ws.Cell(1, 1).Style.Font.FontSize = 14;

                    int startRow = 6;
                    bool isDetailed = reportType == "Detailed";
                    string[] headers = isDetailed 
                        ? new[] { "رقم الشهادة", "الجهة المالكة", "نوع الجهاز", "الموديل", "الرقم التسلسلي", "تاريخ المعايرة", "تاريخ الانتهاء", "النتيجة", "التوقيع الرقمي", "المهندس المعايِر", "التفاصيل" }
                        : new[] { "رقم الشهادة", "الجهة المالكة", "الموديل", "الرقم التسلسلي", "تاريخ الانتهاء", "النتيجة", "الحالة" };

                    for (int col = 0; col < headers.Length; col++)
                    {
                        var cell = ws.Cell(startRow, col + 1);
                        cell.Value = headers[col];
                        cell.Style.Font.FontName = "Cairo";
                        cell.Style.Font.Bold = true;
                        cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1A3A6B");
                        cell.Style.Font.FontColor = XLColor.White;
                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    }

                    int row = startRow + 1;
                    foreach (var record in records)
                    {
                        ws.Row(row).Style.Font.FontName = "Cairo";
                        if (isDetailed)
                        {
                            ws.Cell(row, 1).Value = record.CertificateNumber;
                            ws.Cell(row, 2).Value = record.Device?.Owner?.Name ?? "";
                            ws.Cell(row, 3).Value = record.Device?.DeviceType?.Name ?? "";
                            ws.Cell(row, 4).Value = record.Device?.Model ?? "";
                            ws.Cell(row, 5).Value = record.Device?.SerialNumber ?? "";
                            ws.Cell(row, 6).Value = record.CalibrationDate.ToString("yyyy-MM-dd");
                            ws.Cell(row, 7).Value = record.ExpiryDate.ToString("yyyy-MM-dd");
                            ws.Cell(row, 8).Value = record.Result;
                            ws.Cell(row, 9).Value = record.HmacSignature;
                            ws.Cell(row, 10).Value = record.EngineerName;
                            ws.Cell(row, 11).Value = record.CalibrationDescription;
                        }
                        else
                        {
                            ws.Cell(row, 1).Value = record.CertificateNumber;
                            ws.Cell(row, 2).Value = record.Device?.Owner?.Name ?? "";
                            ws.Cell(row, 3).Value = record.Device?.Model ?? "";
                            ws.Cell(row, 4).Value = record.Device?.SerialNumber ?? "";
                            ws.Cell(row, 5).Value = record.ExpiryDate.ToString("yyyy-MM-dd");
                            ws.Cell(row, 6).Value = record.Result;
                            ws.Cell(row, 7).Value = GetStatusText(record, alertDays);
                        }

                        for (int col = 1; col <= headers.Length; col++)
                        {
                            ws.Cell(row, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        }

                        row++;
                    }

                    ws.Columns().AdjustToContents();
                    workbook.SaveAs(filePath);
                }
            });
        }

        public Task ExportToPdfAsync(IEnumerable<CalibrationRecord> records, string reportType, string filePath)
        {
            return Task.Run(() =>
            {
                int alertDays = GetAlertThresholdDays();
                bool isDetailed = reportType == "Detailed";

                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(1.2f, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        
                        page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(9));

                        // Header
                        page.Header()
                            .ContentFromRightToLeft()
                            .BorderBottom(1)
                            .BorderColor(Colors.Grey.Lighten2)
                            .PaddingBottom(8)
                            .Row(row =>
                            {
                                row.RelativeItem().Column(column =>
                                {
                                    column.Item().Text("مركز البحوث النووية").FontFamily("Cairo").Bold().FontSize(12).FontColor("#1A3A6B");
                                    column.Item().Text("إدارة الوقاية من الإشعاع").FontFamily("Cairo").FontSize(10);
                                    column.Item().Text("قسم قياس وتقدير الجرعات الشخصية والمعايرة | وحدة المعايرة").FontFamily("Cairo").FontSize(9);
                                    column.Item().Text($"تاريخ التقرير: {DateTime.Today:yyyy-MM-dd}").FontFamily("Cairo").FontSize(8).FontColor(Colors.Grey.Darken1);
                                });

                                row.ConstantItem(120).AlignLeft().AlignMiddle().Column(col =>
                                {
                                    col.Item().Background("#1A3A6B").Padding(4).AlignCenter().Text("نظام CAL-QR").FontFamily("Cairo").Bold().FontSize(9).FontColor(Colors.White);
                                    col.Item().PaddingTop(2).AlignCenter().Text($"تقرير: {(isDetailed ? "مفصل" : "مختصر")}").FontFamily("Cairo").FontSize(9).Bold().FontColor("#C9A227");
                                });
                            });

                        // Content Table
                        page.Content()
                            .ContentFromRightToLeft()
                            .PaddingVertical(10)
                            .Table(table =>
                            {
                                if (isDetailed)
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.ConstantColumn(50);
                                        columns.RelativeColumn(1.2f);
                                        columns.RelativeColumn(0.8f);
                                        columns.RelativeColumn(0.8f);
                                        columns.ConstantColumn(60);
                                        columns.ConstantColumn(60);
                                        columns.ConstantColumn(40);
                                        columns.ConstantColumn(40);
                                        columns.RelativeColumn(0.8f);
                                    });
                                }
                                else
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.ConstantColumn(60);
                                        columns.RelativeColumn(1.5f);
                                        columns.RelativeColumn(1f);
                                        columns.RelativeColumn(1f);
                                        columns.ConstantColumn(70);
                                        columns.ConstantColumn(50);
                                        columns.RelativeColumn(1f);
                                    });
                                }

                                table.Header(header =>
                                {
                                    void AddHeaderCell(string text)
                                    {
                                        header.Cell()
                                            .Background("#1A3A6B")
                                            .Padding(5)
                                            .AlignCenter()
                                            .AlignMiddle()
                                            .Text(text)
                                            .FontFamily("Cairo")
                                            .Bold()
                                            .FontSize(9)
                                            .FontColor(Colors.White);
                                    }

                                    if (isDetailed)
                                    {
                                        AddHeaderCell("الشهادة");
                                        AddHeaderCell("الجهة المالكة");
                                        AddHeaderCell("الموديل");
                                        AddHeaderCell("الرقم التسلسلي");
                                        AddHeaderCell("تاريخ المعايرة");
                                        AddHeaderCell("تاريخ الانتهاء");
                                        AddHeaderCell("النتيجة");
                                        AddHeaderCell("التوقيع");
                                        AddHeaderCell("المهندس");
                                    }
                                    else
                                    {
                                        AddHeaderCell("رقم الشهادة");
                                        AddHeaderCell("الجهة المالكة");
                                        AddHeaderCell("الموديل");
                                        AddHeaderCell("الرقم التسلسلي");
                                        AddHeaderCell("تاريخ الانتهاء");
                                        AddHeaderCell("النتيجة");
                                        AddHeaderCell("الحالة");
                                    }
                                });

                                foreach (var record in records)
                                {
                                    void AddCell(string text, bool isBold = false, string colorHex = "#000000")
                                    {
                                        table.Cell()
                                            .BorderBottom(0.5f)
                                            .BorderColor(Colors.Grey.Lighten3)
                                            .Padding(4)
                                            .AlignCenter()
                                            .AlignMiddle()
                                            .Text(text)
                                            .FontFamily("Cairo")
                                            .FontSize(8)
                                            .FontColor(colorHex);
                                    }

                                    if (isDetailed)
                                    {
                                        AddCell(record.CertificateNumber ?? "");
                                        AddCell(record.Device?.Owner?.Name ?? "");
                                        AddCell(record.Device?.Model ?? "");
                                        AddCell(record.Device?.SerialNumber ?? "");
                                        AddCell(record.CalibrationDate.ToString("yyyy-MM-dd"));
                                        AddCell(record.ExpiryDate.ToString("yyyy-MM-dd"));
                                        AddCell(record.Result ?? "");
                                        AddCell(record.HmacSignature ?? "", isBold: true, colorHex: "#C62828");
                                        AddCell(record.EngineerName ?? "");
                                    }
                                    else
                                    {
                                        AddCell(record.CertificateNumber ?? "");
                                        AddCell(record.Device?.Owner?.Name ?? "");
                                        AddCell(record.Device?.Model ?? "");
                                        AddCell(record.Device?.SerialNumber ?? "");
                                        AddCell(record.ExpiryDate.ToString("yyyy-MM-dd"));
                                        AddCell(record.Result ?? "");
                                        
                                        string status = GetStatusText(record, alertDays);
                                        string color = status == "منتهية الصلاحية" ? "#C62828" : (status == "قريبة الانتهاء" ? "#F9A825" : "#2E7D32");
                                        AddCell(status, isBold: true, colorHex: color);
                                    }
                                }
                            });

                        // Footer
                        page.Footer()
                            .ContentFromRightToLeft()
                            .PaddingTop(8)
                            .AlignCenter()
                            .Text(x =>
                            {
                                x.Span("صفحة ").FontFamily("Cairo").FontSize(9);
                                x.CurrentPageNumber().FontFamily("Cairo").FontSize(9);
                                x.Span(" من ").FontFamily("Cairo").FontSize(9);
                                x.TotalPages().FontFamily("Cairo").FontSize(9);
                            });
                    });
                }).GeneratePdf(filePath);
            });
        }
    }
}
