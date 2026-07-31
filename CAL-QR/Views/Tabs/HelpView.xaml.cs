using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using CAL_QR.ViewModels;
using materialDesign = MaterialDesignThemes.Wpf;

namespace CAL_QR.Views.Tabs
{
    public partial class HelpView : UserControl
    {
        public HelpView()
        {
            InitializeComponent();

            if (App.ServiceProvider != null)
            {
                DataContext = App.ServiceProvider.GetRequiredService<HelpViewModel>();
            }
        }

        /// <summary>
        /// ╔══════════════════════════════════════════════════════════════════╗
        /// ║  الربط بالاسم لا بالفهرس — إصلاح عطل كامن.                      ║
        /// ╚══════════════════════════════════════════════════════════════════╝
        /// كانت الحلقة تُظهر ContentContainer.Children[i] حيث i فهرس عنصر
        /// القائمة، فتربط ترتيب اللوحات بترتيب العناوين ربطاً ضمنياً. إخفاء
        /// عنصر قائمة واحد (قسم مفتاح التوقيع لغير المدير) كان **يزيح كل ما
        /// بعده** فيعرض القسم الخطأ تحت العنوان الصحيح — عطل صامت لا يُلقي
        /// استثناءً ولا يظهر في اختبار بناء.
        ///
        /// الآن كل عنصر قائمة يحمل Tag باسم لوحته، والمطابقة بالاسم. تغيير
        /// الترتيب أو إخفاء عنصر لم يعد يكسر شيئاً.
        /// </summary>
        private void HelpSectionsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ContentContainer == null || TxtHeaderTitle == null || HeaderIcon == null || HelpScrollViewer == null)
                return;

            if (HelpSectionsListBox.SelectedItem is not ListBoxItem selectedItem)
                return;

            string? targetPanelName = selectedItem.Tag as string;
            if (string.IsNullOrEmpty(targetPanelName))
                return;

            HelpScrollViewer.ScrollToTop();

            foreach (UIElement child in ContentContainer.Children)
            {
                bool isTarget = child is FrameworkElement fe && fe.Name == targetPanelName;
                child.Visibility = isTarget ? Visibility.Visible : Visibility.Collapsed;
            }

            TxtHeaderTitle.Text = selectedItem.Content?.ToString() ?? string.Empty;
            HeaderIcon.Kind = IconFor(targetPanelName);

            // مغادرة قسم مفتاح التوقيع تُعيد قفله: الفتح لا يبقى قائماً بعد
            // الانصراف عنه، وإلا كفت محاولة واحدة لفتحه بقية الجلسة.
            if (targetPanelName != "PanelSigningKey" && DataContext is HelpViewModel vm)
            {
                vm.Relock();
            }
        }

        private static materialDesign.PackIconKind IconFor(string panelName) => panelName switch
        {
            "Panel1" => materialDesign.PackIconKind.Xml,
            "Panel2" => materialDesign.PackIconKind.LockOutline,
            "Panel3" => materialDesign.PackIconKind.ViewDashboardOutline,
            "Panel4" => materialDesign.PackIconKind.Devices,
            "Panel5" => materialDesign.PackIconKind.Certificate,
            "Panel6" => materialDesign.PackIconKind.Qrcode,
            "Panel7" => materialDesign.PackIconKind.AccountGroupOutline,
            "Panel8" => materialDesign.PackIconKind.FileExportOutline,
            "Panel9" => materialDesign.PackIconKind.CloudUploadOutline,
            "Panel10" => materialDesign.PackIconKind.History,
            "Panel11" => materialDesign.PackIconKind.AlertCircleOutline,
            "Panel12" => materialDesign.PackIconKind.ClipboardListOutline,
            "Panel13" => materialDesign.PackIconKind.ShieldCheckOutline,
            "Panel14" => materialDesign.PackIconKind.CellphoneNfc,
            "PanelRecovery" => materialDesign.PackIconKind.AlertOctagon,
            "PanelSigningKey" => materialDesign.PackIconKind.KeyChain,
            _ => materialDesign.PackIconKind.HelpCircleOutline
        };

        /// <summary>
        /// PasswordBox لا يكشف Password كـDependencyProperty، فلا يُربط بـBinding.
        /// التمرير عبر الحدث نمط قائم في المشروع (SettingsView.xaml.cs).
        /// </summary>
        private void TxtSigningKeyPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is HelpViewModel vm)
            {
                vm.EnteredPassword = ((PasswordBox)sender).Password;
            }
        }
    }
}
