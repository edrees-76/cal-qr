using System.ComponentModel;
using System.Windows;
using CAL_QR.Services;
using CAL_QR.Validation;

namespace CAL_QR.Views.Dialogs
{
    /// <summary>
    /// بعد كلّ استعادة: المدير يؤكّد آخر رقم شهادة صدر فعلاً في السنة. المقترح هو الأكبر ممّا
    /// تعرفه المنظومة؛ يُقبل هو أو أكبر منه فقط (CertificateCounterFloorRules.ParseConfirmed).
    /// لا إلغاء: إغلاق النافذة يعتمد المقترح كما هو، فلا تمرّ استعادة بلا حدّ.
    /// </summary>
    public partial class CertificateCounterConfirmDialog : Window
    {
        private readonly CertificateCounterRestoreReport _report;
        private bool _confirmed;

        public int ConfirmedLastNumber { get; private set; }

        public CertificateCounterConfirmDialog(CertificateCounterRestoreReport report)
        {
            InitializeComponent();
            _report = report;
            ConfirmedLastNumber = report.ProposedLastNumber;

            TxtExplanation.Text =
                "الشهادات التي صدرت بعد تاريخ هذه النسخة الاحتياطيّة لم تعد موجودة في قاعدة البيانات، " +
                "لكنّ أرقامها سُلّمت لأصحابها ولا يجوز أن تُعطى لشهادة أخرى.\n\n" +
                $"آخر رقم في النسخة المستعادة لسنة {report.Year}: {report.RestoredLastNumber}\n" +
                $"آخر رقم تعرفه المنظومة (المقترح): {report.ProposedLastNumber}\n\n" +
                "إن كان في السجلّ الورقيّ رقم أكبر صدر فعلاً فاكتبه. لا يُقبل رقم أصغر من المقترح.";

            if (!string.IsNullOrWhiteSpace(report.Warning))
            {
                TxtWarning.Text = report.Warning;
                TxtWarning.Visibility = Visibility.Visible;
            }

            TxtLastNumber.Text = report.ProposedLastNumber.ToString();
            UpdateNextNumberHint();
            TxtLastNumber.TextChanged += (_, _) => UpdateNextNumberHint();
            Loaded += (_, _) => { TxtLastNumber.Focus(); TxtLastNumber.SelectAll(); };
        }

        private void UpdateNextNumberHint()
        {
            var (value, error) = CertificateCounterFloorRules.ParseConfirmed(TxtLastNumber.Text, _report.ProposedLastNumber);
            TxtNextNumber.Text = value is int last
                ? $"الشهادة التالية ستحمل الرقم TNRC-SSDL-{_report.Year}-{last + 1:D4}"
                : error;
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            var (value, error) = CertificateCounterFloorRules.ParseConfirmed(TxtLastNumber.Text, _report.ProposedLastNumber);
            if (value == null)
            {
                MessageBox.Show(error, "قيمة غير مقبولة", MessageBoxButton.OK, MessageBoxImage.Warning,
                    MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                TxtLastNumber.Focus();
                TxtLastNumber.SelectAll();
                return;
            }

            ConfirmedLastNumber = value.Value;
            _confirmed = true;
            DialogResult = true;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // إغلاق بلا تأكيد: يبقى المقترح (لا يمكن أن يكون أقلّ ممّا يُعرف أنّه صدر).
            if (!_confirmed) ConfirmedLastNumber = _report.ProposedLastNumber;
            base.OnClosing(e);
        }
    }
}
