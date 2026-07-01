using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using CAL_QR.ViewModels;

namespace CAL_QR.Views.Dialogs
{
    public partial class CalibrationFormDialog : Window
    {
        public CalibrationFormDialog()
        {
            InitializeComponent();
            if (App.ServiceProvider != null)
            {
                var vm = App.ServiceProvider.GetRequiredService<CalibrationFormViewModel>();
                DataContext = vm;
                vm.CloseWindowAction = Close;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
