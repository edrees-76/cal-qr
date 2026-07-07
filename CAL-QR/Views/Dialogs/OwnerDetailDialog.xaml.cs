using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using CAL_QR.ViewModels;
using CAL_QR.Models.DisplayItems;

namespace CAL_QR.Views.Dialogs
{
    public partial class OwnerDetailDialog : Window
    {
        public OwnerDetailDialog()
        {
            InitializeComponent();

            if (App.ServiceProvider != null)
            {
                var vm = App.ServiceProvider.GetRequiredService<OwnerDetailViewModel>();
                DataContext = vm;
                vm.CloseWindowAction = Close;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void DataGridRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGridRow row && row.DataContext is OwnerDeviceDisplayItem item)
            {
                if (DataContext is OwnerDetailViewModel vm)
                {
                    vm.NavigateToDeviceCommand.Execute(item.DeviceId);
                }
            }
        }
    }
}
