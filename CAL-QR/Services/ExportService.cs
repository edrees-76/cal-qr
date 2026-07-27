using System;
using System.Collections.Generic;
using System.IO;
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
        private byte[]? _logoBytes;

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

            LoadLogoBytes();
        }

        private void LoadLogoBytes()
        {
            try
            {
                var uri = new Uri("pack://application:,,,/Assets/Logo/cal-qr-3d-logo-new.png");
                var streamResourceInfo = System.Windows.Application.GetResourceStream(uri);
                if (streamResourceInfo != null)
                {
                    using var ms = new MemoryStream();
                    streamResourceInfo.Stream.CopyTo(ms);
                    _logoBytes = ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ExportService] Error loading logo resource: {ex.Message}");
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

                    bool isDetailed = reportType == "Detailed";

                    // Add logo if available
                    if (_logoBytes != null)
                    {
                        try
                        {
                            using (var ms = new MemoryStream(_logoBytes))
                            {
                                var picture = ws.Pictures.Add(ms);
                                picture.MoveTo(ws.Cell(1, isDetailed ? 10 : 7));
                                picture.Width = 60;
                                picture.Height = 60;
                            }
                        }
                        catch { }
                    }

                    int startRow = 6;
                    string[] headers = isDetailed 
                        ? new[] { "ت", "رقم الشهادة", "الجهة المالكة", "نوع الجهاز", "الموديل", "الرقم التسلسلي", "تاريخ المعايرة", "تاريخ الانتهاء", "النتيجة", "المهندس المعايِر", "التفاصيل" }
                        : new[] { "ت", "رقم الشهادة", "الجهة المالكة", "الموديل", "الرقم التسلسلي", "تاريخ الانتهاء", "النتيجة", "الحالة" };

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
                    int idx = 1;
                    foreach (var record in records)
                    {
                        ws.Row(row).Style.Font.FontName = "Cairo";
                        if (isDetailed)
                        {
                            ws.Cell(row, 1).Value = idx;
                            ws.Cell(row, 2).Value = record.CertificateNumber;
                            ws.Cell(row, 3).Value = record.Device?.Owner?.Name ?? "";
                            ws.Cell(row, 4).Value = record.Device?.DeviceType?.Name ?? "";
                            ws.Cell(row, 5).Value = record.Device?.Model ?? "";
                            ws.Cell(row, 6).Value = record.Device?.SerialNumber ?? "";
                            ws.Cell(row, 7).Value = record.CalibrationDate.ToString("yyyy-MM-dd");
                            ws.Cell(row, 8).Value = record.ExpiryDate.ToString("yyyy-MM-dd");
                            ws.Cell(row, 9).Value = record.Result;
                            ws.Cell(row, 10).Value = record.EngineerName;
                            ws.Cell(row, 11).Value = record.CalibrationDescription;
                        }
                        else
                        {
                            ws.Cell(row, 1).Value = idx;
                            ws.Cell(row, 2).Value = record.CertificateNumber;
                            ws.Cell(row, 3).Value = record.Device?.Owner?.Name ?? "";
                            ws.Cell(row, 4).Value = record.Device?.Model ?? "";
                            ws.Cell(row, 5).Value = record.Device?.SerialNumber ?? "";
                            ws.Cell(row, 6).Value = record.ExpiryDate.ToString("yyyy-MM-dd");
                            ws.Cell(row, 7).Value = record.Result;
                            ws.Cell(row, 8).Value = GetStatusText(record, alertDays);
                        }

                        for (int col = 1; col <= headers.Length; col++)
                        {
                            ws.Cell(row, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        }

                        idx++;
                        row++;
                    }

                    ws.Columns().AdjustToContents();

                    // Apply text wrapping to prevent visual overflow
                    ws.Column(3).Style.Alignment.WrapText = true; // الجهة المالكة
                    if (isDetailed)
                    {
                        ws.Column(11).Style.Alignment.WrapText = true; // التفاصيل
                    }

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

                                if (_logoBytes != null)
                                {
                                    row.ConstantItem(55).AlignLeft().AlignMiddle().Image(_logoBytes);
                                }
                                else
                                {
                                    row.ConstantItem(120).AlignLeft().AlignMiddle().Background("#1A3A6B").Padding(4).AlignCenter().Text("نظام CAL-QR").FontFamily("Cairo").Bold().FontSize(9).FontColor(Colors.White);
                                }

                                row.ConstantItem(120).AlignLeft().AlignMiddle().Column(col =>
                                {
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
                                        columns.ConstantColumn(25);   // ت
                                        columns.ConstantColumn(65);   // الشهادة
                                        columns.RelativeColumn(1.5f); // الجهة المالكة
                                        columns.RelativeColumn(1f);   // الموديل
                                        columns.RelativeColumn(1f);   // الرقم التسلسلي
                                        columns.ConstantColumn(65);   // تاريخ المعايرة
                                        columns.ConstantColumn(65);   // تاريخ الانتهاء
                                        columns.ConstantColumn(45);   // النتيجة
                                        columns.RelativeColumn(1f);   // المهندس
                                    });
                                }
                                else
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.ConstantColumn(25);   // ت
                                        columns.ConstantColumn(70);   // رقم الشهادة
                                        columns.RelativeColumn(1.5f); // الجهة المالكة
                                        columns.RelativeColumn(1f);   // الموديل
                                        columns.RelativeColumn(1f);   // الرقم التسلسلي
                                        columns.ConstantColumn(70);   // تاريخ الانتهاء
                                        columns.ConstantColumn(50);   // النتيجة
                                        columns.RelativeColumn(1f);   // الحالة
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
                                        AddHeaderCell("ت");
                                        AddHeaderCell("الشهادة");
                                        AddHeaderCell("الجهة المالكة");
                                        AddHeaderCell("الموديل");
                                        AddHeaderCell("الرقم التسلسلي");
                                        AddHeaderCell("تاريخ المعايرة");
                                        AddHeaderCell("تاريخ الانتهاء");
                                        AddHeaderCell("النتيجة");
                                        AddHeaderCell("المهندس");
                                    }
                                    else
                                    {
                                        AddHeaderCell("ت");
                                        AddHeaderCell("رقم الشهادة");
                                        AddHeaderCell("الجهة المالكة");
                                        AddHeaderCell("الموديل");
                                        AddHeaderCell("الرقم التسلسلي");
                                        AddHeaderCell("تاريخ الانتهاء");
                                        AddHeaderCell("النتيجة");
                                        AddHeaderCell("الحالة");
                                    }
                                });

                                int idx = 1;
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
                                        AddCell(idx.ToString());
                                        AddCell(record.CertificateNumber ?? "");
                                        AddCell(record.Device?.Owner?.Name ?? "");
                                        AddCell(record.Device?.Model ?? "");
                                        AddCell(record.Device?.SerialNumber ?? "");
                                        AddCell(record.CalibrationDate.ToString("yyyy-MM-dd"));
                                        AddCell(record.ExpiryDate.ToString("yyyy-MM-dd"));
                                        AddCell(record.Result ?? "");
                                        AddCell(record.EngineerName ?? "");
                                    }
                                    else
                                    {
                                        AddCell(idx.ToString());
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
                                    idx++;
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

        public Task ExportPerformanceReportToPdfAsync(PerformanceReportData data, string filePath)
        {
            return Task.Run(() =>
            {
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
                                    column.Item().Text($"الفترة: من {data.StartDate:yyyy-MM-dd} إلى {data.EndDate:yyyy-MM-dd}").FontFamily("Cairo").FontSize(8).FontColor(Colors.Grey.Darken1);
                                });

                                if (_logoBytes != null)
                                {
                                    row.ConstantItem(55).AlignLeft().AlignMiddle().Image(_logoBytes);
                                }
                                else
                                {
                                    row.ConstantItem(120).AlignLeft().AlignMiddle().Background("#1A3A6B").Padding(4).AlignCenter().Text("نظام CAL-QR").FontFamily("Cairo").Bold().FontSize(9).FontColor(Colors.White);
                                }

                                row.ConstantItem(150).AlignLeft().AlignMiddle().Column(col =>
                                {
                                    col.Item().AlignCenter().Text("تقرير أداء وحدة المعايرة").FontFamily("Cairo").FontSize(9).Bold().FontColor("#C9A227");
                                    col.Item().PaddingTop(2).AlignCenter().Text($"نوع التقرير: {(data.IsDetailed ? "مفصل" : "مختصر")}").FontFamily("Cairo").FontSize(8).FontColor(Colors.Grey.Darken1);
                                });
                            });

                        // Content
                        page.Content()
                            .ContentFromRightToLeft()
                            .PaddingVertical(10)
                            .Column(col =>
                            {
                                // Section 1: General Summary
                                col.Item().PaddingBottom(5).Text("1. الخلاصة العامة للفترة المحددة").FontFamily("Cairo").Bold().FontSize(11).FontColor("#1A3A6B");
                                col.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn();
                                        columns.RelativeColumn();
                                        columns.RelativeColumn();
                                        columns.RelativeColumn();
                                    });

                                    // Headers
                                    table.Cell().Background("#1A3A6B").Padding(5).AlignCenter().Text("إجمالي المعايرات").FontFamily("Cairo").Bold().FontSize(9).FontColor(Colors.White);
                                    table.Cell().Background("#1A3A6B").Padding(5).AlignCenter().Text("أجهزة جديدة").FontFamily("Cairo").Bold().FontSize(9).FontColor(Colors.White);
                                    table.Cell().Background("#1A3A6B").Padding(5).AlignCenter().Text("جهات مالكة جديدة").FontFamily("Cairo").Bold().FontSize(9).FontColor(Colors.White);
                                    table.Cell().Background("#1A3A6B").Padding(5).AlignCenter().Text("أنواع أجهزة جديدة").FontFamily("Cairo").Bold().FontSize(9).FontColor(Colors.White);

                                    // Values
                                    table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text(data.TotalRecords.ToString()).FontFamily("Cairo").FontSize(10).Bold();
                                    table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text(data.NewDevices.ToString()).FontFamily("Cairo").FontSize(10);
                                    table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text(data.NewOwners.ToString()).FontFamily("Cairo").FontSize(10);
                                    table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text(data.NewDeviceTypes.ToString()).FontFamily("Cairo").FontSize(10);
                                });

                                col.Item().PaddingVertical(10);

                                // Section 2: Results Distribution
                                col.Item().PaddingBottom(5).Text("2. توزيع نتائج المعايرة").FontFamily("Cairo").Bold().FontSize(11).FontColor("#1A3A6B");
                                col.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn();
                                        columns.RelativeColumn();
                                        columns.RelativeColumn();
                                    });

                                    table.Cell().Background("#1A3A6B").Padding(5).AlignCenter().Text("النتيجة: ناجح (Passed)").FontFamily("Cairo").Bold().FontSize(9).FontColor(Colors.White);
                                    table.Cell().Background("#1A3A6B").Padding(5).AlignCenter().Text("النتيجة: راسب (Failed)").FontFamily("Cairo").Bold().FontSize(9).FontColor(Colors.White);
                                    table.Cell().Background("#1A3A6B").Padding(5).AlignCenter().Text("النتيجة: مشروط (Conditional)").FontFamily("Cairo").Bold().FontSize(9).FontColor(Colors.White);

                                    table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text($"{data.PassedCount} ({data.PassedPercent:F1}%)").FontFamily("Cairo").FontSize(10).FontColor("#2E7D32").Bold();
                                    table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text($"{data.FailedCount} ({data.FailedPercent:F1}%)").FontFamily("Cairo").FontSize(10).FontColor("#C62828").Bold();
                                    table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text($"{data.ConditionalCount} ({data.ConditionalPercent:F1}%)").FontFamily("Cairo").FontSize(10).FontColor("#F9A825").Bold();
                                });

                                col.Item().PaddingVertical(10);

                                // Section 3: Top Categories
                                col.Item().PaddingBottom(5).Text("3. توزيع المعايرات حسب الفئات").FontFamily("Cairo").Bold().FontSize(11).FontColor("#1A3A6B");
                                
                                col.Item().Row(r =>
                                {
                                    // Owner distribution
                                    r.RelativeItem().PaddingRight(5).Column(c =>
                                    {
                                        c.Item().PaddingBottom(2).Text("حسب الجهة المالكة").FontFamily("Cairo").Bold().FontSize(9).FontColor(Colors.Grey.Darken2);
                                        c.Item().Table(t =>
                                        {
                                            t.ColumnsDefinition(cols => { cols.RelativeColumn(); cols.ConstantColumn(50); });
                                            t.Cell().Background("#34495E").Padding(3).AlignCenter().Text("الجهة").FontFamily("Cairo").Bold().FontSize(8).FontColor(Colors.White);
                                            t.Cell().Background("#34495E").Padding(3).AlignCenter().Text("العدد").FontFamily("Cairo").Bold().FontSize(8).FontColor(Colors.White);

                                            foreach (var item in data.ByOwner)
                                            {
                                                t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text(item.Name).FontFamily("Cairo").FontSize(8);
                                                t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignCenter().Text(item.Count.ToString()).FontFamily("Cairo").FontSize(8);
                                            }
                                        });
                                    });

                                    // Device Type distribution
                                    r.RelativeItem().PaddingHorizontal(5).Column(c =>
                                    {
                                        c.Item().PaddingBottom(2).Text("حسب نوع الجهاز").FontFamily("Cairo").Bold().FontSize(9).FontColor(Colors.Grey.Darken2);
                                        c.Item().Table(t =>
                                        {
                                            t.ColumnsDefinition(cols => { cols.RelativeColumn(); cols.ConstantColumn(50); });
                                            t.Cell().Background("#34495E").Padding(3).AlignCenter().Text("النوع").FontFamily("Cairo").Bold().FontSize(8).FontColor(Colors.White);
                                            t.Cell().Background("#34495E").Padding(3).AlignCenter().Text("العدد").FontFamily("Cairo").Bold().FontSize(8).FontColor(Colors.White);

                                            foreach (var item in data.ByDeviceType)
                                            {
                                                t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text(item.Name).FontFamily("Cairo").FontSize(8);
                                                t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignCenter().Text(item.Count.ToString()).FontFamily("Cairo").FontSize(8);
                                            }
                                        });
                                    });

                                    // Engineer distribution
                                    r.RelativeItem().PaddingLeft(5).Column(c =>
                                    {
                                        c.Item().PaddingBottom(2).Text("حسب المهندس").FontFamily("Cairo").Bold().FontSize(9).FontColor(Colors.Grey.Darken2);
                                        c.Item().Table(t =>
                                        {
                                            t.ColumnsDefinition(cols => { cols.RelativeColumn(); cols.ConstantColumn(50); });
                                            t.Cell().Background("#34495E").Padding(3).AlignCenter().Text("المهندس").FontFamily("Cairo").Bold().FontSize(8).FontColor(Colors.White);
                                            t.Cell().Background("#34495E").Padding(3).AlignCenter().Text("العدد").FontFamily("Cairo").Bold().FontSize(8).FontColor(Colors.White);

                                            foreach (var item in data.ByEngineer.Take(5))
                                            {
                                                t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text(item.Name).FontFamily("Cairo").FontSize(8);
                                                t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignCenter().Text(item.Count.ToString()).FontFamily("Cairo").FontSize(8);
                                            }
                                        });
                                    });
                                });

                                // Section 4: Detailed Records Table (if detailed report requested)
                                if (data.IsDetailed && data.Records.Count > 0)
                                {
                                    col.Item().PageBreak();
                                    col.Item().PaddingBottom(5).Text("4. السجلات التفصيلية للمعايرة خلال الفترة").FontFamily("Cairo").Bold().FontSize(11).FontColor("#1A3A6B");
                                    col.Item().Table(table =>
                                    {
                                        table.ColumnsDefinition(columns =>
                                        {
                                            columns.ConstantColumn(25);   // ت
                                            columns.ConstantColumn(65);   // الشهادة
                                            columns.RelativeColumn(1.5f); // الجهة المالكة
                                            columns.RelativeColumn(1f);   // الموديل
                                            columns.RelativeColumn(1f);   // الرقم التسلسلي
                                            columns.ConstantColumn(65);   // تاريخ المعايرة
                                            columns.ConstantColumn(65);   // تاريخ الانتهاء
                                            columns.ConstantColumn(45);   // النتيجة
                                            columns.RelativeColumn(1f);   // المهندس
                                        });

                                        table.Header(header =>
                                        {
                                            void AddHeaderCell(string text)
                                            {
                                                header.Cell()
                                                    .Background("#1A3A6B")
                                                    .Padding(4)
                                                    .AlignCenter()
                                                    .AlignMiddle()
                                                    .Text(text)
                                                    .FontFamily("Cairo")
                                                    .Bold()
                                                    .FontSize(8)
                                                    .FontColor(Colors.White);
                                            }

                                            AddHeaderCell("ت");
                                            AddHeaderCell("الشهادة");
                                            AddHeaderCell("الجهة المالكة");
                                            AddHeaderCell("الموديل");
                                            AddHeaderCell("الرقم التسلسلي");
                                            AddHeaderCell("تاريخ المعايرة");
                                            AddHeaderCell("تاريخ الانتهاء");
                                            AddHeaderCell("النتيجة");
                                            AddHeaderCell("المهندس");
                                        });

                                        int pIdx = 1;
                                        foreach (var record in data.Records)
                                        {
                                            void AddCell(string text, string colorHex = "#000000")
                                            {
                                                table.Cell()
                                                    .BorderBottom(0.5f)
                                                    .BorderColor(Colors.Grey.Lighten3)
                                                    .Padding(3)
                                                    .AlignCenter()
                                                    .AlignMiddle()
                                                    .Text(text)
                                                    .FontFamily("Cairo")
                                                    .FontSize(7)
                                                    .FontColor(colorHex);
                                            }

                                            AddCell(pIdx.ToString());
                                            AddCell(record.CertificateNumber ?? "");
                                            AddCell(record.Device?.Owner?.Name ?? "");
                                            AddCell(record.Device?.Model ?? "");
                                            AddCell(record.Device?.SerialNumber ?? "");
                                            AddCell(record.CalibrationDate.ToString("yyyy-MM-dd"));
                                            AddCell(record.ExpiryDate.ToString("yyyy-MM-dd"));
                                            
                                            string resultColor = record.Result == "Passed" ? "#2E7D32" : (record.Result == "Failed" ? "#C62828" : "#F9A825");
                                            AddCell(record.Result ?? "", resultColor);
                                            AddCell(record.EngineerName ?? "");
                                            pIdx++;
                                        }
                                    });
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

        public Task ExportPerformanceReportToExcelAsync(PerformanceReportData data, string filePath)
        {
            return Task.Run(() =>
            {
                using (var workbook = new XLWorkbook())
                {
                    var ws = workbook.Worksheets.Add("أداء وحدة المعايرة");
                    ws.RightToLeft = true;

                    // Fixed Header
                    ws.Cell(1, 1).Value = "مركز البحوث النووية";
                    ws.Cell(2, 1).Value = "إدارة الوقاية من الإشعاع";
                    ws.Cell(3, 1).Value = "قسم قياس وتقدير الجرعات الشخصية والمعايرة | وحدة المعايرة";
                    ws.Cell(4, 1).Value = $"تقرير أداء وحدة المعايرة للفترة من {data.StartDate:yyyy-MM-dd} إلى {data.EndDate:yyyy-MM-dd} | تاريخ التوليد: {DateTime.Today:yyyy-MM-dd}";

                    for (int i = 1; i <= 4; i++)
                    {
                        ws.Row(i).Style.Font.FontName = "Cairo";
                        ws.Row(i).Style.Font.Bold = true;
                    }
                    ws.Cell(1, 1).Style.Font.FontSize = 14;

                    // Add logo if available
                    if (_logoBytes != null)
                    {
                        try
                        {
                            using (var ms = new MemoryStream(_logoBytes))
                            {
                                var picture = ws.Pictures.Add(ms);
                                picture.MoveTo(ws.Cell(1, 7)); // place at column G
                                picture.Width = 60;
                                picture.Height = 60;
                            }
                        }
                        catch { }
                    }

                    // Section 1: Summary Table
                    ws.Cell(6, 1).Value = "1. الخلاصة العامة للفترة";
                    ws.Cell(6, 1).Style.Font.FontName = "Cairo";
                    ws.Cell(6, 1).Style.Font.Bold = true;
                    ws.Cell(6, 1).Style.Font.FontSize = 12;
                    ws.Cell(6, 1).Style.Font.FontColor = XLColor.FromHtml("#1A3A6B");

                    string[] summaryHeaders = { "إجمالي المعايرات", "أجهزة جديدة", "جهات مالكة جديدة", "أنواع أجهزة جديدة" };
                    for (int col = 0; col < summaryHeaders.Length; col++)
                    {
                        var cell = ws.Cell(7, col + 1);
                        cell.Value = summaryHeaders[col];
                        cell.Style.Font.FontName = "Cairo";
                        cell.Style.Font.Bold = true;
                        cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1A3A6B");
                        cell.Style.Font.FontColor = XLColor.White;
                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    }

                    ws.Cell(8, 1).Value = data.TotalRecords;
                    ws.Cell(8, 2).Value = data.NewDevices;
                    ws.Cell(8, 3).Value = data.NewOwners;
                    ws.Cell(8, 4).Value = data.NewDeviceTypes;
                    for (int col = 1; col <= 4; col++)
                    {
                        ws.Cell(8, col).Style.Font.FontName = "Cairo";
                        ws.Cell(8, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    }

                    // Section 2: Results Table
                    ws.Cell(10, 1).Value = "2. توزيع نتائج المعايرة";
                    ws.Cell(10, 1).Style.Font.FontName = "Cairo";
                    ws.Cell(10, 1).Style.Font.Bold = true;
                    ws.Cell(10, 1).Style.Font.FontSize = 12;
                    ws.Cell(10, 1).Style.Font.FontColor = XLColor.FromHtml("#1A3A6B");

                    string[] resultsHeaders = { "النتيجة: ناجح (Passed)", "النتيجة: راسب (Failed)", "النتيجة: مشروط (Conditional)" };
                    for (int col = 0; col < resultsHeaders.Length; col++)
                    {
                        var cell = ws.Cell(11, col + 1);
                        cell.Value = resultsHeaders[col];
                        cell.Style.Font.FontName = "Cairo";
                        cell.Style.Font.Bold = true;
                        cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1A3A6B");
                        cell.Style.Font.FontColor = XLColor.White;
                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    }

                    ws.Cell(12, 1).Value = $"{data.PassedCount} ({data.PassedPercent:F1}%)";
                    ws.Cell(12, 2).Value = $"{data.FailedCount} ({data.FailedPercent:F1}%)";
                    ws.Cell(12, 3).Value = $"{data.ConditionalCount} ({data.ConditionalPercent:F1}%)";
                    for (int col = 1; col <= 3; col++)
                    {
                        ws.Cell(12, col).Style.Font.FontName = "Cairo";
                        ws.Cell(12, col).Style.Font.Bold = true;
                        ws.Cell(12, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    }

                    // Section 3: Top Categories
                    ws.Cell(14, 1).Value = "3. توزيع المعايرات حسب الفئات";
                    ws.Cell(14, 1).Style.Font.FontName = "Cairo";
                    ws.Cell(14, 1).Style.Font.Bold = true;
                    ws.Cell(14, 1).Style.Font.FontSize = 12;
                    ws.Cell(14, 1).Style.Font.FontColor = XLColor.FromHtml("#1A3A6B");

                    int maxRIdx = 17;

                    // By Owner
                    ws.Cell(15, 1).Value = "توزيع حسب الجهة المالكة";
                    ws.Cell(15, 1).Style.Font.FontName = "Cairo";
                    ws.Cell(15, 1).Style.Font.Bold = true;
                    ws.Cell(16, 1).Value = "الجهة";
                    ws.Cell(16, 2).Value = "العدد";
                    ws.Cell(16, 1).Style.Font.FontName = "Cairo";
                    ws.Cell(16, 1).Style.Font.Bold = true;
                    ws.Cell(16, 2).Style.Font.FontName = "Cairo";
                    ws.Cell(16, 2).Style.Font.Bold = true;
                    int rIdx = 17;
                    foreach (var item in data.ByOwner)
                    {
                        ws.Cell(rIdx, 1).Value = item.Name;
                        ws.Cell(rIdx, 2).Value = item.Count;
                        ws.Cell(rIdx, 1).Style.Font.FontName = "Cairo";
                        ws.Cell(rIdx, 2).Style.Font.FontName = "Cairo";
                        rIdx++;
                    }
                    maxRIdx = Math.Max(maxRIdx, rIdx);

                    // By Device Type
                    int colStart = 4;
                    ws.Cell(15, colStart).Value = "توزيع حسب نوع الجهاز";
                    ws.Cell(15, colStart).Style.Font.FontName = "Cairo";
                    ws.Cell(15, colStart).Style.Font.Bold = true;
                    ws.Cell(16, colStart).Value = "النوع";
                    ws.Cell(16, colStart + 1).Value = "العدد";
                    ws.Cell(16, colStart).Style.Font.FontName = "Cairo";
                    ws.Cell(16, colStart).Style.Font.Bold = true;
                    ws.Cell(16, colStart + 1).Style.Font.FontName = "Cairo";
                    ws.Cell(16, colStart + 1).Style.Font.Bold = true;
                    rIdx = 17;
                    foreach (var item in data.ByDeviceType)
                    {
                        ws.Cell(rIdx, colStart).Value = item.Name;
                        ws.Cell(rIdx, colStart + 1).Value = item.Count;
                        ws.Cell(rIdx, colStart).Style.Font.FontName = "Cairo";
                        ws.Cell(rIdx, colStart + 1).Style.Font.FontName = "Cairo";
                        rIdx++;
                    }
                    maxRIdx = Math.Max(maxRIdx, rIdx);

                    // By Engineer
                    colStart = 7;
                    ws.Cell(15, colStart).Value = "توزيع حسب المهندس المعاير";
                    ws.Cell(15, colStart).Style.Font.FontName = "Cairo";
                    ws.Cell(15, colStart).Style.Font.Bold = true;
                    ws.Cell(16, colStart).Value = "المهندس";
                    ws.Cell(16, colStart + 1).Value = "العدد";
                    ws.Cell(16, colStart).Style.Font.FontName = "Cairo";
                    ws.Cell(16, colStart).Style.Font.Bold = true;
                    ws.Cell(16, colStart + 1).Style.Font.FontName = "Cairo";
                    ws.Cell(16, colStart + 1).Style.Font.Bold = true;
                    rIdx = 17;
                    foreach (var item in data.ByEngineer)
                    {
                        ws.Cell(rIdx, colStart).Value = item.Name;
                        ws.Cell(rIdx, colStart + 1).Value = item.Count;
                        ws.Cell(rIdx, colStart).Style.Font.FontName = "Cairo";
                        ws.Cell(rIdx, colStart + 1).Style.Font.FontName = "Cairo";
                        rIdx++;
                    }
                    maxRIdx = Math.Max(maxRIdx, rIdx);

                    // Section 4: Detailed Records (if requested)
                    if (data.IsDetailed && data.Records.Count > 0)
                    {
                        int dStartRow = Math.Max(maxRIdx + 2, 25);
                        ws.Cell(dStartRow, 1).Value = "4. جدول السجلات التفصيلية للمعايرة خلال الفترة";
                        ws.Cell(dStartRow, 1).Style.Font.FontName = "Cairo";
                        ws.Cell(dStartRow, 1).Style.Font.Bold = true;
                        ws.Cell(dStartRow, 1).Style.Font.FontSize = 12;
                        ws.Cell(dStartRow, 1).Style.Font.FontColor = XLColor.FromHtml("#1A3A6B");

                        string[] detailedHeaders = { "ت", "رقم الشهادة", "الجهة المالكة", "الموديل", "الرقم التسلسلي", "تاريخ المعايرة", "تاريخ الانتهاء", "النتيجة", "المهندس المعايِر" };
                        for (int col = 0; col < detailedHeaders.Length; col++)
                        {
                            var cell = ws.Cell(dStartRow + 1, col + 1);
                            cell.Value = detailedHeaders[col];
                            cell.Style.Font.FontName = "Cairo";
                            cell.Style.Font.Bold = true;
                            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1A3A6B");
                            cell.Style.Font.FontColor = XLColor.White;
                            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        }

                        int row = dStartRow + 2;
                        int idx = 1;
                        foreach (var record in data.Records)
                        {
                            ws.Row(row).Style.Font.FontName = "Cairo";
                            ws.Cell(row, 1).Value = idx;
                            ws.Cell(row, 2).Value = record.CertificateNumber;
                            ws.Cell(row, 3).Value = record.Device?.Owner?.Name ?? "";
                            ws.Cell(row, 4).Value = record.Device?.Model ?? "";
                            ws.Cell(row, 5).Value = record.Device?.SerialNumber ?? "";
                            ws.Cell(row, 6).Value = record.CalibrationDate.ToString("yyyy-MM-dd");
                            ws.Cell(row, 7).Value = record.ExpiryDate.ToString("yyyy-MM-dd");
                            ws.Cell(row, 8).Value = record.Result;
                            ws.Cell(row, 9).Value = record.EngineerName;

                            for (int col = 1; col <= detailedHeaders.Length; col++)
                            {
                                ws.Cell(row, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                            }
                            idx++;
                            row++;
                        }
                    }

                    ws.Columns().AdjustToContents();
                    ws.Column(3).Style.Alignment.WrapText = true; // الجهة المالكة
                    workbook.SaveAs(filePath);
                }
            });
        }
    }
}
