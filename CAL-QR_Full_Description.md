# CAL-QR — الوصف الكامل للبرنامج
## وثيقة التنفيذ لـ Antigravity IDE
> المصمم والمطور: م. إدريس فتح الله الهري
> التاريخ: 2026-06-30
> آخر تحديث لحالة التنفيذ: 2026-06-30 (بعد إنجاز المراحل 1-9 + إصلاحات End-to-End — مرحلة واحدة متبقية)

---

## ⚠️ حالة التنفيذ الحالية — اقرأ هذا أولاً قبل أي عمل

> هذا القسم يُحدَّث بعد كل مرحلة. عند بدء أي جلسة جديدة، اقرأه أولاً لمعرفة أين توقف العمل بالضبط قبل كتابة أي كود.

### خريطة المراحل
```
✅ المرحلة 1  — البنية التحتية والأساس (منجزة ومعتمدة)
✅ المرحلة 2  — الشاشات الأساسية (منجزة ومعتمدة)
✅ المرحلة 3  — إدارة البيانات CRUD (منجزة ومعتمدة)
✅ المرحلة 4  — QR والتوقيع الرقمي والتحقق (منجزة ومعتمدة)
✅ المرحلة 5  — الطباعة والقوالب (منجزة ومعتمدة)
✅ المرحلة 6  — Dashboard والتنبيهات (منجزة ومعتمدة)
✅ المرحلة 7  — التقارير والتصدير (منجزة ومعتمدة)
✅ المرحلة 8  — الإعدادات والنسخ الاحتياطي وسجل العمليات (منجزة ومعتمدة)
✅ إصلاح End-to-End تشغيل فعلي (مكتملة خارج الخطة)
✅ المرحلة 9  — عن البرنامج + First Run Wizard (منجزة ومعتمدة)
🔲 المرحلة 10 — الاختبارات والتلميع والنشر ← التالية والأخيرة
```

### ما تم إنجازه فعلياً (تفصيل)

**المرحلة 1 ✅**
- Solution كامل (`CAL-QR` + `CAL-QR.Tests`)، جميع NuGet Packages مثبتة + `Microsoft.Extensions.DependencyInjection`
- 8/8 Models، `CalQrDbContext.cs` مع Indexes وRelations كاملة، `DatabaseMigrator.cs` بـ `ExecuteSqlIfColumnMissing`
- `BaseViewModel.cs`، `RelayCommand.cs`، `DateHelper.cs`، `FileHelper.cs`، `PasswordHelper.cs`
- الشعاران منقولان إلى `Assets/Logo/cal-qr-logo.png` و `Assets/Logo/nuclear-center-logo.png`
- اختبار: `UnitTest1.cs` (ليس `DatabaseTests.cs`) — Passed: 2/2

**المرحلة 2 ✅**
- `SplashWindow` (خلفية Navy + شعار ذهبي + شريط تحميل + Background Task لا يجمّد UI)
- `LoginWindow` (RTL + Cairo + Lockout 3 محاولات/30 ثانية + كلمة ماستر `EdreesElhery` + توقيع المطور)
- `MainWindow` + `MainViewModel` (9 تبويبات + Header بشعارين + Banner تنبيهات + إخفاء/إظهار تبويبات بزر عائم ذهبي)
- **Inactivity Timer منفّذ بالكامل**: `DispatcherTimer` كل 5 ثوانٍ، يقرأ `AutoLockMinutes` من DB، `LockRequested` event، `ResetInactivity()` مربوطة بـ `PreviewMouseMove`/`PreviewKeyDown` في `MainWindow.xaml.cs`
- خط Cairo مدمج من Google Fonts، ملفات `Strings.ar-SA.resx` / `Strings.en-US.resx`

**المرحلة 3 ✅**
- **7/7 Repositories كاملة** — كلها مسجلة `Singleton`، تحمل `IDbContextFactory<CalQrDbContext>` فقط (وليس DbContext مباشرة)، تفتح Context جديد بـ `using var context = await _contextFactory.CreateDbContextAsync()` لكل عملية، `AsNoTracking()` صارم في كل قراءة
- `OwnersView` + `OwnerViewModel` (CRUD + منع حذف جهة لها أجهزة)
- `DeviceTypesView` + `DeviceTypeViewModel` (CRUD + منع حذف نوع له أجهزة)
- `DevicesView` + `DevicesViewModel`: جدول/بطاقات قابلة للتبديل، بحث متقدم كامل، Pagination (10/20/50)، ألوان حالة (`#E8F5E9` أخضر / `#FFFDE7` أصفر / `#FFEBEE` أحمر)
- `CalibrationFormDialog` + `CalibrationFormViewModel`: AutoComplete للجهات/المهندسين، فحص تكرار الرقم التسلسلي مع تحذير وربط تلقائي، فحص `ExpiryDate > CalibrationDate`، مرفقات بمسار `Attachments/[رقم الشهادة]/[اسم الملف]`
- `DeviceDetailDialog` + `DeviceDetailViewModel`: Timeline بصري ملون (أخضر/أحمر/أصفر) لتاريخ المعايرات، فتح المرفقات بالبرنامج الافتراضي
- Build نهائي: `0 Warning(s), 0 Error(s)`

**المرحلة 4 ✅**
- `IHmacService`/`HmacService`: المفتاح الثابت `"CalQR-Nuclear-Center-2026-SecretKey"`، دالة موحَّدة `BuildConcatenatedString(certNo, model, serial, ownerName, calDate, expDate, result, engineerName)` تُستخدم داخلياً في كل من `ComputeSignature` و`VerifySignature` لمنع تكرار المنطق، كلا الدالتين تستقبلان **قيماً نصية خاماً** (وليس كائنات `CalibrationRecord`/`Device`/`Owner`) لتعمل بنفس الشكل من التوليد والتحقق، مع استخدام **Named Parameters** صراحة في كل استدعاء داخلي لتفادي خطأ تبديل الحقول المتشابهة النوع (مثل `calDate`/`expDate`)
- تفاصيل دفاعية إضافية في الكود الفعلي: `?.Trim()` على كل حقل قبل البناء (يحمي من فروقات المسافات)، `StringComparison.OrdinalIgnoreCase` في المقارنة (يقبل كود التحقق بأي حالة أحرف)
- `IQrService`/`QrService`: QRCoder مع `ErrorCorrectionLevel.H`، دمج `nuclear-center-logo.png`، دالة شاملة واحدة `GenerateAndSaveQrForRecord(...)` تبني النص وتولّد الصورة وتحفظها دفعة واحدة (بدل استدعاءات منفصلة قد تتفكك)، أحجام 200/400/600/1200px، حفظ في `QR/[رقم الشهادة].png`
- `CalibrationFormViewModel`: **تم ربط HMAC الحقيقي فعلياً** — لم تعد القيمة placeholder كما في المرحلة 3، أصبحت تستدعي `ComputeSignature` و`GenerateAndSaveQrForRecord` الحقيقيتين عند الحفظ
- `QrVerifyView`/`QrVerifyViewModel`: تبويبتان (لصق نص + تحليل تلقائي / إدخال يدوي للحقول)، بانر أخضر "✅ شهادة أصلية" أو أحمر "❌ تحذير: التوقيع الرقمي غير مطابق!" مع عرض الكود المكتوب مقابل المحسوب، مُدرَج فعلياً في `MainWindow.xaml` مكان الـ placeholder
- اختبارات: `HmacServiceTests.cs` + `QrServiceTests.cs` — **7/7 Passed** (Test Run Successful, 1.58s)
- Build نهائي: `0 Warning(s), 0 Errors(s)`

**المرحلة 5 ✅**
- `IPrintService`/`PrintService`: قراءة الطابعات المثبتة عبر `System.Drawing.Printing.PrinterSettings.InstalledPrinters`، `PrintDialog` + `DrawingVisual` للرسم، تحويل مم→بكسل بمعدل `1mm = 3.7795px @ 96 DPI`، `PrintQrLabel` (ملصق فردي) و`PrintMultipleQrLabels` (متعددة، مرتبة برقم الشهادة) — **الدالة الثانية مكتملة برمجياً لكن بدون واجهة مستخدم تستدعيها بعد (تأجيل متعمَّد وموثَّق لتفادي توسيع نطاق هذه المرحلة)**
- `QrPrintJob`: تحمل `BitmapSource QrImage, PaperTemplate Template, string PrinterName, int StartColumn, int StartRow, string CertificateNumber, string DeviceInfoText`
- `PaperTemplateDialog`/`PaperTemplateViewModel`: نموذج كامل لإنشاء/تعديل قوالب الورق (نوع، أبعاد، أعمدة/صفوف، أبعاد الملصق، هوامش وفراغات)، ربط مع `PaperTemplateRepository` الموجود من المرحلة 3، تفعيل `IsDefault` يُبطل تلقائياً افتراضية بقية القوالب
- `PrintPreviewDialog`/`PrintPreviewViewModel`: **يستقبل `calibrationRecordId` كـ Parameter صريح** عند الفتح من `DevicesView` أو `DeviceDetailDialog`، ويُحمِّل `CalibrationRecord` مع `Device`/`Owner`/`DeviceType` بـ `AsNoTracking()` و`Include`/`ThenInclude`؛ معاينة Canvas تفاعلية ترسم شبكة الملصقات حسب القالب المختار — **التظليل الرمادي للخلايا "السابقة" هو تمثيل بصري فقط لاختيار المستخدم نفسه عبر النقر، وليس تتبعاً تلقائياً من النظام لاستخدام سابق**؛ حفظ آخر طابعة وقالب مستخدمين عبر `AppSettings` (`LastPrinterName`, `DefaultTemplateId`)
- **إصلاح أمان مهم أثناء هذه المرحلة**: ظهرت تحذيرات `CS8602` (Dereference of a possibly null reference) على `CalibrationRecord.Device` في `PrintPreviewViewModel.cs` — تم إصلاحها بمعالجة مزدوجة: `r.Device!` لحل تحذير الكمبايلر فقط، بالإضافة إلى حماية فعلية وقت التشغيل بـ `if (CalibrationRecord != null)` و`?.`/`??` على كل خاصية فرعية (`CalibrationRecord.Device?.Owner?.Name ?? ""`) لمنع انهيار حقيقي في حال سجل معايرة يتيم أو معرّف غير صحيح
- اختبارات: `PaperTemplateRepositoryTests.cs` (يغطي CRUD + قاعدة "قالب افتراضي واحد فقط") — **9/9 Passed** (1.62s)
- Build نهائي: `0 Warning(s), 0 Error(s)` (بعد إصلاح التحذيرات الأربعة المذكورة أعلاه)

**المرحلة 6 ✅**
- `DashboardView`/`DashboardViewModel`: تخطيط Grid ثلاثي الصفوف (4 بطاقات إحصائية + Donut Chart توزيع الجهات + Bar Chart الحالات الثلاث + جدول الأجهزة القريبة من الانتهاء) — يظهر بالكامل في شاشة واحدة بدون Scroll عمودي، مع تكرار فعلي لضبط التخطيط (`Viewed`/`Edited DashboardView.xaml` مرتين) للوصول لهذا التناسب البصري
- `CalibrationEvents.cs` (في `Helpers/`): حدث استاتيكي `CalibrationChanged` يُطلَق من **مصدرين منفصلين** — `CalibrationFormViewModel` بعد كل حفظ/تعديل ناجح، **و**`DevicesViewModel` بعد كل حذف ناعم ناجح — `DashboardViewModel` يشترك فيه عند الإنشاء ويُعيد التحميل فوراً حتى لو كان التبويب مفتوحاً بالفعل في الخلفية (وليس فقط عند "تفعيل التبويب")
- نظام التنبيهات الثلاثي الكامل: **Banner** في `MainWindow` مربوط ببيانات حقيقية من `CalibrationRepository` برسالة "يوجد X جهاز سينتهي خلال Y يوم | Z جهاز منتهي الصلاحية"، مع زر `×` يُخزِّن الحالة في `IsBannerDismissed` (متغيّر `bool` بسيط في الذاكرة داخل `MainViewModel`، **بدون أي حفظ في AppSettings أو قاعدة البيانات** — يعود للظهور تلقائياً عند إعادة فتح البرنامج)؛ **Badge** دائري برقم على تبويب "🏠 الرئيسية"؛ **AlertPopupDialog** يظهر مرة واحدة بتسلسل دقيق موثَّق: إغلاق `LoginWindow` ← `MainWindow.Show()` ← فحص التنبيهات ← فتح `AlertPopupDialog` بـ `Owner = MainWindow` و`ShowDialog()`
- استخدام `AlertDaysThreshold` من `AppSettings` كحد قابل للتعديل (وليس رقماً ثابتاً مكتوباً في الكود) — ينعكس تلقائياً على Tab وBanner وBadge معاً عند تغييره
- اختبارات: `DashboardViewModelTests.cs` — **10/10 Passed** (2.12s)
- Build نهائي: `0 Warning(s), 0 Error(s)`

**المرحلة 7 ✅**
- `IExportService`/`ExportService`: `ExportToExcelAsync` بـ ClosedXML (`worksheet.RightToLeft = true`)، `ExportToPdfAsync` بـ QuestPDF؛ **ملاحظة فنية مهمة**: الخطة الأولية اقترحت `.ContentDirection(ContentDirection.RightToLeft)` لكن هذا لم يكن الـ API الصحيح فعلياً — اكتُشف ذلك أثناء التنفيذ (بحث ويب فعلي عن الطريقة الصحيحة)، والـ API الصحيح المُعتمَد فعلياً هو **`.ContentFromRightToLeft()`**؛ ترويسة رسمية ثابتة في `page.Header` لكل صفحة + `page.Footer` بترقيم الصفحات؛ نوعا تقرير `Detailed` (كل الحقول) و`Summary` (الحقول الأساسية)
- **تحقق بصري فعلي وليس آلياً فقط**: بعد نجاح `dotnet build`/`dotnet test`، تم فتح ملف PDF الناتج فعلياً ومعاينته بصرياً (`Listed directory` + `Viewed test_summary.pdf`) للتأكد أن RTL يعمل فعلياً على المستوى البصري وليس فقط أن الكود يُجمَّع بنجاح — تم تأكيد: محاذاة صحيحة من اليمين لليسار، حروف عربية متصلة وغير معكوسة، جدول يبدأ من اليمين (رقم الشهادة) وينتهي يساراً (الحالة)، Footer "صفحة X من Y" يعمل بشكل صحيح
- `ReportsView`/`ReportsViewModel`: فلترة بـ ComboBox الجهة المالكة (AutoComplete فعلي بخصائص `IsEditable="True" IsReadOnly="False" TextSearch.TextPath="Name"` مطابقة لنمط `CalibrationFormDialog`) + ComboBox النوع + DatePickers من/إلى + ComboBox الحالة؛ **إعادة استخدام صريحة** لـ `IOwnerRepository`/`IDeviceTypeRepository` الموجودين من المرحلة 3 (بدون أي استعلام مكرر)؛ `MatchingCount` يُحسَب بآلية **Debounce 300ms** (إلغاء الطلب القديم تلقائياً عند تغيير سريع متتالٍ للفلاتر) بدل استعلام فوري مع كل تغيير
- التصدير غير المُجمِّد: `Async`/`await` فعلي + `IsLoading`/`ProgressBar` بصري أثناء الكتابة
- اختبارات: `ExportServiceTests.cs` (يتحقق من `File.Exists` لكل من Excel وPDF، Detailed وSummary) — **11/11 Passed** (2.38-2.50s عبر تشغيلات متعددة)
- Build نهائي: `0 Warning(s), 0 Error(s)`

**المرحلة 9 ✅**
- `AboutView.xaml`: محتوى ثابت بالكامل في XAML بدون ViewModel منفصل — شعار CAL-QR + شعار مركز البحوث الذهبي + بيانات الجهة الكاملة (4 مستويات) + بيانات المطور م. إدريس فتح الله الهري + حقوق النشر © 2026؛ تصميم RTL + Cairo + ألوان Navy/Gold
- `FirstRunWizard.xaml`/`.xaml.cs`: 4 خطوات (ترحيب → مسار DB → كلمة المرور + سؤال سري → إتمام)؛ **Atomic Save** في نقطة واحدة فقط — زر "بدء الاستخدام" في الخطوة 4 يكتب `PasswordHash + DatabasePath + SecurityQuestion + SecurityAnswer + FirstRunCompleted="true"` دفعة واحدة لقاعدة البيانات؛ الخطوة 3 تتحقق فقط من صحة البيانات (تطابق كلمتي المرور، طول ≥ 4، عدم فراغ السؤال) دون أي كتابة؛ `IDbContextFactory<CalQrDbContext>` فقط عبر Constructor Injection
- **إصلاح ذاتي أثناء التنفيذ**: `Border` لا يقبل إلا طفلاً واحداً (Single Child) — اكتُشف تلقائياً وحُوِّل إلى `Grid` لاستيعاب 4 لوحات خطوات بجوار بعضها
- `DatabaseMigrator.cs`: إضافة 3 مفاتيح Seed جديدة (`FirstRunCompleted`, `SecurityQuestion`, `SecurityAnswer`)
- `SplashWindow.xaml.cs`: كشف أول تشغيل بعد `DatabaseMigrator.RunMigrations()` — إذا `FirstRunCompleted` فارغة → فتح `FirstRunWizard`؛ إذا `"true"` → فتح `LoginWindow` كالمعتاد
- **تحقق استباقي من الأصول** (`Listed directory Logo`) قبل البناء — تطبيق الدرس المستخلَص من المراحل السابقة (التحقق قبل البناء وليس بعد الفشل)
- Build نهائي: `0 Errors, 0 Warnings`
- `IBackupService`/`BackupService`: استخدام **SQLite Backup API الحقيقي** (`sourceConnection.BackupDatabase(destinationConnection)`) وليس `File.Copy` ساذج — يأخذ لقطة متناسقة من قاعدة البيانات الحية دون أقفال؛ ضغط DB + مجلد Attachments في ZIP واحد باسم `CalQR_Backup_YYYY-MM-DD_HH-mm.zip`؛ الاحتفاظ بآخر 10 نسخ فقط (حذف الأقدم تلقائياً)؛ `StartScheduledBackupTimer()` عبر `System.Threading.Timer` كل ساعة بفحص Daily/Weekly/Monthly
- **درس تقني مهم مُوثَّق من BackupService** (تطلّب ثلاث محاولات تعديل متتالية): ①  `SqliteConnection.ClearAllPools()` يجب استدعاؤها **مباشرة بعد انتهاء BackupDatabase وقبل الضغط** — وليس في كتلة `finally` فقط — لأن Connection Pool يبقي قفلاً على الملف المؤقت حتى بعد إغلاق الاتصال الظاهري، ومحاولة ضغطه قبل تحرير الـ Pool تُفشل عملية القراءة ② توحيد فواصل المسارات داخل ZIP بـ `.Replace('\\', '/')` إلزامياً لأن `Path.Combine` على Windows ينتج `\` بينما معايير ZIP وأدوات البحث تتوقع `/`
- **الاستعادة الحية (Live Restore)**: SQLite Backup API بالمعكوس — يستعيد مباشرة إلى قاعدة البيانات النشطة دون إغلاق التطبيق
- `SettingsView`/`SettingsViewModel`: تغيير كلمة المرور (تحقق من الحالية أولاً + SHA256 hashing)؛ تحديث `AlertDaysThreshold`/`AutoLockMinutes` بـث `CalibrationEvents.CalibrationChanged` فوراً (**`MainViewModel` بقي `Transient` كما هو** — التحديث عبر Events وليس عبر Singleton لتفادي تسريب بيانات جلسة بين تسجيل خروج ودخول)؛ `Microsoft.Win32.OpenFolderDialog` (.NET 8 أصلي) بدل WinForms القديمة؛ رسالة إعادة تشغيل عند تغيير مسارات النظام
- `AuditLogView`/`AuditLogViewModel`: تسجيل فعلي من **6 نقاط** (CalibrationForm، Devices، PrintPreview، Reports، Settings، Backup)؛ فلترة بتاريخ ونوع عملية؛ ترتيب تنازلي تلقائي
- اختبارات: `BackupServiceTests.cs` + `AuditLogRepositoryTests.cs` — **13/13 Passed** (2.1-2.2s عبر تشغيلات متعددة)
- Build نهائي: `0 Warning(s), 0 Error(s)`

**إصلاحات End-to-End (تشغيل فعلي خارج الخطة) ✅**
- اكتُشفت مشكلتان عند التشغيل الفعلي للتطبيق (لم تظهرا في أي `dotnet build`/`dotnet test` سابق):
- ① **`MaterialDesignTheme.Defaults.xaml` ملغى في MaterialDesignThemes 5.x**: كان `App.xaml` يستورده من الإصدار القديم فيُفشل إقلاع التطبيق — الإصلاح: استبداله بـ `MaterialDesign2.Defaults.xaml` المعتمد في الإصدار 5.x
- ② **`ShutdownMode` الخاطئ يُغلق التطبيق عند الانتقال بين النوافذ**: `ShutdownMode.OnLastWindowClose` الافتراضي يُغلق التطبيق لحظة إغلاق `LoginWindow` (آخر نافذة ظاهرة وقتها) قبل أن تُفتح `MainWindow` — الإصلاح: ضبط `ShutdownMode = ShutdownMode.OnExplicitShutdown` في `App.xaml.cs` مع استدعاء `Application.Current.Shutdown()` صراحة عند إغلاق `MainWindow`
- **درس مستخلَص**: `dotnet build`/`dotnet test` ضروريان لكن غير كافيَين وحدهما — هناك فئة كاملة من الأخطاء لا تظهر إلا عند تشغيل التطبيق فعلياً بنوافذه الحقيقية (WPF Resources، Window Lifecycle، Application Startup sequence)

### ملاحظات هيكلية يجب معرفتها قبل المتابعة
1. اسم ملف الاختبار قد يظهر بأسماء مختلفة بين الجلسات (`UnitTest1.cs` أو `DatabaseTests.cs`) — لا يؤثر على النتائج، الأهم هو عدد الاختبارات الناجحة الذي يُعرض دائماً في الملخص.
2. النمط المعتمد لكل Repository (طبّقه حرفياً على أي Repository جديد):
   ```csharp
   public class XRepository : IXRepository
   {
       private readonly IDbContextFactory<CalQrDbContext> _contextFactory;
       public XRepository(IDbContextFactory<CalQrDbContext> contextFactory) => _contextFactory = contextFactory;

       public async Task<IEnumerable<X>> GetAllAsync()
       {
           using var context = await _contextFactory.CreateDbContextAsync();
           return await context.Xs.AsNoTracking().Where(x => !x.IsDeleted).ToListAsync();
       }
   }
   ```
3. **النمط المعتمد للخدمات التي تُستخدم في اتجاهين (توليد + تحقق)**، كما في `HmacService`: الدوال تستقبل **قيماً نصية خاماً** (primitives) بدل كائنات قاعدة البيانات، مع استخدام **Named Parameters** إلزامياً في كل استدعاء لتفادي تبديل الحقول المتشابهة النوع. طبّق نفس النمط على أي خدمة مستقبلية تحتاج التحقق من بيانات مُدخلة يدوياً أو ملصوقة من المستخدم وليس فقط من كائنات قاعدة البيانات.
4. جميع الـ Repositories والـ ViewModels والخدمات المنجزة مسجلة في `App.xaml.cs` — راجعه قبل إضافة أي تسجيل جديد لتفادي التكرار.
5. `CalibrationFormViewModel` الآن مرتبط فعلياً بـ `IHmacService` و`IQrService` الحقيقيين — لا توجد قيم placeholder متبقية في مسار التوقيع أو توليد QR.
6. **النمط المعتمد إلزامياً عند `Include`/`ThenInclude` على خصائص Navigation قابلة للـ null** (مثل `CalibrationRecord.Device`): لا يكفي إسكات تحذير الكمبايلر بـ `!` (null-forgiving operator) وحده — يجب إضافة حماية فعلية وقت التشغيل أيضاً: فحص `if (entity != null)` قبل الاستخدام، بالإضافة إلى `?.` و`??` على كل خاصية فرعية محتملة الغياب (مثال موثَّق من الكود الفعلي: `CalibrationRecord.Device?.Owner?.Name ?? ""`). إسكات التحذير وحده دون الحماية الفعلية يُبقي خطر `NullReferenceException` قائماً بصمت في حالات مثل سجل يتيم (orphaned record) أو معرّف غير صحيح يُمرَّر للـ ViewModel.
7. **مهمة معلّقة موثَّقة من المرحلة 5**: `PrintMultipleQrLabels` في `IPrintService`/`PrintService` مكتملة برمجياً بالكامل لكنها **بدون أي واجهة مستخدم تستدعيها بعد** (لا يوجد تحديد متعدد للأجهزة في `DevicesView`). هذا تأجيل متعمَّد لتفادي توسيع نطاق المرحلة 5 — يجب الرجوع لهذه النقطة عند التخطيط لمرحلة لاحقة إن أراد المستخدم تفعيل الطباعة المتعددة فعلياً عبر الواجهة.
8. **النمط المعتمد للتحديث الفوري بين ViewModels منفصلة** (Live Updates)، كما في `CalibrationEvents.cs`: حدث استاتيكي بسيط في `Helpers/` (وليس مكتبة Messenger خارجية معقدة، نظراً لحجم التطبيق ومستخدم واحد فقط) يُطلَق من **كل نقطة تُغيِّر البيانات فعلياً** (وليس فقط نقطة واحدة) — في حالة `CalibrationChanged` كان لا بد من إطلاقه من مصدرين: `CalibrationFormViewModel` (حفظ/تعديل) **و**`DevicesViewModel` (حذف ناعم) معاً، وإلا تبقى شاشات أخرى (مثل Dashboard) تعرض بيانات قديمة بصمت بعد عمليات حذف رغم تحديثها الصحيح بعد عمليات حفظ. عند إضافة أي تبويب أو شاشة مستقبلية تحتاج تحديثاً فورياً، راجع **جميع** نقاط تعديل البيانات ذات الصلة (حفظ، تعديل، حذف) وليس نقطة واحدة فقط.
9. **تحذير معماري مهم من المرحلة 7**: أسماء الدوال أو الـ APIs المحددة في خطة التنفيذ المُوافَق عليها (حتى بعد عدة جولات مراجعة دقيقة) قد لا تكون دقيقة 100% فعلياً عند مكتبات خارجية ثالثة — مثال موثَّق: الخطة المعتمدة لـ QuestPDF افترضت `.ContentDirection(ContentDirection.RightToLeft)`، لكن الـ API الصحيح فعلياً اتضح أنه **`.ContentFromRightToLeft()`** بعد بحث ويب فعلي أثناء التنفيذ. **القاعدة المستخلصة**: عند أي مخرج بصري نهائي (PDF، صور، ملصقات مطبوعة)، لا يكفي التحقق من نجاح `dotnet build`/`dotnet test` فقط — يجب فتح الملف الناتج فعلياً ومعاينته بصرياً (كما حدث فعلياً مع `test_summary.pdf`) للتأكد أن الناتج صحيح للمستخدم النهائي، وليس فقط أن الكود يُجمَّع بدون أخطاء. طبّق هذا التحقق البصري الإضافي على أي مخرج مشابه في المراحل القادمة (مثلاً: تصدير شهادات، تقارير، أو أي محتوى عربي مُولَّد بمكتبات خارجية).
10. **درس SQLite Backup API وConnection Pooling (من المرحلة 8)**: عند استخدام `sourceConnection.BackupDatabase(destinationConnection)` لنسخ قاعدة بيانات SQLite حية، يجب استدعاء **`SqliteConnection.ClearAllPools()` مباشرة بعد انتهاء BackupDatabase وقبل أي عملية قراءة/ضغط للملف الناتج** — لأن ADO.NET Connection Pool يبقي قفلاً داخلياً على الملف المؤقت حتى بعد إغلاق الاتصال الظاهري. استدعاؤها في كتلة `finally` فقط (بعد الضغط) يُفشل عملية الضغط نفسها بخطأ "Process cannot access the file because it is being used by another process". كذلك يجب **توحيد فواصل مسارات ZIP بـ `.Replace('\\', '/')`** دائماً لأن `Path.Combine` على Windows ينتج `\` بينما معايير ZIP تتوقع `/`.
11. **درس WPF Application Lifecycle (من إصلاحات End-to-End)**: `dotnet build`/`dotnet test` ضروريان لكن غير كافيَين — هناك فئة كاملة من أخطاء WPF لا تظهر إلا عند تشغيل التطبيق فعلياً: ① موارد XAML المُلغاة في إصدارات جديدة (مثل `MaterialDesignTheme.Defaults.xaml` في MaterialDesignThemes 5.x) ② `ShutdownMode` الخاطئ الذي يُغلق التطبيق عند الانتقال بين نوافذ متعددة (Splash→Login→MainWindow). **الإصلاح الموثَّق**: `ShutdownMode = ShutdownMode.OnExplicitShutdown` في `App.xaml.cs` مع استدعاء `Application.Current.Shutdown()` صراحة في `MainWindow_Closed`.

---

## أولاً: نظرة عامة

اسم المشروع: **CAL-QR**
التقنية: **WPF / .NET 8 / MVVM / EF Core / SQLite**
نوع التطبيق: تطبيق سطح مكتب Windows لإدارة شهادات معايرة أجهزة المسح الإشعاعي

الهدف: بناء برنامج يُمكّن مهندس المعايرة في مركز البحوث النووية من:
1. تسجيل بيانات معايرة أجهزة المسح الإشعاعي في قاعدة بيانات تراكمية
2. توليد QR Code محمي بتوقيع HMAC-SHA256 لكل شهادة معايرة
3. طباعة QR على ملصقات بأحجام متنوعة ولصقها على شهادات المعايرة
4. التحقق من أصالة الشهادات ورصد أي تزوير
5. متابعة انتهاء صلاحية المعايرات وتصدير التقارير

---

## ثانياً: بيئة التطوير والمكتبات

```xml
<!-- NuGet Packages المطلوبة -->
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.*" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="8.*" />
<PackageReference Include="QRCoder" Version="1.*" />
<PackageReference Include="MaterialDesignThemes" Version="5.*" />
<PackageReference Include="LiveChartsCore.SkiaSharpView.WPF" Version="2.*" />
<PackageReference Include="ClosedXML" Version="0.102.*" />
<PackageReference Include="QuestPDF" Version="2024.*" />
<PackageReference Include="xunit" Version="2.*" />
<PackageReference Include="Moq" Version="4.*" />
```

**الخط المستخدم:** Cairo (يدعم العربية والإنجليزية)
**أيقونة البرنامج:** cal-qr-logo.ico (شعار CAL-QR)
**النشر:** Self-Contained (مدمج .NET Runtime)
**المثبّت:** Inno Setup

---

## ثالثاً: القواعد المعمارية الصارمة (لا استثناءات أبداً)

```
1. Constructor Injection فقط — ممنوع AppServiceProvider.Resolve في أي مكان
2. AsNoTracking() في جميع استعلامات القراءة بدون استثناء
3. IDbContextFactory<CalQrDbContext> في كل ViewModel
4. ExecuteSqlIfColumnMissing للترقية — ممنوع raw ALTER TABLE
5. decimal مع (double) cast لـ SQLite GroupBy
6. IsDeleted = true للحذف الناعم قبل الحذف النهائي
7. FlowDirection="RightToLeft" في جميع واجهات العربية
8. DateFormat: YYYY-MM-DD في كل مكان
9. Self-Contained Publish
10. جميع الـ Services تُعرَّف بـ Interface أولاً
```

---

## رابعاً: نموذج قاعدة البيانات الكامل

### CalQrDbContext.cs
```csharp
public class CalQrDbContext : DbContext
{
    public DbSet<Owner> Owners { get; set; }
    public DbSet<DeviceType> DeviceTypes { get; set; }
    public DbSet<Device> Devices { get; set; }
    public DbSet<CalibrationRecord> CalibrationRecords { get; set; }
    public DbSet<Attachment> Attachments { get; set; }
    public DbSet<PaperTemplate> PaperTemplates { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<AppSetting> AppSettings { get; set; }
}
```

### الجداول والحقول

**Owners (الجهات المالكة)**
```
Id (PK, auto) | Name (string, required) | Address (string, nullable) |
ContactPhone (string, nullable) | ContactPerson (string, nullable) |
IsDeleted (bool, default=false) | CreatedAt (datetime)
Index على: Name, IsDeleted
```

**DeviceTypes (أنواع الأجهزة)**
```
Id (PK, auto) | Name (string, required) | IsDeleted (bool) | CreatedAt (datetime)
Index على: Name, IsDeleted
```

**Devices (الأجهزة)**
```
Id (PK, auto) | Model (string, required) | SerialNumber (string, required) |
OwnerId (FK → Owners) | DeviceTypeId (FK → DeviceTypes) |
IsDeleted (bool, default=false) | CreatedAt (datetime)
Index على: SerialNumber, OwnerId, DeviceTypeId, IsDeleted
```

**CalibrationRecords (سجلات المعايرة)**
```
Id (PK, auto) | DeviceId (FK → Devices) | CertificateNumber (string, required, unique) |
CalibrationDate (date, required) | ExpiryDate (date, required) |
EngineerName (string, required) | CalibrationDescription (string, nullable) |
Result (string, required) — قيم: "Passed" | "Failed" | "Conditional" |
HmacSignature (string, required) | IsDeleted (bool, default=false) |
CreatedAt (datetime) | UpdatedAt (datetime)
Index على: DeviceId, ExpiryDate, CertificateNumber, IsDeleted
```

**Attachments (المرفقات)**
```
Id (PK, auto) | CalibrationRecordId (FK → CalibrationRecords) |
FileName (string) | FilePath (string) | FileExtension (string) | UploadedAt (datetime)
```

**PaperTemplates (قوالب الورق)**
```
Id (PK, auto) | TemplateName (string, required) | PaperType (string) |
PaperWidthMm (decimal) | PaperHeightMm (decimal) |
Columns (int) | Rows (int) |
LabelWidthMm (decimal) | LabelHeightMm (decimal) |
MarginTopMm (decimal) | MarginLeftMm (decimal) |
HorizontalGapMm (decimal) | VerticalGapMm (decimal) |
IsDefault (bool) | CreatedAt (datetime)
```

**AuditLog (سجل العمليات)**
```
Id (PK, auto) | Action (string) | EntityName (string) |
EntityId (string) | Details (string) | ActionAt (datetime)
```

**AppSettings (إعدادات التطبيق)**
```
Id (PK, auto) | Key (string, unique) | Value (string) | UpdatedAt (datetime)
```

### مفاتيح AppSettings الإلزامية عند التثبيت الأول
```
DatabasePath        = [مسار يختاره المستخدم]
AttachmentsPath     = [مسار يختاره المستخدم]\Attachments
QrOutputPath        = [مسار يختاره المستخدم]\QR
BackupPath          = [مسار يختاره المستخدم]\Backup
BackupSchedule      = Daily
AlertDaysThreshold  = 30
AutoLockMinutes     = 10
Language            = ar-SA
DefaultTemplateId   = 0
PasswordHash        = [SHA256 لكلمة المرور]
LastPrinterName     = [اسم الطابعة الأخيرة]
LastTemplateId      = 0
```

---

## خامساً: هيكل المشروع الكامل

```
CAL-QR/
├── CAL-QR.sln
├── CAL-QR/
│   ├── App.xaml + App.xaml.cs
│   ├── Models/ (8 ملفات)
│   ├── ViewModels/ (12 ملف)
│   ├── Views/
│   │   ├── SplashWindow.xaml
│   │   ├── LoginWindow.xaml
│   │   ├── MainWindow.xaml
│   │   ├── Tabs/ (8 Views)
│   │   └── Dialogs/ (5 Dialogs)
│   ├── Services/ (7 interfaces + implementations)
│   ├── Repositories/ (7 interfaces + implementations)
│   ├── Data/
│   │   ├── CalQrDbContext.cs
│   │   └── DatabaseMigrator.cs
│   ├── Helpers/
│   ├── Localization/ (ar-SA.resx + en-US.resx)
│   └── Assets/ (Logo + Icons)
├── CAL-QR.Tests/
└── Installer/setup.iss
```

---

## سادساً: تدفق تشغيل البرنامج

### عند أول تشغيل
```
1. SplashScreen يظهر (2-3 ثوانٍ) مع شعار CAL-QR + شريط تحميل
2. فحص وجود قاعدة البيانات في AppSettings
3. إذا لم تُعيَّن: معالج الإعداد الأول (First Run Wizard)
   - اختيار مسار قاعدة البيانات
   - إنشاء كلمة المرور
   - إنشاء سؤال سري للاسترداد
4. LoginWindow يظهر
5. MainWindow يفتح مع Dashboard
```

### عند كل تشغيل
```
1. SplashScreen (2-3 ثوانٍ)
2. DatabaseMigrator.RunMigrations() — ترقية آمنة
3. LoginWindow
4. MainWindow + تحميل Dashboard
5. فحص التنبيهات → عرض Popup + Banner + Badge
```

### الإغلاق التلقائي
```
- Timer يراقب عدم النشاط
- بعد AutoLockMinutes دقيقة: إغلاق تلقائي
- عند الضغط على X: رسالة تأكيد الإغلاق
```

---

## سابعاً: الواجهة الرئيسية (MainWindow)

### التصميم العام
```
الألوان:
  Primary (Navy Blue):  #1A3A6B
  Gold:                 #C9A227
  Success Green:        #2E7D32
  Warning Yellow:       #F9A825
  Error Red:            #C62828
  Background:           #F5F5F5

الخط: Cairo
الاتجاه: RTL بالعربية | LTR بالإنجليزية
```

### شريط العنوان (Header)
```
[شعار CAL-QR] | [شعار مركز البحوث الذهبي]
مركز البحوث النووية | إدارة الوقاية من الإشعاع
قسم قياس وتقدير الجرعات الشخصية والمعايرة | وحدة المعايرة
[🔍 بحث سريع شامل] [🌐 AR/EN] [اسم المستخدم] [تسجيل خروج]
```

### شريط التبويبات
```
[🏠 الرئيسية] [📋 السجلات] [✅ التحقق] [🏢 الجهات] [🔧 الأنواع]
[📊 التقارير] [📁 السجل] [⚙️ الإعدادات] [ℹ️ عن البرنامج]
                                          [↕️ إخفاء/إظهار التبويبات]
```

### شريط التنبيهات (Banner)
```
يظهر أسفل التبويبات عند وجود أجهزة منتهية أو قاربت الانتهاء
[⚠️ يوجد 3 أجهزة ستنتهي خلال 30 يوم | 1 جهاز منتهي الصلاحية] [×]
```

### اختصارات لوحة المفاتيح
```
Ctrl+N = سجل معايرة جديد
Ctrl+P = طباعة QR
Ctrl+F = تركيز على خانة البحث
Ctrl+B = نسخ احتياطي
Ctrl+E = تصدير
F1     = عن البرنامج
Esc    = إغلاق النافذة المنبثقة الحالية
Enter  = تأكيد/حفظ في النوافذ المنبثقة
Tab    = الانتقال بين الحقول
```

---

## ثامناً: وصف كل شاشة بالتفصيل

---

### 1. SplashWindow (شاشة البداية)

```
المدة: 2-3 ثوانٍ
المحتوى:
  - خلفية: Navy Blue (#1A3A6B)
  - شعار مركز البحوث النووية الذهبي (مركز الشاشة، حجم كبير)
  - شعار CAL-QR (أسفل الشعار الذهبي)
  - اسم البرنامج: "نظام معايرة أجهزة المسح الإشعاعي"
  - شريط تحميل أسفل الشاشة (Navy → Gold)
  - نص أسفل الشاشة: "م. إدريس فتح الله الهري | مركز البحوث النووية"
المهام في الخلفية:
  - DatabaseMigrator.RunMigrations()
  - تحميل AppSettings
  - التحقق من وجود قاعدة البيانات
```

---

### 2. LoginWindow (شاشة تسجيل الدخول)

```
المحتوى:
  - شعار CAL-QR (أعلى مركز)
  - عنوان: "تسجيل الدخول | Login"
  - حقل: كلمة المرور (Password)
  - زر: دخول
  - رابط: "نسيت كلمة المرور؟"
  - في الأسفل بخط صغير مميز:
    "تصميم وتنفيذ: م. إدريس فتح الله الهري"
المنطق:
  - التحقق من كلمة المرور بـ SHA256
  - كلمة مرور ماستر ثابتة: "EdreesElhery"
  - عند نسيان كلمة المرور: سؤال سري أو كلمة ماستر
  - 3 محاولات خاطئة: تأخير 30 ثانية
```

---

### 3. DashboardView (لوحة الإحصائيات)

```
يجب أن تظهر كاملة بدون تمرير (Scroll)

تخطيط الشاشة:
  ┌─────────────────────────────────────────────────┐
  │  [بطاقة: إجمالي الأجهزة]  [بطاقة: سارية]      │
  │  [بطاقة: ستنتهي قريباً]   [بطاقة: منتهية]      │
  ├──────────────────────────┬──────────────────────┤
  │  Donut Chart             │  Bar Chart            │
  │  (توزيع حسب الجهات)     │  (الحالات الثلاث)    │
  ├──────────────────────────┴──────────────────────┤
  │  قائمة الأجهزة التي ستنتهي قريباً               │
  │  [جهاز] [جهة] [تاريخ الانتهاء] [أيام متبقية]   │
  └─────────────────────────────────────────────────┘

البطاقات:
  - Navy Blue للإجمالي
  - Green للسارية
  - Yellow للقريبة من الانتهاء
  - Red للمنتهية

الرسوم البيانية: LiveCharts2
```

---

### 4. DevicesView (شاشة السجلات)

```
شريط الأدوات:
  [+ سجل جديد (Ctrl+N)] [🖨️ طباعة متعددة] [📤 تصدير] [🔍 بحث بسيط]
  [🔽 بحث متقدم] [عرض: جدول | بطاقات] [عدد في الصفحة: 10|20|50]

البحث المتقدم (قابل للإخفاء):
  [الجهة المالكة ▼] [نوع الجهاز ▼] [الموديل] [الرقم التسلسلي]
  [من تاريخ] [إلى تاريخ] [النتيجة ▼] [الحالة ▼] [بحث] [مسح]

الجدول — الأعمدة:
  □ | # | رقم الشهادة | الجهة المالكة | نوع الجهاز | الموديل |
  الرقم التسلسلي | تاريخ المعايرة | تاريخ الانتهاء | النتيجة |
  الحالة (🟢🟡🔴) | إجراءات (👁️ 🖨️ ✏️ 🗑️)

ألوان الصفوف:
  - 🟢 أخضر فاتح = سارية المفعول
  - 🟡 أصفر فاتح = ستنتهي ضمن AlertDaysThreshold
  - 🔴 أحمر فاتح = منتهية

عرض البطاقات:
  كل جهاز يظهر كبطاقة تحتوي المعلومات الأساسية + مؤشر الحالة الملون

Pagination:
  [← السابق] [1] [2] [3] ... [التالي →]  |  عرض 20 من 150 سجل

عند النقر على أيقونة 👁️: فتح DeviceDetailDialog
عند النقر على ✏️: فتح CalibrationFormDialog بوضع التعديل
عند النقر على 🗑️: حذف ناعم مع تأكيد
```

---

### 5. DeviceDetailDialog (تفاصيل الجهاز)

```
نافذة منبثقة كبيرة (800×600)

القسم الأول: معلومات الجهاز
  الجهة المالكة | نوع الجهاز | الموديل | الرقم التسلسلي
  تاريخ الإضافة

القسم الثاني: تاريخ المعايرات
  جدول يعرض جميع دورات المعايرة السابقة:
  [رقم الشهادة] [تاريخ المعايرة] [تاريخ الانتهاء] [المهندس] [النتيجة] [QR] [طباعة]
  
  Timeline بصري تحت الجدول:
  ●──────●──────●──────● (كل نقطة = دورة معايرة مع التاريخ)
  أخضر = ناجح | أحمر = راسب | أصفر = مشروط

القسم الثالث: المرفقات
  قائمة المرفقات مع أيقونة فتح لكل ملف + زر إضافة مرفق

أزرار:
  [+ إضافة دورة معايرة جديدة] [🖨️ طباعة QR لآخر دورة] [إغلاق]
  
  عند طباعة QR: يُحدد افتراضياً آخر دورة، مع إمكانية اختيار دورة أخرى
```

---

### 6. CalibrationFormDialog (نموذج إضافة/تعديل معايرة)

```
نافذة منبثقة (700×550) — تُستخدم للإضافة والتعديل

الحقول الإلزامية (*):
  * الجهة المالكة    → AutoComplete من قائمة Owners + إضافة جديدة
  * نوع الجهاز      → ComboBox من DeviceTypes
  * موديل الجهاز    → TextBox
  * الرقم التسلسلي  → TextBox (تحذير إذا موجود سابقاً مع عرض السجل القديم)
  * رقم الشهادة     → TextBox (فريد)
  * تاريخ المعايرة  → DatePicker (YYYY-MM-DD)
  * تاريخ الانتهاء  → DatePicker (YYYY-MM-DD)
  * المهندس المعايِر → AutoComplete من سجلات سابقة
  * نتيجة المعايرة  → ComboBox: ✅ ناجح | ❌ راسب | ⚠️ مشروط

الحقول الاختيارية:
  نوع المعايرة/الوصف → TextBox متعدد الأسطر

قسم المرفقات:
  [+ إضافة مرفق] — يفتح File Browser لأي امتداد
  قائمة المرفقات المضافة مع زر حذف لكل منها

أزرار:
  [حفظ وتوليد QR] [حفظ فقط] [إلغاء]

التحقق (Validation):
  - جميع الحقول الإلزامية غير فارغة
  - تاريخ الانتهاء > تاريخ المعايرة
  - رقم الشهادة فريد في قاعدة البيانات
  - عرض رسائل خطأ واضحة تحت كل حقل
  - Enter = حفظ | Esc = إلغاء | Tab = انتقال بين الحقول
```

---

### 7. PrintPreviewDialog (معاينة الطباعة)

```
نافذة منبثقة (900×650)

القسم الأيسر: إعدادات الطباعة
  اختيار الطابعة: [ComboBox بالطابعات المثبتة على الجهاز]
  قالب الورق: [ComboBox بالقوالب المحفوظة + "جديد"]
  
  إذا "جديد" أو "تعديل":
    نوع الورق: [رول / A4 / مخصص]
    عرض الورق (مم): [___]    ارتفاع الورق (مم): [___]
    الأعمدة: [__]             الصفوف: [__]
    عرض الملصق (مم): [___]   ارتفاع الملصق (مم): [___]
    هامش أعلى (مم): [__]     هامش يسار (مم): [__]
    فراغ أفقي (مم): [__]     فراغ رأسي (مم): [__]
    اسم القالب: [___________] [حفظ القالب]

  حجم QR للحفظ: [صغير / متوسط / كبير / طباعة عالية]
  
القسم الأيمن: معاينة بصرية تفاعلية
  مستطيل يمثل الورقة بالتناسب الحقيقي
  شبكة الملصقات مرسومة عليه
  المستخدم ينقر على الملصق لتحديد نقطة البداية
  الملصقات قبل نقطة البداية: رمادي (مستخدمة)
  الملصق المحدد للبدء: أزرق
  الملصقات الفارغة: أبيض

  معاينة الملصق الواحد:
  [شعار مركز البحوث الذهبي في وسط QR]
  QR Code بالحجم المناسب

أزرار:
  [🖨️ طباعة] [💾 حفظ QR كصورة في مجلد QR] [إلغاء]
  
  حفظ QR: يُحفظ باسم [رقم الشهادة].png في مجلد QR
```

---

### 8. QrVerifyView (شاشة التحقق)

```
قسمان:

القسم الأول: لصق النص الكامل
  TextBox كبير متعدد الأسطر:
  "الصق هنا النص المقروء من QR بالكامل"
  زر: [تحليل وتحقق]
  البرنامج يُحلل النص ويستخرج البيانات تلقائياً

القسم الثاني: إدخال يدوي
  حقول منفصلة لكل بيان:
  رقم الشهادة | الجهة | الموديل | الرقم التسلسلي
  تاريخ المعايرة | تاريخ الانتهاء | المهندس | النتيجة
  كود التحقق المقروء
  زر: [تحقق يدوياً]

نتيجة التحقق:
  ✅ شهادة أصلية — الكود مطابق
     [جميع البيانات معروضة بتنسيق جميل]
  ❌ تحذير: الشهادة مزورة أو معدّلة!
     الكود المقروء: XXXXXXXX
     الكود المحسوب: YYYYYYYY
     لا تقبل هذه الشهادة!
```

---

### 9. OwnersView (إدارة الجهات المالكة)

```
جدول يعرض: [الاسم] [العنوان] [رقم التواصل] [المسؤول] [عدد الأجهزة] [إجراءات]

شريط الأدوات: [+ إضافة جهة] [🔍 بحث]

نافذة إضافة/تعديل جهة (Dialog):
  * الاسم (إلزامي)
  العنوان (اختياري)
  رقم التواصل (اختياري)
  اسم المسؤول (اختياري)
  [حفظ] [إلغاء]

الحذف: ناعم (مع التحقق من عدم وجود أجهزة مرتبطة)
```

---

### 10. DeviceTypesView (إدارة أنواع الأجهزة)

```
مشابه لـ OwnersView:
جدول: [نوع الجهاز] [عدد الأجهزة] [إجراءات]
شريط: [+ إضافة نوع] [🔍 بحث]
نافذة: * الاسم (إلزامي)
الحذف: ناعم (مع التحقق)
```

---

### 11. ReportsView (التقارير والتصدير)

```
قسم الفلترة:
  [الجهة المالكة ▼] [النوع ▼] [من تاريخ] [إلى تاريخ]
  [الحالة: الكل | سارية | ستنتهي | منتهية]
  [بحث وعرض]

النتائج: عدد السجلات المطابقة: XX

خيارات التصدير:
  نوع التقرير: ● مفصل  ○ مختصر
  الصيغة:      [📊 Excel] [📄 PDF]
  
  [تصدير]

ترويسة التقارير:
  مركز البحوث النووية
  إدارة الوقاية من الإشعاع
  قسم قياس وتقدير الجرعات الشخصية والمعايرة
  وحدة المعايرة
  ━━━━━━━━━━━━━━━━━━━━━━━━
  [شعار CAL-QR]
  تاريخ التقرير: YYYY-MM-DD
```

---

### 12. AuditLogView (سجل العمليات)

```
جدول: [التاريخ والوقت] [العملية] [الكيان] [التفاصيل]

عمليات مسجلة:
  - إضافة جهاز/سجل معايرة
  - تعديل جهاز/سجل معايرة
  - حذف ناعم
  - حذف نهائي
  - طباعة QR
  - تصدير تقرير
  - نسخ احتياطي
  - تغيير إعدادات

فلتر: [من تاريخ] [إلى تاريخ] [نوع العملية ▼]
```

---

### 13. SettingsView (الإعدادات)

```
منظمة في أقسام:

قسم الأمان:
  تغيير كلمة المرور (القديمة + الجديدة + التأكيد)
  تعديل السؤال السري

قسم التنبيهات:
  عدد أيام التنبيه قبل انتهاء المعايرة: [30] يوم
  
قسم الإغلاق التلقائي:
  مدة عدم النشاط: [10] دقيقة

قسم قاعدة البيانات والملفات:
  مسار قاعدة البيانات: [...............] [تصفح]
  مسار مجلد المرفقات: [...............] [تصفح]
  مسار مجلد QR:        [...............] [تصفح]

قسم النسخ الاحتياطي:
  مسار النسخ الاحتياطي: [...............] [تصفح]
  جدولة: [يومي / أسبوعي / شهري]
  [نسخ احتياطي الآن] [استعادة نسخة]

قسم اللغة:
  [🇸🇦 العربية] [🇬🇧 English]

قسم الطباعة:
  قالب الورق الافتراضي: [ComboBox]
  [إدارة قوالب الورق]

[حفظ الإعدادات]
```

---

### 14. AboutView (عن البرنامج)

```
المحتوى:
  شعار CAL-QR (كبير)
  شعار مركز البحوث النووية الذهبي

  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  CAL-QR
  نظام معايرة أجهزة المسح الإشعاعي بتقنية QR
  الإصدار: 1.0.0
  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

  الجهة:
  مركز البحوث النووية
  إدارة الوقاية من الإشعاع
  قسم قياس وتقدير الجرعات الشخصية والمعايرة
  وحدة المعايرة

  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

  المصمم والمطور:
  م. إدريس فتح الله الهري
  مهندس قياسات إشعاعية
  مركز البحوث النووية — بنغازي، ليبيا

  [معلومات إضافية عن المطور يكتبها إدريس لاحقاً]

  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  جميع الحقوق محفوظة © 2026
  مركز البحوث النووية — ليبيا
```

---

## تاسعاً: منطق QR والتوقيع الرقمي

### HmacService.cs
```csharp
public interface IHmacService
{
    string ComputeSignature(CalibrationRecord record, Device device, Owner owner);
    bool VerifySignature(string data, string signature);
}

public class HmacService : IHmacService
{
    private const string SecretKey = "CalQR-Nuclear-Center-2026-SecretKey";
    
    public string ComputeSignature(CalibrationRecord record, Device device, Owner owner)
    {
        string data = $"{record.CertificateNumber}|{device.Model}|{device.SerialNumber}|" +
                     $"{owner.Name}|{record.CalibrationDate:yyyy-MM-dd}|" +
                     $"{record.ExpiryDate:yyyy-MM-dd}|{record.Result}|{record.EngineerName}";
        
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SecretKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(hash).Substring(0, 8);
    }
}
```

### QrService.cs — محتوى QR النهائي
```
=== شهادة معايرة | Calibration Certificate ===
الجهة / Owner: [اسم الجهة]
النوع / Type: [نوع الجهاز]
الموديل / Model: [موديل الجهاز]
الرقم التسلسلي / S/N: [الرقم التسلسلي]
رقم الشهادة / Cert No: [رقم الشهادة]
تاريخ المعايرة / Cal. Date: [YYYY-MM-DD]
تاريخ الانتهاء / Exp. Date: [YYYY-MM-DD]
المهندس / Engineer: [اسم المهندس]
نوع المعايرة / Cal. Type: [الوصف أو "غير محدد"]
النتيجة / Result: [✅ ناجح Passed | ❌ راسب Failed | ⚠️ مشروط Conditional]
كود التحقق / Verify Code: [XXXXXXXX]
─────────────────────────────────
الجهة المعايِرة / Calibrated by:
مركز البحوث النووية | Nuclear Research Center
وحدة المعايرة | Calibration Unit
```

### QrService.cs — توليد QR مع شعار مدمج
```csharp
// استخدام QRCoder مع دمج شعار مركز البحوث النووية في المنتصف
// خلفية شفافة للشعار
// مستوى تصحيح الأخطاء: H (30%) لاستيعاب الشعار
// الأحجام: Small=200px, Medium=400px, Large=600px, Print=1200px
```

---

## عاشراً: نظام الطباعة المرن

### PrintService.cs
```csharp
public interface IPrintService
{
    IEnumerable<string> GetAvailablePrinters();
    void PrintQrLabel(QrPrintJob job);
    void PrintMultipleQrLabels(IEnumerable<QrPrintJob> jobs);
}

public class QrPrintJob
{
    public BitmapImage QrImage { get; set; }
    public PaperTemplate Template { get; set; }
    public string PrinterName { get; set; }
    public int StartColumn { get; set; }  // موضع البداية (عمود)
    public int StartRow { get; set; }     // موضع البداية (صف)
}
```

### منطق الطباعة
```
1. حساب أبعاد الملصق بالـ DPI للطابعة (1 مم = 3.7795 pixel at 96 DPI)
2. إنشاء DrawingVisual بحجم الملصق الواحد فقط
3. رسم QR مع الشعار الذهبي المدمج
4. استخدام PrintDialog المدمج في WPF
5. الطباعة المتعددة: صفحة لكل ملصق مرتبة حسب رقم الشهادة
```

---

## حادي عشر: نظام النسخ الاحتياطي

### BackupService.cs
```csharp
// النسخ اليدوي: نسخ ملف SQLite + مجلد Attachments مضغوطاً
// النسخ التلقائي: Timer في الخلفية حسب BackupSchedule
// اسم ملف النسخة: CalQR_Backup_YYYY-MM-DD_HH-mm.zip
// الاحتفاظ بآخر 10 نسخ فقط (حذف الأقدم تلقائياً)
```

---

## ثاني عشر: معالجة الأخطاء والفشل

```csharp
// في DatabaseMigrator: try/catch مع رسالة واضحة + خيار استعادة
// في BackupService: تسجيل في AuditLog + Snackbar للمستخدم
// في QrService: التحقق من وجود مجلد QR وإنشاؤه إن غاب
// في AttachmentService: عرض "الملف غير متاح" بدلاً من استثناء
// في PrintService: رسالة واضحة إذا لم تكن طابعة متاحة
// تحقق من مساحة القرص: تحذير إذا بقي أقل من 500MB
```

---

## ثالث عشر: Unit Tests المطلوبة

```
CAL-QR.Tests/
├── Services/
│   ├── HmacServiceTests.cs
│   │   ├── ComputeSignature_SameData_ReturnsSameCode()
│   │   ├── ComputeSignature_DifferentData_ReturnsDifferentCode()
│   │   └── VerifySignature_TamperedData_ReturnsFalse()
│   ├── QrServiceTests.cs
│   │   ├── GenerateQr_ValidData_ReturnsNonNullImage()
│   │   └── QrContent_ContainsAllRequiredFields()
│   └── ExportServiceTests.cs
│       ├── ExportToExcel_ValidData_CreatesFile()
│       └── ExportToPdf_ValidData_CreatesFile()
├── Repositories/
│   ├── DeviceRepositoryTests.cs
│   │   ├── GetAll_ReturnsOnlyNotDeleted()
│   │   ├── Add_Device_SavesCorrectly()
│   │   └── SoftDelete_SetsIsDeletedTrue()
│   └── CalibrationRepositoryTests.cs
│       ├── GetByDevice_ReturnsAllCalibrations()
│       └── GetExpiringSoon_ReturnsCorrectRecords()
└── ViewModels/
    ├── DashboardViewModelTests.cs
    │   └── LoadStats_ReturnsCorrectCounts()
    └── DevicesViewModelTests.cs
        └── Search_FiltersByOwnerName()
```

---

## رابع عشر: Inno Setup (setup.iss)

```iss
[Setup]
AppName=CAL-QR
AppVersion=1.0.0
AppPublisher=م. إدريس فتح الله الهري - مركز البحوث النووية
DefaultDirName={autopf}\CAL-QR
DefaultGroupName=CAL-QR
SetupIconFile=cal-qr-logo.ico
Compression=lzma2
SolidCompression=yes
OutputDir=..\Releases
OutputBaseFilename=CAL-QR_Setup_v1.0.0
WizardStyle=modern

[Languages]
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "..\CAL-QR\bin\Release\net8.0-windows\win-x64\publish\*"; \
  DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\CAL-QR"; Filename: "{app}\CAL-QR.exe"
Name: "{commondesktop}\CAL-QR"; Filename: "{app}\CAL-QR.exe"

[Run]
Filename: "{app}\CAL-QR.exe"; Description: "تشغيل CAL-QR"; Flags: nowait postinstall
```

---

## خامس عشر: ملاحظات التنفيذ الخاصة لـ Antigravity IDE

```
⚠️ CRITICAL — اقرأ هذا أولاً:

1. لا تستخدم AppServiceProvider.Resolve أبداً — Constructor Injection فقط
2. كل استعلام قراءة يجب أن يحتوي AsNoTracking()
3. كل ViewModel يستقبل IDbContextFactory<CalQrDbContext> في constructor
4. لا تكتب ALTER TABLE مباشرة — استخدم ExecuteSqlIfColumnMissing
5. SQLite GroupBy مع decimal: احسب كـ (double) ثم حوّل لـ decimal
6. IsDeleted: الحذف الناعم أولاً، الحذف النهائي فقط من شاشة السجلات المحذوفة
7. FlowDirection="RightToLeft" في MainWindow عند اللغة العربية
8. جميع التواريخ بصيغة YYYY-MM-DD
9. Publish بـ Self-Contained + Single File
10. شعار مركز البحوث النووية الذهبي يُدمج في وسط QR باستخدام QRCoder مع ErrorCorrectionLevel.H
11. مجلد QR يُنشأ تلقائياً إن لم يكن موجوداً
12. مجلد Attachments مُنظَّم: Attachments/[رقم الشهادة]/[اسم الملف]
13. الطابعة تُختار من Windows PrintDialog (الطابعات المثبتة فعلياً)
14. كود التحقق HMAC = أول 8 أحرف من SHA256 بالـ HEX بالحروف الكبيرة
```

---

*وثيقة التنفيذ الكاملة لـ CAL-QR | م. إدريس فتح الله الهري | 2026-06-30*
