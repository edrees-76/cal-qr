using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using CAL_QR.Data;
using CAL_QR.Helpers;
using CAL_QR.Models;
using Microsoft.EntityFrameworkCore;

namespace CAL_QR.Services
{
    public enum RecoveryAnswerStatus { Success, WrongAnswer, LockedOut, NotConfigured }

    public sealed record RecoveryAnswerResult(
        RecoveryAnswerStatus Status, TimeSpan? RemainingLock = null, int RemainingAttempts = 0);

    public interface IRecoveryAnswerService
    {
        /// <summary>BCrypt hash of the normalized answer.</summary>
        string HashAnswer(string answer);

        /// <summary>Full check including durable lockout and lazy legacy upgrade.</summary>
        RecoveryAnswerResult Verify(string answer);
    }

    public sealed class RecoveryAnswerService : IRecoveryAnswerService
    {
        public const string AnswerKey = "SecurityAnswer";
        public const string FailedAttemptsKey = "RecoveryFailedAttempts";
        public const string LockedUntilKey = "RecoveryLockedUntilUtc";
        public const int MaxAttempts = 5;
        public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

        private static readonly Regex WhitespaceRun = new(@"\s+", RegexOptions.Compiled);

        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;
        private readonly Func<DateTime> _utcNow;

        public RecoveryAnswerService(IDbContextFactory<CalQrDbContext> contextFactory)
            : this(contextFactory, () => DateTime.UtcNow)
        {
        }

        public RecoveryAnswerService(IDbContextFactory<CalQrDbContext> contextFactory, Func<DateTime> utcNow)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
        }

        public string HashAnswer(string answer)
        {
            if (string.IsNullOrWhiteSpace(answer))
                throw new ArgumentException("Recovery answer must not be empty.", nameof(answer));
            return BCrypt.Net.BCrypt.HashPassword(Normalize(answer));
        }

        public RecoveryAnswerResult Verify(string answer)
        {
            answer ??= string.Empty;
            using var db = _contextFactory.CreateDbContext();
            var rows = db.AppSettings
                .Where(s => s.Key == AnswerKey || s.Key == FailedAttemptsKey || s.Key == LockedUntilKey)
                .ToList();

            AppSetting? Row(string key) => rows.FirstOrDefault(r => r.Key == key);

            var answerRow = Row(AnswerKey);
            if (answerRow == null || string.IsNullOrWhiteSpace(answerRow.Value))
                return new RecoveryAnswerResult(RecoveryAnswerStatus.NotConfigured);

            var now = _utcNow();
            var lockedRow = Row(LockedUntilKey);
            var attemptsRow = Row(FailedAttemptsKey);

            if (lockedRow != null && !string.IsNullOrWhiteSpace(lockedRow.Value) &&
                DateTime.TryParse(lockedRow.Value, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var lockedUntil) &&
                now < lockedUntil)
            {
                return new RecoveryAnswerResult(RecoveryAnswerStatus.LockedOut, lockedUntil - now);
            }

            string stored = answerRow.Value;
            bool isBcrypt = stored.StartsWith("$2", StringComparison.Ordinal);
            bool ok = isBcrypt
                ? VerifyBcrypt(answer, stored)
                : PasswordHelper.VerifyPassword(answer.Trim(), stored);

            if (ok)
            {
                if (!isBcrypt && !string.IsNullOrWhiteSpace(answer))
                {
                    answerRow.Value = HashAnswer(answer);
                    answerRow.UpdatedAt = DateTime.UtcNow;
                }
                Upsert(db, rows, FailedAttemptsKey, "0");
                Upsert(db, rows, LockedUntilKey, string.Empty);
                db.SaveChanges();
                return new RecoveryAnswerResult(RecoveryAnswerStatus.Success);
            }

            int failed = 0;
            if (attemptsRow != null)
                int.TryParse(attemptsRow.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out failed);
            failed++;

            if (failed >= MaxAttempts)
            {
                var until = now + LockDuration;
                Upsert(db, rows, LockedUntilKey, until.ToString("O", CultureInfo.InvariantCulture));
                Upsert(db, rows, FailedAttemptsKey, "0");
                db.SaveChanges();
                return new RecoveryAnswerResult(RecoveryAnswerStatus.LockedOut, LockDuration);
            }

            Upsert(db, rows, FailedAttemptsKey, failed.ToString(CultureInfo.InvariantCulture));
            db.SaveChanges();
            return new RecoveryAnswerResult(RecoveryAnswerStatus.WrongAnswer, null, MaxAttempts - failed);
        }

        private static string Normalize(string answer) =>
            WhitespaceRun.Replace(answer.Trim(), " ").ToLowerInvariant();

        private static bool VerifyBcrypt(string answer, string storedHash)
        {
            try
            {
                return BCrypt.Net.BCrypt.Verify(Normalize(answer), storedHash);
            }
            catch (BCrypt.Net.SaltParseException)
            {
                // Malformed stored hash: cannot match any answer, so it counts as a wrong answer.
                return false;
            }
            catch (ArgumentException)
            {
                // Same as above for hashes rejected by argument validation.
                return false;
            }
        }

        private static void Upsert(CalQrDbContext db, System.Collections.Generic.List<AppSetting> rows,
            string key, string value)
        {
            var row = rows.FirstOrDefault(r => r.Key == key);
            if (row == null)
            {
                row = new AppSetting { Key = key, Value = value, UpdatedAt = DateTime.UtcNow };
                db.AppSettings.Add(row);
                rows.Add(row);
                return;
            }
            row.Value = value;
            row.UpdatedAt = DateTime.UtcNow;
        }
    }
}
