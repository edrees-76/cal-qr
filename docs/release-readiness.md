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
  - PR مفتوح — في انتظار CI وتحقّق إدريس البصريّ.

- **جولة التراجع عن نظام الثيمات + تحسينات الواجهة** — فرع `claude/revert-theme` (من `origin/main` عند `a4519ae`):
  - السبب: قرار إدريس بإزالة نظام الثيمات والوضع الداكن كليّاً والعودة إلى الألوان الثابتة.
  - **الملفّات المحذوفة:**
    - `CAL-QR/Services/ThemeService.cs` — حُذف كليّاً.
    - `CAL-QR/Helpers/StringEqualityConverter.cs` — حُذف كليّاً (لم يعد مستخدماً).
  - **الملفّات المعدَّلة (التراجع):**
    - `CAL-QR/App.xaml.cs` — حذف كتلة تحميل الثيم.
    - `CAL-QR/MainWindow.xaml` — إعادة الألوان الثابتة `#1A3A6B` و`#C9A227`.
    - `CAL-QR/ViewModels/SettingsViewModel.cs` — حذف `SelectedThemeName`، `AppearanceMode`، `ApplyThemeCommand`.
    - `CAL-QR/Views/Tabs/SettingsView.xaml` — حذف بطاقة المظهر السادسة وبانيلها والـConverters المرتبطة، وإبقاء 5 بطاقات Hub-Cards.
  - **الملفّات المعدَّلة (تحسينات الجولة الحاليّة):**
    - `CAL-QR/Assets/Logo/efh-logo.jpg` — **جديد**: شعار EFH المُرفق من إدريس.
    - `CAL-QR/Views/LoginWindow.xaml` — استبدال `logo_original_4k.png` بـ`efh-logo.jpg` في موضعين (تسجيل الدخول + نسيت المرور)، تقليص MinHeight من 600 إلى 480، تقليص هوامش الهيدر وأحجام الأيقونة.
    - `CAL-QR/Views/Tabs/AboutView.xaml` — استبدال الشعار بـ`efh-logo.jpg` في قسم «عن البرنامج».
    - `CAL-QR/Views/Tabs/SettingsView.xaml` — تحويل Expander منطقة الخطر (Grid.Row="3") إلى بطاقة سادسة `CardReset` («إعادة الضبط»)، وإضافة `PanelReset` داخل الـScrollViewer بديلاً؛ مرئيّ للمسؤول فقط (`IsFactoryResetVisible`).
  - لا تغيير في المخطّط ولا الترحيلات ولا حمولة التوقيع ولا HMAC.
  - dotnet build/test: لم يُشغّلا في بيئة الجلسة السحابيّة (لا .NET/Windows)؛ يُشغَّلان على جهاز إدريس.
  - **التحقّق البصريّ (إدريس، 29 سبتمبر 2026):** نجح — الشعار في واجهة الدخول وعن البرنامج ✔، بطاقة «إعادة الضبط» ✔.

- **جولة بوّابة كلمة المرور لبطاقة «إعادة الضبط»** — فرع `claude/revert-theme` (commit `b472e15`):
  - السبب: الضغط على بطاقة «إعادة الضبط» كان يعرض لوح التصفير مباشرةً دون أيّ حماية.
  - **القرار:** لوح بوّابة وسيط `PanelResetGate` يظهر أوّلاً (MultiDataTrigger: `CardReset.IsChecked=True AND IsResetUnlocked=False`)، يطلب كلمة مرور المستخدم الحالي؛ بعد التحقّق بـ`BCrypt.Verify` تُفتح `PanelReset`. مغادرة البطاقة تُعيّن `IsResetUnlocked=false` تلقائياً.
  - **الملفّات المعدَّلة:**
    - `CAL-QR/ViewModels/SettingsViewModel.cs` — `IsResetUnlocked`, `ResetGatePassword`, `UnlockResetCommand`, `UnlockResetAsync()`, `ResetUnlockState()`.
    - `CAL-QR/Views/Tabs/SettingsView.xaml` — `PanelResetGate` (MultiDataTrigger)، trigger الـ`PanelReset` ← `IsResetUnlocked`، `CardReset.Unchecked` event.
    - `CAL-QR/Views/Tabs/SettingsView.xaml.cs` — `TxtResetGatePassword_PasswordChanged`, `CardReset_Unchecked`, `BtnCancelResetGate_Click`.
  - لا تغيير في المخطّط ولا الترحيلات ولا حمولة التوقيع ولا HMAC.
  - dotnet build/test: لم يُشغّلا في بيئة الجلسة السحابيّة (لا .NET/Windows)؛ يُشغَّلان على جهاز إدريس.
  - **التحقّق البصريّ (إدريس، 29 سبتمبر 2026):** نجح.
  - CI (PR #15، commit `7a966d9`): 389 نجح / 0 فشل / 0 تخطّى. تعارض مع `main` في `SettingsView.xaml` (بقايا نظام الثيم) حُلَّ باستبقاء نسخة الفرع (بلا لوح Appearance). دُمج في PR #15 على `main` (29 سبتمبر 2026). كما حُدِّث الـworkflow لتشغيل CI على `push` إلى أيّ فرع `claude/**` لضمان انطلاق CI في الجولات المستقبليّة.

- **جولة القائمة المنسدلة لأسماء المستخدمين في نافذة الدخول** — فرع `feat/login-username-dropdown` (من `main` عند `80e47e8`):
  - السبب: طلب إدريس — بدلاً من كتابة اسم المستخدم يدويّاً في كلّ مرّة، تظهر قائمة منسدلة بأسماء المستخدمين النشطين.
  - **الملفّات المعدَّلة:**
    - `CAL-QR/Views/LoginWindow.xaml` — تحويل `TextBox` إلى `ComboBox` من نمط `MaterialDesignOutlinedComboBox` مع `IsEditable="True"`.
    - `CAL-QR/Views/LoginWindow.xaml.cs` — إضافة حدث `Loaded` يستدعي `_userRepository.GetAllAsync()`، يصفّي المستخدمين النشطين (`IsActive=true`)، ويربط أسمائهم بـ`ItemsSource`. خطأ التحميل يُسجَّل في Debug ولا يمنع فتح النافذة.
  - لا تغيير في المخطّط ولا الترحيلات ولا حمولة التوقيع ولا HMAC.
  - CI (PR #18): 2 فحوصات ناجحة، لا تعارضات. دُمج في `main` (29 سبتمبر 2026).
  - **التحقّق البصريّ (إدريس، 29 سبتمبر 2026):** نجح — القائمة تعرض المستخدمين الثلاثة النشطين (admin new، admin، mohamed).

- **جولة تصحيح تخطيط الإعدادات وإعادة تسمية مجلد الملصق** — فرع `fix/settings-panels-layout` (3 commits → squashed to `3c16dcc`):
  - السبب: (1) حقلا "المسارات" و"الطباعة" و"عام" كانت ممتدّةً بعرض ناقص بسبب `MaxWidth=600` وَ`HorizontalAlignment="Right"` المُطبَّقَين على الحاويات؛ (2) تسمية "مجلد مخرجات QR" لم تعكس أنّ الملصق نصيّ فقط وليس مصدر رموز QR؛ (3) المجلد الافتراضيّ `QR_Output` يجب أن يُصبح `poster` ليتّسق مع الوظيفة.
  - **الملفّات المعدَّلة:**
    - `CAL-QR/Views/Tabs/SettingsView.xaml` — حذف `MaxWidth` وَ`HorizontalAlignment="Right"` من لوحات المسارات والطباعة والعام الثلاث؛ استعادة حقل المجلد مع تغيير التسمية من "مجلد مخرجات QR" إلى "مجلد مخرجات الملصق"؛ تعديل الهامش السفليّ لقسم الملحقات من `0,0,0,20` إلى `0,0,0,15` لاستيعاب الحقل الجديد فوق زرّ الحفظ.
    - `CAL-QR/ViewModels/SettingsViewModel.cs` — المجلد الافتراضيّ `"QR_Output"` → `"poster"`؛ مسار الاستعراض `"QR"` → `"poster"`؛ نصّ رسالة الخطأ محدَّث من "مسار مجلد مخرجات QR الجديد غير صالح" إلى "مسار مجلد مخرجات الملصق الجديد غير صالح". أسماء الخصائص (`QrOutputPath`) والمفتاح في `AppSettings` لم تتغيّر.
    - `CAL-QR/Services/BackupService.cs` — مدخل ZIP محدَّث من `"QR_Output/"` إلى `"poster/"`؛ منطق الاستعادة يدعم كلاً من `"poster/"` (النسخ الجديدة) وَ`"QR_Output/"` (النسخ القديمة) للتوافق مع الإصدارات السابقة.
    - `CAL-QR.Tests/BackupServiceTests.cs` — تحديث التحقّق من اسم مدخل الـZIP من `"QR_Output/test_qr.png"` إلى `"poster/test_qr.png"` ليتطابق مع السلوك الجديد المقصود.
    - `CAL-QR/Services/PrintService.cs` — استُعيدت نسخة `main` (كانت نسخة الفرع قديمة تضمّ بادئة `"CF "` خاطئة في سطور CorrectionFactor).
  - لا تغيير في المخطّط ولا الترحيلات ولا حمولة التوقيع ولا HMAC.
  - CI (PR #20، commit `3c16dcc`): 389 نجح / 0 فشل / 0 تخطّى. دُمج في `main` (29 سبتمبر 2026).
  - **التحقّق البصريّ:** مطلوب من إدريس — التحقّق من أنّ الحقول الثلاثة في لوحة "المسارات" تمتدّ بعرض كامل، وأنّ الحقل المعيد تسميته يظهر "مجلد مخرجات الملصق" بالعنوان الصحيح.

- **جولة توحيد تأثير التمرير في جميع DataGrid** — فرع `feat/datagrid-unified-selection` (من `main` عند `5bd9f8e`):
  - السبب: تأثير التمرير (لون `#1A3A6B` عند وجود المؤشر على الصف) كان يعمل في تبويب المستخدمين فقط لأنّه الوحيد الذي يضبط `RowStyle` صراحةً. باقي التبويبات (السجلات، الأجهزة، الأنواع، الجهات...) تعرض اللون الافتراضيّ لويندوز.
  - **القرار:** نمط DataGrid ضمنيّ عامّ (بلا `x:Key`) في `App.xaml` يضبط `RowStyle` على `UnifiedDataGridRowHoverStyle` تلقائيّاً على **جميع** الجداول دون لمس أيّ ملفّ تبويب.
  - **السلوك المطلوب:**
    - التمرير (بدون نقر): `#1A3A6B` كحليّ داكن ← من `UnifiedDataGridRowHoverStyle`
    - النقر (تحديد خليّة): `#0078D7` أزرق ويندوز ← يبقى كما هو (لا `SelectionUnit=FullRow`)
  - **الملفّات المعدَّلة:**
    - `CAL-QR/App.xaml` — إضافة نمط ضمنيّ عامّ لـDataGrid (7 أسطر).
  - لا تغيير في المخطّط ولا الترحيلات ولا حمولة التوقيع ولا HMAC.
  - dotnet build: 0 أخطاء، 0 تحذيرات (على جهاز إدريس).
  - **التحقّق البصريّ (إدريس، 29 سبتمبر 2026):** نجح.
  - CI (PR #23، commit `8ac4868`): 389 نجح / 0 فشل / 0 تخطّى. دُمج في `main` (29 سبتمبر 2026).

- **جولة اختصارات لوحة المفاتيح + اختبارات DevicesViewModel** — فرع `feat/keyboard-shortcuts-and-tests` (من `main`):
  - السبب: (١) اختصارات Ctrl+N/P/B/E موثَّقة في `CAL-QR_Full_Description.md` لم تكن مربوطة بأوامر؛ (٢) ملفّ `DevicesViewModelTests` كان مفقوداً.
  - **القرار المعماريّ للاختصارات:** أوامر تنقّل مخصَّصة (`NavigateToDevicesCommand`، `NavigateToSettingsCommand`، `NavigateToReportsCommand`) تستدعي `NavigateToTabIfAllowed` التي تتحقّق من صلاحية المستخدم قبل تغيير التبويب؛ هذا يمنع التنقّل إلى تبويبات لا يملك المستخدم صلاحيتها عبر الاختصار.
  - **الملفّات المعدَّلة:**
    - `CAL-QR/MainWindow.xaml` — إضافة 4 `KeyBinding` في `Window.InputBindings`:
      Ctrl+N/P → تبويب الأجهزة، Ctrl+B → تبويب الإعدادات، Ctrl+E → تبويب التقارير.
    - `CAL-QR/ViewModels/MainViewModel.cs` — 3 أوامر جديدة + دالّة `NavigateToTabIfAllowed(int)`.
    - `CAL-QR.Tests/DevicesViewModelTests.cs` — **جديد**: 6 اختبارات (Stubs لـ `IQrService`، `IPrintService`، `IPaperTemplateRepository`، `ICertificateRepository` + `TestCurrentUserService` القائمة).
  - الاختبارات الجديدة: `CanEdit_AdminUser`، `CanEdit_NullUser`، `CanEdit_EditorUser`، `ToggleAdvancedSearch`، `LoadDataAsync_NonDeletedOnly`، `ClearFiltersAsync_Resets`.
  - لا تغيير في المخطّط ولا الترحيلات ولا حمولة التوقيع ولا HMAC ولا أنماط الواجهة.
  - `dotnet build` على `D:\cal-qr`: 0 أخطاء، 1 تحذير xUnit2013 (أُصلح في commit `0af3e4a`).
  - `dotnet test` على جهاز إدريس: **395 نجح / 0 فشل / 0 تخطّى** (29 سبتمبر 2026).
  - **التحقّق البصريّ (إدريس، 29 سبتمبر 2026):** نجح.
  - CI (PR #24، commit `bf2ae66`): لا workflow مُفعَّل في المستودع (0 check runs). دُمج في `main` (29 سبتمبر 2026).

- **جولة تأثير التمرير على نهج Enjaz (نعم/لا)** — فرع `feat/batch-print-ui` (من `main` عند `bf2ae66`):
  - السبب: طلب إدريس تطبيق نهج Enjaz-2026 في تأثير التمرير على الجداول — خلفية فاتحة جدّاً (#EEF2FB) عند التمرير وأزرق ناعم (#D4E3F7) عند التحديد، مع إبقاء النصّ داكناً بدلاً من إظهاره أبيض.
  - **الملفّات المعدَّلة:**
    - `CAL-QR/App.xaml` — إضافة رمزَي لون جديدَين `DataGridHoverBrush (#EEF2FB)` و`DataGridSelectionBrush (#D4E3F7)`؛ إضافة `SelectionUnit=FullRow` إلى نمط DataGrid العامّ؛ إضافة triggers التحديد إلى `DataGridCell`؛ استبدال `UnifiedDataGridRowHoverStyle` (الكحليّ الداكن + النصّ الأبيض) بالنسخة الناعمة على نهج Enjaz؛ تبسيط `UnifiedNavyDataGridIconStyle` و`UnifiedNavyDataGridTextBlockStyle` و`UnifiedSubtleDataGridTextBlockStyle` بحذف triggers White.
    - `CAL-QR/Views/Tabs/DevicesView.xaml` — حذف 4 مجموعات DataTriggers كانت تغيّر Foreground الأيقونات/النصوص إلى White عند التمرير/التحديد (الإصلاح الضروري لأنّ الخلفية لم تعد داكنة).
  - لا تغيير في المخطّط ولا الترحيلات ولا حمولة التوقيع ولا HMAC.
  - dotnet build/test: يُشغَّلان على جهاز إدريس.
  - **التحقّق البصريّ (إدريس، 29 سبتمبر 2026):** نجح — التمرير الناعم بلا تقسيمات أعمدة ✔.

- **جولة إعادة تصميم لوحة البيانات الرئيسيّة** — فرع `claude/quirky-lamport-df0g4s` (من `main` عند `b3d1345`):
  - السبب: طلب إدريس إعادة تصميم لوحة البيانات احترافيّاً مع توزيع مريح ودون تمرير عموديّ، مع تحسين ظاهر للألوان.
  - **المبدأ المختار (① الشبكة المتوازنة):** عنوان كحليّ بتدرّج + 4 بطاقات KPI بتدرّج لونيّ (كحليّ/أخضر/عنبريّ/أحمر) + رسم بيانيّ (44%) على اليسار وجدولان مكدّسان (56%) على اليمين — كلّ ذلك في ثلاثة صفوف بلا تمرير.
  - **التحسينات البصريّة:**
    - رأس الصفحة: تدرّج كحليّ بدلاً من البطاقة البيضاء مع أيقونة جرس لعتبة التنبيه.
    - بطاقات KPI: تدرّج داكن→فاتح لكلّ لون + أيقونة ظلّيّة 54px + سطر ثالث تفسيريّ صغير.
    - شريط لونيّ 4px في أعلى كلّ بطاقة/قسم (تدرّج كحليّ-ذهبيّ للرسم، عنبريّ لجدول «تنتهي قريباً»، أحمر لجدول «منتهية»).
    - تخطيط صفّ 2 من 4 إلى 3 — دمج صفَّي الرسم والجداول في صفّ واحد (*) يملأ المساحة المتبقّية.
  - **الملفّات المعدَّلة:**
    - `CAL-QR/Views/Tabs/DashboardView.xaml` — إعادة كتابة كاملة للتصميم؛ لا تغيير في الـbindings ولا الـcode-behind ولا الـViewModel.
  - لا تغيير في المخطّط ولا الترحيلات ولا حمولة التوقيع ولا HMAC.
  - dotnet build/test: يُشغَّلان على جهاز إدريس (لا .NET/Windows في بيئة الجلسة السحابيّة).
  - التحقّق البصريّ: مطلوب من إدريس — تأكيد أنّ اللوحة لا تحتاج تمريراً وأنّ الألوان واضحة.
  - PR مفتوح — في انتظار CI وتحقّق إدريس البصريّ.

## ملاحظات مفتوحة

- ملفّات `.zip` القديمة غير المقفلة على جهاز المختبر وأيّ قرص خارجيّ/مجلّد سحابيّ تبقى كاشفة لمفتاح
  التوقيع حتى تُحذف يدوياً؛ الاحتفاظ بآخر 10 نسخ صار يعدّ `.cqbak` فقط فلا يحذفها.
- نسخة مؤقّتة مكشوفة من القاعدة تُكتب في `%TEMP%` أثناء النسخ وتُحذف بعده (قائم قبل هذه الجولة)؛
  انقطاع مفاجئ للتيّار في تلك اللحظة قد يُبقيها.

## ملاحظات مُغلقة

- رسالة الـcommit `abf248a` تتضمّن "Verified on screen before commit" بينما لم يجرِ تحقّق بصريّ
  في تلك الجلسة (موثَّق للأمانة؛ لا يُعاد كتابة التاريخ). أُغلقت: أجرى إدريس التحقّق البصريّ من
  عمود النتيجة المنسدل في نافذة الفحوص الوظيفيّة على `main` (`899dd11`) ونجح.
