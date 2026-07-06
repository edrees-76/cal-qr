# CAL-QR — وثيقة سياق المشروع (ProjectContext.md)

---

## القسم 1: هوية المشروع
* **اسم المشروع:** CAL-QR
* **الإصدار:** 1.0.0
* **المصمم والمطور:** م. إدريس فتح الله الهري
* **الجهة:** مركز البحوث النووية - تاجوراء — إدارة الوقاية من الإشعاع — قسم قياس وتقدير الجرعات الشخصية والمعايرة — وحدة المعايرة
* **الهدف من المنظومة:** برنامج سطح مكتب Windows متكامل باللغة العربية لإدارة شهادات معايرة أجهزة المسح الإشعاعي (ألفا، بيتا، وجاما)، وتوليد رموز الاستجابة السريعة (QR Code) المؤمنة بتوقيع رقمي (HMAC-SHA256) لمنع التزوير والعبث بالبيانات، مع تنبيهات مرنة لصلاحية المعايرة، وتصدير التقارير والإحصائيات بصيغ Excel وPDF.
* **بيئة التطوير:** Antigravity IDE 2.1.1، .NET 8، WPF (MVVM)، SQLite (EF Core)
* **مسار المشروع:** `D:\cal-qr\`

---

## القسم 2: أدوار العمل المتفق عليها
* **دور Claude:** المهندس المعماري — يخطط ويراجع ويعتمد الخطط الفنية والمعمارية قبل بدء التنفيذ.
* **دور Antigravity IDE:** المنفذ — يقوم بكتابة وتعديل الأكواد البرمجية والتجميع والاختبار بناءً على الخطط المعتمدة.
* **دور م. إدريس:** المدير والمرجع النهائي — يوجه المشروع، يحدد المتطلبات، يختبر التطبيق عملياً، ويوافق على الخطط والنتائج النهائية.
* **آلية العمل:** يقوم Antigravity بكتابة خطة التعديلات المقترحة (`Proposed Changes`) في ملف الخطة ← يراجعها Claude معمارياً للتأكد من ملاءمتها للقواعد الصارمة ← يعتمدها م. إدريس ← يقوم Antigravity بالتنفيذ الفعلي.

---

## القسم 3: القواعد المعمارية الصارمة (لا استثناءات)
1. **Constructor Injection فقط:** يمنع منعاً باتاً استدعاء `AppServiceProvider.Resolve` أو محددات الخدمة (Service Locators) اليدوية في أي مكان في الكود.
2. **استخدام `AsNoTracking()` دائماً:** جميع استعلامات قراءة البيانات وقوائم الاستعراض يجب أن تستخدم `AsNoTracking()` صراحةً لضمان أقصى سرعة أداء لـ SQLite ومنع استهلاك الذاكرة.
3. **حقن مصنع السياق (`IDbContextFactory`):** يجب حقن `IDbContextFactory<CalQrDbContext>` في جميع الـ ViewModels والـ Repositories واستخدامه لفتح اتصالات قصيرة المدى (`using var context = await ...`) بدلاً من تمرير الـ DbContext مباشرة.
4. **تحديث الجداول الدفاعي:** يمنع استخدام جمل `ALTER TABLE` مباشرة عند تحديث هيكل قاعدة البيانات؛ يجب استخدام دالة `ExecuteSqlIfColumnMissing` المخصصة لحماية سلامة البيانات الحية.
5. **الحذف الناعم أولاً:** تعتمد المنظومة على خاصية `IsDeleted` لحجب البيانات المحذوفة بصورة ناعمة من قوائم العرض، مع الاحتفاظ ببياناتها التاريخية، ولا يتم حذفها نهائياً إلا من شاشة السجلات المحذوفة المخصصة.
6. **دعم العربية بصورة افتراضية:** يجب تفعيل خاصية `FlowDirection="RightToLeft"` في كافة النوافذ والشاشات.
7. **صيغة موحدة للتاريخ:** جميع الحقول والشهادات تتعامل بصيغة تاريخ موحدة وصارمة هي `YYYY-MM-DD`.
8. **النشر والتوزيع المستقل:** يتم تصدير ونشر البرنامج بصيغة `Self-Contained Publish` ليعمل على أي بيئة Windows دون الحاجة لتثبيت مسبق للـ .NET Runtime.
9. **الاعتماد على الواجهات للخدمات:** يجب تعريف واجهة (`Interface`) لكل خدمة أولاً وتسجيلها في حاوية الـ DI قبل تطبيق الكلاس الفعلي لها.
10. **خلو تام من الأخطاء والتحذيرات:** يجب أن يكتمل أمر التجميع `dotnet build` دائماً بـ `0 Warning(s), 0 Error(s)`.
11. **إقلاع وإغلاق صريح للشباك:** تعتمد المنظومة على `ShutdownMode = ShutdownMode.OnExplicitShutdown` للتحكم الدقيق في انتقال النوافذ (Splash -> Login -> MainWindow) دون إغلاق التطبيق فجأة عند التنقل.

---

## القسم 4: هيكل المشروع الكامل

### 1. مجلد النماذج (Models/)
* [AppSetting.cs](file:///d:/cal-qr/CAL-QR/Models/AppSetting.cs) — إعدادات النظام وقيم المفاتيح المشتركة.
* [Attachment.cs](file:///d:/cal-qr/CAL-QR/Models/Attachment.cs) — مرفقات شهادات المعايرة (ملفات الـ PDF).
* [AuditLog.cs](file:///d:/cal-qr/CAL-QR/Models/AuditLog.cs) — سجل العمليات التشغيلية للمراقبة.
* [CalibrationRecord.cs](file:///d:/cal-qr/CAL-QR/Models/CalibrationRecord.cs) — سجلات وتفاصيل المعايرة الدورية للأجهزة.
* [Device.cs](file:///d:/cal-qr/CAL-QR/Models/Device.cs) — بيانات الأجهزة المسجلة بالمنظومة.
* [DeviceType.cs](file:///d:/cal-qr/CAL-QR/Models/DeviceType.cs) — تصنيفات وأنواع أجهزة المسح الإشعاعي.
* [Owner.cs](file:///d:/cal-qr/CAL-QR/Models/Owner.cs) — الجهات المالكة للأجهزة.
* [PaperTemplate.cs](file:///d:/cal-qr/CAL-QR/Models/PaperTemplate.cs) — قوالب أبعاد ورق طباعة ملصقات الـ QR.

### 2. مجلد نماذج العرض (ViewModels/)
* [MainViewModel.cs](file:///d:/cal-qr/CAL-QR/ViewModels/MainViewModel.cs) — إدارة الشاشة الرئيسية والتبويبات.
* [DashboardViewModel.cs](file:///d:/cal-qr/CAL-QR/ViewModels/DashboardViewModel.cs) — لوحة التحكم والإحصائيات الحيوية وتنبيهات الألوان.
* [DevicesViewModel.cs](file:///d:/cal-qr/CAL-QR/ViewModels/DevicesViewModel.cs) — CRUD للأجهزة مع ميزات الفلترة والتبديل البصري.
* [DeviceDetailViewModel.cs](file:///d:/cal-qr/CAL-QR/ViewModels/DeviceDetailViewModel.cs) — الخط الزمني لتاريخ معايرات ومرفقات الجهاز الفردي.
* [CalibrationFormViewModel.cs](file:///d:/cal-qr/CAL-QR/ViewModels/CalibrationFormViewModel.cs) — إضافة وتعديل سجلات المعايرة وحساب HMAC وتوليد QR.
* [OwnerViewModel.cs](file:///d:/cal-qr/CAL-QR/ViewModels/OwnerViewModel.cs) — إدارة الجهات المالكة.
* [DeviceTypeViewModel.cs](file:///d:/cal-qr/CAL-QR/ViewModels/DeviceTypeViewModel.cs) — إدارة أنواع الأجهزة.
* [QrVerifyViewModel.cs](file:///d:/cal-qr/CAL-QR/ViewModels/QrVerifyViewModel.cs) — التحقق الميداني واليدوي من أصالة الشهادة وتطابق HMAC.
* [ReportsViewModel.cs](file:///d:/cal-qr/CAL-QR/ViewModels/ReportsViewModel.cs) — تجميع وفلترة السجلات وتوليد تقارير PDF وExcel.
* [AuditLogViewModel.cs](file:///d:/cal-qr/CAL-QR/ViewModels/AuditLogViewModel.cs) — عرض ومراقبة سجل نشاطات المنظومة.
* [SettingsViewModel.cs](file:///d:/cal-qr/CAL-QR/ViewModels/SettingsViewModel.cs) — إدارة إعدادات المسارات وصلاحية التنبيه والنسخ الاحتياطي وتغيير الكلمة.
* [PaperTemplateViewModel.cs](file:///d:/cal-qr/CAL-QR/ViewModels/PaperTemplateViewModel.cs) — إدارة وتعديل قوالب أبعاد الطباعة.
* [PrintPreviewViewModel.cs](file:///d:/cal-qr/CAL-QR/ViewModels/PrintPreviewViewModel.cs) — معاينة الملصقات التفاعلية قبل إرسالها للطابعة.
* **مجلد فرعي (ViewModels/Base/):**
  * [BaseViewModel.cs](file:///d:/cal-qr/CAL-QR/ViewModels/Base/BaseViewModel.cs) — المرجع الأساسي لتطبيق `INotifyPropertyChanged`.
  * [RelayCommand.cs](file:///d:/cal-qr/CAL-QR/ViewModels/Base/RelayCommand.cs) — إدارة وتنفيذ أوامر WPF بمرونة.

### 3. مجلد شاشات التبويبات (Views/Tabs/)
* [AboutView.xaml](file:///d:/cal-qr/CAL-QR/Views/Tabs/AboutView.xaml) — تفاصيل المنظومة والمطور وإدارة الوقاية والجهة.
* [AuditLogView.xaml](file:///d:/cal-qr/CAL-QR/Views/Tabs/AuditLogView.xaml) — واجهة مراقبة سجل العمليات.
* [DashboardView.xaml](file:///d:/cal-qr/CAL-QR/Views/Tabs/DashboardView.xaml) — لوحة البيانات التفاعلية والرسوم البيانية.
* [DevicesView.xaml](file:///d:/cal-qr/CAL-QR/Views/Tabs/DevicesView.xaml) — إدارة وعرض قائمة الأجهزة.
* [DeviceTypesView.xaml](file:///d:/cal-qr/CAL-QR/Views/Tabs/DeviceTypesView.xaml) — إدارة تصنيفات الأجهزة.
* [OwnersView.xaml](file:///d:/cal-qr/CAL-QR/Views/Tabs/OwnersView.xaml) — إدارة الجهات المالكة.
* [QrVerifyView.xaml](file:///d:/cal-qr/CAL-QR/Views/Tabs/QrVerifyView.xaml) — شاشة مطابقة رموز الـ QR يدوياً وتلقائياً.
* [ReportsView.xaml](file:///d:/cal-qr/CAL-QR/Views/Tabs/ReportsView.xaml) — شاشة استخراج وتصنيف التقارير.
* [SettingsView.xaml](file:///d:/cal-qr/CAL-QR/Views/Tabs/SettingsView.xaml) — واجهة إعدادات التطبيق العامة.
* [HelpView.xaml](file:///d:/cal-qr/CAL-QR/Views/Tabs/HelpView.xaml) — دليل المستخدم المتكامل المبسط لمشغل النظام.

### 4. مجلد النوافذ المنبثقة (Views/Dialogs/)
* [AlertPopupDialog.xaml](file:///d:/cal-qr/CAL-QR/Views/Dialogs/AlertPopupDialog.xaml) — نافذة التحذير التلقائي المنبثقة للأجهزة القريبة من انتهاء صلاحية معايرتها.
* [CalibrationFormDialog.xaml](file:///d:/cal-qr/CAL-QR/Views/Dialogs/CalibrationFormDialog.xaml) — نموذج تسجيل ومراجعة عمليات المعايرة الفردية.
* [DeviceDetailDialog.xaml](file:///d:/cal-qr/CAL-QR/Views/Dialogs/DeviceDetailDialog.xaml) — شباك تفاصيل الجهاز وتاريخ معايراته الكامل.
* [PaperTemplateDialog.xaml](file:///d:/cal-qr/CAL-QR/Views/Dialogs/PaperTemplateDialog.xaml) — شباك تحديد أبعاد وتخطيط قوالب الملصقات.
* [PrintPreviewDialog.xaml](file:///d:/cal-qr/CAL-QR/Views/Dialogs/PrintPreviewDialog.xaml) — لوحة المعاينة التفاعلية وتحديد الطابعة ومكان انطلاق الملصق.

### 5. مجلد نوافذ التشغيل الرئيسية (Views/)
* [SplashWindow.xaml](file:///d:/cal-qr/CAL-QR/Views/SplashWindow.xaml) — شاشة التحميل الترحيبية المضيئة المحدثة بثلاثة أسطر للحقوق.
* [LoginWindow.xaml](file:///d:/cal-qr/CAL-QR/Views/LoginWindow.xaml) — شاشة تسجيل الدخول المحترفة المعتمة مع ميزة كشف العين واسترداد الحساب بالسؤال السري وأزرار التحكم بالحجم والخلفية الضبابية.
* [FirstRunWizard.xaml](file:///d:/cal-qr/CAL-QR/Views/FirstRunWizard.xaml) — معالج إعداد قاعدة البيانات والكلمة الأولى لحماية المنظومة عند التشغيل لأول مرة.

### 6. مجلد الخدمات (Services/)
* `IHmacService`/`HmacService.cs` — توليد وتفحص توقيع HMAC-SHA256 الآمن.
* `IQrService`/`QrService.cs` — إنشاء وحفظ كود الـ QR مدمجاً بوسطه شعار مركز البحوث.
* `IPrintService`/`PrintService.cs` — الرسم وإدارة الطابعات والطباعة الفردية والمتعددة.
* `IExportService`/`ExportService.cs` — تصدير التقارير الاحترافية لـ Excel (RTL) و PDF.
* `IBackupService`/`BackupService.cs` — أتمتة النسخ الاحتياطي عبر SQLite Backup API الحقيقي.

### 7. مجلد المستودعات (Repositories/)
* `IOwnerRepository`/`OwnerRepository.cs`
* `IDeviceTypeRepository`/`DeviceTypeRepository.cs`
* `IDeviceRepository`/`DeviceRepository.cs`
* `ICalibrationRepository`/`CalibrationRepository.cs`
* `IAttachmentRepository`/`AttachmentRepository.cs`
* `IPaperTemplateRepository`/`PaperTemplateRepository.cs`
* `IAuditLogRepository`/`AuditLogRepository.cs`

### 8. مجلد المساعدات (Helpers/)
* [CalibrationEvents.cs](file:///d:/cal-qr/CAL-QR/Helpers/CalibrationEvents.cs) — إدارة الأحداث الساكنة لربط البيانات بين ViewModels المتفرقة (Dashboard، Devices، إلخ).
* [DateHelper.cs](file:///d:/cal-qr/CAL-QR/Helpers/DateHelper.cs) — معالجة وتنسيق التواريخ.
* [FileHelper.cs](file:///d:/cal-qr/CAL-QR/Helpers/FileHelper.cs) — عمليات المسارات وحساب المساحة التخزينية المتبقية.
* [PasswordHelper.cs](file:///d:/cal-qr/CAL-QR/Helpers/PasswordHelper.cs) — توليد هاش التشفير للكلمة ومطابقتها.

### 9. مجلد قاعدة البيانات والإعدادات (Data/)
* [CalQrDbContext.cs](file:///d:/cal-qr/CAL-QR/Data/CalQrDbContext.cs) — سياق الكائنات والروابط والفهارس الصارمة لقاعدة البيانات.
* [DatabaseMigrator.cs](file:///d:/cal-qr/CAL-QR/Data/DatabaseMigrator.cs) — تطبيق الهجرة الرقمية وقيم البذور المبدئية عند الإقلاع.

### 10. مجلد الأصول والموارد (Assets/)
* `Assets/Fonts/Cairo.ttf` — خط Cairo الاحترافي المدمج بالمشروع.
* `Assets/Logo/cal-qr-logo.png` — الشعار الأساسي للمنظومة.
* `Assets/Logo/cal-qr-logo-removebg-preview.png` — الشعار الشفاف المعالج.
* `Assets/Logo/nuclear-center-logo.png` — شعار مركز البحوث النووية الذهبي.
* `Assets/Logo/magic_eraser1782906686524.png` — صورة المعايرة والمعدات المدمجة بالخلفية.

---

## القسم 5: قاعدة البيانات (ERD مختصر)

تتألف قاعدة البيانات المحلية من 8 جداول مترابطة كالتالي:

1. **Owners (الجهات المالكة):**
   * `Id` (PK - int)
   * `Name` (string - مطلوب ومفهرس)
   * `Address` (string - اختياري)
   * `ContactPhone` (string - اختياري)
   * `ContactPerson` (string - اختياري)
   * `IsDeleted` (bool - مفهرس)
   * `CreatedAt` (DateTime)
   * *العلاقات:* ارتباط (1 إلى متعدد) مع جدول الأجهزة `Devices`.

2. **DeviceTypes (أنواع الأجهزة):**
   * `Id` (PK - int)
   * `Name` (string - مطلوب ومفهرس)
   * `IsDeleted` (bool - مفهرس)
   * `CreatedAt` (DateTime)
   * *العلاقات:* ارتباط (1 إلى متعدد) مع جدول الأجهزة `Devices`.

3. **Devices (الأجهزة):**
   * `Id` (PK - int)
   * `Model` (string - مطلوب)
   * `SerialNumber` (string - مطلوب ومفهرس)
   * `OwnerId` (FK - int - مفهرس) ← يرتبط بـ `Owners.Id` (Restrict)
   * `DeviceTypeId` (FK - int - مفهرس) ← يرتبط بـ `DeviceTypes.Id` (Restrict)
   * `IsDeleted` (bool - مفهرس)
   * `CreatedAt` (DateTime)
   * *العلاقات:* ارتباط (1 إلى متعدد) مع سجلات المعايرة `CalibrationRecords`.

4. **CalibrationRecords (سجلات المعايرة):**
   * `Id` (PK - int)
   * `DeviceId` (FK - int - مفهرس) ← يرتبط بـ `Devices.Id` (Restrict)
   * `CertificateNumber` (string - مطلوب، فريد ومفهرس)
   * `CalibrationDate` (DateTime)
   * `ExpiryDate` (DateTime - مفهرس)
   * `EngineerName` (string - مطلوب)
   * `CalibrationDescription` (string - اختياري)
   * `Result` (string - مطلوب، قيم محددة: Passed / Failed / Conditional)
   * `HmacSignature` (string - مطلوب - كود التوقيع الفريد بطول 8 محارف)
   * `IsDeleted` (bool - مفهرس)
   * `CreatedAt` (DateTime)
   * `UpdatedAt` (DateTime)
   * *العلاقات:* ارتباط (1 إلى متعدد) مع جدول المرفقات `Attachments`.

5. **Attachments (مرفقات المعايرة):**
   * `Id` (PK - int)
   * `CalibrationRecordId` (FK - int) ← يرتبط بـ `CalibrationRecords.Id` (Cascade)
   * `FileName` (string - مطلوب)
   * `FilePath` (string - مطلوب)
   * `FileExtension` (string)
   * `UploadedAt` (DateTime)

6. **PaperTemplates (قوالب أبعاد الطباعة):**
   * `Id` (PK - int)
   * `TemplateName` (string - مطلوب)
   * `PaperType` (string - رول / A4)
   * `PaperWidthMm` (decimal)
   * `PaperHeightMm` (decimal)
   * `Columns` (int)
   * `Rows` (int)
   * `LabelWidthMm` (decimal)
   * `LabelHeightMm` (decimal)
   * `MarginTopMm` (decimal)
   * `MarginLeftMm` (decimal)
   * `HorizontalGapMm` (decimal)
   * `VerticalGapMm` (decimal)
   * `IsDefault` (bool - يضمن النظام أن يكون هناك قالب افتراضي واحد نشط فقط)
   * `CreatedAt` (DateTime)

7. **AuditLogs (سجل النشاط):**
   * `Id` (PK - int)
   * `Action` (string - نوع الحركة)
   * `EntityName` (string - اسم الكائن)
   * `EntityId` (string - رقم المعرف)
   * `Details` (string - تفاصيل الحدث)
   * `ActionAt` (DateTime)

8. **AppSettings (إعدادات النظام):**
   * `Id` (PK - int)
   * `Key` (string - مطلوب، فريد ومفهرس)
   * `Value` (string - القيمة المخزنة)
   * `UpdatedAt` (DateTime)

---

## القسم 6: المكتبات المستخدمة (NuGet Packages)
* **Microsoft.EntityFrameworkCore.Sqlite (8.0.\*):** محرك ووسيط الوصول لقاعدة بيانات SQLite محلياً.
* **Microsoft.EntityFrameworkCore.Tools (8.0.\*):** أدوات مساعدة لعمليات الهجرة البرمجية وتوليد المخططات.
* **QRCoder (1.4.\*):** توليد وتشكيل مصفوفة كود الـ QR ودمج الشعار في وسطها بدقة.
* **MaterialDesignThemes (5.0.\*):** حزمة التنسيق الجمالي البصري العصري وعناصر واجهة WPF.
* **LiveChartsCore.SkiaSharpView.WPF (2.0.0-rc2):** رسم وعرض المخططات الإحصائية التفاعلية (Donut & Bar) في لوحة التحكم.
* **ClosedXML (0.102.\*):** أتمتة وإنشاء ملفات جداول البيانات Excel وتفعيل الكتابة من اليمين لليسار.
* **QuestPDF (2024.3.\*):** محرك توليد وتنسيق مستندات وتقارير الـ PDF المحترفة وتقديم تخطيط RTL متكامل.
* **Microsoft.Extensions.DependencyInjection (8.0.\*):** حاوية حقن التبعيات وإدارة دورة حياة كائنات المنظومة.

---

## القسم 7: حالة التنفيذ الحالية

جميع مراحل التطوير العشر المجدولة مكتملة وتم تجميعها وبناؤها واختبارها بنجاح كامل:

* **المراحل المكتملة:**
  1. **المرحلة 1:** بناء هيكلية الـ Solution والملفات وقاعدة البيانات والمستودعات وقيم البذور الأساسية.
  2. **المرحلة 2:** تصميم النوافذ الثلاث الأساسية (Splash و Login و Main) وتفعيل مؤقت الخمول والكلمة الماستر.
  3. **المرحلة 3:** أتمتة عمليات الـ CRUD للجهات والأنواع والأجهزة مع واجهة التفاصيل والخط الزمني.
  4. **المرحلة 4:** تفعيل خوارزمية HMAC وتوليد QR Code المدمج بشعار المركز وتطبيق شاشة مطابقة الأكواد.
  5. **المرحلة 5:** تطبيق نظام الطابعات وإدارة القوالب وإتاحة لوحة المعاينة التفاعلية.
  6. **المرحلة 6:** بناء الـ Dashboard ورسوم الإحصائيات وبانر التنبيهات الملون ونظام الفحص عند إقلاع البرنامج.
  7. **المرحلة 7:** توليد وتأكيد صحة تصدير ملفات Excel و PDF بدعم RTL حقيقي ومطابق للفحص البصري.
  8. **المرحلة 8:** إدارة الإعدادات وخوارزمية استعادة وعمل نسخ SQLite Backup الآمنة.
  9. **المرحلة 9:** شاشة عن البرنامج وتطبيق معالج التشغيل الأول للبرنامج للمستخدم الجديد.
  10. **المرحلة 10:** اختبار شامل للنظام وتلميحه وحل المشاكل التقنية.

* **تطوير ما بعد المرحلة 10:**
  * **عين كشف كلمة المرور:** تمت إضافة أزرار كشف العين في شاشة الدخول وشاشات استرداد كلمة المرور الجديدة للتأكد من القيمة المدخلة.
  * **نافذة تأكيد الخروج:** إضافة صندوق حوار لتأكيد الخروج ("هل تريد الخروج من المنظومة؟") عند إغلاق شاشة الدخول (التي تنهي البرنامج)، وعند إغلاق النافذة الرئيسية (X أو Alt+F4) أو النقر على زر "خروج" الأحمر في الترويسة بحيث يوجه المستخدم للعودة إلى واجهة الدخول بدلاً من إغلاق البرنامج، مع تخطيه صامتاً فقط عند تسجيل الخروج التلقائي للخمول.
  * **توسيع النوافذ وتعديل النصوص:** توسيع قياس واجهات الدخول والتحميل الترحيبي وإعادة هيكلة مصفوفة حقوق المطورين لتظهر مرتبة بشكل عمودي في 3 أسطر متناسقة.
  * **تعديل واجهة عن البرنامج (AboutView):** تم حذف شعار مركز البحوث النووية والاكتفاء بشعار المنظومة، وإضافة "تاجوراء" لاسم الجهة وحذف سطر حقوق الجهة من الأسفل، وتعديل عنوان وصياغة الحقوق لتقتصر على "تصميم و تطوير و تنفيد" متبوعة بأسماء المطورين (م. إدريس فتح الله الهري وم. رضا المريمى) عمودياً بشكل متناسق.
  * **أزرار التحكم (تصغير/تكبير):** دعم أزرار تصغير وتكبير وإغلاق الشباك بطريقة احترافية مع قفل أبعاد بطاقة الدخول وتوسيطها بالمنتصف وتفعيل خاصية السحب اليدوي الخارجي.
  * **خلفية التكبير الضبابية (Acrylic Background):** ربط خلفية معتمة تمنع إظهار سطح المكتب وتظهر صورة معدات المعايرة بشكل ضبابي ومموه جذاب بدقة تضبيب 18 بكسل وفلتر كحلي.
  * **قسم الدليل والمساعدة:** بناء شاشة `HelpView` المتكاملة برمجياً بناءً على دليل التشغيل الرسمي لمشغل النظام.
  * **الطباعة المتعددة (Batch Print):** تفعيل التحديد المتعدد للأجهزة في DevicesView عبر CheckBox في الجدول والكروت، مع زر "طباعة المحدد" المربوط بدالة PrintMultipleQrLabels الجاهزة في PrintService، وتسجيل كل عملية طباعة دفعية في AuditLog.
  * **إصلاح وضوح وتوسيط حقول الإدخال:** معالجة مشكلة اختفاء وقص نصوص حقول الإدخال (TextBox, PasswordBox, ComboBox, DatePicker) عبر كل شاشات المنظومة، عبر تعريف فرشاة لون مركزية واحدة (PrimaryInputForeground بقيمة #1A3A6B) في App.xaml، وتوحيد الارتفاع (Height=45)، البطانة (Padding=12,8,12,8)، والتوسيط العمودي (VerticalContentAlignment=Center) على مستوى الأنماط الثمانية المسماة لـ MaterialDesignThemes، مع تخصيص نمط DatePickerTextBox لضمان سريان اللون للحقل الداخلي القابل للتحرير.
  * **تعديل موضع أيقونة إظهار كلمة المرور:** نقل أيقونة العين (إظهار/إخفاء كلمة المرور) في LoginWindow.xaml من الجهة اليمنى إلى الجهة اليسرى لحقول كلمة المرور الثلاثة (تسجيل الدخول، كلمة المرور الجديدة، تأكيد كلمة المرور الجديدة).
  * **إعادة تصميم الترويسة العلوية:** حذف شعار مركز البحوث النووية من MainWindow.xaml، وإعادة تنظيم عناوين المنظومة إلى أربعة أسطر هرمية منفصلة (مركز البحوث النووية / إدارة الوقاية من الإشعاع / قسم قياس وتقدير الجرعات الشخصية والمعايرة / وحدة المعايرة) بدل دمجها بفاصل ( | ).
  * **إزالة ميزة تبديل اللغة الإنجليزية غير المفعّلة:** حذف زر "EN" من الترويسة، ودالة LanguageButton_Click، ومجلد Localization غير المستخدم بالكامل (Strings.resx وملحقاته)، وسطر البيانات الأولية 'Language' من DatabaseMigrator.cs - كل ذلك كان بقايا ميزة لم تُفعَّل فعلياً (الزر كان يقلب FlowDirection فقط دون ترجمة حقيقية).
  * **إصلاح شامل للوحة البيانات الرئيسية (Dashboard):**
    - إصلاح قص نصوص البطاقات الإحصائية الأربعة: السبب الجذري كان ClipToBounds الافتراضي في materialDesign:Card المتعارض مع ارتفاع حروف خط Cairo؛ الحل استبدال Card بعنصر Border مخصص.
    - تحويل مخطط 'توزيع الأجهزة حسب الجهات المالكة' من PieChart (LiveCharts2) إلى قائمة WPF أصلية (ItemsControl + ScrollViewer) بأشرطة أفقية ملوّنة، ترقيم تسلسلي، ومحاذاة يمين - بسبب عجز SkiaSharp/LiveCharts2 عن تشكيل النص العربي (Arabic Shaping) بشكل صحيح في الـ Legend، ما كان يُنتج نصاً مفككاً/معكوساً لا يُصلَح بمعالجة النص وحدها.
    - حذف قسم 'مقارنة حالات الصلاحية' بالكامل لتكرار بياناته مع البطاقات الأربعة الموجودة أعلى اللوحة، واستُغلت مساحته بتوسيع قسم الجهات المالكة ليأخذ العرض الكامل للصف.
    - إضافة عمود ترقيم تسلسلي لجدول 'الأجهزة التي ستنتهي معايرتها قريباً'.
    - توحيد محاذاة عناوين كل الأقسام في اللوحة لتكون يمين (Right) بشكل متسق.
  * **البحث الشامل الحي (Global Live Search):** تفعيل حقل البحث العلوي في MainWindow.xaml (كان شكلياً بدون وظيفة) ليصبح بحثاً حياً عبر أربعة كيانات: الأجهزة (اسم الموديل والرقم التسلسلي)، الجهات المالكة (الاسم)، أنواع الأجهزة (الاسم)، وشهادات المعايرة (رقم الشهادة CertificateNumber). البنية المعمارية: خدمة ISearchService/SearchService تُنفّذ 4 استعلامات متوازية عبر Task.WhenAll باستخدام IDbContextFactory وAsNoTracking، مع دعم CancellationToken لحماية النتائج من تسابق البيانات (Race Condition) عند الكتابة السريعة المتتالية. التنقل بين التبويبات عند اختيار نتيجة يعتمد على وسيط أحداث ساكن جديد (SearchEvents.cs) يُرسل معرف (Id) الكيان مباشرة دون المساس بحالة فلترة المستخدم الحالية في التبويب الوجهة، مع تبديل تلقائي للتبويب النشط (SelectedTabIndex بثوابت مسمّاة بدل أرقام مباشرة). تفعيل اختصار Ctrl+F للتركيز المباشر على حقل البحث، مع قائمة نتائج منسدلة (Popup) مصنّفة بأيقونات وألوان مميزة لكل نوع كيان. أثناء هذا التطوير تم اكتشاف وإصلاح مخالفتين معماريتين منفصلتين لقاعدة منع Service Locator (استدعاءات مباشرة لـ App.ServiceProvider.GetRequiredService لكل من DeviceDetailDialog وCalibrationFormDialog)، واستُبدلتا بحقن مصانع Func<T> عبر Constructor Injection وفق القاعدة المعمارية الأولى.
  * **تحسينات تبويب السجلات (DevicesView وDeviceDetailDialog):** إضافة عمود تسلسل مطلق عبر الصفحات (SequenceNumber محسوب في الـ ViewModel وفق (CurrentPage-1)×PageSize+الترتيب)، تحويل عرض الأعمدة من قيم ثابتة بالبكسل إلى أوزان نسبية (*) مع MinWidth وTooltip، ثم إلى TextWrapping="Wrap" مع ارتفاع صف تلقائي بعد اكتشاف أن الأوزان النسبية وحدها لا تكفي لعرض نصوص عربية-إنجليزية مركبة بالكامل (تقرر تجنب حل "حجم خط ديناميكي" لكسره التناسق البصري). إصلاح قص زر "طباعة ملصق المعايرة" في DeviceDetailDialog عبر تغليف القسم بـ ScrollViewer.
  * **إعادة تصميم DeviceDetailDialog (تخطيط ثلاثي الأعمدة):** تكبير أبعاد النافذة من 850×600 إلى 1200×750 بكسل، وإعادة توزيعها لثلاثة أعمدة (بيانات الجهاز الأساسية 300px ثابت، المخطط الزمني وجدول تاريخ المعايرات 2.5* نسبي، تفاصيل المعايرة النشطة والمرفقات 2* نسبي) لتعمل كلوحة معلومات متكاملة دون تمرير أفقي أو رأسي في الحالة الاعتيادية.
  * **إضافة جهة/نوع جهاز مباشرة من نافذة تسجيل المعايرة (CalibrationFormDialog):** تفعيل IsEditable على ComboBox الخاص بالجهة المالكة ونوع الجهاز مع حارس مزامنة ثنائي الاتجاه (isSyncingOwnerSelection/isSyncingDeviceTypeSelection) لمنع حلقة استدعاء لا نهائية بين خاصيتي Text وSelectedItem. عند إدخال اسم جديد غير موجود، يُنشأ الكيان (Owner/DeviceType) ويُحفظ ضمن معاملة قاعدة بيانات ذرية واحدة (Single Transaction) مع الجهاز والشهادة لمنع السجلات اليتيمة عند فشل الحفظ. فحص التكرار يستثني المحذوف ناعماً (!IsDeleted) مع إعادة إحياء (Reincarnation) للسجلات المحذوفة ناعماً بدلاً من إنشاء معرف جديد. إشعار الواجهات الحية (OwnerViewModel/DeviceTypeViewModel) بالإضافات الجديدة فورياً عبر وسيط أحداث ساكن جديد (MasterDataEvents.cs) بنفس نمط SearchEvents.cs.
  * **نظام الطباعة الدفعية المستقل (BatchPrintPreviewDialog):** بعد عدة محاولات فاشلة لإصلاح تظليل خلايا متعددة في شبكة معاينة PrintPreviewDialog الأصلية (مشكلة عرض بصري متكررة رغم صحة البيانات المؤكدة بالتشخيص عبر كل مراحل الأنبوب)، تقرر معمارياً فصل الطباعة الدفعية في نافذة جديدة كلياً (BatchPrintPreviewDialog) تحتفظ فقط بمنطق اختيار خلية بداية واحدة (المُثبت العمل)، مع قائمة جانبية (ItemsControl) تعرض QR مصغر ورقم شهادة لكل سجل بترتيب تسلسلي، بدلاً من محاولة تظليل عدة خلايا. DevicesViewModel.PrintBatchAsync يوجّه لـ PrintPreviewDialog الأصلي عند تحديد سجل واحد، وBatchPrintPreviewDialog الجديد عند تحديد أكثر من سجل.
  * **اكتشاف وإصلاح عيب بيانات في نظام قوالب الطباعة (PaperTemplate):** التحقق الفعلي على الورق (طباعة حقيقية) كشف أن قالباً محفوظاً مسبقاً يحمل تضارباً داخلياً بين Columns المُعلن (4) والأبعاد الفعلية (MarginLeftMm + Columns×LabelWidthMm + (Columns-1)×HorizontalGapMm = 280mm يتجاوز PaperWidthMm=210mm A4)، مما تسبب في قص العمود الرابع صامتاً عند الطباعة الفعلية رغم ظهوره صحيحاً في المعاينة الرقمية. تم تصحيح القالب المعطوب مباشرة في قاعدة البيانات (تعديل Columns إلى 3)، وإضافة تحقق وقائي دائم (Validation) في PaperTemplateViewModel.SaveAsync يمنع حفظ أي قالب مستقبلي تتجاوز أبعاده الإجمالية المحسوبة حدود الورقة الفعلية.
  * **إضافة تاريخي المعايرة والانتهاء إلى نص ملصق QR المطبوع:** إضافة سطرين جديدين (تاريخ المعايرة وتاريخ الانتهاء بصيغة yyyy-MM-dd) إلى نص معلومات الملصق المطبوع فعلياً على الورق، محدَّثاً بشكل متسق في مساري الطباعة الفردية والدفعية معاً (PrintPreviewViewModel.cs) وفي كود الرسم الفعلي على الطباعة (PrintService.cs). لضمان اتساع النص الجديد (5 أسطر بدلاً من 3) ضمن حدود الملصق (LabelHeightMm=35mm) دون قص أو تداخل مع رمز QR، خُفّض حجم خط معلومات الجهاز من 8pt إلى 7.5pt بعد تحليل حسابي دقيق لارتفاع وعرض كتلة النص مقابل المساحة المتاحة (أظهر هامش أمان 15mm رأسياً و8.29mm أفقياً)، ثم تأكد نجاحه عملياً بطباعة تجريبية فعلية على الورق قبل الاعتماد النهائي - تكراراً لمنهجية "التحقق الفعلي على الورق لا يُغني عنه أي حساب نظري" المعتمدة في هذا المشروع بعد درس عيب بيانات قوالب الطباعة السابق.
  * **شاشة توقف (Screensaver) عند القفل التلقائي للخمول:** إضافة نافذة ScreensaverWindow.xaml جديدة تظهر عند القفل التلقائي بعد الخمول (بدلاً من الانتقال المباشر السابق لشاشة تسجيل الدخول), معروضة بملء الشاشة (Topmost، WindowStyle=None) وبهوية بصرية كاملة: شعار المنظومة داخل بطاقة دائرية بيضاء بظل احترافي (لضمان التباين فوق الخلفية الداكنة)، عنوان النظام، والتسلسل الهرمي الكامل للمؤسسة (مركز البحوث النووية / إدارة الوقاية من الإشعاع / قسم قياس وتقدير الجرعات الشخصية والمعايرة بالذهبي / وحدة المعايرة بالذهبي). عولجت نقطتان تقنيتان معروفتان في WPF: (1) تجاهل أول حركة مؤشر فور ظهور النافذة عبر تأخير 250ms بـ DispatcherTimer لمنع الإغلاق الفوري غير المقصود، (2) استدعاء صريح لـ Activate()/Focus() على الشبكة الجذرية داخل حدث Loaded لضمان استقبال ضغطات لوحة المفاتيح فوراً (نافذة معروضة برمجياً عبر Show() لا تضمن نقل تركيز الكيبورد تلقائياً في WPF). ترتيب الإغلاق في ViewModel_LockRequested حافظ على فتح الشاشة الجديدة قبل إغلاق MainWindow لتفادي كسر منطق ShutdownMode.OnExplicitShutdown القائم على وجود نافذة مفتوحة دائماً.
  * **تحويل بطاقة "توزيع الأجهزة حسب الجهات المالكة" (Dashboard) إلى أشرطة تناسبية:** استبدال القائمة الرقمية البسيطة والـ ProgressBar غير المتجاوب مع RTL بأشرطة أفقية تناسبية حقيقية (Proportional Horizontal Bars) مبنية على عناصر WPF أصلية (Grid بعمودين نجميين + محول جديد DoubleToGridLengthConverter.cs)، بدلاً من LiveCharts2/SkiaSharp، تجنباً لمشكلة تشكيل النص العربي الموثقة سابقاً في هذه المكتبة. عرض كل شريط يُحسب كنسبة من أعلى قيمة في القائمة (BarWidthPercentage)، مع تدرج لوني من الأزرق الداكن #1A3A6B إلى الذهبي #C9A227. أثناء الضبط الدقيق للمسافات، اكتُشف أن خاصية TextAlignment="Left" على عناصر TextBlock تتصرف بشكل غامض وغير متسق مع FlowDirection="RightToLeft" الموروث (سلوك معروف في WPF)، وكانت السبب الجذري وراء تذبذب المسافة بين رقم الترتيب واسم الجهة عبر عدة محاولات تعديل بالنسب والهوامش. الحل الجذري: حذف TextAlignment الصريح والاعتماد على عمود Grid فاصل ثابت العرض بالبكسل (بدلاً من Margin أو محاذاة نص) لضمان مسافة موثوقة لا تتأثر بسلوك RTL الملتبس. النسب النهائية المعتمدة للأعمدة الرئيسية: عمود الرقم+الاسم 1.1*، عمود الشريط الملون 2.4*، عمود العدد 35px ثابت.

---

## القسم 8: الدروس المعمارية المستخلصة (مهمة جداً)

1. **إدارة ملفات الاختبار:** اسم ملف الاختبار قد يظهر بأسماء مختلفة بين الجلسات (`UnitTest1.cs` أو `DatabaseTests.cs`) — لا يؤثر على النتائج، الأهم هو عدد الاختبارات الناجحة الذي يُعرض دائماً في الملخص.
2. **النمط المعتمد للمستودعات:** جميع الـ Repositories تعتمد على حقن `IDbContextFactory<CalQrDbContext>` وتقوم بإنشاء سياق اتصال محلي وقصير المدى داخل كتلة `using var context = ...` مع استخدام `AsNoTracking()` وتفحص حقل الحذف الناعم `!x.IsDeleted` لضمان أقصى سرعة أمان.
3. **توليد والتحقق من التوقيع:** لضمان ثبات التوقيع، تستقبل دوال خدمة `HmacService` قيماً نصية خاماً (Primitives) بدلاً من الكائنات المعقدة لقاعدة البيانات، مع الالتزام باستخدام الـ **Named Parameters** صراحة لمنع أي تبديل خاطئ في ترتيب حقول البيانات المتطابقة في النوع.
4. **تسجيل التبعيات:** يتم تسجيل جميع الـ Repositories والخدمات والـ ViewModels بشكل مركزي في ملف `App.xaml.cs` مع مراجعتها بدقة لمنع تكرار التسجيل.
5. **الربط الحقيقي للـ QR:** تم ربط نماذج الإدخال بخدمات توليد الـ QR والـ HMAC الحقيقية بشكل كامل، ولم يعد هناك أي اعتماد على بيانات وهمية (Placeholders).
6. **معالجة حقول الارتباط القابلة للـ Null:** عند تتبع خصائص التنقل كـ `CalibrationRecord.Device` لا يكفي استخدام علامة `!` لإسكات تحذيرات المترجم، بل يجب كتابة تفحص برمجي فعلي كـ `if (entity != null)` لحماية تشغيل النظام ضد أخطاء `NullReferenceException`.
7. **الطباعة المتعددة (Batch Print):** تم تطبيق دالة `PrintMultipleQrLabels` برمجياً بالكامل في الخدمة، وتأجيل بناء واجهة تحديد المستندات المتعددة لتفادي توسيع نطاق مهام نظام الطباعة في الواجهات حالياً.
8. **التحديث الفوري بين شاشات التطبيق:** تعتمد المنظومة على فئة الأحداث الساكنة البسيطة `CalibrationEvents.cs` لإطلاق حدث `CalibrationChanged` من كافة مصادر تحديث أو حذف البيانات لضمان تحديث الرسوم والعدادات في التبويبات الأخرى بالخلفية فورياً دون تعقيد.
9. **التحقق البصري الميداني للمخرجات:** لا يكفي نجاح التجميع (`dotnet build`) للتحقق من أداء مكتبات توليد التقارير؛ يجب فتح المستندات والشهادات الناتجة ومعاينتها بصرياً للتأكد من مطابقة محاذاة اللغة العربية من اليمين لليسار.
10. **إدارة تجمعات الاتصال (Connection Pools) في SQLite:** عند عمل نسخ احتياطي أو ضغط لقاعدة البيانات النشطة، يجب استدعاء دالة `SqliteConnection.ClearAllPools()` مباشرة قبل بدء عملية الضغط لتجنب حدوث خطأ إقفال الملف من قبل عمليات أخرى، وتوحيد فواصل مسارات الـ ZIP لتكون مائلة للأمام `/` دائماً.
11. **دورة حياة تطبيقات WPF (WPF Application Lifecycle):** إن تجميع البرنامج بنجاح لا يضمن إقلاعه السليم؛ حيث وجب فحص الموارد النشطة مثل تحديث مسارات عناصر Material Design لتتوافق مع الإصدار 5.x، وضبط خاصية `ShutdownMode` لتكون صريحة `OnExplicitShutdown` للتحكم في غلق وتمرير النوافذ دون انهيار التطبيق.
12. **درس رقم 10 - LiveCharts2/SkiaSharp والنص العربي:** أي عنصر واجهة يُرسَم عبر محرك SkiaSharp (المستخدم داخلياً في LiveCharts2 لكل من Legend، Tooltip، وAxis Labels) لا يدعم تشكيل الحروف العربية المتصلة (Arabic Shaping) ولا اتجاه Bidi بشكل صحيح تلقائياً، حتى مع FlowDirection=RightToLeft على الحاوية. الحلول الممكنة مرتبة حسب الأولوية: (1) الأفضل - استبدال العنصر بعنصر WPF أصلي (ItemsControl/TextBlock) حيثما أمكن، لأن محرك WPF يدعم العربية تلقائياً وبثبات كامل؛ (2) إن تعذّر الاستبدال (كما في Axis Labels التي لا بديل WPF أصلي لها) - استخدام الكلاس المساعد المعزول Helpers/ArabicFixer.cs الذي يُشكّل الحروف يدوياً (Isolated/Initial/Medial/Final + Ligatures مثل لا/لأ/لإ/لآ) ثم يعكس اتجاه النص، ويُطبَّق حصراً على النصوص المُرسَلة فعلياً لعناصر LiveCharts2 (Series.Name، Axis.Labels) دون أي تأثير على TextBlock عادي في XAML.

---

## القسم 9: المهام المعلقة
لا توجد مهام معلقة — جميع الميزات المخططة مكتملة.

---

## القسم 10: الأمان
* **حماية كلمة المرور الماستر:** كلمة المرور الماستر الخاصة بالنظام مشفرة وتتم مقارنتها آمنياً في الكود البرمجي باستخدام خوارزمية SHA256 لمنع استخراج القيمة الفعلية من ملفات التجميع.
* **التوقيع الرقمي للشهادات (HMAC):** يتم توليد كود التحقق المطبوع على الـ QR Code بالاعتماد على مفتاح سري ثابت مدمج في الكود، حيث يتم مقارنة التوقيع المحسوب بالتوقيع المخزن للتحقق من أصالة الشهادة ورصد أي تلاعب في بيانات الجهاز.
* **آلية التحقق:** يتم تفحص ومطابقة كلمات المرور بالاعتماد على دالة التحقق الآمنة `PasswordHelper.VerifyPassword`.

---

## القسم 11: كيفية استخدام هذا الملف
> [!IMPORTANT]
> عند بدء جلسة عمل أو محادثة جديدة مع Claude (المهندس المعماري) أو Antigravity IDE، يرجى إرسال محتوى هذا الملف بالكامل كرسالة أولى. سيقرأه النموذج البرمجي ويفهم معمارية المشروع، توزيع الملفات، هيكلية قاعدة البيانات، والقواعد المعمارية الصارمة فوراً، مما يضمن المتابعة الدقيقة والتنفيذ السليم دون الحاجة لإعادة شرح التفاصيل أو استهلاك وقت الجلسة.
