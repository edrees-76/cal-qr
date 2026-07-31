using System;
using System.Linq;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Services;
using CAL_QR.ViewModels.Base;

namespace CAL_QR.ViewModels
{
    /// <summary>
    /// حالة قسمَي «الاستعادة والطوارئ» و«مفتاح التوقيع» في تبويب المساعدة.
    ///
    /// كان HelpView واجهة code-behind خالصة بلا نموذج عرض. أُضيف هذا النموذج
    /// لأن القفل منطق تطبيق — تحقّق من صلاحية، وقراءة تجزئة من قاعدة البيانات،
    /// ومقارنة BCrypt — ولا يُكتب في معالج حدث واجهة.
    ///
    /// ╔══════════════════════════════════════════════════════════════════════╗
    /// ║  قسم «الاستعادة والطوارئ» مفتوح دائماً — لا يُقفل بحال.              ║
    /// ╚══════════════════════════════════════════════════════════════════════╝
    /// لا خاصّية هنا تحجبه، ولا شرط يُقيّده. من يحتاجه يقرأه غالباً في أسوأ
    /// لحظة: الجهاز تعطّل، والواقف أمامه فنّي صيانة بلا حساب ولا كلمة سرّ.
    /// تحذير «لا تثبّت نظيفاً» الذي لا يُقرأ إلا بعد تسجيل الدخول **يُقرأ بعد
    /// فوات أوانه** — يكون التثبيت النظيف قد تمّ، والمفتاح قد وُلِّد من جديد،
    /// وكل شهادة سابقة قد فشلت في التحقّق بلا رسالة خطأ واحدة.
    /// </summary>
    public class HelpViewModel : BaseViewModel
    {
        /// <summary>مفتاح تجزئة كلمة سرّ قسم مفتاح التوقيع في AppSettings.</summary>
        public const string PasswordHashKey = "HelpSectionPasswordHash";

        /// <summary>مفتاح التلميح — نصّ حرّ يكتبه المدير، بلا قيمة افتراضية.</summary>
        public const string PasswordHintKey = "HelpSectionPasswordHint";

        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;
        private readonly ICurrentUserService _currentUserService;

        private bool _isSigningKeySectionUnlocked;
        private string _enteredPassword = string.Empty;
        private string _passwordHint = string.Empty;
        private bool _hasPassword;
        private string _unlockError = string.Empty;

        public HelpViewModel(
            IDbContextFactory<CalQrDbContext> contextFactory,
            ICurrentUserService currentUserService)
        {
            _contextFactory = contextFactory;
            _currentUserService = currentUserService;

            UnlockCommand = new RelayCommand(Unlock, () => CanAttemptUnlock);

            LoadLockState();
        }

        /// <summary>
        /// الطبقة الأولى: صلاحية المدير. غير المدير **لا يرى القسم إطلاقاً** —
        /// لا عنصر قائمة، ولا لوحة، ولا رسالة «ممنوع». وجود القفل بذاته معلومة.
        ///
        /// UserRole.Admin لا SystemPermissions.Settings: الأخير يحرس ضبط مسار
        /// مجلد، وهذا القسم يكشف طريق استخراج مفتاح التوقيع — يُحرس بأضيق ما يمكن.
        /// </summary>
        public bool IsSigningKeySectionVisible =>
            _currentUserService.CurrentUser?.Role == UserRole.Admin;

        /// <summary>هل ضُبطت كلمة سرّ أصلاً؟ غياب المفتاح = لم تُضبط.</summary>
        public bool HasPassword
        {
            get => _hasPassword;
            private set
            {
                if (SetProperty(ref _hasPassword, value))
                {
                    OnPropertyChanged(nameof(ShowPasswordPrompt));
                    OnPropertyChanged(nameof(ShowNotConfiguredNotice));
                }
            }
        }

        /// <summary>
        /// الطبقة الثانية. في الذاكرة فقط: يسقط بتبديل التبويب أو إغلاق البرنامج.
        /// ❌ لا يُحفظ في قاعدة البيانات — فتحٌ دائم يُبطل القفل بعد أول استعمال.
        /// </summary>
        public bool IsSigningKeySectionUnlocked
        {
            get => _isSigningKeySectionUnlocked;
            private set
            {
                if (SetProperty(ref _isSigningKeySectionUnlocked, value))
                {
                    OnPropertyChanged(nameof(ShowPasswordPrompt));
                    OnPropertyChanged(nameof(ShowNotConfiguredNotice));
                }
            }
        }

        /// <summary>ما يكتبه المستخدم في حقل الفتح. يُمسح بعد كل محاولة.</summary>
        public string EnteredPassword
        {
            get => _enteredPassword;
            set => SetProperty(ref _enteredPassword, value);
        }

        /// <summary>
        /// التلميح كما كتبه المدير حرفياً. يبدأ فارغاً ولا قيمة افتراضية له
        /// في الكود — تلميح مكتوب مسبقاً يدلّ على كلمة السرّ لا عليها وحدها.
        /// </summary>
        public string PasswordHint
        {
            get => _passwordHint;
            private set
            {
                if (SetProperty(ref _passwordHint, value))
                {
                    OnPropertyChanged(nameof(HasPasswordHint));
                }
            }
        }

        public bool HasPasswordHint => !string.IsNullOrWhiteSpace(PasswordHint);

        /// <summary>رسالة فشل المحاولة. لا تُفصح عن شيء سوى أن الكلمة غير مطابقة.</summary>
        public string UnlockError
        {
            get => _unlockError;
            private set
            {
                if (SetProperty(ref _unlockError, value))
                {
                    OnPropertyChanged(nameof(HasUnlockError));
                }
            }
        }

        public bool HasUnlockError => !string.IsNullOrWhiteSpace(UnlockError);

        /// <summary>حقل الإدخال: كلمة سرّ مضبوطة والقسم ما يزال مقفلاً.</summary>
        public bool ShowPasswordPrompt => HasPassword && !IsSigningKeySectionUnlocked;

        /// <summary>بديل حقل الإدخال حين لا كلمة سرّ: إرشاد لا محتوى.</summary>
        public bool ShowNotConfiguredNotice => !HasPassword && !IsSigningKeySectionUnlocked;

        public ICommand UnlockCommand { get; }

        private bool CanAttemptUnlock =>
            IsSigningKeySectionVisible && HasPassword && !IsSigningKeySectionUnlocked &&
            !string.IsNullOrEmpty(EnteredPassword);

        /// <summary>
        /// يقرأ التجزئة والتلميح من AppSettings. يُستدعى عند الإنشاء وبعد ضبط
        /// كلمة السرّ من الإعدادات.
        ///
        /// لا يُلقي: تبويب المساعدة يجب أن يُفتح ولو تعذّرت قراءة القاعدة —
        /// وإلا حُجب قسم «الاستعادة والطوارئ» في اللحظة التي يُحتاج فيها بالضبط.
        /// </summary>
        public void LoadLockState()
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                var hash = context.AppSettings.AsNoTracking()
                    .FirstOrDefault(s => s.Key == PasswordHashKey);
                var hint = context.AppSettings.AsNoTracking()
                    .FirstOrDefault(s => s.Key == PasswordHintKey);

                HasPassword = !string.IsNullOrWhiteSpace(hash?.Value);
                PasswordHint = hint?.Value ?? string.Empty;
            }
            catch
            {
                // تعذّرت القراءة ⇒ يُعامَل كأن لا كلمة سرّ: القسم مقفل بلا محتوى،
                // ورسالة الإرشاد ظاهرة. لا فتح صامت عند الفشل.
                HasPassword = false;
                PasswordHint = string.Empty;
            }
        }

        /// <summary>
        /// يقارن المُدخل بالتجزئة عبر BCrypt — نفس آلية كلمات مرور المستخدمين.
        /// ❌ لا مقارنة نصّية، ولا كلمة سرّ مكتوبة في الكود، ولا مسار تجاوز.
        /// </summary>
        private void Unlock()
        {
            if (!IsSigningKeySectionVisible)
            {
                return;
            }

            string entered = EnteredPassword;
            EnteredPassword = string.Empty;

            if (string.IsNullOrEmpty(entered))
            {
                return;
            }

            string? storedHash = null;
            try
            {
                using var context = _contextFactory.CreateDbContext();
                storedHash = context.AppSettings.AsNoTracking()
                    .FirstOrDefault(s => s.Key == PasswordHashKey)?.Value;
            }
            catch
            {
                UnlockError = "تعذّر قراءة الإعدادات. حاول مرة أخرى.";
                return;
            }

            if (string.IsNullOrWhiteSpace(storedHash))
            {
                HasPassword = false;
                UnlockError = string.Empty;
                return;
            }

            bool matches;
            try
            {
                matches = BCrypt.Net.BCrypt.Verify(entered, storedHash);
            }
            catch
            {
                // تجزئة تالفة أو بصيغة غير معروفة ⇒ فشل صريح، لا فتح.
                matches = false;
            }

            if (matches)
            {
                UnlockError = string.Empty;
                IsSigningKeySectionUnlocked = true;
            }
            else
            {
                UnlockError = "كلمة السرّ غير صحيحة.";
            }
        }

        /// <summary>
        /// يُعيد القفل. يُستدعى عند مغادرة التبويب: الفتح لا يعبر جلسة عرض.
        /// </summary>
        public void Relock()
        {
            IsSigningKeySectionUnlocked = false;
            EnteredPassword = string.Empty;
            UnlockError = string.Empty;
        }
    }
}
