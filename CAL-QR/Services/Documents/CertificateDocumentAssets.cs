namespace CAL_QR.Services.Documents
{
    /// <summary>
    /// موارد الشهادة الجاهزة كبايتات — شعار المركز، شعار المؤسسة، ورمز QR.
    /// غياب أيّ منها يطوي الخانة المقابلة بلا استثناء: غياب شعار لا يمنع طباعة وثيقة.
    /// </summary>
    public sealed class CertificateDocumentAssets
    {
        public byte[]? CenterLogoPng { get; init; }
        public byte[]? EstablishmentLogoPng { get; init; }
        public byte[]? QrPng { get; init; }
    }
}
