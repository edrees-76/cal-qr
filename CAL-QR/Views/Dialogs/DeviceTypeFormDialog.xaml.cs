using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using CAL_QR.ViewModels;

namespace CAL_QR.Views.Dialogs
{
    public partial class DeviceTypeFormDialog : Window
    {
        public DeviceTypeFormDialog()
        {
            InitializeComponent();
            
            if (App.ServiceProvider != null)
            {
                var vm = App.ServiceProvider.GetRequiredService<DeviceTypeFormViewModel>();
                DataContext = vm;
                
                vm.CloseWindowAction = () =>
                {
                    try
                    {
                        this.DialogResult = vm.DialogResult;
                    }
                    catch (InvalidOperationException)
                    {
                        // Fallback if not shown as dialog
                    }
                    this.Close();
                };
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.DialogResult = false;
            }
            catch (InvalidOperationException) { }
            this.Close();
        }
    }
}
