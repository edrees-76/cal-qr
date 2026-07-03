using System;
using System.Windows;
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
                var vm = App.ServiceProvider.GetRequiredService<DeviceTypeViewModel>();
                DataContext = vm;
                Loaded += (s, e) =>
                {
                    _ = vm.LoadDataAsync();
                    var window = Window.GetWindow(this);
                    if (window != null)
                    {
                        window.Closed += (ws, we) => vm.Dispose();
                    }
                };
            }
        }
    }
}
