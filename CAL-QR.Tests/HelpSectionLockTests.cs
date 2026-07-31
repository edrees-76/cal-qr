using System;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Services;
using CAL_QR.ViewModels;

namespace CAL_QR.Tests
{
    /// <summary>
    /// قفل قسم «مفتاح التوقيع» في تبويب المساعدة، وانفتاح قسم «الاستعادة
    /// والطوارئ» بلا قيد.
    ///
    /// ⚠ **لا كلمة سرّ ولا تلميح مكتوب في هذا الملف.** كل القيم تُولَّد عشوائياً
    /// وقت التشغيل — قيمة مكتوبة في اختبار تُقرأ من المستودع كما تُقرأ من الكود.
    ///
    /// SQLite حقيقي لا InMemory: مسار القفل يقرأ AppSettings عبر
    /// IDbContextFactory، والسلوك عند غياب الصفّ جزء من المُختبَر.
    /// </summary>
    public class HelpSectionLockTests
    {
        private static string NewDbPath(string tag) =>
            Path.Combine(Path.GetTempPath(), $"cal_qr_helplock_{tag}_{Guid.NewGuid():N}.db");

        private static void CleanUp(string dbPath)
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                try { File.Delete(dbPath); } catch { }
            }
        }

        /// <summary>قيمة عشوائية وقت التشغيل — لا سرّ مكتوب في المستودع.</summary>
        private static string RandomSecret() => Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

        private sealed class TestContextFactory : IDbContextFactory<CalQrDbContext>
        {
            private readonly DbContextOptions<CalQrDbContext> _options;
            public TestContextFactory(string dbPath)
            {
                _options = new DbContextOptionsBuilder<CalQrDbContext>()
                    .UseSqlite($"Data Source={dbPath}")
                    .Options;
            }
            public CalQrDbContext CreateDbContext() => new CalQrDbContext(_options);
        }

        private static void CreateSchema(string dbPath)
        {
            var factory = new TestContextFactory(dbPath);
            using var context = factory.CreateDbContext();
            DatabaseMigrator.RunMigrations(context);
        }

        private static void SetPassword(string dbPath, string plainPassword, string? hint = null)
        {
            var factory = new TestContextFactory(dbPath);
            using var context = factory.CreateDbContext();

            context.AppSettings.Add(new AppSetting
            {
                Key = HelpViewModel.PasswordHashKey,
                Value = BCrypt.Net.BCrypt.HashPassword(plainPassword),
                UpdatedAt = DateTime.UtcNow
            });

            if (hint != null)
            {
                context.AppSettings.Add(new AppSetting
                {
                    Key = HelpViewModel.PasswordHintKey,
                    Value = hint,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            context.SaveChanges();
        }

        private static HelpViewModel BuildViewModel(string dbPath, User? user)
        {
            var currentUser = new TestCurrentUserService();
            if (user != null)
            {
                currentUser.SetCurrentUser(user);
            }
            return new HelpViewModel(new TestContextFactory(dbPath), currentUser);
        }

        private static User AdminUser() => new User
        {
            Username = "admin_test",
            FullName = "مدير الاختبار",
            Role = UserRole.Admin,
            Permissions = SystemPermissions.Settings,
            IsActive = true
        };

        private static User NonAdminUser() => new User
        {
            Username = "operator_test",
            FullName = "مشغّل الاختبار",
            Role = UserRole.User,
            // صلاحية الإعدادات ممنوحة عمداً: الحارس يجب أن يمنعه رغمها،
            // لأن الحدّ هو UserRole.Admin لا SystemPermissions.Settings.
            Permissions = SystemPermissions.Settings,
            IsActive = true
        };

        // ═══════════════════════════════════════════════════════════════════
        //  الحارس الأهمّ: القسم أ مفتوح بلا مستخدم مسجَّل الدخول إطلاقاً
        // ═══════════════════════════════════════════════════════════════════

        [Fact]
        public void RecoverySection_IsNeverGated_EvenWithNoLoggedInUser()
        {
            // سيناريو الكارثة بعينه: فنّي صيانة أمام جهاز معطّل، بلا حساب ولا
            // كلمة سرّ، يحتاج تحذير «لا تثبّت نظيفاً» قبل أن يمسح كل شيء.
            //
            // الحارس يمنع أن يُضاف يوماً شرط رؤية على قسم الاستعادة. النموذج
            // لا يحمل — ويجب ألّا يحمل — أي خاصّية تحجبه: لا IsVisible ولا
            // IsUnlocked ولا Has* يخصّه. رؤيته ثابتة في XAML بلا Binding.
            string dbPath = NewDbPath("nouser");
            try
            {
                CreateSchema(dbPath);

                // CurrentUser = null — لا أحد سجّل الدخول
                var vm = BuildViewModel(dbPath, user: null);

                // القسم المحميّ محجوب…
                Assert.False(vm.IsSigningKeySectionVisible);
                Assert.False(vm.IsSigningKeySectionUnlocked);

                // …ولا توجد في النموذج أي خاصّية تحجب قسم الاستعادة.
                var gatingProperties = typeof(HelpViewModel)
                    .GetProperties()
                    .Select(p => p.Name)
                    .Where(n => n.Contains("Recovery", StringComparison.OrdinalIgnoreCase) ||
                                n.Contains("Emergency", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                Assert.Empty(gatingProperties);
            }
            finally { CleanUp(dbPath); }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  الطبقة الأولى: الصلاحية
        // ═══════════════════════════════════════════════════════════════════

        [Fact]
        public void SigningKeySection_IsHiddenFromNonAdmin_EvenWithSettingsPermission()
        {
            // الحدّ UserRole.Admin لا SystemPermissions.Settings: الأخير يحرس
            // ضبط مسار مجلد، وهذا القسم يكشف طريق استخراج مفتاح التوقيع.
            string dbPath = NewDbPath("nonadmin");
            try
            {
                CreateSchema(dbPath);
                SetPassword(dbPath, RandomSecret());

                var vm = BuildViewModel(dbPath, NonAdminUser());

                Assert.False(vm.IsSigningKeySectionVisible);
            }
            finally { CleanUp(dbPath); }
        }

        [Fact]
        public void SigningKeySection_IsVisibleToAdmin_ButStillLocked()
        {
            // الصلاحية تكشف وجود القسم، ولا تفتحه. الطبقتان مستقلتان.
            string dbPath = NewDbPath("adminlocked");
            try
            {
                CreateSchema(dbPath);
                SetPassword(dbPath, RandomSecret());

                var vm = BuildViewModel(dbPath, AdminUser());

                Assert.True(vm.IsSigningKeySectionVisible);
                Assert.False(vm.IsSigningKeySectionUnlocked);
                Assert.True(vm.ShowPasswordPrompt);
            }
            finally { CleanUp(dbPath); }
        }

        [Fact]
        public void NonAdmin_CannotUnlock_EvenWithTheCorrectPassword()
        {
            // الطبقة الأولى ليست إخفاءً بصرياً: حتى بالكلمة الصحيحة لا ينفتح.
            string dbPath = NewDbPath("nonadminunlock");
            try
            {
                CreateSchema(dbPath);
                string secret = RandomSecret();
                SetPassword(dbPath, secret);

                var vm = BuildViewModel(dbPath, NonAdminUser());
                vm.EnteredPassword = secret;
                vm.UnlockCommand.Execute(null);

                Assert.False(vm.IsSigningKeySectionUnlocked);
            }
            finally { CleanUp(dbPath); }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  الطبقة الثانية: كلمة السرّ
        // ═══════════════════════════════════════════════════════════════════

        [Fact]
        public void CorrectPassword_UnlocksTheSection()
        {
            string dbPath = NewDbPath("correct");
            try
            {
                CreateSchema(dbPath);
                string secret = RandomSecret();
                SetPassword(dbPath, secret);

                var vm = BuildViewModel(dbPath, AdminUser());
                vm.EnteredPassword = secret;
                vm.UnlockCommand.Execute(null);

                Assert.True(vm.IsSigningKeySectionUnlocked);
                Assert.False(vm.HasUnlockError);
                Assert.False(vm.ShowPasswordPrompt);

                // الكلمة لا تبقى في الذاكرة بعد المحاولة
                Assert.Equal(string.Empty, vm.EnteredPassword);
            }
            finally { CleanUp(dbPath); }
        }

        [Fact]
        public void WrongPassword_DoesNotUnlock_AndReportsFailure()
        {
            string dbPath = NewDbPath("wrong");
            try
            {
                CreateSchema(dbPath);
                SetPassword(dbPath, RandomSecret());

                var vm = BuildViewModel(dbPath, AdminUser());
                vm.EnteredPassword = RandomSecret();   // قيمة أخرى
                vm.UnlockCommand.Execute(null);

                Assert.False(vm.IsSigningKeySectionUnlocked);
                Assert.True(vm.HasUnlockError);
                Assert.True(vm.ShowPasswordPrompt);
            }
            finally { CleanUp(dbPath); }
        }

        [Fact]
        public void PasswordIsStoredHashed_NeverInPlainText()
        {
            // الحارس ضدّ تخزين نصّ صريح: لا صفّ في AppSettings يساوي الكلمة،
            // والقيمة المخزَّنة تجزئة BCrypt تتحقّق بـVerify.
            string dbPath = NewDbPath("hashed");
            try
            {
                CreateSchema(dbPath);
                string secret = RandomSecret();
                SetPassword(dbPath, secret);

                var factory = new TestContextFactory(dbPath);
                using var context = factory.CreateDbContext();

                var stored = context.AppSettings
                    .Single(s => s.Key == HelpViewModel.PasswordHashKey).Value;

                Assert.NotEqual(secret, stored);
                Assert.DoesNotContain(secret, stored, StringComparison.Ordinal);
                Assert.StartsWith("$2", stored, StringComparison.Ordinal);
                Assert.True(BCrypt.Net.BCrypt.Verify(secret, stored));

                // ولا صفّ آخر في الجدول يحمل الكلمة صريحة
                Assert.DoesNotContain(
                    context.AppSettings.ToList(),
                    s => s.Value != null && s.Value.Contains(secret, StringComparison.Ordinal));
            }
            finally { CleanUp(dbPath); }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  حالة «لم تُضبط»
        // ═══════════════════════════════════════════════════════════════════

        [Fact]
        public void NoPasswordConfigured_ShowsNotice_AndNeverExposesContent()
        {
            // غياب المفتاح ⇒ إرشاد لا محتوى. ❌ لا انفتاح صامت عند غياب القفل —
            // وهو الخطأ الطبيعي: «لا كلمة سرّ» تُقرأ أحياناً على أنها «بلا حماية».
            string dbPath = NewDbPath("notset");
            try
            {
                CreateSchema(dbPath);   // بلا SetPassword

                var vm = BuildViewModel(dbPath, AdminUser());

                Assert.True(vm.IsSigningKeySectionVisible);
                Assert.False(vm.HasPassword);
                Assert.True(vm.ShowNotConfiguredNotice);
                Assert.False(vm.ShowPasswordPrompt);
                Assert.False(vm.IsSigningKeySectionUnlocked);

                // ومحاولة الفتح بأي قيمة لا تُغيّر شيئاً
                vm.EnteredPassword = RandomSecret();
                vm.UnlockCommand.Execute(null);
                Assert.False(vm.IsSigningKeySectionUnlocked);
            }
            finally { CleanUp(dbPath); }
        }

        [Fact]
        public void HintIsStoredVerbatim_AndAbsentWhenNotSet()
        {
            // التلميح نصّ حرّ يكتبه المدير: يُخزَّن ويُعرض كما هو، بلا معالجة
            // وبلا قيمة افتراضية في الكود.
            string dbPath = NewDbPath("hint");
            try
            {
                CreateSchema(dbPath);
                string hint = "تلميح-" + Guid.NewGuid().ToString("N");
                SetPassword(dbPath, RandomSecret(), hint);

                var vm = BuildViewModel(dbPath, AdminUser());

                Assert.Equal(hint, vm.PasswordHint);
                Assert.True(vm.HasPasswordHint);
            }
            finally { CleanUp(dbPath); }

            string dbPath2 = NewDbPath("nohint");
            try
            {
                CreateSchema(dbPath2);
                SetPassword(dbPath2, RandomSecret());   // بلا تلميح

                var vm = BuildViewModel(dbPath2, AdminUser());

                Assert.Equal(string.Empty, vm.PasswordHint);
                Assert.False(vm.HasPasswordHint);
            }
            finally { CleanUp(dbPath2); }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  الفتح لا يُخزَّن
        // ═══════════════════════════════════════════════════════════════════

        [Fact]
        public void UnlockedState_IsNotPersisted_AndRelockRestoresTheLock()
        {
            // فتحٌ يعبر الجلسات يُبطل القفل بعد أول استعمال. الحالة في الذاكرة
            // وحدها: نموذج جديد على نفس القاعدة يبدأ مقفلاً.
            string dbPath = NewDbPath("notpersisted");
            try
            {
                CreateSchema(dbPath);
                string secret = RandomSecret();
                SetPassword(dbPath, secret);

                var vm = BuildViewModel(dbPath, AdminUser());
                vm.EnteredPassword = secret;
                vm.UnlockCommand.Execute(null);
                Assert.True(vm.IsSigningKeySectionUnlocked);

                // مغادرة القسم تُعيد القفل
                vm.Relock();
                Assert.False(vm.IsSigningKeySectionUnlocked);
                Assert.True(vm.ShowPasswordPrompt);

                // ولا أثر للفتح في قاعدة البيانات
                var factory = new TestContextFactory(dbPath);
                using (var context = factory.CreateDbContext())
                {
                    Assert.DoesNotContain(
                        context.AppSettings.ToList(),
                        s => s.Key.Contains("Unlock", StringComparison.OrdinalIgnoreCase));
                }

                // ونموذج جديد يبدأ مقفلاً
                var fresh = BuildViewModel(dbPath, AdminUser());
                Assert.False(fresh.IsSigningKeySectionUnlocked);
            }
            finally { CleanUp(dbPath); }
        }
    }
}
