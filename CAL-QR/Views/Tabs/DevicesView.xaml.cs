using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using CAL_QR.ViewModels;

namespace CAL_QR.Views.Tabs
{
    public partial class DevicesView : UserControl
    {
        public DevicesView()
        {
            InitializeComponent();
            if (App.ServiceProvider != null)
            {
                var vm = App.ServiceProvider.GetRequiredService<DevicesViewModel>();
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
