namespace CAL_QR.Enums
{
    /// <summary>
    /// نوع الوثيقة الصادرة. القيمة تُخزَّن رقماً (INTEGER) في عمود
    /// Certificates.DocumentType، بـ DEFAULT 0 فكلّ صفّ قائم = شهادة معايرة.
    /// </summary>
    public enum CertificateDocumentType
    {
        CalibrationCertificate = 0,
        CalibrationStatusReport = 1
    }
}
