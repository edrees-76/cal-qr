using System;
using System.Threading;
using System.Threading.Tasks;

namespace CAL_QR.Services
{
    /// <summary>
    /// بوّابة تسلسل لعمليّات الصيانة الثقيلة (نسخ احتياطيّ، استعادة، تصفير كامل). إن تقاطعت
    /// نسخةٌ مجدولة مع استعادة أو تصفير فقد تلتقط قاعدة نصف مستبدَلة أو مجلّدات مُعاد تسميتها.
    /// العمليّة الثانية تنتظر انتهاء الأولى ثمّ تعمل، فلا تُرفض ولا تتداخل.
    /// </summary>
    public static class MaintenanceGate
    {
        private static readonly SemaphoreSlim Gate = new(1, 1);

        public static async Task<IDisposable> EnterAsync()
        {
            await Gate.WaitAsync().ConfigureAwait(false);
            return new Releaser();
        }

        private sealed class Releaser : IDisposable
        {
            private int _released;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _released, 1) == 0)
                    Gate.Release();
            }
        }
    }
}
