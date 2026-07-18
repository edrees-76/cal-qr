using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using CAL_QR.ViewModels;

namespace CAL_QR.Views.Tabs
{
    public partial class UsersView : UserControl
    {
        public UsersView()
        {
            InitializeComponent();

            if (App.ServiceProvider != null)
            {
                var vm = App.ServiceProvider.GetRequiredService<UsersViewModel>();
                DataContext = vm;

                this.Loaded += async (s, e) =>
                {
                    await vm.LoadUsersAsync();
                    await vm.LoadAuditLogsAsync();
                };
            }
        }
    }
}
