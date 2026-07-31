using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SSDL.CalibrationSystem
{
    /// <summary>
    /// .NET 8 Solution for Generating ISO/IEC 17025 Accredited Calibration Certificates.
    /// Supports Single-Page and Two-Page layouts with bilingual text (Arabic/English).
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("==========================================================");
            Console.WriteLine(" SSDL Nuclear Calibration System - .NET 8 Report Generator");
            Console.WriteLine("==========================================================");

            // Fetch sample dataset for Pancake GM Detector (B105)
            var certB105 = CertificateRepository.GetB105Dataset();

            // Fetch sample dataset for Gamma Scintillation Probe (G501)
            var certG501 = CertificateRepository.GetG501Dataset();

            // Initialize Html Generator Engine
            var generator = new CertificateHtmlEngine();

            // Generate Single-Page Certificate
            string htmlB105Single = generator.GenerateHtml(certB105, isTwoPageLayout: false);
            File.WriteAllText("Certificate_B105_SinglePage.html", htmlB105Single, Encoding.UTF8);
            Console.WriteLine("[✔] Successfully generated: Certificate_B105_SinglePage.html");

            // Generate Two-Page Certificate (Expanded with GUM Uncertainty Budget)
            string htmlB105TwoPage = generator.GenerateHtml(certB105, isTwoPageLayout: true);
            File.WriteAllText("Certificate_B105_TwoPages.html", htmlB105TwoPage, Encoding.UTF8);
            Console.WriteLine("[✔] Successfully generated: Certificate_B105_TwoPages.html");

            // Generate Gamma Probe Certificate
            string htmlG501Single = generator.GenerateHtml(certG501, isTwoPageLayout: false);
            File.WriteAllText("Certificate_G501_SinglePage.html", htmlG501Single, Encoding.UTF8);
            Console.WriteLine("[✔] Successfully generated: Certificate_G501_SinglePage.html");

            Console.WriteLine("\nAll certificates generated successfully in .NET 8 environment!");
        }
    }

    #region Data Models

    public record CalibrationCertificateModel
    {
        public string CertNo { get; init; } = string.Empty;
        public string RequestNo { get; init; } = string.Empty;
        public string TitleEn { get; init; } = string.Empty;
        public string TitleAr { get; init; } = string.Empty;
        public string ClientName { get; init; } = string.Empty;
        public string ClientAddress { get; init; } = string.Empty;
        public string SurveyMeter { get; init; } = string.Empty;
        public string SurveyMeterSN { get; init; } = string.Empty;
        public string Detector { get; init; } = string.Empty;
        public string DetectorSN { get; init; } = string.Empty;
        public string Manufacturer { get; init; } = string.Empty;
        public string CalDate { get; init; } = string.Empty;
        public string DueDate { get; init; } = string.Empty;
        public string Procedure { get; init; } = string.Empty;
        public string Location { get; init; } = string.Empty;
        public string Technician { get; init; } = string.Empty;
        public string VerdictEn { get; init; } = string.Empty;
        public string VerdictAr { get; init; } = string.Empty;
        public string EnvTemp { get; init; } = string.Empty;
        public string EnvRH { get; init; } = string.Empty;
        public string EnvPress { get; init; } = string.Empty;
        public string CountingTime { get; init; } = string.Empty;
        public string CountingUnit { get; init; } = string.Empty;
        public string Geometry { get; init; } = string.Empty;
        public string SourceID { get; init; } = string.Empty;
        public string Radionuclide { get; init; } = string.Empty;
        public string SourceActivity { get; init; } = string.Empty;
        public string Traceability { get; init; } = string.Empty;
        public string CfAvg { get; init; } = string.Empty;
        public string CombinedUncertainty { get; init; } = string.Empty;
        public string ExpandedUncertainty { get; init; } = string.Empty;

        public List<CalibrationResultRow> CalResults { get; init; } = new();
        public List<UncertaintyComponentRow> UncertaintyBudget { get; init; } = new();
        public List<FunctionalCheckRow> FunctionalChecks { get; init; } = new();
    }

    public record CalibrationResultRow(string Scale, string RefVal, string MeasVal, string Cf);
    public record UncertaintyComponentRow(string No, string Component, string Type, string StdUnc, string Distribution, string Contribution);
    public record FunctionalCheckRow(string Name, string Requirement, string Result, bool IsPass);

    #endregion

    #region Data Repository

    public static class CertificateRepository
    {
        public static CalibrationCertificateModel GetB105Dataset()
        {
            return new CalibrationCertificateModel
            {
                CertNo = "TNRC-SSDL-2026-013",
                RequestNo = "REQ-2026-088",
                TitleEn = "CALIBRATION CERTIFICATE FOR PANCAKE GM DETECTOR (MODEL 44-9)",
                TitleAr = "شهادة معايرة كاشف إشعاعي نوع بانكيك (موديل 44-9)",
                ClientName = "Waha Oil Company",
                ClientAddress = "Tripoli, Libya",
                SurveyMeter = "Ludlum Model 3 (Survey Meter)",
                SurveyMeterSN = "306795",
                Detector = "Pancake GM Probe (Alpha/Beta Detector) Model 44-9",
                DetectorSN = "PR330661",
                Manufacturer = "LUDLUM MEASUREMENTS, INC.",
                CalDate = "25/06/2026",
                DueDate = "25/06/2027",
                Procedure = "SSDL-CP-01 Rev. 04 (ISO/IEC 17025:2017)",
                Location = "SSDL – Calibration Lab (Tajoura)",
                Technician = "Eng. Radiation Safety Team",
                VerdictEn = "APPROVED FOR OPERATIONAL USE",
                VerdictAr = "معتمد للاستخدام التشغيلي",
                EnvTemp = "23.0 ± 2.0 °C",
                EnvRH = "50 ± 10 % RH",
                EnvPress = "101.30 ± 1.0 kPa",
                CountingTime = "60 Sec",
                CountingUnit = "kCPM",
                Geometry = "Contact",
                SourceID = "6CO-122 / 6CO-123 / 6CO-124",
                Radionuclide = "Sr-90 / Y-90",
                SourceActivity = "37.0 MBq",
                Traceability = "IAEA - Vienna / NIST - USA",
                CfAvg = "1.13",
                CombinedUncertainty = "5.40 %",
                ExpandedUncertainty = "10.80 % (k = 2, 95% CL)",
                CalResults = new List<CalibrationResultRow>
                {
                    new("× 0.1", "0.242", "0.250", "0.97"),
                    new("× 1.0", "2.420", "2.000", "1.21"),
                    new("× 10.0", "24.20", "20.000", "1.21")
                },
                FunctionalChecks = new List<FunctionalCheckRow>
                {
                    new("Background Check / الخلفية الإشعاعية", "Normal Limits", "Pass / مقبول", true),
                    new("High Voltage Check / الجهد العالي", "Within Range", "Pass / مقبول", true),
                    new("Audio/Alarm Check / الصوت والإنذار", "Operational", "Pass / مقبول", true),
                    new("Visual Inspection / الفحص البصري", "No Damage", "Pass / مقبول", true)
                },
                UncertaintyBudget = new List<UncertaintyComponentRow>
                {
                    new("1", "Source Calibration (Certificate)", "Type B", "5.00 %", "Normal", "85.7 %"),
                    new("2", "Radioactive Decay Correction", "Type B", "0.73 %", "Normal", "1.8 %"),
                    new("3", "Counting Statistics", "Type A", "0.71 %", "Poisson", "1.7 %"),
                    new("4", "Measurement Repeatability", "Type A", "1.25 %", "Normal", "5.4 %"),
                    new("5", "Geometry & Positioning", "Type B", "1.25 %", "Rectangular", "5.4 %")
                }
            };
        }

        public static CalibrationCertificateModel GetG501Dataset()
        {
            return new CalibrationCertificateModel
            {
                CertNo = "TNRC-SSDL-2026-018",
                RequestNo = "REQ-2026-092",
                TitleEn = "CALIBRATION CERTIFICATE FOR GAMMA SCINTILLATION PROBE (MODEL 44-2)",
                TitleAr = "شهادة معايرة مسبار وميضي لقياس أشعة جاما (موديل 44-2)",
                ClientName = "Waha Oil Company",
                ClientAddress = "Tripoli, Libya",
                SurveyMeter = "Ludlum Model 3 (Survey Meter)",
                SurveyMeterSN = "298192",
                Detector = "Gamma Scintillation Probe (Model 44-2)",
                DetectorSN = "PR508178",
                Manufacturer = "LUDLUM MEASUREMENTS, INC.",
                CalDate = "25/06/2026",
                DueDate = "25/06/2027",
                Procedure = "SSDL-CP-02 Rev. 03 (ISO/IEC 17025:2017)",
                Location = "SSDL – High Field Gamma Room",
                Technician = "Eng. Radiation Safety Team",
                VerdictEn = "APPROVED FOR OPERATIONAL RADIATION SAFETY USE",
                VerdictAr = "معتمد للاستخدام في الوقاية الإشعاعية",
                EnvTemp = "23.0 ± 2.0 °C",
                EnvRH = "50 ± 10 % RH",
                EnvPress = "101.30 ± 1.00 kPa",
                CountingTime = "60 Sec",
                CountingUnit = "µSv/h",
                Geometry = "Axis at 1.0 m",
                SourceID = "Co-60 / Cs-137 Point Sources",
                Radionuclide = "Cs-137 / Co-60",
                SourceActivity = "74.0 MBq",
                Traceability = "IAEA - Vienna",
                CfAvg = "1.04",
                CombinedUncertainty = "3.85 %",
                ExpandedUncertainty = "7.70 % (k = 2, 95% CL)",
                CalResults = new List<CalibrationResultRow>
                {
                    new("10.0 µSv/h", "10.00", "9.61", "1.04"),
                    new("100.0 µSv/h", "100.00", "96.15", "1.04"),
                    new("1000.0 µSv/h", "1000.00", "961.50", "1.04")
                },
                FunctionalChecks = new List<FunctionalCheckRow>
                {
                    new("Background Check / الخلفية الإشعاعية", "Normal Limits", "Pass / مقبول", true),
                    new("High Voltage Check / الجهد العالي", "Within Range", "Pass / مقبول", true),
                    new("Audio/Alarm Check / الصوت والإنذار", "Operational", "Pass / مقبول", true)
                },
                UncertaintyBudget = new List<UncertaintyComponentRow>
                {
                    new("1", "Reference Ionization Chamber", "Type B", "2.50 %", "Normal", "78.0 %"),
                    new("2", "Distance Alignment (1.0m)", "Type B", "1.00 %", "Rectangular", "12.5 %"),
                    new("3", "Environmental Variations", "Type B", "0.50 %", "Normal", "3.1 %"),
                    new("4", "Instrument Reading Stability", "Type A", "0.80 %", "Normal", "6.4 %")
                }
            };
        }
    }

    #endregion

    #region Certificate HTML Engine (.NET 8)

    public class CertificateHtmlEngine
    {
        public string GenerateHtml(CalibrationCertificateModel model, bool isTwoPageLayout)
        {
            var sb = new StringBuilder();

            sb.AppendLine("""
            <!DOCTYPE html>
            <html lang="ar" dir="rtl">
            <head>
                <meta charset="UTF-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <title>شهادة معايرة معتمدة | International Calibration Certificate</title>
                <script src="https://cdn.tailwindcss.com"></script>
                <link href="https://fonts.googleapis.com/css2?family=Cairo:wght@300;400;600;700;800&family=Inter:wght@300;400;500;600;700&display=swap" rel="stylesheet">
                <script>
                    tailwind.config = {
                        theme: {
                            extend: {
                                colors: {
                                    navy: { 800: '#0B1F3A', 900: '#0A2540', 950: '#061526' },
                                    gold: { 500: '#C5A059', 600: '#B08B44' },
                                    slateBg: '#F8FAFC'
                                },
                                fontFamily: { sans: ['Cairo', 'Inter', 'sans-serif'] }
                            }
                        }
                    }
                </script>
                <style>
                    body { font-family: 'Cairo', 'Inter', sans-serif; background-color: #0f172a; color: #1e293b; }
                    .a4-page { width: 210mm; min-height: 297mm; padding: 12mm 15mm; margin: 0 auto; background: #ffffff; box-shadow: 0 10px 25px rgba(0,0,0,0.3); position: relative; box-sizing: border-box; page-break-after: always; }
                    @media print {
                        @page { size: A4 portrait; margin: 0; }
                        body { background: none !important; padding: 0 !important; }
                        .a4-page { box-shadow: none !important; margin: 0 !important; width: 210mm !important; height: 297mm !important; }
                    }
                </style>
            </head>
            <body class="py-8 text-slate-800">
                <div class="space-y-8">
            """);

            sb.AppendLine(RenderPage1(model));

            if (isTwoPageLayout)
            {
                sb.AppendLine(RenderPage2(model));
            }

            sb.AppendLine("""
                </div>
            </body>
            </html>
            """);

            return sb.ToString();
        }

        private string RenderPage1(CalibrationCertificateModel m)
        {
            var resultsRows = new StringBuilder();
            foreach (var r in m.CalResults)
            {
                resultsRows.AppendLine($"""
                    <tr class="border-b border-slate-200 text-center font-mono text-xs">
                        <td class="py-1.5 font-bold text-slate-700">{r.Scale}</td>
                        <td class="py-1.5 text-navy-900">{r.RefVal}</td>
                        <td class="py-1.5 text-slate-800">{r.MeasVal}</td>
                        <td class="py-1.5 font-bold text-gold-600">{r.Cf}</td>
                    </tr>
                """);
            }

            var checksRows = new StringBuilder();
            foreach (var c in m.FunctionalChecks)
            {
                checksRows.AppendLine($"""
                    <div class="flex items-center justify-between bg-slate-50 px-2 py-1 rounded border border-slate-200/60">
                        <span class="text-slate-600 text-[11px]">{c.Name}</span>
                        <span class="bg-emerald-100 text-emerald-800 text-[9.5px] font-bold px-2 py-0.5 rounded border border-emerald-300">✔ {c.Result}</span>
                    </div>
                """);
            }

            return $"""
            <div class="a4-page flex flex-col justify-between">
                <div>
                    <!-- Header -->
                    <div class="border-b-2 border-gold-500 pb-3 mb-3">
                        <div class="text-center">
                            <h2 class="text-base font-extrabold text-navy-900">دولة ليبيا — STATE OF LIBYA</h2>
                            <h3 class="text-xs font-bold text-slate-700">مركز البحوث النووية - تاجوراء | Nuclear Research Center - Tajoura</h3>
                            <p class="text-[11px] font-semibold text-gold-600">Secondary Standard Dosimetry Laboratory (SSDL) | وحدة المعايرة</p>
                        </div>
                        <div class="mt-3 bg-navy-900 text-white rounded p-2 text-center border-y-2 border-gold-500">
                            <h1 class="text-base font-black">CALIBRATION CERTIFICATE</h1>
                            <div class="text-xs font-semibold text-gold-500">{m.TitleAr}</div>
                            <div class="inline-block mt-1 bg-white/10 px-3 py-0.5 rounded-full text-[11px] font-mono text-gold-500">
                                CERTIFICATE NO: <span class="font-bold text-white">{m.CertNo}</span>
                            </div>
                        </div>
                    </div>

                    <!-- Client Metadata Grid -->
                    <div class="bg-slateBg rounded border border-slate-200 p-2.5 mb-3 text-xs grid grid-cols-2 gap-2">
                        <div><span class="text-slate-500">العميل / Client:</span> <strong class="text-navy-900">{m.ClientName} ({m.ClientAddress})</strong></div>
                        <div><span class="text-slate-500">رقم الطلب / Request:</span> <strong class="text-navy-900 font-mono">{m.RequestNo}</strong></div>
                        <div><span class="text-slate-500">الجهاز / Instrument:</span> <strong class="text-navy-900">{m.SurveyMeter}</strong></div>
                        <div><span class="text-slate-500">الرقم التسلسلي / S/N:</span> <strong class="text-navy-900 font-mono">{m.SurveyMeterSN}</strong></div>
                        <div><span class="text-slate-500">المسبار / Probe:</span> <strong class="text-navy-900">{m.Detector}</strong></div>
                        <div><span class="text-slate-500">سلسلة المسبار / Probe S/N:</span> <strong class="text-navy-900 font-mono">{m.DetectorSN}</strong></div>
                        <div><span class="text-slate-500">تاريخ المعايرة / Cal Date:</span> <strong class="text-emerald-700 font-mono">{m.CalDate}</strong></div>
                        <div><span class="text-slate-500">تاريخ الاستحقاق / Due Date:</span> <strong class="text-rose-700 font-mono">{m.DueDate}</strong></div>
                    </div>

                    <!-- Env and Functional Side-by-side -->
                    <div class="grid grid-cols-12 gap-3 mb-3">
                        <div class="col-span-6 border border-slate-200 rounded p-2 bg-white">
                            <h4 class="text-xs font-bold text-navy-900 border-b pb-1 mb-1">الظروف البيئية / Environmental Conditions</h4>
                            <div class="text-[11px] space-y-1 font-mono">
                                <div class="flex justify-between"><span>Temperature:</span> <strong>{m.EnvTemp}</strong></div>
                                <div class="flex justify-between"><span>Humidity:</span> <strong>{m.EnvRH}</strong></div>
                                <div class="flex justify-between"><span>Pressure:</span> <strong>{m.EnvPress}</strong></div>
                            </div>
                        </div>
                        <div class="col-span-6 border border-slate-200 rounded p-2 bg-white">
                            <h4 class="text-xs font-bold text-navy-900 border-b pb-1 mb-1">الفحوصات الوظيفية / Functional Checks</h4>
                            <div class="space-y-1">{checksRows}</div>
                        </div>
                    </div>

                    <!-- Calibration Results Table -->
                    <div class="bg-white border border-slate-200 rounded overflow-hidden mb-3">
                        <div class="bg-navy-900 text-white px-3 py-1.5 flex justify-between text-xs font-bold">
                            <span>نتائج المعايرة / Calibration Results</span>
                            <span class="text-gold-500 text-[10px]">Corrected = Measured × CFavg</span>
                        </div>
                        <table class="w-full text-xs">
                            <thead class="bg-slate-100 text-navy-900 border-b">
                                <tr>
                                    <th class="py-1">المدى / Scale</th>
                                    <th class="py-1">القيمة المرجعية / Reference ({m.CountingUnit})</th>
                                    <th class="py-1">القراءة المقاسة / Measured ({m.CountingUnit})</th>
                                    <th class="py-1">معامل التصحيح / CF</th>
                                </tr>
                            </thead>
                            <tbody>{resultsRows}</tbody>
                        </table>
                        <div class="bg-slate-50 p-2 flex justify-around text-xs font-bold text-navy-900 border-t">
                            <div>CFavg: <span class="text-gold-600 font-mono">{m.CfAvg}</span></div>
                            <div>Combined Unc (uc): <span class="font-mono">{m.CombinedUncertainty}</span></div>
                            <div>Expanded Unc (U): <span class="text-emerald-700 font-mono">{m.ExpandedUncertainty}</span></div>
                        </div>
                    </div>

                    <!-- Verdict -->
                    <div class="bg-emerald-50 border-2 border-emerald-500/60 rounded p-2 mb-3 flex items-center justify-between">
                        <div>
                            <div class="text-[10px] text-emerald-800 font-bold">القرار النهائي / FINAL VERDICT</div>
                            <div class="text-xs font-black text-emerald-950">{m.VerdictEn} — ({m.VerdictAr})</div>
                        </div>
                        <div class="text-[10px] text-emerald-800 font-bold">ISO/IEC 17025:2017 Compliant</div>
                    </div>
                </div>

                <!-- Signatures -->
                <div class="border-t pt-2">
                    <div class="grid grid-cols-4 gap-2 text-center text-[10px]">
                        <div class="bg-slateBg p-1.5 rounded border"><strong>Calibrated By</strong><br>تمت المعايرة بواسطة</div>
                        <div class="bg-slateBg p-1.5 rounded border"><strong>Head of SSDL Unit</strong><br>رئيس وحدة المعايرة</div>
                        <div class="bg-slateBg p-1.5 rounded border"><strong>Director of NRC</strong><br>مدير مركز البحوث</div>
                        <div class="bg-slateBg p-1.5 rounded border border-dashed flex items-center justify-center font-bold text-slate-400">مكان الختم الرسمي<br>OFFICIAL STAMP</div>
                    </div>
                    <div class="mt-2 text-center text-[9px] text-slate-400 font-mono">TNRC-SSDL-VERIFY-2026 | ISO/IEC 17025 Accredited Laboratory</div>
                </div>
            </div>
            """;
        }

        private string RenderPage2(CalibrationCertificateModel m)
        {
            var budgetRows = new StringBuilder();
            foreach (var b in m.UncertaintyBudget)
            {
                budgetRows.AppendLine($"""
                    <tr class="border-b border-slate-200 text-center font-mono text-xs">
                        <td class="py-1.5 font-bold text-slate-600">{b.No}</td>
                        <td class="py-1.5 text-right font-sans">{b.Component}</td>
                        <td class="py-1.5 text-slate-600">{b.Type}</td>
                        <td class="py-1.5 text-navy-900 font-bold">{b.StdUnc}</td>
                        <td class="py-1.5 text-slate-600">{b.Distribution}</td>
                        <td class="py-1.5 font-bold text-gold-600">{b.Contribution}</td>
                    </tr>
                """);
            }

            return $"""
            <!-- PAGE 2 ANNEX -->
            <div class="a4-page flex flex-col justify-between">
                <div>
                    <h3 class="text-sm font-bold text-navy-900 border-b-2 border-gold-500 pb-1 mb-3">ملحق ميزانية الارتياب / Uncertainty Budget Annex (GUM Model)</h3>
                    
                    <div class="bg-slateBg p-3 rounded border text-xs leading-relaxed mb-4">
                        <h4 class="font-bold text-navy-900 mb-1">شروط وملاحظات تقنية / Technical Notes:</h4>
                        <ul class="list-disc list-inside space-y-1 text-slate-700 text-[11px]">
                            <li>تم حساب الارتياب الممتد معامل تغطية k = 2 عند مستوى ثقة 95%.</li>
                            <li>تمت القياسات والمطابقة وفق متطلبات المواصفة الدولية ISO/IEC 17025:2017.</li>
                        </ul>
                    </div>

                    <div class="bg-white border border-slate-200 rounded overflow-hidden">
                        <div class="bg-navy-900 text-white px-3 py-1.5 text-xs font-bold">مكونات ميزانية الارتياب / Uncertainty Components</div>
                        <table class="w-full text-xs">
                            <thead class="bg-slate-100 text-navy-900 border-b">
                                <tr>
                                    <th class="py-1">#</th>
                                    <th class="py-1 text-right">المكون / Component</th>
                                    <th class="py-1">النوع / Type</th>
                                    <th class="py-1">الارتياب القياسي / Std Unc</th>
                                    <th class="py-1">التوزيع / Dist</th>
                                    <th class="py-1">المساهمة / Contribution</th>
                                </tr>
                            </thead>
                            <tbody>{budgetRows}</tbody>
                        </table>
                    </div>
                </div>

                <div class="text-center text-[9px] text-slate-400 font-mono border-t pt-2">
                    TNRC-SSDL Page 2 of 2 Annex | ISO/IEC 17025:2017
                </div>
            </div>
            """;
        }
    }

    #endregion
}