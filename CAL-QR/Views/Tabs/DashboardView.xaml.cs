using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using CAL_QR.ViewModels;

namespace CAL_QR.Views.Tabs
{
    public partial class DashboardView : UserControl
    {
        public DashboardView()
        {
            InitializeComponent();
            if (App.ServiceProvider != null)
            {
                DataContext = App.ServiceProvider.GetRequiredService<DashboardViewModel>();
                Loaded += (s, e) => _ = ((DashboardViewModel)DataContext).LoadDataAsync();
            }
        }

        private void DataGridRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGridRow row && row.DataContext != null)
            {
                int deviceId = 0;
                if (row.DataContext is ExpiringDeviceDisplayItem expiringItem)
                {
                    deviceId = expiringItem.DeviceId;
                }
                else if (row.DataContext is ExpiredDeviceDisplayItem expiredItem)
                {
                    deviceId = expiredItem.DeviceId;
                }

                if (deviceId > 0 && DataContext is DashboardViewModel vm)
                {
                    vm.NavigateToDeviceCommand.Execute(deviceId);
                }
            }
        }
    }
}
