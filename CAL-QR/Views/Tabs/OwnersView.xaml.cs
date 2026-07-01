using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using CAL_QR.ViewModels;

namespace CAL_QR.Views.Tabs
{
    public partial class OwnersView : UserControl
    {
        public OwnersView()
        {
            InitializeComponent();
            if (App.ServiceProvider != null)
            {
                DataContext = App.ServiceProvider.GetRequiredService<OwnerViewModel>();
                Loaded += (s, e) => _ = ((OwnerViewModel)DataContext).LoadDataAsync();
            }
        }
    }
}
