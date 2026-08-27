using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using CAL_QR.ViewModels;

namespace CAL_QR.Views.Dialogs
{
    public partial class SignedCopyDialog : Window
    {
        public SignedCopyDialog()
        {
            InitializeComponent();

            if (App.ServiceProvider != null)
            {
                var vm = App.ServiceProvider.GetRequiredService<SignedCopyViewModel>();
                DataContext = vm;

                vm.CloseWindowAction = (result) =>
                {
                    try
                    {
                        this.DialogResult = result;
                    }
                    catch (InvalidOperationException)
                    {
                        // DialogResult can only be set when window is shown as dialog.
                        // If shown normally, we just close.
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
