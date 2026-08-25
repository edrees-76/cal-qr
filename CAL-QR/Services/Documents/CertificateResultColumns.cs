using System.Collections.Generic;
using System.Linq;
using CAL_QR.Models;

namespace CAL_QR.Services.Documents
{
    /// <summary>
    /// أعمدة جدول § CALIBRATION RESULTS الأحد عشر الممكنة. الأنواع الستّة
    /// تملأ مجموعات مختلفة منها، والقاعدة (Select) تطبع فقط ما حمله صفٌّ
    /// واحد على الأقل — دون بناء منطق مزدوج لكل نوع جهاز.
    /// </summary>
    public enum CertificateResultColumn
    {
        SourceId,
        Radionuclide,
        Scale,
        ReferenceDoseLevel,
        ReferenceValue,
        MeasuredReading,
        CorrectionFactor,
        RelativeError,
        AbsoluteRelativeError,
        Unit,
        Remarks
    }

    public static class CertificateResultColumns
    {
        // الترتيب الثابت الذي لا يتغير مهما كان ترتيب الملء.
        private static readonly CertificateResultColumn[] FixedOrder =
        {
            CertificateResultColumn.SourceId,
            CertificateResultColumn.Radionuclide,
            CertificateResultColumn.Scale,
            CertificateResultColumn.ReferenceDoseLevel,
            CertificateResultColumn.ReferenceValue,
            CertificateResultColumn.MeasuredReading,
            CertificateResultColumn.CorrectionFactor,
            CertificateResultColumn.RelativeError,
            CertificateResultColumn.AbsoluteRelativeError,
            CertificateResultColumn.Unit,
            CertificateResultColumn.Remarks
        };

        public static IReadOnlyList<CertificateResultColumn> Select(IEnumerable<CertificateCalibrationResult> rows)
        {
            var rowList = rows?.ToList() ?? new List<CertificateCalibrationResult>();

            return FixedOrder
                .Where(column => rowList.Any(row => !string.IsNullOrWhiteSpace(GetValue(column, row))))
                .ToList();
        }

        public static string GetHeader(CertificateResultColumn column) => column switch
        {
            CertificateResultColumn.SourceId => "Source ID",
            CertificateResultColumn.Radionuclide => "Radionuclide",
            CertificateResultColumn.Scale => "Scale",
            CertificateResultColumn.ReferenceDoseLevel => "Reference Dose Level",
            CertificateResultColumn.ReferenceValue => "Reference Value",
            CertificateResultColumn.MeasuredReading => "Measured Reading",
            CertificateResultColumn.CorrectionFactor => "Correction Factor",
            CertificateResultColumn.RelativeError => "Relative Error",
            CertificateResultColumn.AbsoluteRelativeError => "Absolute Relative Error",
            CertificateResultColumn.Unit => "Unit",
            CertificateResultColumn.Remarks => "Remarks",
            _ => string.Empty
        };

        public static string GetValue(CertificateResultColumn column, CertificateCalibrationResult row) => column switch
        {
            CertificateResultColumn.SourceId => row.SourceId ?? string.Empty,
            CertificateResultColumn.Radionuclide => row.Radionuclide ?? string.Empty,
            CertificateResultColumn.Scale => row.Scale ?? string.Empty,
            CertificateResultColumn.ReferenceDoseLevel => row.ReferenceDoseLevel ?? string.Empty,
            CertificateResultColumn.ReferenceValue => row.ReferenceValue ?? string.Empty,
            CertificateResultColumn.MeasuredReading => row.MeasuredReading ?? string.Empty,
            CertificateResultColumn.CorrectionFactor => row.CorrectionFactor ?? string.Empty,
            CertificateResultColumn.RelativeError => row.RelativeError ?? string.Empty,
            CertificateResultColumn.AbsoluteRelativeError => row.AbsoluteRelativeError ?? string.Empty,
            CertificateResultColumn.Unit => row.Unit ?? string.Empty,
            CertificateResultColumn.Remarks => row.Remarks ?? string.Empty,
            _ => string.Empty
        };
    }
}
