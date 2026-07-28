using CAL_QR.Models;

namespace CAL_QR.Services.Payloads
{
    /// <summary>
    /// بانِي نص التوقيع لإصدار واحد. كل إصدار صنف مستقل ومجمَّد: التغيير يكون
    /// بإضافة بانٍ جديد، لا بتعديل بانٍ قائم.
    /// </summary>
    public interface ISignaturePayloadBuilder
    {
        /// <summary>معرّف الإصدار كما يُخزَّن في Certificate.SignaturePayloadVersion.</summary>
        string Version { get; }

        /// <summary>
        /// يبني نص التوقيع. يُستدعى دائماً **بعد** SaveChanges الأولى، لأن فاصل
        /// التعادل ThenBy(Id) عاطل قبلها (كل الأبناء الجدد Id = 0).
        /// </summary>
        string Build(Certificate certificate);
    }
}
