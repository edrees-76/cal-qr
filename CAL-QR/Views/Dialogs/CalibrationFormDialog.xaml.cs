using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using CAL_QR.ViewModels;

namespace CAL_QR.Views.Dialogs
{
    public partial class CalibrationFormDialog : Window
    {
        public CalibrationFormDialog()
        {
            InitializeComponent();
            if (App.ServiceProvider != null)
            {
                var vm = App.ServiceProvider.GetRequiredService<CalibrationFormViewModel>();
                DataContext = vm;
                vm.CloseWindowAction = Close;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void AttachmentItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBox listBox && listBox.SelectedItem is AttachmentItem item)
            {
                // Prevent opening if the user double-clicked on a button (like Delete) inside the row
                if (e.OriginalSource is DependencyObject depObj)
                {
                    DependencyObject parent = depObj;
                    while (parent != null && parent != listBox)
                    {
                        if (parent is Button)
                        {
                            return;
                        }
                        parent = VisualTreeHelper.GetParent(parent);
                    }
                }

                OpenFile(item);
            }
        }

        private void OpenFile(AttachmentItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.FullPath))
            {
                MessageBox.Show("مسار الملف غير صالح.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!System.IO.File.Exists(item.FullPath))
            {
                MessageBox.Show($"الملف غير موجود في المسار المحدد:\n{item.FullPath}", "الملف غير موجود", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo(item.FullPath)
                {
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                MessageBox.Show($"لا يوجد برنامج مرتبط لفتح هذا النوع من الملفات على جهازك، أو تم حظر التشغيل.\nتفاصيل الخطأ: {ex.Message}", "فشل فتح الملف", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ غير متوقع أثناء محاولة فتح الملف:\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
