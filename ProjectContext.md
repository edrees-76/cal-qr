# CAL-QR — ProjectContext.md
> المرجع الكامل للمشروع | تحديث: 2026-06-30
> يُرسل هذا الملف في بداية كل جلسة جديدة مع Claude أو Antigravity IDE

---

## 1. هوية المشروع

| الحقل | القيمة |
|-------|--------|
| اسم المشروع | CAL-QR |
| الاسم الكامل | نظام معايرة أجهزة المسح الإشعاعي بتقنية QR |
| الإصدار الحالي | 1.0.0 (قيد التطوير) |
| المصمم والمطور | م. إدريس فتح الله الهري |
| الجهة | مركز البحوث النووية — إدارة الوقاية من الإشعاع |
| القسم | قسم قياس وتقدير الجرعات الشخصية والمعايرة |
| الوحدة | وحدة المعايرة |
| تاريخ البدء | 2026-06-30 |

---

## 2. بيئة التطوير

| الأداة | الإصدار |
|--------|--------|
| Antigravity IDE | 2.1.1 |
| VSCode OSS | 1.107.0 |
| Electron | 39.2.3 |
| Node.js | 22.21.1 |
| OS | Windows 10 x64 (10.0.19045) |
| .NET | 8.0 |
| التقنية | WPF / MVVM |
| قاعدة البيانات | SQLite (EF Core) |

---

## 3. الهدف من البرنامج

برنامج سطح مكتب Windows لإدارة شهادات معايرة أجهزة المسح الإشعاعي، يتيح:
- تسجيل بيانات المعايرة لكل جهاز مع تاريخه الكامل
- توليد QR Code محمي بتوقيع HMAC-SHA256 يحتوي على بيانات الشهادة
- طباعة QR على ملصقات حرارية أو ورق A4 أو أي ورق آخر
- التحقق من أصالة شهادات المعايرة ورصد أي تزوير
- تنبيهات انتهاء صلاحية المعايرة
- تصدير تقارير Excel وPDF

---

## 4. المستخدم المستهدف

- مستخدم واحد فقط (مهندس المعايرة)
- يعمل محلياً على جهاز واحد
- البرنامج قابل للتوسع مستقبلاً لأكثر من مستخدم

---

## 5. هيكل المشروع (Solution Structure)

```
CAL-QR/
├── CAL-QR.sln
├── CAL-QR/                          ← المشروع الرئيسي WPF
│   ├── App.xaml
│   ├── App.xaml.cs
│   ├── AssemblyInfo.cs
│   ├── Models/
│   │   ├── Owner.cs
│   │   ├── DeviceType.cs
│   │   ├── Device.cs
│   │   ├── CalibrationRecord.cs
│   │   ├── Attachment.cs
│   │   ├── PaperTemplate.cs
│   │   ├── AuditLog.cs
│   │   └── AppSettings.cs
│   ├── ViewModels/
│   │   ├── Base/
│   │   │   ├── BaseViewModel.cs
│   │   │   └── RelayCommand.cs
│   │   ├── MainViewModel.cs
│   │   ├── DashboardViewModel.cs
│   │   ├── DevicesViewModel.cs
│   │   ├── DeviceDetailViewModel.cs
│   │   ├── CalibrationFormViewModel.cs
│   │   ├── OwnerViewModel.cs
│   │   ├── DeviceTypeViewModel.cs
│   │   ├── QrVerifyViewModel.cs
│   │   ├── ReportsViewModel.cs
│   │   ├── AuditLogViewModel.cs
│   │   └── SettingsViewModel.cs
│   ├── Views/
│   │   ├── SplashWindow.xaml
│   │   ├── LoginWindow.xaml
│   │   ├── MainWindow.xaml
│   │   ├── Tabs/
│   │   │   ├── DashboardView.xaml
│   │   │   ├── DevicesView.xaml
│   │   │   ├── QrVerifyView.xaml
│   │   │   ├── OwnersView.xaml
│   │   │   ├── DeviceTypesView.xaml
│   │   │   ├── ReportsView.xaml
│   │   │   ├── AuditLogView.xaml
│   │   │   └── SettingsView.xaml
│   │   └── Dialogs/
│   │       ├── CalibrationFormDialog.xaml
│   │       ├── DeviceDetailDialog.xaml
│   │       ├── PrintPreviewDialog.xaml
│   │       ├── PaperTemplateDialog.xaml
│   │       └── AttachmentDialog.xaml
│   ├── Services/
│   │   ├── IQrService.cs / QrService.cs
│   │   ├── IHmacService.cs / HmacService.cs
│   │   ├── IPrintService.cs / PrintService.cs
│   │   ├── IExportService.cs / ExportService.cs
│   │   ├── IBackupService.cs / BackupService.cs
│   │   ├── INotificationService.cs / NotificationService.cs
│   │   └── ILocalizationService.cs / LocalizationService.cs
│   ├── Repositories/
│   │   ├── IOwnerRepository.cs / OwnerRepository.cs
│   │   ├── IDeviceTypeRepository.cs / DeviceTypeRepository.cs
│   │   ├── IDeviceRepository.cs / DeviceRepository.cs
│   │   ├── ICalibrationRepository.cs / CalibrationRepository.cs
│   │   ├── IAttachmentRepository.cs / AttachmentRepository.cs
│   │   ├── IPaperTemplateRepository.cs / PaperTemplateRepository.cs
│   │   └── IAuditLogRepository.cs / AuditLogRepository.cs
│   ├── Data/
│   │   ├── CalQrDbContext.cs
│   │   └── DatabaseMigrator.cs
│   ├── Helpers/
│   │   ├── DateHelper.cs
│   │   ├── FileHelper.cs
│   │   └── PasswordHelper.cs
│   ├── Localization/
│   │   ├── Strings.ar-SA.resx
│   │   └── Strings.en-US.resx
│   └── Assets/
│       ├── Logo/
│       │   ├── cal-qr-logo.png
│       │   └── nuclear-center-logo.png
│       └── Icons/
│           └── app-icon.ico
├── CAL-QR.Tests/                    ← مشروع الاختبارات
│   ├── Services/
│   │   ├── QrServiceTests.cs
│   │   ├── HmacServiceTests.cs
│   │   └── ExportServiceTests.cs
│   ├── Repositories/
│   │   ├── DeviceRepositoryTests.cs
│   │   └── CalibrationRepositoryTests.cs
│   └── ViewModels/
│       ├── DashboardViewModelTests.cs
│       └── DevicesViewModelTests.cs
└── Installer/
    └── setup.iss                    ← Inno Setup script
```

---

## 6. نموذج البيانات (ERD)

### جدول: Owners (الجهات المالكة)
```
Id (PK) | Name* | Address | ContactPhone | ContactPerson | IsDeleted | CreatedAt
```

### جدول: DeviceTypes (أنواع الأجهزة)
```
Id (PK) | Name* | IsDeleted | CreatedAt
```

### جدول: Devices (الأجهزة)
```
Id (PK) | Model* | SerialNumber* | OwnerId* (FK) | DeviceTypeId* (FK) | IsDeleted | CreatedAt
```

### جدول: CalibrationRecords (سجلات المعايرة)
```
Id (PK) | DeviceId* (FK) | CertificateNumber* | CalibrationDate* | ExpiryDate* |
EngineerName* | CalibrationDescription | Result* | HmacSignature* | IsDeleted | CreatedAt | UpdatedAt
```

### جدول: Attachments (المرفقات)
```
Id (PK) | CalibrationRecordId* (FK) | FileName | FilePath | FileExtension | UploadedAt
```

### جدول: PaperTemplates (قوالب الورق)
```
Id (PK) | TemplateName* | PaperType | PaperWidth | PaperHeight | Columns | Rows |
LabelWidth | LabelHeight | MarginTop | MarginLeft | HorizontalGap | VerticalGap | IsDefault | CreatedAt
```

### جدول: AuditLog (سجل العمليات)
```
Id (PK) | Action | EntityName | EntityId | Details | ActionAt
```

### جدول: AppSettings (إعدادات التطبيق)
```
Id (PK) | Key | Value | UpdatedAt
```

> * = حقل إلزامي

### العلاقات
- Owner (1) → Device (∞)
- DeviceType (1) → Device (∞)
- Device (1) → CalibrationRecord (∞)
- CalibrationRecord (1) → Attachment (∞)

---

## 7. المكتبات المستخدمة (NuGet Packages)

| المكتبة | الاستخدام |
|---------|-----------|
| Microsoft.EntityFrameworkCore.Sqlite | قاعدة البيانات |
| QRCoder | توليد QR Code |
| MaterialDesignThemes | واجهة المستخدم |
| LiveCharts2 (WPF) | الرسوم البيانية |
| ClosedXML | تصدير Excel |
| QuestPDF | تصدير PDF |
| Cairo Font | الخط الرئيسي |
| xUnit + Moq | الاختبارات |

---

## 8. القواعد المعمارية الصارمة (لا استثناءات)

```
✅ Constructor Injection — ممنوع AppServiceProvider.Resolve
✅ AsNoTracking() — في جميع استعلامات القراءة
✅ IDbContextFactory — في كل ViewModel
✅ ExecuteSqlIfColumnMissing — بدلاً من raw ALTER TABLE
✅ decimal مع double-cast — لـ SQLite GroupBy
✅ حذف ناعم (IsDeleted) — قبل الحذف النهائي
✅ RTL إلزامي — لجميع واجهات العربية
✅ FlowDirection="RightToLeft" — في الـ XAML الجذر عند اللغة العربية
✅ DateFormat: YYYY-MM-DD — في كل مكان
✅ Self-Contained Publish — .NET Runtime مدمج
```

---

## 9. الأمان والتوقيع الرقمي

### آلية HMAC-SHA256
```csharp
// المفتاح السري (ثابت مدمج في البرنامج)
private const string HmacKey = "CalQR-Nuclear-Center-2026-SecretKey";

// البيانات المدخلة في الحساب
string dataToSign = $"{certificateNumber}|{deviceModel}|{serialNumber}|" +
                    $"{ownerName}|{calibrationDate}|{expiryDate}|{result}|{engineerName}";

// ناتج الحساب (8 أحرف hex)
string hmacCode = ComputeHmac(dataToSign, HmacKey).Substring(0, 8).ToUpper();
```

### محتوى QR (ثنائي اللغة)
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
نوع المعايرة / Cal. Type: [الوصف]
النتيجة / Result: [✅ ناجح / ❌ راسب / ⚠️ مشروط]
كود التحقق / Verify Code: [XXXXXXXX]
الجهة المعايِرة: مركز البحوث النووية — وحدة المعايرة
```

---

## 10. هيكل التبويبات الرئيسية

```
[🏠 الرئيسية] [📋 السجلات] [✅ التحقق] [🏢 الجهات] [🔧 الأنواع] [📊 التقارير] [📁 السجل] [⚙️ الإعدادات] [ℹ️ عن البرنامج]
                                                                                              [🔽 إخفاء التبويبات]
```

---

## 11. مواصفات الواجهة

### الألوان الرئيسية
```
الأزرق الداكن (Navy):  #1A3A6B  ← اللون الأساسي
الذهبي:               #C9A227  ← شعار المركز والتمييز
الأخضر:               #2E7D32  ← التحقق والنجاح
الأحمر:               #C62828  ← التحذير والخطأ
الأصفر:               #F9A825  ← التنبيه والقريب من الانتهاء
```

### الخط
```
Cairo — يدعم العربية والإنجليزية معاً
الحجم الرئيسي: 14px | العناوين: 18-22px | الملاحظات: 12px
```

### ألوان حالة الجهاز في الجدول
```
🟢 أخضر  = سارية المفعول
🟡 أصفر  = ستنتهي ضمن حد التنبيه
🔴 أحمر  = منتهية الصلاحية
```

---

## 12. إعدادات AppSettings (المفاتيح)

```
DatabasePath        ← مسار قاعدة البيانات
AttachmentsPath     ← مسار مجلد المرفقات
QrOutputPath        ← مسار مجلد QR
BackupPath          ← مسار النسخ الاحتياطي
BackupSchedule      ← جدول النسخ (يومي/أسبوعي/شهري)
AlertDaysThreshold  ← عدد أيام التنبيه (افتراضي: 30)
AutoLockMinutes     ← مدة الإغلاق التلقائي (افتراضي: 10)
Language            ← اللغة (ar-SA / en-US)
DefaultTemplateId   ← قالب الورق الافتراضي
PasswordHash        ← هاش كلمة المرور
DateFormat          ← صيغة التاريخ (YYYY-MM-DD)
```

---

## 13. اختصارات لوحة المفاتيح

| الاختصار | الوظيفة |
|---------|---------|
| Ctrl+N | سجل معايرة جديد |
| Ctrl+P | طباعة QR |
| Ctrl+F | بحث |
| Ctrl+B | نسخ احتياطي |
| Ctrl+E | تصدير |
| F1 | مساعدة / عن البرنامج |
| Esc | إغلاق النافذة المنبثقة |
| Enter | تأكيد / حفظ |
| Tab | الانتقال بين الحقول |

---

## 14. مواصفات نظام الطباعة

### قوالب الورق (PaperTemplate)
كل قالب يحفظ:
- نوع الورق (رول / A4 / مخصص)
- أبعاد الورق (عرض × ارتفاع بالمم)
- شبكة الملصقات (أعمدة × صفوف)
- أبعاد الملصق الواحد
- الهوامش والمسافات بين الملصقات
- اسم القالب (للاسترجاع السريع)

### معاينة بصرية تفاعلية
- تحاكي شبكة ورق Word
- المستخدم ينقر على الملصق الذي يريد البدء منه
- الملصقات المستخدمة تظهر بلون مختلف
- تحديد موضع البداية قبل الطباعة

### اختيار الطابعة
- يعرض الطابعات المثبتة على الجهاز (Windows PrintDialog)
- يحفظ الطابعة الأخيرة المستخدمة

---

## 15. سيناريوهات الفشل والمعالجة

| السيناريو | المعالجة |
|-----------|---------|
| تلف قاعدة البيانات | رسالة واضحة + توجيه لاستعادة نسخة احتياطية |
| حذف مجلد QR | إعادة إنشاء المجلد تلقائياً |
| حذف مجلد Attachments | تحذير + خيار إعادة تحديد المسار |
| فشل النسخ الاحتياطي | تسجيل في Activity Log + إشعار للمستخدم |
| امتلاء القرص | تحذير مبكر عند وصول المساحة لـ 500MB |
| انقطاع الكهرباء أثناء الطباعة | SQLite transactions تضمن سلامة البيانات |
| ملف مرفق محذوف | عرض "الملف غير متاح" بدلاً من خطأ |

---

## 16. معايير الأداء

- SQLite مع Indexes على: OwnerId, DeviceTypeId, ExpiryDate, IsDeleted
- AsNoTracking() في جميع استعلامات القراءة
- Pagination: 20 سجل في الصفحة افتراضياً
- Dashboard: يُحمَّل مرة عند الفتح ويُحدَّث عند أي تعديل
- QR Generation: أقل من 500ms
- تصدير Excel/PDF: أقل من 3 ثوانٍ لـ 1000 سجل

---

## 17. Roadmap

### الإصدار 1.0 (الحالي)
جميع الميزات الأساسية المذكورة في هذا الملف

### الإصدار 1.1
- تصدير قائمة التنبيهات كـ PDF
- طباعة متعددة (Batch Print) محسّنة
- إحصائيات متقدمة (تقارير زمنية)
- دعم شاشات HiDPI
- تحسين الأداء + Indexes متقدمة

### الإصدار 2.0
- دعم متعدد المستخدمين + صلاحيات
- قاعدة بيانات شبكية (SQL Server)
- إرسال تنبيهات بالبريد الإلكتروني
- API للتكامل مع الأنظمة الخارجية
- توقيع رقمي حقيقي RSA/ECDSA

---

## 18. معلومات المطور

```
الاسم:    م. إدريس فتح الله الهري
المسمى:   مهندس قياسات إشعاعية
الجهة:    مركز البحوث النووية — بنغازي، ليبيا
التخصص:  هندسة قياسات إشعاعية + تطوير برمجيات WPF/.NET 8
المنهجية: AI-Powered Development (Claude + Antigravity IDE)
```

---

## 19. سجل التطوير

| التاريخ | الجلسة | المنجز |
|---------|--------|--------|
| 2026-06-30 | #1 | جمع المتطلبات الكاملة (7 جولات × 15 سؤال) |
| 2026-06-30 | #1 | بناء ERD + Roadmap + ProjectContext.md |
| | | |

---

## 20. ملاحظات خاصة لـ Antigravity IDE

```
⚠️ لا تستخدم AppServiceProvider.Resolve أبداً
⚠️ لا تستخدم ALTER TABLE مباشرة — استخدم ExecuteSqlIfColumnMissing
⚠️ جميع الاستعلامات تستخدم AsNoTracking()
⚠️ جميع الـ ViewModels تستخدم IDbContextFactory
⚠️ الحذف يكون ناعماً (IsDeleted = true) أولاً
⚠️ RTL إلزامي في جميع واجهات العربية
⚠️ التاريخ دائماً YYYY-MM-DD
⚠️ decimal مع (double) cast في SQLite GroupBy
```

---

*آخر تحديث: 2026-06-30 | م. إدريس فتح الله الهري*
