using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using CAL_QR.ViewModels;

namespace CAL_QR.Views.Tabs
{
    public partial class DashboardView : UserControl
    {
        private bool _closeHooked;

        public DashboardView()
        {
            InitializeComponent();
            if (App.ServiceProvider != null)
            {
                DataContext = App.ServiceProvider.GetRequiredService<DashboardViewModel>();
                Loaded += (s, e) =>
                {
                    var vm = (DashboardViewModel)DataContext;
                    _ = vm.LoadDataAsync();

                    // فكّ اشتراك CalibrationChanged عند إغلاق النافذة المستضيفة (لا عند Unloaded:
                    // تبديل التبويب يُطلق Unloaded ثمّ Loaded ويجب أن يبقى الاشتراك).
                    if (!_closeHooked && System.Windows.Window.GetWindow(this) is System.Windows.Window host)
                    {
                        _closeHooked = true;
                        host.Closed += (_, _) => vm.Dispose();
                    }
                };
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
