using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using CAL_QR.ViewModels;

namespace CAL_QR.Views.Dialogs
{
    public partial class UserFormDialog : Window
    {
        public UserFormDialog()
        {
            InitializeComponent();

            if (App.ServiceProvider != null)
            {
                var vm = App.ServiceProvider.GetRequiredService<UserFormViewModel>();
                DataContext = vm;

                vm.CloseWindowAction = (result) =>
                {
                    try
                    {
                        this.DialogResult = result;
                    }
                    catch (InvalidOperationException)
                    {
                        // Handle shown non-dialog case
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

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is UserFormViewModel vm)
            {
                // Pass PasswordBox text securely before invoking Save
                vm.Password = TxtPassword.Password;
                if (vm.SaveCommand.CanExecute(null))
                {
                    vm.SaveCommand.Execute(null);
                }
            }
        }
    }
}
