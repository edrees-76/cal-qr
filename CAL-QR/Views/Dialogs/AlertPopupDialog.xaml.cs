using System.Windows;
using CAL_QR.ViewModels;

namespace CAL_QR.Views.Dialogs
{
    public partial class AlertPopupDialog : Window
    {
        public AlertPopupDialog()
        {
            InitializeComponent();
        }

        private async void ShowDetailsButton_Click(object sender, RoutedEventArgs e)
        {
            // معالج حدث (async void لا مفرّ منه): يُلتقط الاستثناء ويُسجَّل، ويُغلق الحوار دائماً.
            try
            {
                if (DataContext is MainViewModel vm)
                {
                    await vm.AcknowledgeAllExpiredDevicesAsync();
                    vm.SelectedTabIndex = MainViewModel.TabIndexDashboard;
                }
            }
            catch (System.Exception ex)
            {
                CAL_QR.Services.AppLog.Error("AlertPopupDialog.ShowDetails", ex);
            }
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
