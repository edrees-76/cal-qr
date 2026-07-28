using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CAL_QR.Models;

namespace CAL_QR.Services.Payloads
{
    /// <summary>
    /// ╔══════════════════════════════════════════════════════════════════════════╗
    /// ║  ⚠⚠  نص التوقيع SIG1 — مجمَّد. لا يُعدَّل هذا الصنف إطلاقاً.  ⚠⚠         ║
    /// ╚══════════════════════════════════════════════════════════════════════════╝
    ///
    /// ترتيب الحقول، وأسماء مفاتيحها، وفواصلها، ومسافاتها، ورمز القيمة الخالية،
    /// وقواعد الهروب، وفاصل الأسطر — كلها **مجمَّدة إلى الأبد**. تغيير أيٍّ منها
    /// يُبطل التحقق من كل شهادة صدرت بهذا الإصدار: التوقيع المخزَّن يبقى كما هو،
    /// والنص المُعاد بناؤه يختلف، فتظهر كل شهادة سليمة كأنها مزوَّرة.
    ///
    /// إن لزم تغيير: أنشئ SignaturePayloadBuilderV2 وسجّله بجانب هذا، واجعله
    /// الإصدار الحالي. الشهادات القديمة تبقى محمولة على SIG1 عبر
    /// Certificate.SignaturePayloadVersion. لا تلمس هذا الملف.
    ///
    /// SignaturePayloadGoldenTests يثبّت البايتات. فشله ليس اختباراً يحتاج تحديثاً،
    /// بل إنذاراً بأن تعديلاً هنا أبطل شهادات صادرة.
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
    /// IsDeleted · IssuedAt · CreatedAt · UpdatedAt · AmendedAt · FirstPrintedAt
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
            Add("TP", PayloadNormalizer.Text(c.Temperature));
            Add("RH", PayloadNormalizer.Text(c.RelativeHumidity));
            Add("AP", PayloadNormalizer.Text(c.AtmosphericPressure));
            Add("CD", PayloadNormalizer.Date(c.CalibrationDate));
            Add("ID", PayloadNormalizer.Date(c.IssueDate));
            Add("DD", PayloadNormalizer.Date(c.DueDate));
            Add("AF", PayloadNormalizer.Text(c.AverageCorrectionFactor));
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
