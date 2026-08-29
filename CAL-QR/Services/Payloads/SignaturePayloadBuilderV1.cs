using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CAL_QR.Models;

namespace CAL_QR.Services.Payloads
{
    /// <summary>
    /// ╔══════════════════════════════════════════════════════════════════════════╗
    /// ║  ⚠⚠  نص التوقيع SIG1 — القاعدة الدقيقة أدناه. اقرأها قبل أي تعديل.  ⚠⚠  ║
    /// ╚══════════════════════════════════════════════════════════════════════════╝
    ///
    /// **الصيغة مسوّدة حتى تُصدَر أول شهادة؛ وبعدها مجمَّدة، وأي تغيير يتطلب V2.**
    ///
    /// الغرض من التجميد حماية **وثائق صادرة**، لا حماية الملف من التحرير. فما دام
    ///     SELECT COUNT(*) FROM Certificates = 0
    /// فلا وثيقة موقّعة في الوجود، والتعديل تصحيحُ مواصفةٍ قبل نفاذها لا كسرُ تجميد.
    /// ومع أول شهادة تُصدَر يصير التجميد نافذاً بأثر مطلق.
    ///
    /// **سجل التعديلات قبل النفاذ — وهو جزء من التحذير لا هامش عليه:**
    /// عُدّلت هذه الصيغة مرتين وهي مسوّدة:
    ///   • المرحلة ١: إضافة TT و RE و Remarks في ذيل R و F قبل أول التزام.
    ///   • المرحلة ٢: حذف AF، وإضافة PN و LO و IN و DT، وإضافة كتلة النويدات NC/N.
    ///   • المرحلة ٣: إضافة DK وRM وSR قبل كتلة النويدات — قبل أول إصدار.
    ///   • المرحلة ٤: إضافة CM (CalibrationMode) بعد CU — قبل أول إصدار.
    /// يُسجَّل هذا صراحةً لأن تعليقاً يقول «مجمَّد إلى الأبد» ثم يُعدَّل مرتين
    /// يفقد قيمته كتحذير: القارئ التالي يتعلم أن التحذير لا يُؤخذ حرفياً.
    /// التحذير الدقيق يُطاع؛ التحذير المبالَغ يُتجاوز.
    ///
    /// **بعد أول إصدار:** أي تغيير هنا يُبطل التحقق من كل شهادة صدرت — التوقيع
    /// المخزَّن يبقى، والنص المُعاد بناؤه يختلف، فتظهر كل شهادة سليمة كأنها مزوَّرة.
    /// العلاج عندها: SignaturePayloadBuilderV2 مسجَّل بجانب هذا ويصير الإصدار
    /// الحالي، والشهادات القديمة تبقى محمولة على SIG1 عبر
    /// Certificate.SignaturePayloadVersion. ولا يُلمس هذا الملف.
    ///
    /// SignaturePayloadGoldenTests يثبّت البايتات. **بعد أول إصدار**، فشله ليس
    /// اختباراً يحتاج تحديثاً بل إنذاراً بأن تعديلاً هنا أبطل شهادات صادرة.
    ///
    /// ─── قواعد الصيغة ───
    /// • فاصل الأسطر: \n حصراً — ممنوع Environment.NewLine (يُنتج CRLF على ويندوز).
    /// • لا سطر جديد في النهاية.
    /// • فاصل الحقول داخل الصف: ;
    /// • القيمة الخالية: ~ (null و"" سواء).
    /// • التواريخ: YYYYMMDD تحت InvariantCulture.
    /// • المنطقيات: 0 / 1.
    /// • ترتيب المجموعات: OrderBy(SortOrder).ThenBy(Id) — فاصل التعادل إلزامي،
    ///   وبدونه يصير الترتيب غير حتمي ويفشل التحقق عشوائياً.
    /// • فهرس الصف (R1، U1، F1) موضعي بعد الترتيب — لا يساوي SortOrder، لأن
    ///   SortOrder قد يتكرر.
    /// • سطر العدّ (RC/UCC/FC) يُكتب **دائماً**، وبقيمة 0 عند الفراغ. حذفه عند
    ///   الفراغ غير مسموح: نصّان مختلفان لشهادة واحدة. ويمنع كذلك الاقتطاع الصامت
    ///   لصف من جدول مطبوع.
    ///
    /// ─── الخارج عن التوقيع عمداً ───
    /// ReferenceNo · Notes · AdditionalInformation · السطر التحذيري !: في الـQR ·
    /// QrPayload · VerifyCode · SignaturePayloadVersion · Id · CalibrationRecordId ·
    /// IsDeleted · IssuedAt · CreatedAt · UpdatedAt · AmendedAt · FirstPrintedAt ·
    /// FinancialReceiptNo · IsSignedCopyAttached · SignedCopyConfirmedAt
    ///
    /// FinancialReceiptNo خارجه لأنّه بيان إداريّ يُملأ يدويًّا بعد الطباعة الأولى —
    /// توقيعه يُفشل التحقّق على شهادة سليمة مُلئت خانتها بقلم.
    /// والعَلَمان الأخيران حالة تتغيّر بعد الإصدار، كـ AmendedAt.
    ///
    /// AmendedAt خارجه بالضرورة: لو دخله لَغيَّر تسجيلُ التعديل نصَّ التوقيع بذاته،
    /// فتلزم دورة إعادة حساب لا تنتهي.
    /// </summary>
    public sealed class SignaturePayloadBuilderV1 : ISignaturePayloadBuilder
    {
        public const string VersionId = "SIG1";
        private const string Header = "CALQR-SIG-V1";
        private const char FieldSeparator = ';';

        public string Version => VersionId;

        public string Build(Certificate c)
        {
            var lines = new List<string>(64) { Header };

            void Add(string key, string value) => lines.Add(key + ":" + value);

            Add("CN", PayloadNormalizer.Text(c.CertificateNumber));
            Add("TT", PayloadNormalizer.Text(c.CertificateTemplateType));
            Add("PN", PayloadNormalizer.Text(c.ProcedureNo));
            Add("LO", PayloadNormalizer.Text(c.CalibrationLocation));
            Add("IN", PayloadNormalizer.Text(c.Instrumentation));
            Add("DT", PayloadNormalizer.Text(c.DetectorType));
            Add("CL", PayloadNormalizer.Text(c.ClientName));
            Add("CA", PayloadNormalizer.Text(c.ClientAddress));
            Add("DM", PayloadNormalizer.Text(c.DeviceModel));
            Add("DS", PayloadNormalizer.Text(c.DeviceSerialNumber));
            Add("DF", PayloadNormalizer.Text(c.DeviceManufacturer));
            Add("SM", PayloadNormalizer.Text(c.SurveyMeterModel));
            Add("SS", PayloadNormalizer.Text(c.SurveyMeterSerialNumber));
            Add("MT", PayloadNormalizer.Text(c.MeasurementType));
            Add("DI", PayloadNormalizer.Text(c.Distance));
            Add("CT", PayloadNormalizer.Text(c.CountingTime));
            Add("CU", PayloadNormalizer.Text(c.CountingUnit));
            Add("CM", PayloadNormalizer.Text(c.CalibrationMode));
            Add("TP", PayloadNormalizer.Text(c.Temperature));
            Add("RH", PayloadNormalizer.Text(c.RelativeHumidity));
            Add("AP", PayloadNormalizer.Text(c.AtmosphericPressure));
            Add("CD", PayloadNormalizer.Date(c.CalibrationDate));
            Add("ID", PayloadNormalizer.Date(c.IssueDate));
            Add("DD", PayloadNormalizer.Date(c.DueDate));
            Add("RF", PayloadNormalizer.Text(c.CorrectedReadingFormula));
            Add("VD", PayloadNormalizer.Text(c.ComplianceVerdict));
            Add("ST", PayloadNormalizer.Text(c.CalibrationStandard));
            Add("ME", PayloadNormalizer.Flag(c.MethodologyEnabled));
            Add("MS", PayloadNormalizer.Text(c.RadiationSource));
            Add("MG", PayloadNormalizer.Text(c.ReferenceGeometry));
            Add("MX", PayloadNormalizer.Text(c.MethodologyText));
            Add("MR", PayloadNormalizer.Text(c.TraceabilityReference));
            Add("UE", PayloadNormalizer.Flag(c.UncertaintyEnabled));
            Add("UC", PayloadNormalizer.Text(c.CombinedUncertainty));
            Add("UX", PayloadNormalizer.Text(c.ExpandedUncertainty));
            Add("UK", PayloadNormalizer.Text(c.CoverageFactor));

            Add("DK", PayloadNormalizer.Text(c.DocumentType.ToString()));
            Add("RM", PayloadNormalizer.Text(c.Remarks));
            Add("SR", PayloadNormalizer.Text(c.StatusReason));

            // كتلة النويدات: الطبقة التي يقرأ منها الملصق. تسبق صفوف النتائج
            // الخام لأنها أعلى منها في التجريد.
            var nuclides = Ordered(c.NuclideSummaries, n => n.SortOrder, n => n.Id);
            Add("NC", nuclides.Count.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < nuclides.Count; i++)
            {
                var n = nuclides[i];
                Add("N" + (i + 1).ToString(CultureInfo.InvariantCulture), Join(
                    PayloadNormalizer.Text(n.Radionuclide),
                    PayloadNormalizer.Text(n.AverageCorrectionFactor)));
            }

            var results = Ordered(c.CalibrationResults, r => r.SortOrder, r => r.Id);
            Add("RC", results.Count.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < results.Count; i++)
            {
                var r = results[i];
                Add("R" + (i + 1).ToString(CultureInfo.InvariantCulture), Join(
                    PayloadNormalizer.Text(r.SourceId),
                    PayloadNormalizer.Text(r.Radionuclide),
                    PayloadNormalizer.Text(r.Scale),
                    PayloadNormalizer.Text(r.ReferenceDoseLevel),
                    PayloadNormalizer.Text(r.ReferenceValue),
                    PayloadNormalizer.Text(r.MeasuredReading),
                    PayloadNormalizer.Text(r.CorrectionFactor),
                    PayloadNormalizer.Text(r.RelativeError),
                    PayloadNormalizer.Text(r.AbsoluteRelativeError),
                    PayloadNormalizer.Text(r.Unit),
                    PayloadNormalizer.Text(r.Remarks)));
            }

            var components = Ordered(c.UncertaintyComponents, u => u.SortOrder, u => u.Id);
            Add("UCC", components.Count.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < components.Count; i++)
            {
                var u = components[i];
                Add("U" + (i + 1).ToString(CultureInfo.InvariantCulture), Join(
                    PayloadNormalizer.Text(u.ComponentName),
                    PayloadNormalizer.Text(u.EvaluationType),
                    PayloadNormalizer.Text(u.StandardUncertainty),
                    PayloadNormalizer.Text(u.ContributionPercent),
                    PayloadNormalizer.Text(u.Distribution)));
            }

            var checks = Ordered(c.FunctionalChecks, f => f.SortOrder, f => f.Id);
            Add("FC", checks.Count.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < checks.Count; i++)
            {
                var f = checks[i];
                Add("F" + (i + 1).ToString(CultureInfo.InvariantCulture), Join(
                    PayloadNormalizer.Text(f.CheckName),
                    PayloadNormalizer.Text(f.Requirement),
                    PayloadNormalizer.Text(f.Result),
                    PayloadNormalizer.Text(f.Remarks)));
            }

            // كتلة الاعتماد — أخطر ما في الشهادة قانونياً. تغيير «اعتمدها: فلان»
            // تزوير مباشر، وأثقل من تغيير قيمة عشرية.
            Add("G1", Join(
                PayloadNormalizer.Text(c.CalibratedByName),
                PayloadNormalizer.Text(c.CalibratedByTitle),
                PayloadNormalizer.Date(c.CalibratedByDate)));
            Add("G2", Join(
                PayloadNormalizer.Text(c.ReviewedByName),
                PayloadNormalizer.Text(c.ReviewedByTitle),
                PayloadNormalizer.Date(c.ReviewedByDate)));
            Add("G3", Join(
                PayloadNormalizer.Text(c.ApprovedByName),
                PayloadNormalizer.Text(c.ApprovedByTitle),
                PayloadNormalizer.Date(c.ApprovedByDate)));
            Add("G4", Join(
                PayloadNormalizer.Text(c.AuthorizedByName),
                PayloadNormalizer.Text(c.AuthorizedByTitle),
                PayloadNormalizer.Date(c.AuthorizedByDate)));

            return string.Join("\n", lines);
        }

        private static string Join(params string[] fields) =>
            string.Join(FieldSeparator.ToString(), fields);

        /// <summary>
        /// الترتيب الحتمي: SortOrder ثم Id. فاصل التعادل بالمعرّف إلزامي — صفّان
        /// بنفس SortOrder بلا فاصل تعادل يُرتَّبان حسب ترتيب التحميل، وهو يختلف
        /// بين لحظة الإصدار ولحظة القراءة من قاعدة البيانات، فيفشل التحقق عشوائياً.
        /// </summary>
        private static List<T> Ordered<T>(
            IEnumerable<T> source,
            Func<T, int> sortOrder,
            Func<T, int> id) =>
            source.OrderBy(sortOrder).ThenBy(id).ToList();
    }
}
