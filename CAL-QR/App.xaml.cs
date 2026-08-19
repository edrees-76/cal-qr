using System;
using System.IO;
using System.Threading;
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

        // قفل النسخة الواحدة: يُبقى static ليعيش طوال عمر التطبيق (وإلا جمعه GC
        // فأُفرِج القفل باكراً). الاسم ثابت وفريد للمنظومة. الغرض انضباط تشغيليّ
        // لا حماية بيانات — تخصيص الأرقام ذرّيّ وآمن للتزامن أصلاً (AllocateAsync).
        private static Mutex? _singleInstanceMutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                // قبل أيّ بناء: إن كانت نسخة تعمل، أبلِغ المستخدم وأغلِق هذه النسخة.
                // createdNew=false ⇒ الـMutex مملوك لنسخة أخرى حيّة.
                _singleInstanceMutex = new Mutex(initiallyOwned: true, "CAL-QR-SingleInstance-Mutex", out bool createdNew);
                if (!createdNew)
                {
                    MessageBox.Show(
                        "المنظومة تعمل بالفعل في نافذة أخرى.",
                        "تنبيه",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information,
                        MessageBoxResult.OK,
                        MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                    Shutdown();
                    return;
                }

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

                // Initialize HMAC key rotation service
                var hmacService = ServiceProvider.GetRequiredService<IHmacService>();
                hmacService.Initialize();

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

            string configPathFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "db_path.txt");
            if (File.Exists(configPathFile))
            {
                try
                {
                    string savedPath = File.ReadAllText(configPathFile).Trim();
                    if (!string.IsNullOrWhiteSpace(savedPath))
                    {
                        dbPath = savedPath;
                    }
                }
                catch { }
            }

            services.AddDbContextFactory<CalQrDbContext>(options =>
                options.UseSqlite($"Data Source={dbPath};Default Timeout=5"));

            // Register Services
            services.AddSingleton<IHmacService, HmacService>();
            services.AddSingleton<IQrService, QrService>();
            services.AddSingleton<IPrintService, PrintService>();
            services.AddSingleton<IExportService, ExportService>();
            services.AddSingleton<ICertificatePdfService, CertificatePdfService>();
            services.AddSingleton<IBackupService, BackupService>();
            services.AddSingleton<ISearchService, SearchService>();
            services.AddSingleton<ICurrentUserService, CurrentUserService>();
            services.AddSingleton<ICertificateNumberService, CertificateNumberService>();
            services.AddSingleton<ICertificateSignatureService, CertificateSignatureService>();
            // بانِي المسوّدة صرف بلا حالة ولا DbContext ⇒ Singleton آمن.
            services.AddSingleton<ICertificateDraftBuilder, CertificateDraftBuilder>();

            // Register Repositories
            services.AddSingleton<IOwnerRepository, OwnerRepository>();
            services.AddSingleton<IDeviceTypeRepository, DeviceTypeRepository>();
            services.AddSingleton<IDeviceRepository, DeviceRepository>();
            services.AddSingleton<ICalibrationRepository, CalibrationRepository>();
            services.AddSingleton<IAttachmentRepository, AttachmentRepository>();
            services.AddSingleton<IPaperTemplateRepository, PaperTemplateRepository>();
            services.AddSingleton<IAuditLogRepository, AuditLogRepository>();
            services.AddSingleton<IUserRepository, UserRepository>();
            services.AddSingleton<ICertificateRepository, CertificateRepository>();

            // Register ViewModels
            services.AddTransient<MainViewModel>();
            services.AddTransient<OwnerViewModel>();
            services.AddTransient<DeviceTypeViewModel>();
            services.AddTransient<DevicesViewModel>();
            services.AddTransient<CalibrationFormViewModel>();
            services.AddTransient<CertificateFormViewModel>();
            services.AddTransient<DeviceDetailViewModel>();
            services.AddTransient<QrVerifyViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<PaperTemplateViewModel>();
            services.AddTransient<PrintPreviewViewModel>();
            services.AddTransient<ReportsViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<OwnerFormViewModel>();
            services.AddTransient<OwnerDetailViewModel>();
            services.AddTransient<DeviceTypeFormViewModel>();
            services.AddTransient<DeviceTypeDetailViewModel>();
            services.AddTransient<UsersViewModel>();
            services.AddTransient<UserFormViewModel>();
            services.AddTransient<HelpViewModel>();

            // Register Windows
            services.AddTransient<SplashWindow>();
            services.AddTransient<LoginWindow>();
            services.AddTransient<MainWindow>();
            services.AddTransient<Views.ScreensaverWindow>();
            services.AddTransient<Views.Dialogs.CalibrationFormDialog>();
            services.AddTransient<Views.Dialogs.CertificateFormDialog>();
            services.AddTransient<Views.Dialogs.DeviceDetailDialog>();
            services.AddTransient<Views.Dialogs.PaperTemplateDialog>();
            services.AddTransient<Views.Dialogs.PrintPreviewDialog>();
            services.AddTransient<Views.Dialogs.AlertPopupDialog>();
            services.AddTransient<Views.FirstRunWizard>();
            services.AddTransient<Views.Dialogs.OwnerFormDialog>();
            services.AddTransient<Views.Dialogs.OwnerDetailDialog>();
            services.AddTransient<Views.Dialogs.DeviceTypeFormDialog>();
            services.AddTransient<Views.Dialogs.DeviceTypeDetailDialog>();
            services.AddTransient<Views.Dialogs.UserFormDialog>();
            // Register Dialog Factories to support Constructor Injection
            services.AddTransient<Func<Views.Dialogs.DeviceDetailDialog>>(provider => () => provider.GetRequiredService<Views.Dialogs.DeviceDetailDialog>());
            services.AddTransient<Func<Views.Dialogs.CalibrationFormDialog>>(provider => () => provider.GetRequiredService<Views.Dialogs.CalibrationFormDialog>());
            services.AddTransient<Func<Views.Dialogs.CertificateFormDialog>>(provider => () => provider.GetRequiredService<Views.Dialogs.CertificateFormDialog>());
            // مصنع الـViewModel: يحقنه مُنشئ CertificateFormDialog بدل App.ServiceProvider.
            services.AddTransient<Func<CertificateFormViewModel>>(provider => () => provider.GetRequiredService<CertificateFormViewModel>());
            services.AddTransient<Func<Views.Dialogs.OwnerFormDialog>>(provider => () => provider.GetRequiredService<Views.Dialogs.OwnerFormDialog>());
            services.AddTransient<Func<Views.Dialogs.OwnerDetailDialog>>(provider => () => provider.GetRequiredService<Views.Dialogs.OwnerDetailDialog>());
            services.AddTransient<Func<Views.Dialogs.DeviceTypeFormDialog>>(provider => () => provider.GetRequiredService<Views.Dialogs.DeviceTypeFormDialog>());
            services.AddTransient<Func<Views.Dialogs.DeviceTypeDetailDialog>>(provider => () => provider.GetRequiredService<Views.Dialogs.DeviceTypeDetailDialog>());
            services.AddTransient<Func<Views.Dialogs.UserFormDialog>>(provider => () => provider.GetRequiredService<Views.Dialogs.UserFormDialog>());
            services.AddTransient<Func<Views.Dialogs.PaperTemplateDialog>>(provider => () => provider.GetRequiredService<Views.Dialogs.PaperTemplateDialog>());
        }
    }
}
