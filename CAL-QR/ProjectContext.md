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
  * **نافذة تأكيد الخروج:** إضافة صندوق حوار لتأكيد الخروج ("هل تريد الخروج من المنظومة؟") عند إغلاق نافذة الدخول، وعند غلق النافذة الرئيسية (X أو Alt+F4)، وعند النقر على زر "خروج" الأحمر في الترويسة الرئيسية، مع تخطيه صامتاً فقط عند تسجيل الخروج التلقائي للخمول.
  * **توسيع النوافذ وتعديل النصوص:** توسيع قياس واجهات الدخول والتحميل الترحيبي وإعادة هيكلة مصفوفة حقوق المطورين لتظهر مرتبة بشكل عمودي في 3 أسطر متناسقة.
  * **تعديل واجهة عن البرنامج (AboutView):** تم حذف شعار مركز البحوث النووية والاكتفاء بشعار المنظومة، وإضافة "تاجوراء" لاسم الجهة وحذف سطر حقوق الجهة من الأسفل، وتعديل عنوان وصياغة الحقوق لتقتصر على "تصميم و تطوير و تنفيد" متبوعة بأسماء المطورين (م. إدريس فتح الله الهري وم. رضا المريمى) عمودياً بشكل متناسق.
  * **أزرار التحكم (تصغير/تكبير):** دعم أزرار تصغير وتكبير وإغلاق الشباك بطريقة احترافية مع قفل أبعاد بطاقة الدخول وتوسيطها بالمنتصف وتفعيل خاصية السحب اليدوي الخارجي.
  * **خلفية التكبير الضبابية (Acrylic Background):** ربط خلفية معتمة تمنع إظهار سطح المكتب وتظهر صورة معدات المعايرة بشكل ضبابي ومموه جذاب بدقة تضبيب 18 بكسل وفلتر كحلي.
  * **قسم الدليل والمساعدة:** بناء شاشة `HelpView` المتكاملة برمجياً بناءً على دليل التشغيل الرسمي لمشغل النظام.

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

---

## القسم 9: المهام المعلقة
* **الطباعة المتعددة في الواجهة:** دالة `PrintMultipleQrLabels` متوفرة وجاهزة برمجياً بالكامل داخل خدمة `PrintService` ولكن لا يوجد عنصر تحكم في واجهة `DevicesView` لتفعيل التحديد المتعدد للأجهزة وإرسالها للطباعة دفعة واحدة حتى الآن (مؤجلة لحين طلب المستخدم لتفعيلها في شاشات الواجهة).

---

## القسم 10: الأمان
* **حماية كلمة المرور الماستر:** كلمة المرور الماستر الخاصة بالنظام مشفرة وتتم مقارنتها آمنياً في الكود البرمجي باستخدام خوارزمية SHA256 لمنع استخراج القيمة الفعلية من ملفات التجميع.
* **التوقيع الرقمي للشهادات (HMAC):** يتم توليد كود التحقق المطبوع على الـ QR Code بالاعتماد على مفتاح سري ثابت مدمج في الكود، حيث يتم مقارنة التوقيع المحسوب بالتوقيع المخزن للتحقق من أصالة الشهادة ورصد أي تلاعب في بيانات الجهاز.
* **آلية التحقق:** يتم تفحص ومطابقة كلمات المرور بالاعتماد على دالة التحقق الآمنة `PasswordHelper.VerifyPassword`.

---

## القسم 11: كيفية استخدام هذا الملف
> [!IMPORTANT]
> عند بدء جلسة عمل أو محادثة جديدة مع Claude (المهندس المعماري) أو Antigravity IDE، يرجى إرسال محتوى هذا الملف بالكامل كرسالة أولى. سيقرأه النموذج البرمجي ويفهم معمارية المشروع، توزيع الملفات، هيكلية قاعدة البيانات، والقواعد المعمارية الصارمة فوراً، مما يضمن المتابعة الدقيقة والتنفيذ السليم دون الحاجة لإعادة شرح التفاصيل أو استهلاك وقت الجلسة.
