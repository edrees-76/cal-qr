using System;
using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CAL_QR.Data;
using CAL_QR.Views;
using CAL_QR.ViewModels;
using CAL_QR.Repositories;
using CAL_QR.Services;

namespace CAL_QR
{
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                base.OnStartup(e);

                // Prevent app from auto-closing when transitioning between windows
                ShutdownMode = ShutdownMode.OnExplicitShutdown;

                // Catch any unhandled exceptions and show them instead of silently crashing
                DispatcherUnhandledException += (s, args) =>
                {
                    MessageBox.Show($"خطأ غير متوقع:\n{args.Exception.Message}\n\n{args.Exception.StackTrace}",
                        "خطأ في التطبيق", MessageBoxButton.OK, MessageBoxImage.Error);
                    args.Handled = true;
                };
                var serviceCollection = new ServiceCollection();
                ConfigureServices(serviceCollection);

                ServiceProvider = serviceCollection.BuildServiceProvider();

                // Run database migrations and seed settings
                using (var scope = ServiceProvider.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<CalQrDbContext>();
                    DatabaseMigrator.RunMigrations(context);
                }

                // Start scheduled backup checks
                var backupService = ServiceProvider.GetRequiredService<IBackupService>();
                backupService.StartScheduledBackupTimer();

                var splashWindow = ServiceProvider.GetRequiredService<SplashWindow>();
                splashWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء تشغيل التطبيق في OnStartup:\n{ex.Message}\n\n{ex.StackTrace}", 
                    "خطأ حرج في الإقلاع", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private void ConfigureServices(IServiceCollection services)
        {
            string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cal-qr-simulation.db");

            services.AddDbContextFactory<CalQrDbContext>(options =>
                options.UseSqlite($"Data Source={dbPath}"));

            // Register Services
            services.AddSingleton<IHmacService, HmacService>();
            services.AddSingleton<IQrService, QrService>();
            services.AddSingleton<IPrintService, PrintService>();
            services.AddSingleton<IExportService, ExportService>();
            services.AddSingleton<IBackupService, BackupService>();
            services.AddSingleton<ISearchService, SearchService>();

            // Register Repositories
            services.AddSingleton<IOwnerRepository, OwnerRepository>();
            services.AddSingleton<IDeviceTypeRepository, DeviceTypeRepository>();
            services.AddSingleton<IDeviceRepository, DeviceRepository>();
            services.AddSingleton<ICalibrationRepository, CalibrationRepository>();
            services.AddSingleton<IAttachmentRepository, AttachmentRepository>();
            services.AddSingleton<IPaperTemplateRepository, PaperTemplateRepository>();
            services.AddSingleton<IAuditLogRepository, AuditLogRepository>();

            // Register ViewModels
            services.AddTransient<MainViewModel>();
            services.AddTransient<OwnerViewModel>();
            services.AddTransient<DeviceTypeViewModel>();
            services.AddTransient<DevicesViewModel>();
            services.AddTransient<CalibrationFormViewModel>();
            services.AddTransient<DeviceDetailViewModel>();
            services.AddTransient<QrVerifyViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<PaperTemplateViewModel>();
            services.AddTransient<PrintPreviewViewModel>();
            services.AddTransient<ReportsViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<AuditLogViewModel>();

            // Register Windows
            services.AddTransient<SplashWindow>();
            services.AddTransient<LoginWindow>();
            services.AddTransient<MainWindow>();
            services.AddTransient<Views.Dialogs.CalibrationFormDialog>();
            services.AddTransient<Views.Dialogs.DeviceDetailDialog>();
            services.AddTransient<Views.Dialogs.PaperTemplateDialog>();
            services.AddTransient<Views.Dialogs.PrintPreviewDialog>();
            services.AddTransient<Views.Dialogs.AlertPopupDialog>();
            services.AddTransient<Views.FirstRunWizard>();
            // Register Dialog Factories to support Constructor Injection
            services.AddTransient<Func<Views.Dialogs.DeviceDetailDialog>>(provider => () => provider.GetRequiredService<Views.Dialogs.DeviceDetailDialog>());
            services.AddTransient<Func<Views.Dialogs.CalibrationFormDialog>>(provider => () => provider.GetRequiredService<Views.Dialogs.CalibrationFormDialog>());
        }
    }
}
