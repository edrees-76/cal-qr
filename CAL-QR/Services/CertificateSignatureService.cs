using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CAL_QR.Models;
using CAL_QR.Services.Payloads;

namespace CAL_QR.Services
{
    public class CertificateSignatureService : ICertificateSignatureService
    {
        /// <summary>
        /// السطر التحذيري في الـQR. **خارج نص التوقيع** عمداً، فنصّه قابل للتعديل
        /// مستقبلاً دون إبطال أي شهادة صادرة.
        ///
        /// خطر متبقٍ مقبول ومسجَّل: بقاؤه خارج التوقيع يعني أن تغييره لا يكسر V.
        /// مقبول لأنه نص إرشادي لا بيانات قياس — وكل ما يُطبع من قياسات محميّ.
        /// </summary>
        public const string WarningLine =
            "!:Verify at SSDL-TNRC. Scan alone does not prove authenticity.";

        public const string LaboratoryCode = "TNRC-SSDL";

        private readonly IHmacService _hmacService;
        private readonly IReadOnlyDictionary<string, ISignaturePayloadBuilder> _builders;

        public CertificateSignatureService(IHmacService hmacService)
        {
            _hmacService = hmacService;

            // سجل البواني. إضافة إصدار جديد = سطر واحد هنا + صنف جديد،
            // بلا أي تعديل على البواني القائمة.
            var builders = new ISignaturePayloadBuilder[]
            {
                new SignaturePayloadBuilderV1()
            };

            _builders = builders.ToDictionary(b => b.Version, StringComparer.Ordinal);
            CurrentVersion = SignaturePayloadBuilderV1.VersionId;
        }

        public string CurrentVersion { get; }

        public bool SupportsVersion(string? version) =>
            !string.IsNullOrWhiteSpace(version) && _builders.ContainsKey(version);

        public string BuildSignaturePayload(Certificate certificate, string version)
        {
            if (certificate == null) throw new ArgumentNullException(nameof(certificate));

            // فشل مغلق: إصدار مجهول أو خالٍ يُرفض صراحةً ولا يسقط إلى الإصدار
            // الحالي. السقوط التلقائي هو ما يحوّل آلية الإصدارات إلى ثغرة —
            // ويجعل حذف عمود الإصدار كافياً للتحايل.
            if (!SupportsVersion(version) || !_builders.TryGetValue(version!, out var builder))
            {
                throw new NotSupportedException(
                    $"إصدار نص التوقيع غير مدعوم: '{version ?? "(خالٍ)"}'.");
            }

            return builder.Build(certificate);
        }

        public string ComputeVerifyCode(Certificate certificate, string version)
        {
            string payload = BuildSignaturePayload(certificate, version);
            return ComputeHmac(payload);
        }

        public string BuildQrPayload(Certificate certificate)
        {
            if (certificate == null) throw new ArgumentNullException(nameof(certificate));

            // الصيغة المعتمدة حرفياً. لا رمز إصدار فيها بقرار صريح: التحقق من
            // SIG1 فصاعداً يتطلب بحثاً في قاعدة البيانات بـ CN أو VerifyCode،
            // والمسار الأوفلاين غير مدعوم لهذه الصيغة ويفشل برسالة صريحة
            // «يتطلب التحقق من المنظومة» — لا برسالة تزوير.
            var lines = new[]
            {
                "L:" + LaboratoryCode,
                "C:" + (certificate.ClientName ?? string.Empty).Trim(),
                "SN:" + (certificate.DeviceSerialNumber ?? string.Empty).Trim(),
                "CN:" + (certificate.CertificateNumber ?? string.Empty).Trim(),
                "V:" + (certificate.VerifyCode ?? string.Empty).Trim(),
                WarningLine
            };

            return string.Join("\n", lines);
        }

        /// <summary>
        /// HMAC-SHA256 على نص التوقيع، بأول ١٦ خانة hex بحروف كبيرة — نفس طول
        /// وشكل التوقيع المستعمل في HmacService، وبنفس المفتاح النشط، حتى يبقى
        /// في النظام مفتاح واحد لا مفتاحان.
        /// </summary>
        private string ComputeHmac(string payload)
        {
            // نمرّ عبر HmacService لضمان استعمال المفتاح النشط المخزَّن في
            // AppSettings ذاته. حقل واحد يحمل النص كله، والحقول السبعة الباقية
            // فارغة — فلا يمكن أن يتصادم نص شهادة مع نص سجل معايرة قديم لأن
            // الأخير لا يبدأ أبداً بـ "CALQR-SIG-".
            return _hmacService.ComputeSignature(
                certNo: payload,
                model: string.Empty,
                serial: string.Empty,
                ownerName: string.Empty,
                calDate: string.Empty,
                expDate: string.Empty,
                result: string.Empty,
                engineerName: string.Empty);
        }

        /// <summary>مقارنة ثابتة الزمن، حتى لا يتسرب موضع أول اختلاف.</summary>
        public static bool CodesMatch(string? a, string? b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            {
                return false;
            }

            byte[] left = Encoding.UTF8.GetBytes(a.Trim().ToUpperInvariant());
            byte[] right = Encoding.UTF8.GetBytes(b.Trim().ToUpperInvariant());

            return CryptographicOperations.FixedTimeEquals(left, right);
        }
    }
}
