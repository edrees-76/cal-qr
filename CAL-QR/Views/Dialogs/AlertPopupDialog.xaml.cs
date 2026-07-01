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

        private void ShowDetailsButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.SelectedTabIndex = 0;
            }
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
