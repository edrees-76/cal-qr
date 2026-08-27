using System;
using System.Globalization;
using System.Windows.Data;

namespace CAL_QR.Converters
{
    /// <summary>
    /// «غير مملوء» = فارغ أو ما زال يحمل شرطتَي الهيكل المبذور للظروف البيئيّة.
    /// نفس تعريف IsEnvironmentFieldUnfilled في CertificateFormViewModel حرفيًّا —
    /// العلامة تُقرأ من CertificateDraftBuilder فلا يوجد مصدرا حقيقة.
    /// يُستعمل في مُطلِق أسلوب FieldBox لإظهار إطار تذكير ذهبيّ عند الحفظ.
    /// </summary>
    public class UnfilledFieldConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            string? text = value as string;
            return string.IsNullOrWhiteSpace(text)
                || text.Contains(CAL_QR.Services.CertificateDraftBuilder.EnvironmentPlaceholderMarker,
                                 StringComparison.Ordinal);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
