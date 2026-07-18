using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CAL_QR.Data;

namespace CAL_QR.Views
{
    public partial class SplashWindow : Window
    {
        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;

        public SplashWindow(IDbContextFactory<CalQrDbContext> contextFactory)
        {
            InitializeComponent();
            _contextFactory = contextFactory;
            Loaded += SplashWindow_Loaded;
        }

        private async void SplashWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateProgress(20, "تحميل إعدادات النظام...");
                await Task.Delay(500);

                UpdateProgress(50, "فحص وقراءة قاعدة البيانات...");
                await Task.Run(() =>
                {
                    using (var context = _contextFactory.CreateDbContext())
                    {
                        DatabaseMigrator.RunMigrations(context);
                    }
                });

                UpdateProgress(80, "تحميل مكونات الواجهة...");
                await Task.Delay(500);

                UpdateProgress(100, "اكتمل التشغيل بنجاح.");
                await Task.Delay(300);

                // Check if this is the first run using unified source of truth helper
                bool isFirstRun = DetermineFirstRun(AppDomain.CurrentDomain.BaseDirectory, _contextFactory);

                if (isFirstRun)
                {
                    var wizard = App.ServiceProvider.GetRequiredService<FirstRunWizard>();
                    wizard.Show();
                }
                else
                {
                    var loginWindow = App.ServiceProvider.GetRequiredService<LoginWindow>();
                    loginWindow.Show();
                }
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء تشغيل التطبيق:\n{ex.Message}", "خطأ في التشغيل", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        private void UpdateProgress(double value, string statusText)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressBar.Value = value;
                TxtStatus.Text = statusText;
            });
        }

        public static bool DetermineFirstRun(string baseDirectory, IDbContextFactory<CalQrDbContext> contextFactory)
        {
            string configPathFile = System.IO.Path.Combine(baseDirectory, "db_path.txt");
            
            if (System.IO.File.Exists(configPathFile))
            {
                try
                {
                    string savedPath = System.IO.File.ReadAllText(configPathFile).Trim();
                    if (!string.IsNullOrWhiteSpace(savedPath) && System.IO.File.Exists(savedPath))
                    {
                        // db_path.txt exists and points to an existing database file.
                        return false;
                    }
                }
                catch { }
            }
            else
            {
                string defaultDbPath = System.IO.Path.Combine(baseDirectory, "cal-qr-simulation.db");
                if (!System.IO.File.Exists(defaultDbPath))
                {
                    // Neither db_path.txt nor the default database exists. This is a fresh install.
                    return true;
                }
            }

            // Fallback: Check the database setting itself
            try
            {
                using (var context = contextFactory.CreateDbContext())
                {
                    var firstRunSetting = context.AppSettings.AsNoTracking().FirstOrDefault(s => s.Key == "FirstRunCompleted");
                    return firstRunSetting == null || string.IsNullOrEmpty(firstRunSetting.Value);
                }
            }
            catch
            {
                return true;
            }
        }
    }
}
