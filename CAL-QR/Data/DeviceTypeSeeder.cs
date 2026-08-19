using System;
using System.Collections.Generic;
using System.Linq;
using CAL_QR.Models;

namespace CAL_QR.Data
{
    /// <summary>حصيلة البذر — تُعاد لتكون مرئية للمستدعي بدل أن تُبتلع بصمت.</summary>
    public sealed class DeviceTypeSeedResult
    {
        public int Created { get; set; }
        public int Renamed { get; set; }
        public int Filled { get; set; }

        /// <summary>
        /// أنواع وُجد اسمها المعتمد **و** أحد مرادفاتها معاً كصفّين منفصلين.
        /// لم يُدمجا ولم يُعد تسمية شيء — الدمج يعني نقل أجهزة بين نوعين وهو
        /// إجراء إتلافي يحتاج قراراً بشرياً.
        /// </summary>
        public List<string> AliasConflicts { get; } = new List<string>();

        /// <summary>
        /// أنواع مطابِقة موجودة لكنها **محذوفة ناعماً**، فبقيت مخفية بعد ملء قالبها.
        /// الأثر على المستخدم: يرى أربعة أنواع، ولا يستطيع إنشاء الخامس لأن اسمه
        /// «محجوز» بصفّ غير مرئي.
        /// لا يُسترجع تلقائياً — قد يكون الإخفاء متعمداً — لكنه يُسجَّل.
        /// </summary>
        public List<string> HiddenTypes { get; } = new List<string>();

        /// <summary>
        /// سبب فشل البذر إن وقع. غير خالٍ يعني أن البذر لم يُنفَّذ، وأن السبب
        /// مكتوب في AppSettings تحت DeviceTypeSeeder.FailureKey، وأن المحاولة
        /// ستُعاد عند الإقلاع التالي.
        /// </summary>
        public Exception? Failure { get; set; }

        /// <summary>هل ثمة ما يستحق عرضه على المدير؟</summary>
        public bool HasDiagnostics =>
            Failure != null || AliasConflicts.Count > 0 || HiddenTypes.Count > 0 || Renamed > 0;

        /// <summary>نصّ مقروء يُخزَّن في AppSettings ويُعرض كما هو.</summary>
        public string ToDiagnosticsText()
        {
            var lines = new List<string>();

            if (Failure != null)
            {
                lines.Add(
                    $"فشل بذر أنواع الأجهزة: {Failure.GetType().Name} — {Failure.Message}. " +
                    "المنظومة تعمل بلا القيم الافتراضية، وستُعاد المحاولة عند الإقلاع التالي.");
            }

            if (Renamed > 0)
            {
                lines.Add($"أُعيدت تسمية {Renamed} نوع جهاز إلى الأسماء المعتمدة (مع الحفاظ على ارتباط الأجهزة).");
            }

            foreach (var conflict in AliasConflicts)
            {
                lines.Add(
                    $"تعارض تسمية: «{conflict}» موجودان معاً كنوعين منفصلين. " +
                    "لم يُدمجا — الدمج ينقل أجهزة بين نوعين ويحتاج قرارك. " +
                    "القالب مُلئ على الاسم المعتمد وحده.");
            }

            foreach (var hidden in HiddenTypes)
            {
                lines.Add(
                    $"النوع «{hidden}» موجود لكنه مخفي (محذوف ناعماً). " +
                    "لن يظهر في القوائم، ولن يُقبل إنشاء نوع جديد بنفس الاسم. " +
                    "استرجعه من إدارة الأنواع إن كان إخفاؤه غير مقصود.");
            }

            return string.Join("\n", lines);
        }
    }

    /// <summary>
    /// بذر أنواع الأجهزة الخمسة وقوالبها من DeviceTypeCatalog.
    ///
    /// ثلاث طبقات حماية من الدهس:
    ///   ١. علم تنفيذ لمرة واحدة في AppSettings — الحارس الأساسي.
    ///   ٢. المطابقة بالاسم المعتمد ثم بالمرادفات، وإصابة المرادف تُعيد التسمية
    ///      ولا تُنشئ صفّاً — فيُحفظ DeviceTypeId وكل الأجهزة المرتبطة به.
    ///   ٣. الملء لا الدهس: حقل يُكتب فقط إن كان فارغاً؛ وصفوف القالب تُزرع فقط
    ///      إن كان النوع بلا صفوف من ذلك النوع إطلاقاً.
    /// </summary>
    public static class DeviceTypeSeeder
    {
        public const string SeedFlagKey = "DeviceTypeTemplatesSeeded_v1";

        /// <summary>
        /// مفتاح حصيلة البذر التشخيصية. يُقرأ عند الإقلاع وتُعرض قيمته للمدير.
        /// وجوده يعني أن البذر واجه حالة تستحق قراراً بشرياً.
        /// </summary>
        public const string ConflictsKey = "DeviceTypeSeedConflicts_v1";

        /// <summary>
        /// مفتاح سبب فشل البذر. وجوده يعني أن الأنواع الخمسة أو قوالبها لم تُزرع،
        /// وأن المحاولة ستُعاد عند الإقلاع التالي.
        /// </summary>
        public const string FailureKey = "DeviceTypeSeedFailure_v1";

        /// <summary>
        /// علم الترقية التصحيحية لمرة واحدة (المرحلة ٣-أ). مستقلّ عن SeedFlagKey
        /// عمداً: الأخير مضبوط سلفاً على كل قاعدة عاملة وهو ما جمّد الخلل، وتصفيره
        /// كان سيُعيد بذراً كاملاً على قواعد لا تحتاجه.
        /// </summary>
        public const string RepairFlagKey = "DeviceTypeCatalogRepair_v1";

        /// <summary>
        /// سبب فشل الترقية التصحيحية إن وقع. وجوده يعني أن العلم لم يُضبط وأن
        /// المحاولة ستُعاد عند الإقلاع التالي.
        /// </summary>
        public const string RepairFailureKey = "DeviceTypeCatalogRepairFailure_v1";

        /// <summary>
        /// مسار الإقلاع. يُنفَّذ مرة واحدة، ووجود علم البذر يعني الخروج فوراً.
        ///
        /// ⚠ **لا يُصعِّد استثناءً أبداً.** هذا مقصود ويخصّ هذه الدالّة وحدها:
        /// تُستدعى من DatabaseMigrator.RunMigrations عند كل إقلاع، واستثناء
        /// يصعد منها يمنع التطبيق من الإقلاع. والأسوأ أن علم البذر لا يُكتب عند
        /// الفشل، فتتكرر المحاولة كل تشغيل — تعطّل دائم على جهاز واحد معزول بلا
        /// مسار إنقاذ.
        ///
        /// والقوالب ميزة ثانوية: المنظومة تعمل كاملةً بدونها، ويفقد المستخدم
        /// القيم الافتراضية لا النظام. فحجب الإقلاع لأجلها مقايضة خاسرة.
        ///
        /// وهذا **ليس catch صامتاً**: السبب يُكتب في AppSettings تحت FailureKey
        /// فيبقى قابلاً للقراءة والتشخيص، وعلم البذر لا يُكتب فتُعاد المحاولة
        /// تلقائياً بعد إصلاح السبب.
        ///
        /// نظيرتها Apply **غير محروسة عمداً**: تُستدعى من تصفير المصنع، وهو إجراء
        /// صريح طلبه المستخدم ونتيجته يجب أن تظهر له فوراً لا أن تُبتلع.
        /// </summary>
        public static DeviceTypeSeedResult SeedIfNeeded(CalQrDbContext context)
        {
            var result = new DeviceTypeSeedResult();

            try
            {
                var flag = context.AppSettings.FirstOrDefault(s => s.Key == SeedFlagKey);
                if (flag != null && flag.Value == "true")
                {
                    return result;
                }

                Apply(context, result);

                if (flag == null)
                {
                    context.AppSettings.Add(new AppSetting { Key = SeedFlagKey, Value = "true", UpdatedAt = DateTime.UtcNow });
                }
                else
                {
                    flag.Value = "true";
                    flag.UpdatedAt = DateTime.UtcNow;
                }

                ClearFailure(context);

                context.SaveChanges();
                return result;
            }
            catch (Exception ex)
            {
                result.Failure = ex;
                RecordFailure(context, ex);
                return result;
            }
        }

        /// <summary>
        /// ترقية تصحيحية تُنفَّذ **مرّة واحدة** على القواعد القائمة (المرحلة ٣-أ).
        ///
        /// السبب: علم البذر SeedFlagKey مضبوط سلفاً على كل قاعدة عاملة، فـSeedIfNeeded
        /// تخرج فوراً ولا تُصلح شيئاً. وقواعد الإنتاج تحمل خللاً مثبتاً:
        ///   • العَلَمان خاطئان على الأنواع الخمسة كلها (كانا يُضبطان عند الإنشاء فقط).
        ///   • نوع Beta Scintillation Probe بلا فحوص ولا مكوّنات عدم يقين وبحقول خالية.
        /// وبلا هذه الترقية تبقى الحالة مجمّدة مهما أُعيد التشغيل.
        ///
        /// لا تُكرّر منطق البذر: تستدعي Apply نفسها. بعد أن صار فرض العَلَمين داخلها،
        /// صارت Apply هي الإصلاح المطلوب حرفياً — تفرض العَلَمين، وتملأ النصّي الناقص،
        /// وتزرع الأبناء الغائبين. ونسخةٌ ثانية من منطق المطابقة كانت ستتباعد عن الأصل
        /// بأول تعديل.
        ///
        /// idempotent بالكامل:
        ///   • FillBlanks لا تكتب إلا على حقل خالٍ ⇒ لا تدهس قيمة كتبها المستخدم.
        ///   • SeedChildTemplates لا تضيف إلا حين لا ابن واحد للنوع ⇒ لا ازدواج صفوف.
        ///   • فرض العَلَمين إسناد لنفس القيمة في كل مرّة.
        /// فإعادة تشغيلها — ولو حُذف علمها يدوياً — بلا ضرر.
        ///
        /// ⚠ نوع أبناؤه ناقصون جزئياً (٣ فحوص من ٥) **يبقى كما هو**: حارس
        /// SeedChildTemplates كلٌّ-أو-لا-شيء عن قصد، لأن إضافة الناقص كانت قد تُحيي
        /// فحصاً حذفه المستخدم عمداً. لا نوع جزئيّ في قواعد اليوم (كلها صفر أو خمسة).
        ///
        /// محروسة كـSeedIfNeeded: لا تُصعِّد استثناءً أبداً. فشلها يُسجَّل في
        /// RepairFailureKey ولا يُسقط الإقلاع، والعلم لا يُكتب فتُعاد المحاولة.
        /// </summary>
        public static DeviceTypeSeedResult RepairCatalogTypesIfNeeded(CalQrDbContext context)
        {
            var result = new DeviceTypeSeedResult();

            try
            {
                var flag = context.AppSettings.FirstOrDefault(s => s.Key == RepairFlagKey);
                if (flag != null && flag.Value == "true")
                {
                    return result;
                }

                Apply(context, result);

                if (flag == null)
                {
                    context.AppSettings.Add(new AppSetting { Key = RepairFlagKey, Value = "true", UpdatedAt = DateTime.UtcNow });
                }
                else
                {
                    flag.Value = "true";
                    flag.UpdatedAt = DateTime.UtcNow;
                }

                var staleFailure = context.AppSettings.FirstOrDefault(s => s.Key == RepairFailureKey);
                if (staleFailure != null)
                {
                    context.AppSettings.Remove(staleFailure);
                }

                context.SaveChanges();
                return result;
            }
            catch (Exception ex)
            {
                result.Failure = ex;
                RecordRepairFailure(context, ex);
                return result;
            }
        }

        /// <summary>
        /// مزامنة الإضافات الجديدة للكتالوج مع القواعد القائمة — بحارس محتوى بلا عَلَم.
        ///
        /// السبب: SeedIfNeeded و RepairCatalogTypesIfNeeded كلتاهما محروستان بعَلَم
        /// مضبوط سلفاً على كل قاعدة عاملة، فحين يكبر الكتالوج بنوع جديد لا يستدعي
        /// أيّ مسار Apply ثانيةً، فيبقى النوع غائباً عن القائمة الحيّة. ربطُ كل إضافة
        /// مستقبلية بعَلَم جديد كان سيفرض تعديل هذا الملف عند كل نوع، وهو ما تتجنّبه
        /// سياسة «الأنواع الجديدة لا تتطلّب تغيير كود القالب».
        ///
        /// الحارس بالمحتوى: إن كان كل اسم معتمد في الكتالوج موجوداً أصلاً (بمطابقة
        /// NameEquals، على كل الصفوف بما فيها المحذوف ناعماً كما تقرأ Apply تماماً،
        /// كي لا يُبعث اسم أُخفي عمداً) ⇒ خروج فوريّ بعد استعلام واحد. وإلّا تُستدعى
        /// Apply مرّة — وهي idempotent: تُطابِق القائم وتفرض أعلامه وتملأ الفارغ فقط،
        /// وتُنشئ الغائب وحده. فتُشفى القاعدة تلقائياً لأيّ إضافة بلا تعديل هذا الملف.
        ///
        /// بلا عَلَم عمداً: الحارس يتصفّر بذاته حالما تُوجد الأسماء، وفشلٌ عابر يُعاد
        /// في الإقلاع التالي. محروسة كنظيرتيها: لا تُصعِّد استثناءً أبداً كي لا تحجب
        /// الإقلاع. وفشلٌ دائم هنا يعني فشل نفس مسار Apply في البذر، وذاك مسجَّل تحت
        /// FailureKey — فلا حاجة إلى مفتاح فشل ثالث.
        /// </summary>
        public static DeviceTypeSeedResult SyncMissingCatalogTypesIfNeeded(CalQrDbContext context)
        {
            var result = new DeviceTypeSeedResult();

            try
            {
                var existingNames = context.DeviceTypes.Select(t => t.Name).ToList();

                bool anyMissing = DeviceTypeCatalog.CanonicalNames
                    .Any(canonical => !existingNames.Any(n => NameEquals(n, canonical)));

                if (!anyMissing)
                {
                    return result;
                }

                Apply(context, result);
                return result;
            }
            catch (Exception ex)
            {
                result.Failure = ex;
                System.Diagnostics.Debug.WriteLine(
                    $"[DeviceTypeSeeder] فشلت مزامنة إضافات الكتالوج: {ex.GetType().Name} — {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// تسجيل سبب فشل الترقية التصحيحية. نظيرة RecordFailure ومبنية على نفس
        /// المبدأ: تُسقط ما تتبّعه المحاولة الفاشلة قبل أي كتابة، ولا تُصعِّد شيئاً.
        /// </summary>
        private static void RecordRepairFailure(CalQrDbContext context, Exception ex)
        {
            try
            {
                context.ChangeTracker.Clear();

                string text =
                    $"فشلت الترقية التصحيحية لأنواع الأجهزة في {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC.\n" +
                    $"السبب: {ex.GetType().Name} — {ex.Message}\n" +
                    "المنظومة تعمل، لكن أعلام الأقسام أو قوالب بعض الأنواع قد تبقى ناقصة. " +
                    "ستُعاد المحاولة تلقائياً عند الإقلاع التالي بعد إصلاح السبب.";

                var setting = context.AppSettings.FirstOrDefault(s => s.Key == RepairFailureKey);
                if (setting == null)
                {
                    context.AppSettings.Add(new AppSetting { Key = RepairFailureKey, Value = text, UpdatedAt = DateTime.UtcNow });
                }
                else
                {
                    setting.Value = text;
                    setting.UpdatedAt = DateTime.UtcNow;
                }

                context.SaveChanges();
            }
            catch (Exception recordingException)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[DeviceTypeSeeder] تعذّر تسجيل فشل الترقية التصحيحية: {recordingException.Message}");
            }
        }

        /// <summary>
        /// تسجيل سبب الفشل في AppSettings. يُستعمل سياقاً جديداً لا السياق الفاشل:
        /// ذاك يحمل تغييرات مُتتبَّعة نصف مطبَّقة، ومحاولة الحفظ عليه تفشل بنفس
        /// السبب فيضيع التسجيل مع البذر.
        /// </summary>
        private static void RecordFailure(CalQrDbContext context, Exception ex)
        {
            try
            {
                // إسقاط كل ما تتبّعه البذر الفاشل قبل أي كتابة
                context.ChangeTracker.Clear();

                string text =
                    $"فشل بذر أنواع الأجهزة وقوالبها في {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC.\n" +
                    $"السبب: {ex.GetType().Name} — {ex.Message}\n" +
                    "المنظومة تعمل، لكن القيم الافتراضية لأنواع الأجهزة غير مزروعة. " +
                    "ستُعاد المحاولة تلقائياً عند الإقلاع التالي بعد إصلاح السبب.";

                var setting = context.AppSettings.FirstOrDefault(s => s.Key == FailureKey);
                if (setting == null)
                {
                    context.AppSettings.Add(new AppSetting { Key = FailureKey, Value = text, UpdatedAt = DateTime.UtcNow });
                }
                else
                {
                    setting.Value = text;
                    setting.UpdatedAt = DateTime.UtcNow;
                }

                context.SaveChanges();
            }
            catch (Exception recordingException)
            {
                // بلوغ هذا الموضع يعني أن قاعدة البيانات نفسها غير قابلة للكتابة،
                // وعندها فشل البذر أهون مشاكل التطبيق وسيظهر الخلل الأصلي في أول
                // عملية حفظ. حجب الإقلاع هنا لا يفيد أحداً ويُخفي السبب الحقيقي.
                System.Diagnostics.Debug.WriteLine(
                    $"[DeviceTypeSeeder] تعذّر تسجيل فشل البذر: {recordingException.Message}");
            }
        }

        /// <summary>محو تسجيل فشل قديم بعد نجاح البذر.</summary>
        private static void ClearFailure(CalQrDbContext context)
        {
            var setting = context.AppSettings.FirstOrDefault(s => s.Key == FailureKey);
            if (setting != null)
            {
                context.AppSettings.Remove(setting);
            }
        }

        /// <summary>
        /// التطبيق بلا فحص العلم — لتصفير المصنع، حيث تُمحى الأنواع كلها ويُعاد
        /// بناؤها بالأسماء المعتمدة نفسها. عامّ عمداً كي لا تُكرَّر الأسماء هناك.
        /// </summary>
        public static DeviceTypeSeedResult Apply(CalQrDbContext context, DeviceTypeSeedResult? result = null)
        {
            result ??= new DeviceTypeSeedResult();

            // كل الأنواع بما فيها المحذوفة ناعماً: نوع أُخفي سابقاً باسم قديم
            // يجب أن يُعاد تسميته لا أن يُنشأ بجانبه توأم.
            var existing = context.DeviceTypes.ToList();

            foreach (var definition in DeviceTypeCatalog.All)
            {
                var canonical = existing.FirstOrDefault(t => NameEquals(t.Name, definition.Name));
                var aliasMatch = existing.FirstOrDefault(
                    t => definition.Aliases.Any(a => NameEquals(t.Name, a)));

                DeviceType target;

                if (canonical != null && aliasMatch != null)
                {
                    // ملاحظة: canonical و aliasMatch قد يكونان محذوفين ناعماً؛
                    // القراءة تشمل المحذوف عمداً كي لا يُنشأ توأم لاسم محجوز.
                    // الاسمان موجودان كصفّين. إعادة التسمية كانت ستُنتج اسمين
                    // متطابقين، والدمج نقل أجهزة بين نوعين — إجراء إتلافي.
                    // القرار: يُملأ القالب على الصفّ المعتمد، ويُترك الآخر كما هو،
                    // ويُسجَّل التعارض ليُعرض على المستخدم لا ليُبتلع.
                    result.AliasConflicts.Add($"{definition.Name} / {aliasMatch.Name}");
                    target = canonical;
                }
                else if (canonical != null)
                {
                    target = canonical;
                }
                else if (aliasMatch != null)
                {
                    // إعادة تسمية الصفّ القائم: يُحفظ Id وكل ارتباطات الأجهزة.
                    // لا تمسّ أي شهادة صادرة، لأن CertificateTemplateType منسوخ
                    // نصاً وقت الإصدار.
                    aliasMatch.Name = definition.Name;
                    result.Renamed++;
                    target = aliasMatch;
                }
                else
                {
                    target = new DeviceType
                    {
                        Name = definition.Name,
                        IsDeleted = false,
                        CreatedAt = DateTime.UtcNow
                        // العَلَمان لا يُضبطان هنا — يُفرضان أدناه على كل نوع
                        // مطابق للكتالوج سواء أنُشئ الآن أم كان قائماً.
                    };
                    context.DeviceTypes.Add(target);
                    existing.Add(target);
                    result.Created++;
                }

                // العَلَمان يُفرضان من الكتالوج فرضاً — دهس مقصود ومحصور.
                //
                // كانا يُضبطان في فرع الإنشاء وحده، فبقي كل نوع **قائم** بعَلَمين
                // خاطئين إلى الأبد: قاعدة الإنتاج أظهرت الأنواع الخمسة كلها
                // بـMethodologyEnabled = false بينما الكتالوج يضبطها true للخمسة،
                // وPancake بخمسة مكوّنات عدم يقين وعَلَمه false. الأثر: قسما عدم
                // اليقين والمنهجية مخفيان في نافذة الشهادة لكل الأنواع.
                //
                // ولماذا يُدهسان بينما FillBlanks تملأ ولا تدهس؟ لأن الحقل النصّي
                // يحتمل «قيمة كتبها المستخدم» فتُصان، أما bool فلا يحتمل «لم يُضبط»:
                // false تعني «مخفي عمداً» و«لم يُضبط قطّ» معاً بلا تمييز، فلا سبيل
                // لصون قرار المستخدم فيه أصلاً. والكتالوج هو مصدر الحقيقة لأنواعه.
                //
                // الحصر مضمون بالبنية: هذه الحلقة لا تدور إلا على
                // DeviceTypeCatalog.All، والمطابقة بالاسم المعتمد أو أحد الألياس،
                // فنوع أنشأه المستخدم لا يبلغ هذا السطر إطلاقاً.
                target.UncertaintyEnabled = definition.UncertaintyEnabled;
                target.MethodologyEnabled = definition.MethodologyEnabled;

                if (FillBlanks(target, definition))
                {
                    result.Filled++;
                }

                // نوع مطابِق لكنه مخفي: مُلئ قالبه لكنه لن يظهر في أي قائمة،
                // ولن يُقبل إنشاء نوع جديد باسمه. لا يُسترجع تلقائياً.
                if (target.IsDeleted)
                {
                    result.HiddenTypes.Add(target.Name);
                }

                SeedChildTemplates(context, target, definition);
            }

            PersistDiagnostics(context, result);

            context.SaveChanges();
            return result;
        }

        /// <summary>
        /// كتابة الحصيلة التشخيصية في AppSettings.
        ///
        /// ⚠ بدون هذا تُبنى DeviceTypeSeedResult وتُملأ ثم تُرمى، فتمرّ حالة
        /// تعارض التسمية بصمت تام: المستخدم يرى ستة أنواع بلا تفسير، وأحدها بلا
        /// قالب، وتشخيص ما جرى بعد وقوعه مستحيل. أثر ذلك مطابق لأثر catch صامت
        /// وإن لم يكن استثناءً — معلومة تشخيصية تُنتَج ثم تُفقد.
        ///
        /// حصيلة نظيفة تمحو المفتاح، حتى لا يبقى تحذير قديم معروضاً بعد زوال سببه.
        /// </summary>
        private static void PersistDiagnostics(CalQrDbContext context, DeviceTypeSeedResult result)
        {
            var setting = context.AppSettings.FirstOrDefault(s => s.Key == ConflictsKey);

            if (!result.HasDiagnostics)
            {
                if (setting != null)
                {
                    context.AppSettings.Remove(setting);
                }
                return;
            }

            string text = result.ToDiagnosticsText();

            if (setting == null)
            {
                context.AppSettings.Add(new AppSetting
                {
                    Key = ConflictsKey,
                    Value = text,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                setting.Value = text;
                setting.UpdatedAt = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// الملء لا الدهس: قيمة كتبها المستخدم لا تُمسّ أبداً. يُعيد true إن
        /// كُتب حقل واحد على الأقل.
        /// </summary>
        private static bool FillBlanks(DeviceType target, DeviceTypeDefinition d)
        {
            bool changed = false;

            void Fill(Func<DeviceType, string?> get, Action<DeviceType, string?> set, string? value)
            {
                if (!string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(get(target)))
                {
                    set(target, value);
                    changed = true;
                }
            }

            Fill(t => t.ProcedureNo, (t, v) => t.ProcedureNo = v, d.ProcedureNo);
            Fill(t => t.CalibrationLocation, (t, v) => t.CalibrationLocation = v, d.CalibrationLocation);
            Fill(t => t.ReferenceGeometry, (t, v) => t.ReferenceGeometry = v, d.ReferenceGeometry);
            Fill(t => t.CountingTime, (t, v) => t.CountingTime = v, d.CountingTime);
            Fill(t => t.CountingUnit, (t, v) => t.CountingUnit = v, d.CountingUnit);
            Fill(t => t.CalibrationMode, (t, v) => t.CalibrationMode = v, d.CalibrationMode);
            Fill(t => t.MethodologyText, (t, v) => t.MethodologyText = v, d.MethodologyText);
            Fill(t => t.TraceabilityReference, (t, v) => t.TraceabilityReference = v, d.TraceabilityReference);
            Fill(t => t.ComplianceVerdict, (t, v) => t.ComplianceVerdict = v, d.ComplianceVerdict);
            Fill(t => t.CalibrationStandard, (t, v) => t.CalibrationStandard = v, d.CalibrationStandard);
            Fill(t => t.Notes, (t, v) => t.Notes = v, d.Notes);
            Fill(t => t.AdditionalInformation, (t, v) => t.AdditionalInformation = v, d.AdditionalInformation);
            Fill(t => t.MeasurementType, (t, v) => t.MeasurementType = v, d.MeasurementType);
            Fill(t => t.Distance, (t, v) => t.Distance = v, d.Distance);
            Fill(t => t.CorrectedReadingFormula, (t, v) => t.CorrectedReadingFormula = v, d.CorrectedReadingFormula);
            Fill(t => t.DetectorType, (t, v) => t.DetectorType = v, d.DetectorType);
            Fill(t => t.Instrumentation, (t, v) => t.Instrumentation = v, d.Instrumentation);

            return changed;
        }

        /// <summary>
        /// صفوف القالب: كل شيء أو لا شيء. دمج جزئي كان سينتج قائمة فحوص نصفها
        /// من الكتالوج ونصفها من المستخدم، بترتيب لا يعكس أي نموذج.
        /// </summary>
        private static void SeedChildTemplates(CalQrDbContext context, DeviceType target, DeviceTypeDefinition d)
        {
            if (d.FunctionalChecks.Length > 0)
            {
                bool hasAny = target.Id != 0 &&
                    context.DeviceTypeFunctionalCheckTemplates.Any(t => t.DeviceTypeId == target.Id);

                if (!hasAny && target.FunctionalCheckTemplates.Count == 0)
                {
                    foreach (var check in d.FunctionalChecks)
                    {
                        target.FunctionalCheckTemplates.Add(new DeviceTypeFunctionalCheckTemplate
                        {
                            SortOrder = check.SortOrder,
                            CheckName = check.CheckName,
                            Requirement = check.Requirement,
                            DefaultResult = check.DefaultResult
                        });
                    }
                }
            }

            if (d.UncertaintyComponents.Length > 0)
            {
                bool hasAny = target.Id != 0 &&
                    context.DeviceTypeUncertaintyComponentTemplates.Any(t => t.DeviceTypeId == target.Id);

                if (!hasAny && target.UncertaintyComponentTemplates.Count == 0)
                {
                    foreach (var component in d.UncertaintyComponents)
                    {
                        target.UncertaintyComponentTemplates.Add(new DeviceTypeUncertaintyComponentTemplate
                        {
                            SortOrder = component.SortOrder,
                            ComponentName = component.ComponentName,
                            EvaluationType = component.EvaluationType,
                            Distribution = component.Distribution
                            // StandardUncertainty و ContributionPercent فارغان عمداً
                        });
                    }
                }
            }
        }

        private static bool NameEquals(string? a, string? b) =>
            string.Equals((a ?? string.Empty).Trim(), (b ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
