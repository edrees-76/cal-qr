using System;
using System.Windows;
using System.Windows.Controls;
using materialDesign = MaterialDesignThemes.Wpf;

namespace CAL_QR.Views.Tabs
{
    public partial class HelpView : UserControl
    {
        public HelpView()
        {
            InitializeComponent();
        }

        private void HelpSectionsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ContentContainer == null || TxtHeaderTitle == null || HeaderIcon == null || HelpScrollViewer == null) 
                return;

            int index = HelpSectionsListBox.SelectedIndex;
            if (index < 0) return;

            // Reset scroll to top
            HelpScrollViewer.ScrollToTop();

            // Toggle visibility of panels
            for (int i = 0; i < ContentContainer.Children.Count; i++)
            {
                ContentContainer.Children[i].Visibility = (i == index) ? Visibility.Visible : Visibility.Collapsed;
            }

            // Update Header Icon and Text
            ListBoxItem selectedItem = (ListBoxItem)HelpSectionsListBox.SelectedItem;
            TxtHeaderTitle.Text = selectedItem.Content.ToString() ?? "";

            switch (index)
            {
                case 0:
                    HeaderIcon.Kind = materialDesign.PackIconKind.Xml;
                    break;
                case 1:
                    HeaderIcon.Kind = materialDesign.PackIconKind.LockOutline;
                    break;
                case 2:
                    HeaderIcon.Kind = materialDesign.PackIconKind.ViewDashboardOutline;
                    break;
                case 3:
                    HeaderIcon.Kind = materialDesign.PackIconKind.Devices;
                    break;
                case 4:
                    HeaderIcon.Kind = materialDesign.PackIconKind.Certificate;
                    break;
                case 5:
                    HeaderIcon.Kind = materialDesign.PackIconKind.Qrcode;
                    break;
                case 6:
                    HeaderIcon.Kind = materialDesign.PackIconKind.AccountGroupOutline;
                    break;
                case 7:
                    HeaderIcon.Kind = materialDesign.PackIconKind.FileExportOutline;
                    break;
                case 8:
                    HeaderIcon.Kind = materialDesign.PackIconKind.CloudUploadOutline;
                    break;
                case 9:
                    HeaderIcon.Kind = materialDesign.PackIconKind.History;
                    break;
                case 10:
                    HeaderIcon.Kind = materialDesign.PackIconKind.AlertCircleOutline;
                    break;
                case 11:
                    HeaderIcon.Kind = materialDesign.PackIconKind.ClipboardListOutline;
                    break;
                case 12:
                    HeaderIcon.Kind = materialDesign.PackIconKind.ShieldCheckOutline;
                    break;
                case 13:
                    HeaderIcon.Kind = materialDesign.PackIconKind.CellphoneNfc;
                    break;
                default:
                    HeaderIcon.Kind = materialDesign.PackIconKind.HelpCircleOutline;
                    break;
            }
        }
    }
}
