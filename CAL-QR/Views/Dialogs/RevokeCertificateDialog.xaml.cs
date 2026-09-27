using System.Windows;

namespace CAL_QR.Views.Dialogs
{
    public partial class RevokeCertificateDialog : Window
    {
        public string RevokedByName { get; private set; } = string.Empty;
        public string RevocationReason { get; private set; } = string.Empty;

        public RevokeCertificateDialog(string certificateNumber)
        {
            InitializeComponent();
            TxtWarning.Text =
                $"أنت على وشك إلغاء الشهادة رقم {certificateNumber} بصورة نهائية.\n" +
                "بعد الإلغاء لن يُعتدّ بها عند التحقق، ويمكن إصدار شهادة بديلة لنفس سجل المعايرة.\n" +
                "لا يمكن التراجع عن هذا الإجراء.";
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            string name = TxtRevokedBy.Text.Trim();
            string reason = TxtReason.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("يرجى إدخال اسم المُلغي.", "حقل مطلوب",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtRevokedBy.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                MessageBox.Show("يرجى إدخال سبب الإلغاء.", "حقل مطلوب",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtReason.Focus();
                return;
            }

            RevokedByName = name;
            RevocationReason = reason;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
