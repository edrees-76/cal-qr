using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using CAL_QR.ViewModels;

namespace CAL_QR.Views.Tabs
{
    public partial class DeviceTypesView : UserControl
    {
        public DeviceTypesView()
        {
            InitializeComponent();
            if (App.ServiceProvider != null)
            {
                DataContext = App.ServiceProvider.GetRequiredService<DeviceTypeViewModel>();
                Loaded += (s, e) => _ = ((DeviceTypeViewModel)DataContext).LoadDataAsync();
            }
        }
    }
}
