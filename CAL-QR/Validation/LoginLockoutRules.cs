using System;

namespace CAL_QR.Validation
{
    /// <summary>
    /// Pure rules for the durable login lockout. Times are UTC. The counter and the lock end live on
    /// the user row, so the lock survives closing/restarting the application.
    /// </summary>
    public static class LoginLockoutRules
    {
        public const int MaxAttempts = 3;
        public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(5);

        /// <summary>Time left on an active lock, or null when there is none (or it has expired).</summary>
        public static TimeSpan? RemainingLock(DateTime? lockedUntilUtc, DateTime nowUtc)
        {
            if (lockedUntilUtc == null || nowUtc >= lockedUntilUtc.Value) return null;
            return lockedUntilUtc.Value - nowUtc;
        }

        /// <summary>
        /// Registers one failed attempt. An expired lock starts a fresh count. Reaching
        /// <see cref="MaxAttempts"/> sets a new lock and resets the counter.
        /// </summary>
        public static (int Attempts, DateTime? LockedUntilUtc) RegisterFailure(
            int currentAttempts, DateTime? lockedUntilUtc, DateTime nowUtc)
        {
            if (RemainingLock(lockedUntilUtc, nowUtc) != null)
                return (currentAttempts, lockedUntilUtc);

            int attempts = (lockedUntilUtc != null ? 0 : Math.Max(0, currentAttempts)) + 1;
            if (attempts >= MaxAttempts)
                return (0, nowUtc + LockDuration);
            return (attempts, null);
        }

        public static string FormatRemaining(TimeSpan remaining)
        {
            int total = (int)Math.Ceiling(remaining.TotalSeconds);
            if (total < 0) total = 0;
            return $"{total / 60:00}:{total % 60:00}";
        }
    }
}
