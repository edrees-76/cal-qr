using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace CAL_QR.Views
{
    public partial class ScreensaverWindow : Window
    {
        public event EventHandler? Dismissed;

        private readonly DispatcherTimer _delayTimer;
        private bool _canDismiss = false;
        private bool _isDismissed = false;

        public ScreensaverWindow()
        {
            InitializeComponent();

            // Set up a 250ms delay timer before allowing MouseMove to dismiss the screensaver.
            // This prevents immediate dismissal if the cursor is already over the window on display.
            _delayTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _delayTimer.Tick += DelayTimer_Tick;

            Loaded += ScreensaverWindow_Loaded;
            PreviewMouseMove += ScreensaverWindow_PreviewMouseMove;
            PreviewKeyDown += ScreensaverWindow_PreviewKeyDown;
            PreviewMouseDown += ScreensaverWindow_PreviewMouseDown;
        }

        private void ScreensaverWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Activate the window and transfer focus to root Grid to reliably capture keystrokes.
            this.Activate();
            RootGrid.Focus();

            _delayTimer.Start();
        }

        private void DelayTimer_Tick(object? sender, EventArgs e)
        {
            _delayTimer.Stop();
            _canDismiss = true;
        }

        private void ScreensaverWindow_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_canDismiss)
            {
                Dismiss();
            }
        }

        private void ScreensaverWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Keyboard keys always trigger dismissal immediately
            Dismiss();
        }

        private void ScreensaverWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Clicks always trigger dismissal immediately
            Dismiss();
        }

        private void Dismiss()
        {
            if (_isDismissed) return;
            _isDismissed = true;

            // Stop the delay timer explicitly to release resources
            _delayTimer.Stop();

            Dismissed?.Invoke(this, EventArgs.Empty);
        }
    }
}
