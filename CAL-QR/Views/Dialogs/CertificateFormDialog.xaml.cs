using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CAL_QR.ViewModels;

namespace CAL_QR.Views.Dialogs
{
    public partial class CertificateFormDialog : Window
    {
        private readonly CertificateFormViewModel _viewModel;

        // يُرفع فقط حين يغلق الـViewModel النافذة بعد حفظ/إصدار/إلغاء ناجح:
        // ذلك المسار لا يسأل عن تغييرات غير محفوظة أبداً.
        private bool _closeWithoutPrompt;

        /// <summary>
        /// الـViewModel يصل عبر مصنع محقون لا عبر App.ServiceProvider: الحوار لا يعرف
        /// حاوية الخدمات، فيبقى قابلاً للإنشاء في اختبار بلا إقلاع تطبيق كامل.
        /// </summary>
        public CertificateFormDialog(Func<CertificateFormViewModel> viewModelFactory)
        {
            if (viewModelFactory == null) throw new ArgumentNullException(nameof(viewModelFactory));

            InitializeComponent();

            _viewModel = viewModelFactory();
            DataContext = _viewModel;
            _viewModel.CloseWindowAction = CloseWithoutPrompt;
            Closing += Window_Closing;
        }

        /// <summary>
        /// إغلاق بلا سؤال «تغييرات غير محفوظة». يستعمله الـViewModel بعد نجاح الحفظ/الإصدار/الإلغاء،
        /// وتستعمله النافذة الرئيسيّة حين تنتهي الجلسة قسراً (قفل خمول أو خروج): سؤال حوار
        /// ينتظر إجابة شخص غادر كان يُبقي الجلسة مكشوفة ويوقف القفل.
        /// </summary>
        public void CloseWithoutPrompt()
        {
            _closeWithoutPrompt = true;
            Close();
        }

        /// <summary>
        /// نقطة واحدة تغطّي زرّي الإغلاق/الإلغاء وزرّ X وAlt+F4: كلّها تمرّ بـClosing.
        /// السؤال يظهر فقط إذا وُجدت تغييرات غير محفوظة ولم يكن الإغلاق من مسار النجاح.
        /// </summary>
        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_closeWithoutPrompt || !_viewModel.HasUnsavedChanges) return;

            var answer = MessageBox.Show(
                this,
                "هناك تغييرات غير محفوظة. هل تريد تجاهلها وإغلاق النموذج؟",
                "تغييرات غير محفوظة",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No,
                MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);

            if (answer != MessageBoxResult.Yes) e.Cancel = true;
        }

        /// <summary>يُكشف ليشترك المستدعي في CertificateIssued قبل العرض.</summary>
        public CertificateFormViewModel ViewModel => _viewModel;

        public void LoadForRecord(int calibrationRecordId) => _viewModel.LoadForRecord(calibrationRecordId);

        public void LoadForStatusReport(int calibrationRecordId) => _viewModel.LoadForStatusReport(calibrationRecordId);

        public void LoadForEdit(int certificateId) => _viewModel.LoadForEdit(certificateId);

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

        private void CancelButton_Click(object sender, RoutedEventArgs e)
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

        /// <summary>
        /// مغادرة أيّ حقل بيئيّ فارغًا تُعيد هيكل الكتابة (__ ± __ °C).
        /// الحدث على الـGrid لا على كلّ TextBox: LostFocus صاعد فمعالِج واحد يكفي،
        /// ولا يتكرّر الربط ثلاث مرّات في XAML.
        /// </summary>
        private void EnvironmentFields_LostFocus(object sender, RoutedEventArgs e)
        {
            if (DataContext is CertificateFormViewModel vm)
                vm.RestoreEnvironmentTemplatesIfEmpty();
        }

        private void DataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            e.Row.Header = (e.Row.GetIndex() + 1).ToString();
        }

        /// <summary>
        /// تحرير خليّة في أيّ جدول تعتمد عليه فحوص التطابق يعيد حسابها.
        /// BeginInvoke ضروريّ: القيمة لا تصل إلى الكائن قبل انتهاء التحرير،
        /// فالاستدعاء المباشر يقرأ القيمة السابقة.
        /// يخدم جدول الفحوص الوظيفيّة — كان بلا ربط، فبقي شريط التحذير على قيمته
        /// لحظة الفتح مهما كتب المستخدم. جدول النتائج له ResultsGrid_CellEditEnding
        /// أدناه، ويستدعي الفحوص نفسها.
        /// </summary>
        private void ConsistencyGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (DataContext is CertificateFormViewModel vm)
                {
                    vm.MarkDirty();
                    vm.RunTemplateConsistencyChecks();
                }
            }), DispatcherPriority.Background);
        }

        /// <summary>
        /// جدول النتائج يغذّي أمرين: فحوص التطابق، وتسمية CF/CFavg (رأس عمود الملخّص
        /// ومعادلة القراءة المصحّحة) التي تتبع عدد صفوف كلّ نظير. BeginInvoke للسبب نفسه
        /// المذكور أعلاه.
        /// </summary>
        private void ResultsGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (DataContext is CertificateFormViewModel vm)
                {
                    vm.MarkDirty();
                    vm.RunTemplateConsistencyChecks();
                    vm.RefreshCorrectionFactorLabels();
                }
            }), DispatcherPriority.Background);
        }

        /// <summary>
        /// جدول ملخّص النويدات: اسم النظير وقيمته يحدّدان أيّ النظائر تُطبع وتسميتها.
        /// </summary>
        private void NuclideSummaryGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (DataContext is CertificateFormViewModel vm)
                {
                    vm.MarkDirty();
                    vm.RefreshCorrectionFactorLabels();
                }
            }), DispatcherPriority.Background);
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
    }
}
