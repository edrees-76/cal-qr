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

        // يُملأ في ConfigureServices إن كان db_path.txt يشير إلى قاعدة غير موجودة أو تعذّرت قراءته.
        private string? _dbPathProblem;

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

                // أيّ استثناء غير معالَج يُسجَّل في الملفّ ويُعرض للمستخدم بلا تتبّع المكدّس.
                DispatcherUnhandledException += (s, args) =>
                {
                    AppLog.Error("DispatcherUnhandledException", args.Exception);
                    MessageBox.Show(
                        "حدث خطأ غير متوقّع. سُجّلت التفاصيل في ملفّ السجلّ:\n" + AppLog.LogDirectory +
                        "\n\nالرسالة: " + args.Exception.Message,
                        "خطأ في التطبيق", MessageBoxButton.OK, MessageBoxImage.Error);
                    args.Handled = true;
                };
                AppDomain.CurrentDomain.UnhandledException += (s, args) =>
                {
                    if (args.ExceptionObject is Exception unhandled)
                        AppLog.Error("AppDomain.UnhandledException", unhandled);
                };
                System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, args) =>
                {
                    AppLog.Error("UnobservedTaskException", args.Exception);
                    args.SetObserved();
                };
                var serviceCollection = new ServiceCollection();
                ConfigureServices(serviceCollection);

                if (_dbPathProblem != null)
                {
                    // لا نُنشئ قاعدة فارغة بصمت: ذلك يُعيد عدّاد الشهادات من الصفر على قاعدة جديدة.
                    AppLog.Warn(_dbPathProblem);
                    MessageBox.Show(
                        _dbPathProblem +
                        "\n\nأعد توصيل القرص/المجلّد الذي فيه قاعدة البيانات ثمّ شغّل البرنامج مجدّداً." +
                        "\nإن كنت تريد فعلاً العودة إلى المسار الافتراضيّ فاحذف الملفّ db_path.txt من مجلّد البرنامج." +
                        "\nلم يُغيَّر شيء في بياناتك.",
                        "قاعدة البيانات غير متاحة", MessageBoxButton.OK, MessageBoxImage.Error,
                        MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                    Shutdown();
                    return;
                }

                ServiceProvider = serviceCollection.BuildServiceProvider();

                // خطّاف نشاط عامّ لكلّ نوافذ التطبيق — يُسجَّل مرّة واحدة هنا.
                ServiceProvider.GetRequiredService<IdleLockService>().AttachGlobalActivityHooks();

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

        /// <summary>
        /// true بعد أن شغّل البرنامج نسخةً جديدة من نفسه (بعد التصفير الكامل أو تغيير المسارات)
        /// وقبل إغلاق هذه النسخة: إغلاق النوافذ حينها ليس «خروجاً» يطلبه المستخدم، فلا يُسأل
        /// «هل تريد الخروج؟» ولا تُفتح نافذة دخول من النسخة القديمة (والنسخة الجديدة تفتح دخولها).
        /// </summary>
        public static bool IsRestarting { get; private set; }

        /// <summary>يُستدعى بعد نجاح تشغيل النسخة الجديدة وقبل Shutdown مباشرةً.</summary>
        public static void MarkRestarting() => IsRestarting = true;

        /// <summary>
        /// يُعيد تشغيل البرنامج لنفسه: يحرّر أقفال SQLite وقفل النسخة الواحدة، يُطلق نسخة جديدة،
        /// ثمّ يرفع IsRestarting ويغلق الحاليّة. يُلقي إن تعذّر إيجاد المسار التنفيذيّ أو تشغيله،
        /// ولا يغلق الحاليّة في تلك الحالة؛ على المستدعي أن يعرض الرسالة المناسبة.
        /// </summary>
        public static void RestartApplication()
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            // تحرير قفل النسخة الواحدة قبل إطلاق النسخة الجديدة، وإلا رأت
            // القفل محجوزاً من هذه العملية ورفضت العمل.
            ReleaseSingleInstanceMutex();

            var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath))
            {
                throw new InvalidOperationException("تعذر العثور على المسار التنفيذي للتطبيق.");
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                UseShellExecute = true
            });
            MarkRestarting();
            Current.Shutdown();
        }

        /// <summary>
        /// يُحرّر قفل النسخة الواحدة صراحةً قبل إعادة تشغيل التطبيق لنفسه
        /// (بعد تغيير المسارات أو التصفير الكامل). بدونه تُقلع النسخة الجديدة
        /// بينما لا يزال القفل محجوزاً من العملية القديمة التي لم تُغلق بعد،
        /// فتظهر «المنظومة تعمل بالفعل» وتُغلق النسخة الجديدة فوراً.
        ///
        /// آمن: يُستدعى من خيط الواجهة نفسه الذي أنشأ الـMutex في OnStartup،
        /// و ReleaseMutex يتطلّب المالك نفسه. مغلّف بـ try لأن التحرير المتكرر
        /// أو على mutex غير مملوك يُلقي، ولا نريد كسر إعادة التشغيل بسبب ذلك.
        /// </summary>
        public static void ReleaseSingleInstanceMutex()
        {
            try
            {
                _singleInstanceMutex?.ReleaseMutex();
                _singleInstanceMutex?.Dispose();
                _singleInstanceMutex = null;
            }
            catch
            {
                // تحرير على mutex غير مملوك/مُتخلَّص منه — لا يضر، نتجاهله.
            }
        }

        private void ConfigureServices(IServiceCollection services)
        {
            string dbPath = CAL_QR.Helpers.AppPaths.DefaultDbPath();

            string? configPathFile = CAL_QR.Helpers.AppPaths.FindPointerFile();
            if (configPathFile != null)
            {
                try
                {
                    string savedPath = File.ReadAllText(configPathFile).Trim();
                    if (!string.IsNullOrWhiteSpace(savedPath))
                    {
                        dbPath = savedPath;
                        if (!File.Exists(savedPath))
                        {
                            _dbPathProblem = $"ملفّ قاعدة البيانات المحدَّد في db_path.txt غير موجود:\n{savedPath}";
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppLog.Error($"Failed to read db_path.txt ({configPathFile})", ex);
                    _dbPathProblem = $"تعذّرت قراءة الملفّ db_path.txt:\n{configPathFile}\n{ex.Message}";
                }
            }

            // SQLite لا تنشئ المجلّدات: تُنشأ هنا (مجلّد بيانات ProgramData في التثبيت الجديد). لا يُنشأ
            // شيء إن كان المسار المحدَّد في المؤشّر ناقصاً، فالإقلاع سيتوقّف برسالة قبل أيّ قاعدة جديدة.
            if (_dbPathProblem == null)
            {
                CAL_QR.Helpers.AppPaths.EnsureDirectory(Path.GetDirectoryName(dbPath));
            }

            services.AddDbContextFactory<CalQrDbContext>(options =>
                options.UseSqlite($"Data Source={dbPath};Default Timeout=5"));

            // Register Services
            services.AddSingleton<IHmacService, HmacService>();
            services.AddSingleton<IQrService, QrService>();
            services.AddSingleton<IPrintService, PrintService>();
            services.AddSingleton<IExportService, ExportService>();
            services.AddSingleton<ICertificatePdfService, CertificatePdfService>();
            services.AddSingleton<IBackupPasswordStore, DpapiBackupPasswordStore>();
            services.AddSingleton<IBackupService, BackupService>();
            services.AddSingleton<ISearchService, SearchService>();
            services.AddSingleton<ICurrentUserService, CurrentUserService>();
            services.AddSingleton<ICertificateNumberService, CertificateNumberService>();
            services.AddSingleton<IRecoveryAnswerService, RecoveryAnswerService>();
            services.AddSingleton<ICertificateSignatureService, CertificateSignatureService>();
            services.AddSingleton<IdleLockService>();
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
            services.AddTransient<SignedCopyViewModel>();
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
            services.AddTransient<Views.Dialogs.SignedCopyDialog>();
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
            services.AddTransient<Func<Views.Dialogs.SignedCopyDialog>>(provider => () => provider.GetRequiredService<Views.Dialogs.SignedCopyDialog>());
            services.AddTransient<Func<Views.Dialogs.OwnerDetailDialog>>(provider => () => provider.GetRequiredService<Views.Dialogs.OwnerDetailDialog>());
            services.AddTransient<Func<Views.Dialogs.DeviceTypeFormDialog>>(provider => () => provider.GetRequiredService<Views.Dialogs.DeviceTypeFormDialog>());
            services.AddTransient<Func<Views.Dialogs.DeviceTypeDetailDialog>>(provider => () => provider.GetRequiredService<Views.Dialogs.DeviceTypeDetailDialog>());
            services.AddTransient<Func<Views.Dialogs.UserFormDialog>>(provider => () => provider.GetRequiredService<Views.Dialogs.UserFormDialog>());
            services.AddTransient<Func<Views.Dialogs.PaperTemplateDialog>>(provider => () => provider.GetRequiredService<Views.Dialogs.PaperTemplateDialog>());
        }
    }
}
