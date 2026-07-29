using System;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CAL_QR.Data;
using CAL_QR.Models;

namespace CAL_QR.Tests
{
    /// <summary>
    /// بذر أنواع الأجهزة الخمسة وقوالبها.
    ///
    /// أهم اختبار هنا AliasMatch_RenamesExistingRow_KeepsIdAndDevices: هو الحارس
    /// على أن ترحيل قاعدة قائمة لا يُنتج أنواعاً مضاعفة ولا يفصل الأجهزة عن نوعها.
    ///
    /// SQLite حقيقي لا InMemory: الاختبارات تعتمد على المفاتيح الأجنبية وسلوك
    /// Cascade والفهارس التي لا يطبّقها مزوّد InMemory.
    /// </summary>
    public class DeviceTypeTemplateSeedingTests
    {
        private static string NewDbPath(string tag) =>
            Path.Combine(Path.GetTempPath(), $"cal_qr_devtype_seed_{tag}_{Guid.NewGuid():N}.db");

        private static DbContextOptions<CalQrDbContext> OptionsFor(string dbPath) =>
            new DbContextOptionsBuilder<CalQrDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

        private static void CleanUp(string dbPath)
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                try { File.Delete(dbPath); } catch { }
            }
        }

        [Fact]
        public void Seed_OnEmptyDatabase_CreatesExactlyFiveCanonicalTypes()
        {
            string dbPath = NewDbPath("fresh");
            var options = OptionsFor(dbPath);
            try
            {
                using (var context = new CalQrDbContext(options))
                {
                    DatabaseMigrator.RunMigrations(context);
                }

                using (var context = new CalQrDbContext(options))
                {
                    var names = context.DeviceTypes.Select(t => t.Name).OrderBy(n => n).ToList();

                    Assert.Equal(5, names.Count);
                    Assert.Equal(
                        DeviceTypeCatalog.CanonicalNames.OrderBy(n => n).ToArray(),
                        names.ToArray());
                }
            }
            finally { CleanUp(dbPath); }
        }

        [Fact]
        public void AliasMatch_RenamesExistingRow_KeepsIdAndDevices()
        {
            // الحارس على ت-١: قاعدة تحمل الأسماء القديمة الثلاثة، وأجهزة مرتبطة بها.
            // بعد الهجرة يجب أن تكون خمسة أنواع بالضبط، بأسماء معتمدة، وبنفس
            // المعرّفات، والأجهزة لم تفقد ارتباطها.
            string dbPath = NewDbPath("alias");
            var options = OptionsFor(dbPath);
            try
            {
                int gammaId, betaId, pedId, deviceId;

                using (var context = new CalQrDbContext(options))
                {
                    // مخطط كامل، ثم محاكاة قاعدة قائمة سابقة للمرحلة ٢:
                    // نمسح ما بذرته الهجرة ونضع الأسماء القديمة مكانه.
                    DatabaseMigrator.RunMigrations(context);
                    context.DeviceTypes.RemoveRange(context.DeviceTypes.ToList());
                    context.SaveChanges();

                    var gamma = new DeviceType { Name = "Gamma Probe" };
                    var beta = new DeviceType { Name = "Beta Scintillator Probe" };
                    var ped = new DeviceType { Name = "PED" };
                    context.DeviceTypes.AddRange(gamma, beta, ped);
                    context.SaveChanges();

                    gammaId = gamma.Id;
                    betaId = beta.Id;
                    pedId = ped.Id;

                    var owner = new Owner { Name = "مستشفى بنغازي الطبي" };
                    context.Owners.Add(owner);
                    context.SaveChanges();

                    var device = new Device
                    {
                        Model = "Ludlum 44-2",
                        SerialNumber = "G501",
                        OwnerId = owner.Id,
                        DeviceTypeId = gammaId
                    };
                    context.Devices.Add(device);
                    context.SaveChanges();
                    deviceId = device.Id;

                    // إزالة علم البذر لمحاكاة قاعدة لم تُبذر بعد
                    var flag = context.AppSettings.Single(s => s.Key == DeviceTypeSeeder.SeedFlagKey);
                    context.AppSettings.Remove(flag);
                    context.SaveChanges();
                }

                using (var context = new CalQrDbContext(options))
                {
                    DatabaseMigrator.RunMigrations(context);
                }

                using (var context = new CalQrDbContext(options))
                {
                    // خمسة بالضبط — لا ثمانية
                    Assert.Equal(5, context.DeviceTypes.Count());

                    Assert.Equal(
                        DeviceTypeCatalog.CanonicalNames.OrderBy(n => n).ToArray(),
                        context.DeviceTypes.Select(t => t.Name).OrderBy(n => n).ToArray());

                    // المعرّفات لم تتغير: إعادة تسمية لا إنشاء
                    Assert.Equal("Gamma Scintillation Probe", context.DeviceTypes.Single(t => t.Id == gammaId).Name);
                    Assert.Equal("Beta Scintillation Probe", context.DeviceTypes.Single(t => t.Id == betaId).Name);
                    Assert.Equal("Personal Electronic Dosimeter (PED)", context.DeviceTypes.Single(t => t.Id == pedId).Name);

                    // الجهاز ما زال مرتبطاً بنوعه، والنوع يحمل قالبه الآن
                    var device = context.Devices.Single(d => d.Id == deviceId);
                    Assert.Equal(gammaId, device.DeviceTypeId);

                    var gamma = context.DeviceTypes.Single(t => t.Id == gammaId);
                    Assert.Equal("APPROVED FOR OPERATIONAL RADIATION SAFETY USE", gamma.ComplianceVerdict);
                    Assert.Equal(5, context.DeviceTypeFunctionalCheckTemplates.Count(t => t.DeviceTypeId == gammaId));
                }
            }
            finally { CleanUp(dbPath); }
        }

        [Fact]
        public void Seed_RunTwice_DoesNotDuplicateAnything()
        {
            string dbPath = NewDbPath("twice");
            var options = OptionsFor(dbPath);
            try
            {
                using (var context = new CalQrDbContext(options))
                {
                    DatabaseMigrator.RunMigrations(context);
                }
                using (var context = new CalQrDbContext(options))
                {
                    DatabaseMigrator.RunMigrations(context);
                }

                using (var context = new CalQrDbContext(options))
                {
                    Assert.Equal(5, context.DeviceTypes.Count());
                    // Pancake وحده يحمل مكوّنات عدم يقين في الكتالوج
                    Assert.Equal(5, context.DeviceTypeUncertaintyComponentTemplates.Count());
                    // خمسة فحوص لكل نوع عدا Beta (نصّه [يحتاج تأكيد])
                    Assert.Equal(20, context.DeviceTypeFunctionalCheckTemplates.Count());
                }
            }
            finally { CleanUp(dbPath); }
        }

        [Fact]
        public void Seed_DoesNotOverwriteUserEditedValues()
        {
            string dbPath = NewDbPath("nooverwrite");
            var options = OptionsFor(dbPath);
            try
            {
                using (var context = new CalQrDbContext(options))
                {
                    DatabaseMigrator.RunMigrations(context);

                    var pancake = context.DeviceTypes.Single(t => t.Name == "Pancake Probe");
                    pancake.ProcedureNo = "قيمة عدّلها المستخدم";
                    context.SaveChanges();

                    // إعادة تشغيل البذر رغم العلم
                    DeviceTypeSeeder.Apply(context);
                }

                using (var context = new CalQrDbContext(options))
                {
                    Assert.Equal(
                        "قيمة عدّلها المستخدم",
                        context.DeviceTypes.Single(t => t.Name == "Pancake Probe").ProcedureNo);
                }
            }
            finally { CleanUp(dbPath); }
        }

        [Fact]
        public void Seed_FillsOnlyBlankFields_LeavingOthersUntouched()
        {
            string dbPath = NewDbPath("fillblank");
            var options = OptionsFor(dbPath);
            try
            {
                using (var context = new CalQrDbContext(options))
                {
                    DatabaseMigrator.RunMigrations(context);

                    var pancake = context.DeviceTypes.Single(t => t.Name == "Pancake Probe");
                    pancake.ProcedureNo = "مخصّص";
                    pancake.CountingUnit = null;
                    context.SaveChanges();

                    DeviceTypeSeeder.Apply(context);
                }

                using (var context = new CalQrDbContext(options))
                {
                    var pancake = context.DeviceTypes.Single(t => t.Name == "Pancake Probe");
                    Assert.Equal("مخصّص", pancake.ProcedureNo);   // لم يُدهس
                    Assert.Equal("kCPM", pancake.CountingUnit);     // مُلئ
                }
            }
            finally { CleanUp(dbPath); }
        }

        [Fact]
        public void Seed_DoesNotAddChildRowsWhenTypeAlreadyHasThem()
        {
            string dbPath = NewDbPath("childrows");
            var options = OptionsFor(dbPath);
            try
            {
                using (var context = new CalQrDbContext(options))
                {
                    DatabaseMigrator.RunMigrations(context);

                    var pancake = context.DeviceTypes.Single(t => t.Name == "Pancake Probe");
                    int before = context.DeviceTypeFunctionalCheckTemplates.Count(t => t.DeviceTypeId == pancake.Id);
                    Assert.Equal(5, before);

                    DeviceTypeSeeder.Apply(context);

                    Assert.Equal(5, context.DeviceTypeFunctionalCheckTemplates.Count(t => t.DeviceTypeId == pancake.Id));
                }
            }
            finally { CleanUp(dbPath); }
        }

        [Fact]
        public void CanonicalAndAliasBothPresent_RecordsConflict_AndPersistsItToAppSettings()
        {
            // الاختبار يفحص **القاعدة** لا القيمة المُعادة: القيمة المُعادة كانت
            // تُبنى وتُرمى، فمرّ التعارض بصمت. ما يهمّ أن الأثر باقٍ بعد انتهاء
            // البذر ويمكن لأي مستدعٍ قراءته.
            string dbPath = NewDbPath("conflict");
            var options = OptionsFor(dbPath);
            try
            {
                using (var context = new CalQrDbContext(options))
                {
                    DatabaseMigrator.RunMigrations(context);

                    // الاسمان معاً: المعتمد مزروع، والمرادف يُضاف يدوياً
                    context.DeviceTypes.Add(new DeviceType { Name = "Gamma Probe" });
                    context.SaveChanges();

                    var result = DeviceTypeSeeder.Apply(context);

                    Assert.Contains(
                        result.AliasConflicts,
                        c => c.Contains("Gamma Scintillation Probe") && c.Contains("Gamma Probe"));

                    // لم يُدمج شيء ولم تُعد تسمية شيء
                    Assert.Equal(6, context.DeviceTypes.Count());
                }

                // الأثر باقٍ في القاعدة بعد انتهاء البذر
                using (var context = new CalQrDbContext(options))
                {
                    var setting = context.AppSettings.SingleOrDefault(s => s.Key == DeviceTypeSeeder.ConflictsKey);

                    Assert.NotNull(setting);
                    Assert.Contains("Gamma Probe", setting!.Value);
                    Assert.Contains("تعارض تسمية", setting.Value);
                }
            }
            finally { CleanUp(dbPath); }
        }

        [Fact]
        public void SoftDeletedMatch_IsRecordedAsHidden_NotSilentlyRevived()
        {
            // نوع مخفي يُملأ قالبه ويبقى مخفياً — والمستخدم يرى أربعة أنواع ولا
            // يستطيع إنشاء الخامس لأن الاسم محجوز بصفّ غير مرئي. لا استرجاع
            // تلقائي (قد يكون الإخفاء متعمداً)، لكن لا صمت أيضاً.
            string dbPath = NewDbPath("hidden");
            var options = OptionsFor(dbPath);
            try
            {
                using (var context = new CalQrDbContext(options))
                {
                    DatabaseMigrator.RunMigrations(context);

                    var pancake = context.DeviceTypes.Single(t => t.Name == "Pancake Probe");
                    pancake.IsDeleted = true;
                    context.SaveChanges();

                    var result = DeviceTypeSeeder.Apply(context);

                    Assert.Contains("Pancake Probe", result.HiddenTypes);
                    // لم يُنشأ توأم للاسم المحجوز
                    Assert.Equal(5, context.DeviceTypes.Count());
                    // ولم يُسترجع تلقائياً
                    Assert.True(context.DeviceTypes.Single(t => t.Name == "Pancake Probe").IsDeleted);
                }

                using (var context = new CalQrDbContext(options))
                {
                    var setting = context.AppSettings.SingleOrDefault(s => s.Key == DeviceTypeSeeder.ConflictsKey);

                    Assert.NotNull(setting);
                    Assert.Contains("مخفي", setting!.Value);
                }
            }
            finally { CleanUp(dbPath); }
        }

        [Fact]
        public void SeedFailure_DoesNotEscape_RecordsReason_AndLeavesFlagUnwritten()
        {
            // فشل البذر يجب ألا يمنع الإقلاع. القوالب ميزة ثانوية، والمنظومة
            // تعمل بدونها؛ أما استثناء يصعد من RunMigrations فيوقف التطبيق كلياً،
            // وعلم البذر غير المكتوب يجعل التعطّل دائماً على جهاز معزول.
            string dbPath = NewDbPath("seedfailure");
            var options = OptionsFor(dbPath);
            try
            {
                using (var context = new CalQrDbContext(options))
                {
                    DatabaseMigrator.RunMigrations(context);

                    // إزالة علم البذر ليُعاد تشغيله، وإسقاط جدول يحتاجه ليفشل
                    var flag = context.AppSettings.Single(s => s.Key == DeviceTypeSeeder.SeedFlagKey);
                    context.AppSettings.Remove(flag);
                    context.SaveChanges();

                    context.Database.ExecuteSqlRaw("DROP TABLE DeviceTypeFunctionalCheckTemplates;");
                }

                DeviceTypeSeedResult result;
                using (var context = new CalQrDbContext(options))
                {
                    // لا يُلقي — وهذا هو جوهر الاختبار
                    result = DeviceTypeSeeder.SeedIfNeeded(context);
                }

                Assert.NotNull(result.Failure);
                Assert.True(result.HasDiagnostics);

                using (var context = new CalQrDbContext(options))
                {
                    // السبب مكتوب ودائم
                    var failure = context.AppSettings.SingleOrDefault(s => s.Key == DeviceTypeSeeder.FailureKey);
                    Assert.NotNull(failure);
                    Assert.Contains("فشل بذر أنواع الأجهزة", failure!.Value);

                    // وعلم البذر لم يُكتب، فتُعاد المحاولة بعد الإصلاح
                    Assert.Null(context.AppSettings.SingleOrDefault(s => s.Key == DeviceTypeSeeder.SeedFlagKey));
                }
            }
            finally { CleanUp(dbPath); }
        }

        [Fact]
        public void SeedSucceedingAfterAFailure_ClearsTheFailureKey()
        {
            // تسجيل فشل قديم يجب ألا يبقى معروضاً بعد نجاح المحاولة التالية.
            string dbPath = NewDbPath("failurecleared");
            var options = OptionsFor(dbPath);
            try
            {
                using (var context = new CalQrDbContext(options))
                {
                    DatabaseMigrator.RunMigrations(context);

                    var flag = context.AppSettings.Single(s => s.Key == DeviceTypeSeeder.SeedFlagKey);
                    context.AppSettings.Remove(flag);
                    context.AppSettings.Add(new AppSetting
                    {
                        Key = DeviceTypeSeeder.FailureKey,
                        Value = "فشل قديم"
                    });
                    context.SaveChanges();
                }

                using (var context = new CalQrDbContext(options))
                {
                    var result = DeviceTypeSeeder.SeedIfNeeded(context);
                    Assert.Null(result.Failure);
                }

                using (var context = new CalQrDbContext(options))
                {
                    Assert.Null(context.AppSettings.SingleOrDefault(s => s.Key == DeviceTypeSeeder.FailureKey));
                    Assert.NotNull(context.AppSettings.SingleOrDefault(s => s.Key == DeviceTypeSeeder.SeedFlagKey));
                }
            }
            finally { CleanUp(dbPath); }
        }

        [Fact]
        public void CleanSeed_LeavesNoStaleConflictKey()
        {
            // حصيلة نظيفة تمحو المفتاح، وإلا بقي تحذير قديم معروضاً بعد زوال سببه.
            string dbPath = NewDbPath("nostale");
            var options = OptionsFor(dbPath);
            try
            {
                using (var context = new CalQrDbContext(options))
                {
                    DatabaseMigrator.RunMigrations(context);

                    // تحذير قديم مفتعل
                    context.AppSettings.Add(new AppSetting
                    {
                        Key = DeviceTypeSeeder.ConflictsKey,
                        Value = "تحذير قديم"
                    });
                    context.SaveChanges();

                    DeviceTypeSeeder.Apply(context);
                }

                using (var context = new CalQrDbContext(options))
                {
                    Assert.Null(context.AppSettings.SingleOrDefault(s => s.Key == DeviceTypeSeeder.ConflictsKey));
                }
            }
            finally { CleanUp(dbPath); }
        }

        [Fact]
        public void Catalog_HasNoDuplicateNamesOrAliases()
        {
            // مصدر الحقيقة الواحد يجب أن يكون متسقاً بذاته: اسم معتمد لا يساوي
            // مرادفاً لنوع آخر، وإلا صارت المطابقة غير حتمية.
            var canonical = DeviceTypeCatalog.CanonicalNames.ToList();
            Assert.Equal(canonical.Count, canonical.Distinct(StringComparer.OrdinalIgnoreCase).Count());

            var aliases = DeviceTypeCatalog.All.SelectMany(d => d.Aliases).ToList();
            Assert.Equal(aliases.Count, aliases.Distinct(StringComparer.OrdinalIgnoreCase).Count());

            Assert.Empty(aliases.Intersect(canonical, StringComparer.OrdinalIgnoreCase));
        }

        [Fact]
        public void Catalog_LeavesBetaTemplateBlank_NoFabricatedText()
        {
            // نموذج Beta تالف OCR. البنية تُزرع، والنصوص غير المؤكدة تبقى فارغة.
            // هذا الاختبار يمنع «ملء الفراغ» بنسخ من Pancake بحسن نية.
            var beta = DeviceTypeCatalog.All.Single(d => d.Name == "Beta Scintillation Probe");

            Assert.Null(beta.MethodologyText);
            Assert.Null(beta.ComplianceVerdict);
            Assert.Null(beta.CountingTime);
            Assert.Null(beta.CountingUnit);
            Assert.Null(beta.ReferenceGeometry);
            Assert.Empty(beta.FunctionalChecks);
            Assert.Empty(beta.UncertaintyComponents);

            // والمؤكَّد مزروع
            Assert.NotNull(beta.CalibrationStandard);
            Assert.True(beta.UncertaintyEnabled);
        }

        [Fact]
        public void Catalog_UncertaintyTemplates_CarryNoPreFilledValues()
        {
            // المكوّن ثابت، والقيمة تتغير بكل معايرة. زرع قيمة كان سيُنتج ميزانية
            // عدم يقين تبدو محسوبة وهي منسوخة.
            foreach (var component in DeviceTypeCatalog.All.SelectMany(d => d.UncertaintyComponents))
            {
                Assert.False(string.IsNullOrWhiteSpace(component.ComponentName));
            }

            string dbPath = NewDbPath("nouncvalues");
            var options = OptionsFor(dbPath);
            try
            {
                using (var context = new CalQrDbContext(options))
                {
                    DatabaseMigrator.RunMigrations(context);
                }

                using (var context = new CalQrDbContext(options))
                {
                    Assert.All(
                        context.DeviceTypeUncertaintyComponentTemplates.ToList(),
                        t =>
                        {
                            Assert.Null(t.StandardUncertainty);
                            Assert.Null(t.ContributionPercent);
                        });
                }
            }
            finally { CleanUp(dbPath); }
        }
    }
}
