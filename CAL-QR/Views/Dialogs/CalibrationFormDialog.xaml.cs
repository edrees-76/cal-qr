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

        protected override void OnSourceInitialized(System.EventArgs e)
        {
            base.OnSourceInitialized(e);
            System.IntPtr handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            System.Windows.Interop.HwndSource.FromHwnd(handle)?.AddHook(WindowProc);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
            }
            else
            {
                WindowState = WindowState.Maximized;
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                MaximizeButton_Click(sender, e);
            }
            else if (e.ChangedButton == MouseButton.Left)
            {
                if (WindowState != WindowState.Maximized)
                {
                    DragMove();
                }
            }
        }

        #region Win32 Interop to prevent maximizing over taskbar

        private System.IntPtr WindowProc(System.IntPtr hwnd, int msg, System.IntPtr wParam, System.IntPtr lParam, ref bool handled)
        {
            if (msg == 0x0024) // WM_GETMINMAXINFO
            {
                WmGetMinMaxInfo(hwnd, lParam);
                handled = true;
            }
            return System.IntPtr.Zero;
        }

        private void WmGetMinMaxInfo(System.IntPtr hwnd, System.IntPtr lParam)
        {
            var obj = System.Runtime.InteropServices.Marshal.PtrToStructure(lParam, typeof(MINMAXINFO));
            if (obj == null) return;
            MINMAXINFO mmi = (MINMAXINFO)obj;

            System.IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);

            if (monitor != System.IntPtr.Zero)
            {
                MONITORINFO monitorInfo = new MONITORINFO();
                if (GetMonitorInfo(monitor, monitorInfo))
                {
                    RECT rcWorkArea = monitorInfo.rcWork;
                    RECT rcMonitorArea = monitorInfo.rcMonitor;

                    mmi.ptMaxSize.x = System.Math.Abs(rcWorkArea.Right - rcWorkArea.Left);
                    mmi.ptMaxSize.y = System.Math.Abs(rcWorkArea.Bottom - rcWorkArea.Top);
                    mmi.ptMaxPosition.x = System.Math.Abs(rcWorkArea.Left - rcMonitorArea.Left);
                    mmi.ptMaxPosition.y = System.Math.Abs(rcWorkArea.Top - rcMonitorArea.Top);
                }
            }

            System.Runtime.InteropServices.Marshal.StructureToPtr(mmi, lParam, true);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(System.IntPtr hMonitor, [System.Runtime.InteropServices.In, System.Runtime.InteropServices.Out] MONITORINFO lpmi);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern System.IntPtr MonitorFromWindow(System.IntPtr hwnd, uint dwFlags);

        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private class MONITORINFO
        {
            public int cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(MONITORINFO));
            public RECT rcMonitor = new RECT();
            public RECT rcWork = new RECT();
            public int dwFlags = 0;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        #endregion

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
