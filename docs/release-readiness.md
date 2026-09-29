# CAL-QR — الحالة وسجلّ الجولات

**آخر commit على `main` عند إنشاء هذا الملفّ:** `300b3f1` — `feat(certificate): derive CF/CFavg labeling from actual nuclide row counts`
(329 اختباراً ناجحاً محلّيّاً حسب آخر جلسة تطوير.)

## سجلّ الجولات

- **إعداد سير العمل:** إضافة `CLAUDE.md` (قواعد العمل: فروع و PR، لا دفع مباشر إلى `main`،
  حكم «موافق على الدمج» ثمّ دمج إدريس) و `.github/workflows/tests.yml` (بناء واختبار على ويندوز
  لكلّ PR ولكلّ دفع إلى `main`).

- **جولة CF/CFavg (تتمّة `300b3f1`)** — فرع `claude/inspiring-edison-u44rbt`:
  - `6d7acca` معادلة القراءة المصحّحة تتبع القاعدة: `CorrectionFactorLabelRules.FormulaFor`
    تبدّل بين نصّي القالب (`× CF` / `× CFavg`) فقط؛ النصّ المخصّص والحالة المختلطة وغياب
    النظائر بلا تغيير. تُطبَّق في النموذج (عند تغيّر الجدولين وقبل الحفظ) للإصدار الجديد فقط،
    لأنّ النصّ داخل حمولة التوقيع (`RF`): المطبوع = الموقَّع، ولا تبديل لشهادة صادرة.
    بنية الحمولة لم تتغيّر.
  - `d9726a3` سطر الملصق: `CFavg Cs-137 = 1.02` من `NuclideLines` (نفس دالّة سطور شريط
    الشهادة)، وحُذفت البادئة الثابتة `"CF "` من `PrintService`؛ نصّ الاحتياط يتبع
    `ShortLabelFor`.
  - تحديث `CAL-QR/CAL-QR-Project-State.md` (كان متوقّفاً عند 30 يوليو و212 اختباراً).
  - الاختبارات (CI على ويندوز، PR 2، الـcommit `0319498`): **349 نجح / 0 فشل / 0 تخطّى**
    (كانت 329؛ +20 جديدة). البناء: 0 تحذيرات، 0 أخطاء. لم تُشغَّل محلّيّاً (لا `dotnet` في
    بيئة الجلسة السحابيّة).
  - **التحقّق البصريّ (إدريس، 26 سبتمبر 2026، بعد الدمج):** نجح، بحسب إفادة إدريس.
    المطلوب كان: (١) شهادة Pancake جديدة بنظير واحد وقراءة واحدة ⇒ المعادلة `× CF`،
    ثمّ قراءتان ⇒ `× CFavg`. (٢) ملصق شهادة بنظير متوسّط ⇒ `CFavg …`، وعلى أصغر مقاس
    ملصق هل يلجأ إلى نصّ الاحتياط (السطر أطول بحرفين).
    النتيجة على أصغر مقاس: طُبع سطر `CFavg …` كاملاً دون اللجوء إلى نصّ الاحتياط.
  - على جهاز إدريس (`D:\cal-qr`): `dotnet test` ⇒ 349 نجح / 0 فشل / 0 تخطّى؛ و`dotnet build`
    على `main` بعد الدمج (`457418f`) ⇒ 0 تحذيرات، 0 أخطاء.
  - دُمج في PR 2 (`457418f`).

- **جولة تحديث CF/CFavg أثناء تحرير الخليّة** (تغلق الملاحظة المفتوحة من الجولة السابقة):
  - السبب: صفوف الجدولين POCO بلا `INotifyPropertyChanged`، فتعديل اسم النظير أو قيمة CF
    داخل صفّ لا يصل إلى الـViewModel؛ جدول الملخّص لم يكن له معالج `CellEditEnding` أصلاً.
  - الحلّ على النمط القائم (`ConsistencyGrid_CellEditEnding`): `RefreshCorrectionFactorLabels()`
    في `CertificateFormViewModel` يُستدعى بعد انتهاء التحرير من جدول النتائج
    (`ResultsGrid_CellEditEnding`، ويستدعي فحوص التطابق كما كان) وجدول الملخّص
    (`NuclideSummaryGrid_CellEditEnding`). لا تغيير في النماذج ولا المخطّط ولا حمولة التوقيع؛
    حارس وضع التعديل على المعادلة باقٍ.
  - اختباران جديدان في `CertificateFormViewModelTests`.
  - CI على `17cf56c` (PR 4): البناء 0 تحذيرات، 0 أخطاء؛ الاختبارات 351 نجح، 0 فشل، 0 تخطّى.
  - تحقّق بصريّ أجراه إدريس على `main` بعد الدمج: نجح. في شهادة جديدة بصفّين لنظيرين مختلفين،
    تغيير اسم نظير الصفّ الثاني ليطابق الأوّل ⇒ عند مغادرة الخليّة يتحوّل رأس عمود الملخّص إلى
    `Average Correction Factor (CFavg)` والمعادلة إلى `× CFavg`، ويعودان إلى `CF` عند إعادة الاسم.
  - دُمج في PR 4 (`899dd11`). ملاحظة: الدمج سبق تسجيل التحقّق البصريّ، والتحقّق جرى بعده.

- **جولة قفل النسخ الاحتياطيّ بكلمة سرّ** — فرع `claude/inspiring-edison-u44rbt` (من `main` عند `67a3f85`):
  - السبب: `BackupService` كان يضع قاعدة البيانات كاملة، وفيها مفتاح HMAC نصّاً صريحاً، في ZIP بلا
    تشفير (`BackupService.cs:117` قبل التعديل) ⇒ من يملك أيّ نسخة يوقّع شهادات مزوَّرة تجتاز التحقّق.
  - القرار (إدريس فوّض القرار: «انت الخبير»): كلمة سرّ نسخ احتياطيّ يضبطها المدير مرّة، تُحفظ على الجهاز
    مقفلة بـDPAPI (`LocalMachine`) لأنّ النسخ المجدول يعمل بلا أحد، وتُكتب على ورق في الظرف المختوم.
    البديل المرفوض: قفل المفتاح نفسه بـDPAPI وحده ⇒ فقدان الجهاز يُفقد التحقّق من كلّ الشهادات.
  - `Services/BackupEncryption.cs`: صيغة `.cqbak` = AES-256-CBC + HMAC-SHA256 (تشفير ثمّ مصادقة)،
    المفتاحان من PBKDF2-SHA256 بـ600,000 تكرار؛ المصادقة تسبق أيّ فكّ. تدفّقيّة (لا تحمّل الملفّ في الذاكرة).
  - `Services/BackupPasswordStore.cs` (`DpapiBackupPasswordStore`): المفتاح `BackupPasswordProtected`
    في `AppSettings`؛ قيمة لا تُفكّ ⇒ استثناء صريح لا «غير مضبوطة».
  - `BackupService`: بلا كلمة سرّ ⇒ رفض قبل كتابة أيّ ملفّ (يشمل نسخة التصفير الإلزاميّة). الأرشيف
    المكشوف يُبنى في المجلّد المؤقّت ويُحذف. الاستعادة: `.cqbak` بالكلمة المكتوبة أو المحفوظة؛ `.zip`
    القديم يُستعاد مع تحذير. قيمة كلمة السرّ المحفوظة على هذا الجهاز تبقى بعد الاستعادة (كالمسارات).
  - إصلاح catch صامت قائم في `CheckAndRunScheduledBackupAsync`: فشل النسخ المجدول يُسجَّل في سجلّ العمليات.
  - الواجهة: حقول كلمة السرّ وتأكيدها وحالتها، وخانة كلمة سرّ الاستعادة، في بطاقة النسخ الاحتياطيّ
    (أنماط البطاقة نفسها، ومعالجات `PasswordChanged` على النمط القائم لأنّ `PasswordBox` لا يُربط).
    تحديث نصّ «الاستعادة والطوارئ» في المساعدة ودليل المشغّل.
  - حزمة جديدة: `System.Security.Cryptography.ProtectedData` 8.0.0 (مايكروسوفت، لـDPAPI).
  - لا تغيير في حمولة التوقيع ولا HMAC ولا المخطّط ولا الترحيلات. الشهادات الصادرة لا تتأثّر.
  - الاختبارات: `BackupEncryptionTests` (الصيغة، كلمة خاطئة، أيّ بايت معدَّل، ملفّ مقطوع، قواعد الكلمة)،
    `BackupPasswordStoreTests` (DPAPI)، `BackupServiceEncryptionTests` (رفض بلا كلمة، ملفّ مشفَّر فقط،
    كلمة خاطئة لا تمسّ القاعدة، استعادة على جهاز جديد، بقاء كلمة هذا الجهاز، `.zip` القديم).
    اختبارات `BackupServiceTests` و`EndToEndIntegrationTests` القائمة عُدّلت لأنّ الصيغة تغيّرت عمداً:
    مخزن كلمة سرّ اختباريّ في المُنشئ، `*.cqbak` بدل `*.zip`، وفكّ الأرشيف قبل فحص محتواه.
    منطق كلّ اختبار وما يثبته لم يتغيّر.
  - CI على `7ed9d9f` (PR 6): البناء 0 تحذيرات، 0 أخطاء؛ الاختبارات 374 نجح، 0 فشل، 0 تخطّى
    (كانت 351؛ +23 جديدة). لم تُشغَّل محلّيّاً (لا `dotnet` في بيئة الجلسة).
  - تحقّق بصريّ أجراه إدريس (27 سبتمبر 2026، قاعدة تجريبيّة `cal-qr-TRIAL.db`، لقطات شاشة):
    (١) «نسخ احتياطي الآن» بلا كلمة سرّ ⇒ رسالة الرفض؛ (٢) حفظ كلمة السرّ ⇒ الحالة «مضبوطة ✔»
    ورسالة الظرف المختوم؛ (٣) «نسخ احتياطي الآن» ⇒ «تم إنشاء النسخة الاحتياطية بنجاح»؛
    (٤) استعادتها بخانة كلمة الاستعادة فارغة ⇒ «تمت استعادة البيانات بنجاح» (رسالة التأكيد بلا
    تحذير النسخة القديمة، أي أنّ الملفّ المختار مشفَّر). لم تُعرض تجربة الكلمة الخاطئة على الشاشة؛
    يغطّيها اختبار `Restore_WithWrongPassword_FailsAndLeavesLiveDataUntouched`.
    ملاحظة شكليّة ظهرت كما نُبّه عليها: نقاط الخانة تبقى بعد الحفظ (نفس نمط خانات كلمات السرّ القائمة).
  - دُمج في PR 6 (`9649493`). على جهاز إدريس: `dotnet test` ⇒ 374 نجح / 0 فشل / 0 تخطّى
    (على الفرع قبل الانتقال إلى `main`، ومحتواه هو المدموج)؛ `dotnet build` على `main` (`9649493`)
    ⇒ 0 تحذيرات، 0 أخطاء.

- **قرار إدريس (27 سبتمبر 2026): حماية مفتاح التوقيع (HMAC) على الجهاز نفسه محذوفة نهائياً من
  تطوير المنظومة.** لا تشفير لحقل المفتاح في القاعدة الحيّة، ولا DPAPI له، ولا يُقترح البند ثانيةً.
  الحماية المعتمدة: قفل النسخ الاحتياطيّة (PR 6) + نسخة المفتاح الورقيّة في الظرف المختوم.

- **جولة إلغاء الشهادة الصادرة** — فرع `claude/inspiring-edison-u44rbt` (من `main` عند `8d234f8`):
  - السبب: قرب أوّل إصدار شهادة حقيقيّة. الإلغاء ضروريّ لمواجهة الأخطاء وإصدار بديل.
  - **القرار المعماريّ:** `IsRevoked = true` + `IsDeleted = true` معاً ⇒ الفهرس الفريد المشروط
    (`IsDeleted=0`) على `CalibrationRecordId` يتيح شهادة بديلة فوراً؛ رمز التحقّق يعود `Revoked`
    لا `NotFound` فيعلم المتلقّي أنّ الشهادة أُلغيت لا مزوَّرة.
  - **الملفّات المعدَّلة:**
    - `CAL-QR/Models/Certificate.cs` — 4 حقول: `IsRevoked`, `RevokedAt`, `RevokedByName`, `RevocationReason`.
    - `CAL-QR/Data/DatabaseMigrator.cs` — 4 استدعاءات `ExecuteSqlIfColumnMissing` + فهرس `IX_Certificates_IsRevoked`.
    - `CAL-QR/Data/CalQrDbContext.cs` — إعدادات الخاصيّة والفهرس في `OnModelCreating`.
    - `CAL-QR/Repositories/ICertificateRepository.cs` — `Revoked` في `CertificateVerificationStatus`،
      3 حقول في `CertificateVerificationResult`، `RevokeAsync` في الواجهة.
    - `CAL-QR/Repositories/CertificateRepository.cs` — خطوة ٢ في `VerifyByCodeAsync` (الشهادة الملغاة)،
      تنفيذ `RevokeAsync`.
    - `CAL-QR/ViewModels/QrVerifyViewModel.cs` — `Revoked` في `VerificationDisplayState`، 3 خصائص، حالة العرض.
    - `CAL-QR/Views/Dialogs/RevokeCertificateDialog.xaml` + `.cs` — حوار جديد يجمع اسم المُلغي والسبب.
    - `CAL-QR/ViewModels/CertificateFormViewModel.cs` — `RevokeCommand`, `RevokeAsync`, `_loadedCertificateNumber`, `IsEditMode`.
    - `CAL-QR/Views/Dialogs/CertificateFormDialog.xaml` — زرّ «🚫 إلغاء الشهادة» في وضع التعديل.
    - `CAL-QR/Views/Tabs/QrVerifyView.xaml` — حالة `IsRevoked` مع لون وأيقونة وتفاصيل الإلغاء.
    - `CAL-QR.Tests/CertificateRevocationTests.cs` — 9 اختبارات جديدة (SQLite حقيقي).
  - لا تغيير في حمولة التوقيع ولا HMAC. الشهادات الصادرة السابقة لا تتأثّر.
  - CI على `f079a30` (PR 8): البناء 0 تحذيرات، 0 أخطاء؛ الاختبارات **383 نجح، 0 فشل، 0 تخطّى**
    (كانت 374؛ +9 جديدة لاختبارات الإلغاء). دُمج في PR 8 (`32035cc`).
  - **التحقّق البصريّ (إدريس، 27 سبتمبر 2026، بعد الدمج):** نجح.

- **جولة واجهة تحرير قوالب أنواع الأجهزة** — فرع `claude/quirky-lamport-df0g4s` (من `main` عند `32035cc`):
  - السبب: أيّ تعديل في القالب (الـ17 حقلاً + العلمان + الجدولين) كان يتطلّب تعديل كود مباشرة.
  - **الملفّات المعدَّلة:**
    - `CAL-QR/Repositories/IDeviceTypeRepository.cs` — إضافة `UpdateTemplateAsync(DeviceType)`.
    - `CAL-QR/Repositories/DeviceTypeRepository.cs` — تنفيذ `UpdateTemplateAsync`: يحمّل الصفّ متتبَّعاً، يحدّث الـ20 حقلاً، يحذف الأبناء القديمة ويُضيف الجديدة.
    - `CAL-QR/ViewModels/DeviceTypeFormViewModel.cs` — إعادة كتابة كاملة: `FunctionalCheckRow`، `UncertaintyComponentRow`، 17 حقلاً نصّياً + علمان + `IsCatalogType`، `LoadForEditAsync`، `BuildDeviceType`، أوامر الجداول.
    - `CAL-QR/ViewModels/DeviceTypeViewModel.cs` — `OpenEditTypeAsync` يستدعي `LoadForEditAsync(id)` بدلاً من `LoadForEdit(item)`.
    - `CAL-QR/Views/Dialogs/DeviceTypeFormDialog.xaml` — إعادة كتابة كاملة: نافذة 820×720، شريط تحذير للأنواع الكتالوجيّة، TabControl بثلاثة تبويبات.
    - `CAL-QR.Tests/DeviceTypeTemplateUpdateTests.cs` — 6 اختبارات جديدة (SQLite حقيقي).
  - لا تغيير في المخطّط ولا الترحيلات ولا حمولة التوقيع.
  - CI على `41b96bf` (PR 10): البناء 0 تحذيرات، 0 أخطاء؛ الاختبارات **389 نجح، 0 فشل، 0 تخطّى**
    (كانت 383؛ +6 جديدة). دُمج في PR 10.
  - **التحقّق البصريّ (إدريس، 27 سبتمبر 2026، بعد الدمج):** نجح.

- **جولة نظام الثيم وإعادة تصميم الإعدادات** — فرع `claude/quirky-lamport-df0g4s` (من `main` بعد دمج PR 10):
  - السبب: طلب إدريس تحسين المظهر وإضافة الوضع الداكن وإعادة تصميم قسم الإعدادات الذي كان مزدحماً.
  - **القرارات:** (أ) 4 لوحات ألوان قابلة للتبديل وقت التشغيل (Steel/Gold/Cobalt/Olive)؛ (ب) 3 أوضاع إضاءة (فاتح/داكن/النظام)؛ (ج) تصميم «بطاقات تشعّبيّة» (Hub Cards) للإعدادات.
  - **الملفّات المعدَّلة:**
    - `CAL-QR/Services/ThemeService.cs` — **جديد**: `Palettes` (4 ألوان) + `Apply(name, mode)` يُحدّث MaterialDesign PaletteHelper والفراشي المخصّصة في `App.Resources` دفعةً واحدة.
    - `CAL-QR/App.xaml.cs` — تحميل الثيم المحفوظ قبل أيّ نافذة (بعد الترحيلات، قبل HMAC).
    - `CAL-QR/MainWindow.xaml` — الخلفية وأيقونتا المستخدم/قاعدة البيانات والتبويب المحدَّد ← `DynamicResource` بدل ألوان ثابتة.
    - `CAL-QR/ViewModels/SettingsViewModel.cs` — `SelectedThemeName`، `AppearanceMode`، `ApplyThemeCommand`، `ApplyThemeAsync` (يطبّق ويحفظ في AppSettings).
    - `CAL-QR/Views/Tabs/SettingsView.xaml` — إعادة كتابة كاملة: 6 بطاقات تشعّبيّة RadioButton، 6 لوحات مخفيّة/مرئيّة بـDataTrigger، منطقة الخطر Expander في الأسفل.
    - `CAL-QR/Helpers/StringEqualityConverter.cs` — **جديد**: `IValueConverter` يربط `RadioButton.IsChecked` بخاصية `string` في الـViewModel.
  - لا تغيير في المخطّط ولا الترحيلات ولا حمولة التوقيع ولا HMAC.
  - dotnet build/test: لم يُشغّلا في بيئة الجلسة السحابيّة (لا .NET/Windows)؛ يُشغَّلان على جهاز إدريس.
  - **CI (PR 12):** اجتاز ← 389 نجح / 0 فشل. **التحقّق البصريّ (إدريس):** ظهرت 3 ملاحظات مرئيّة (يُعالجها الجولة التالية).
  - دُمج في PR 12 (`a4519ae`).

- **جولة إصلاح مشاكل الثيم الثلاث** — فرع `claude/quirky-lamport-df0g4s` (من `main` عند `a4519ae`):
  - **السبب (3 ملاحظات مرئيّة أبلغ عنها إدريس بعد اختبار PR 12):**
    1. «لا تتغير المنظومة بالكامل فقط لون العنوان الرئيسى» — شريط التبويبات وخلفية النافذة وألواح الإعدادات بقيت مُشفَّرة على White/#F5F5F5.
    2. «عند الوضع الداكن اسماء التبويبات لا تظهر» — `Foreground="#606060"` في حالة IsSelected=False أصبح غير مرئيّ على خلفية داكنة.
    3. «ايقونات الاعدات غير مناسبة» — `Kind="{TemplateBinding Tag}"` مع `Tag` نصّيّ (string) لا يُحوَّل تلقائيّاً إلى `PackIconKind` enum.
  - **الإصلاحات المعماريّة:**
    - **السبب الجذريّ لإصلاح 1+2:** `StaticResource` لا يُحدَّث عند تغيير مورد وقت التشغيل؛ الحلّ: `DynamicResource`.
    - **السبب الجذريّ لإصلاح 3:** WPF لا يُحوّل string إلى PackIconKind في TemplateBinding؛ الحلّ: `{x:Static materialDesign:PackIconKind.XYZ}` مباشرةً في `Tag`.
  - **الملفّات المعدَّلة:**
    - `CAL-QR/App.xaml` — تغيير 22 مرجع `StaticResource PrimaryInputForeground` إلى `DynamicResource` في أنماط TextBox/PasswordBox/ComboBox/DatePicker حتّى يتحدّث لون النصّ عند تبديل الثيم.
    - `CAL-QR/MainWindow.xaml` — (أ) خلفية النافذة `#F5F5F5` → `MaterialDesignPaper`؛ (ب) خلفية شريط التبويبات `White` → `MaterialDesignPaper`؛ (ج) `#606060` في IsSelected=False → `MaterialDesignBody`؛ (د) hover `#F5F5F5` → `MaterialDesignBackground`.
    - `CAL-QR/Views/Tabs/SettingsView.xaml` — (أ) `Tag="XYZ"` → `{x:Static materialDesign:PackIconKind.XYZ}` لـ 9 أيقونات؛ (ب) `Background="White"` → `MaterialDesignPaper` على 7 Borders؛ (ج) `#374151/#6B7280` → `MaterialDesignBody` على النصوص.
  - لا تغيير في المخطّط ولا الترحيلات ولا حمولة التوقيع ولا HMAC.
  - dotnet build/test: يُشغَّلان على جهاز إدريس (لا .NET/Windows في بيئة الجلسة).
  - PR 13 — في انتظار CI وتحقّق إدريس البصريّ.

- **جولة الثيم الشامل (Tinted Surfaces)** — فرع `claude/quirky-lamport-df0g4s` (من `main` عند `a4519ae` + الجولة السابقة):
  - **السبب:** إدريس أراد أن يتغيّر لون الخلفيّة الرئيسيّة والألواح عند تبديل لوحة الألوان مثل Antigravity IDE.
  - **القرار المعماريّ:** إضافة 4 موارد ديناميكيّة (`AppBackground`, `AppSurface`, `AppSurfaceAlt`, `AppBorder`) تُحسب في ThemeService من اللوحة الحاليّة — نبرة شاحبة جداً في الوضع الفاتح (5-12% خلط) وداكنة جداً في الوضع الداكن. ألوان الحالة (أحمر/أخضر/برتقالي) وأيقونات العمليّة تبقى ثابتة لأنّها دلاليّة.
  - **الملفّات المعدَّلة:**
    - `CAL-QR/Services/ThemeService.cs` — `TintLight`/`TintDark` + `IsSystemDark` + حساب الـ 4 موارد في `Apply()`.
    - `CAL-QR/App.xaml` — إضافة الـ 4 موارد الافتراضيّة + أنماط DataGrid/DataGridColumnHeader/DataGridCell لدعم الوضع الداكن + إصلاح `UnifiedDataGridRowHoverStyle` من StaticResource إلى DynamicResource.
    - `CAL-QR/MainWindow.xaml` — خلفية النافذة ← `AppBackground`.
    - 8 تبويبات (Dashboard, Devices, DeviceTypes, Owners, QrVerify, Reports, Users, Help) — `Background="White"` و`#F5F5F5/#FAFAFA/#F8FAFC` → `AppSurface`/`AppSurfaceAlt`؛ `BorderBrush="#E0E0E0/#E2E8F0"` → `AppBorder`.
    - `AboutView.xaml` + `HelpView.xaml` — `#1A3A6B` في Background/Foreground → `DynamicResource PrimaryNavy`.
  - لا تغيير في المخطّط ولا الترحيلات ولا حمولة التوقيع ولا HMAC.
  - dotnet build/test: يُشغَّلان على جهاز إدريس.
  - PR 14 — في انتظار CI وتحقّق إدريس البصريّ.

- **جولة مراجعة الثيم الشاملة (Color Audit)** — فرع `claude/quirky-lamport-df0g4s` (commit `b129a30`):
  - **السبب:** إدريس أبلغ عن DataGrid صفوف بيضاء غير مقروءة في الوضع الداكن في جميع الأقسام، وطلب مراجعة شاملة لكلّ الثيمات.
  - **تشخيص السبب الجذريّ:** (أ) `UnifiedDataGridRowHoverStyle` كان يستخدم `StaticResource PrimaryNavy` الذي لا يُحدَّث وقت التشغيل. (ب) الألوان المشفَّرة مباشرة `#1A3A6B/#C9A227/#707070/#E0E0E0` في 27 ملفّ XAML لا تستجيب للثيم.
  - **الإصلاحات:**
    - `App.xaml` — أنماط DataGrid/DataGridColumnHeader/DataGridCell بـ`MaterialDesignPaper`/`MaterialDesignBackground`/`MaterialDesignBody` (تتكيّف مع الوضع الداكن تلقائيّاً)؛ إصلاح 4 أنماط في `UnifiedDataGrid*Style` إلى DynamicResource.
    - **14 نافذة حوار** (كلّها): `Background="White"` → `{DynamicResource AppSurface}`، `#1A3A6B` → `{DynamicResource PrimaryNavy}`، `#C9A227` → `{DynamicResource GoldAccent}`. استثناء: `PaperCanvas` أُبقي `White` (محاكاة ورقة طباعة حقيقيّة).
    - **10 تبويبات** + **LoginWindow** + **FirstRunWizard** + **ScreensaverWindow** + **SplashWindow**: نفس التحويلات؛ `BorderBrush="#E0E0E0"` → `{DynamicResource AppBorder}`.
    - `DashboardView.xaml` — إعادة `GradientStop Color="{DynamicResource PrimaryNavy}"` إلى `Color="#1A3A6B"` (WPF لا يدعم DynamicResource على GradientStop.Color لأنّه Color وليس Brush).
    - `#801A3A6B` (تراكب شفّاف زخرفيّ في LoginWindow) + ألوان الحالة الدلاليّة (`#C62828`, `#2E7D32`, `#F9A825`) — أُبقيت ثابتة.
  - **النتيجة:** 0 مراجع ألوان مشفَّرة قابلة للتبديل متبقّية؛ مرجعان وحيدان في GradientStop لا يدعمان DynamicResource (قيد WPF)، وهما ثابتان من هويّة التطبيق.
  - لا تغيير في المخطّط ولا الترحيلات ولا حمولة التوقيع ولا HMAC.
  - dotnet build/test: يُشغَّلان على جهاز إدريس.
  - PR 14 — يحتاج تحقّق إدريس البصريّ (الوضع الداكن والفاتح على كلّ الأقسام).

## ملاحظات مفتوحة

- ملفّات `.zip` القديمة غير المقفلة على جهاز المختبر وأيّ قرص خارجيّ/مجلّد سحابيّ تبقى كاشفة لمفتاح
  التوقيع حتى تُحذف يدوياً؛ الاحتفاظ بآخر 10 نسخ صار يعدّ `.cqbak` فقط فلا يحذفها.
- نسخة مؤقّتة مكشوفة من القاعدة تُكتب في `%TEMP%` أثناء النسخ وتُحذف بعده (قائم قبل هذه الجولة)؛
  انقطاع مفاجئ للتيّار في تلك اللحظة قد يُبقيها.

## ملاحظات مُغلقة

- رسالة الـcommit `abf248a` تتضمّن "Verified on screen before commit" بينما لم يجرِ تحقّق بصريّ
  في تلك الجلسة (موثَّق للأمانة؛ لا يُعاد كتابة التاريخ). أُغلقت: أجرى إدريس التحقّق البصريّ من
  عمود النتيجة المنسدل في نافذة الفحوص الوظيفيّة على `main` (`899dd11`) ونجح.
