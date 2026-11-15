using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Data;

namespace CAL_QR.Services
{
    /// <summary>
    /// نُقلت ملكيّة مؤقّت الخمول إلى هنا من MainViewModel (Transient) لأن الخدمة
    /// وحيدة طوال عمر التطبيق بينما الـViewModel يُعاد إنشاؤه. الخطّاف على
    /// EventManager.RegisterClassHandler يعمل على كلّ Window لا على MainWindow
    /// وحدها، فالحركة في أيّ نافذة تصفّر الخمول.
    /// </summary>
    public sealed class IdleLockService
    {
        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;
        private DateTime _lastActivityTime;
        private DispatcherTimer? _timer;
        private int _autoLockMinutes = CAL_QR.Validation.IdleLockRules.DefaultMinutes;
        private bool _hooksAttached;

        public event EventHandler? LockRequested;

        public IdleLockService(IDbContextFactory<CalQrDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public void AttachGlobalActivityHooks()
        {
            if (_hooksAttached) return;
            _hooksAttached = true;

            EventManager.RegisterClassHandler(typeof(Window), UIElement.PreviewMouseMoveEvent, new MouseEventHandler((_, __) => Notify()), true);
            EventManager.RegisterClassHandler(typeof(Window), UIElement.PreviewKeyDownEvent, new KeyEventHandler((_, __) => Notify()), true);
        }

        public void Notify() => _lastActivityTime = DateTime.Now;

        public void ReloadThreshold()
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                var setting = context.AppSettings.AsNoTracking().FirstOrDefault(s => s.Key == "AutoLockMinutes");
                if (setting != null)
                {
                    // قيمة غير صالحة (صفر أو سالبة في قاعدة قائمة) لا تُطبَّق: كانت تقفل
                    // البرنامج بعد ثوانٍ من كلّ دخول. تبقى المدّة السابقة ويُسجَّل السبب.
                    if (CAL_QR.Validation.IdleLockRules.TryReadStored(setting.Value, out int minutes))
                    {
                        _autoLockMinutes = minutes;
                    }
                    else
                    {
                        CAL_QR.Services.AppLog.Warn(
                            $"AutoLockMinutes المخزَّنة غير صالحة ('{setting.Value}')؛ تبقى المدّة {_autoLockMinutes} دقيقة.");
                    }
                }
            }
            catch (System.Exception swallowEx1)
            {
                CAL_QR.Services.AppLog.Error("IdleLockService.cs:54", swallowEx1);
                // فشل القراءة يترك القيمة السابقة كما هي.
            }
        }

        public void Start()
        {
            ReloadThreshold();
            _lastActivityTime = DateTime.Now;

            if (_timer == null)
            {
                _timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(5)
                };
                _timer.Tick += Timer_Tick;
            }

            _timer.Start();
        }

        public void Stop()
        {
            _timer?.Stop();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            var inactiveTime = DateTime.Now - _lastActivityTime;
            if (inactiveTime.TotalMinutes >= _autoLockMinutes)
            {
                _timer?.Stop();
                LockRequested?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
