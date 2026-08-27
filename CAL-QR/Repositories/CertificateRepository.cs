using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Services;
using CAL_QR.Validation;

namespace CAL_QR.Repositories
{
    public class CertificateRepository : ICertificateRepository
    {
        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;
        private readonly ICertificateNumberService _numberService;
        private readonly ICertificateSignatureService _signatureService;

        public CertificateRepository(
            IDbContextFactory<CalQrDbContext> contextFactory,
            ICertificateNumberService numberService,
            ICertificateSignatureService signatureService)
        {
            _contextFactory = contextFactory;
            _numberService = numberService;
            _signatureService = signatureService;
        }

        public async Task<Certificate?> GetByIdAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Certificates
                .AsNoTracking()
                .Include(c => c.NuclideSummaries)
                .Include(c => c.CalibrationResults)
                .Include(c => c.UncertaintyComponents)
                .Include(c => c.FunctionalChecks)
                .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
        }

        public async Task<Certificate?> GetByCertificateNumberAsync(string certificateNumber)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            string number = (certificateNumber ?? string.Empty).Trim();

            return await context.Certificates
                .AsNoTracking()
                .Include(c => c.NuclideSummaries)
                .Include(c => c.CalibrationResults)
                .Include(c => c.UncertaintyComponents)
                .Include(c => c.FunctionalChecks)
                .FirstOrDefaultAsync(c => c.CertificateNumber == number && !c.IsDeleted);
        }

        /// <summary>
        /// ترتيب الخطوات ملزم:
        ///   1. فتح المعاملة
        ///   2. تحقق التواريخ (نقي، بلا قراءة من القاعدة)
        ///   3. تخصيص الرقم — أول جملة قاعدة بيانات في المعاملة، وهي كتابة،
        ///      فتأخذ قفل الكتابة فوراً بلا ترقية قفل قراءة
        ///   4. SaveChanges الأولى — هنا تُخصَّص Ids الأبناء
        ///   5. بناء التوقيع والحمولة ثم SaveChanges الثانية
        ///   6. Commit
        ///   7. LogAsync بعد الـCommit (خارج هذه الدالّة، مسؤولية المستدعي)
        ///
        /// الخطوة 5 **لا يجوز** أن تسبق الخطوة 4: فاصل التعادل ThenBy(Id) في
        /// بانِي نص التوقيع عاطل قبل الحفظ لأن كل الأبناء الجدد Id = 0، فينتج
        /// ترتيب مختلف عن ترتيب القراءة اللاحقة ويفشل التحقق عشوائياً.
        /// </summary>
        public async Task<string> AddAsync(Certificate certificate)
        {
            if (certificate == null) throw new ArgumentNullException(nameof(certificate));

            using var context = await _contextFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();

            var validation = CertificateDateRules.Validate(
                certificate.CalibrationDate,
                certificate.IssueDate,
                DateTime.Today);

            if (!validation.IsValid)
            {
                throw new InvalidOperationException(validation.ErrorMessage);
            }

            certificate.DueDate = CertificateDateRules.ComputeDueDate(certificate.CalibrationDate);
            NormalizeDerivedValues(certificate);

            string number = await _numberService.AllocateAsync(context, certificate.IssueDate);
            certificate.CertificateNumber = number;

            certificate.IssuedAt = DateTime.UtcNow;
            certificate.CreatedAt = DateTime.UtcNow;
            certificate.UpdatedAt = DateTime.UtcNow;
            certificate.IsDeleted = false;
            certificate.AmendedAt = null;
            certificate.FirstPrintedAt = null;

            context.Certificates.Add(certificate);
            await context.SaveChangesAsync();

            certificate.SignaturePayloadVersion = _signatureService.CurrentVersion;
            certificate.VerifyCode = _signatureService.ComputeVerifyCode(
                certificate, certificate.SignaturePayloadVersion);
            certificate.QrPayload = _signatureService.BuildQrPayload(certificate);

            await context.SaveChangesAsync();
            await transaction.CommitAsync();

            return number;
        }

        /// <summary>
        /// الرمز نفسه هو كاشف التغيير: لا تتبّع حقول ولا قائمة تُنسى عند إضافة عمود.
        /// أي حقل داخل نص التوقيع يتغير ⇒ الرمز يتغير ⇒ أرشفة وتدوير ووسم تعديل.
        /// وأي حقل خارجه (Notes، ReferenceNo) ⇒ الرمز ثابت ⇒ لا شيء من ذلك.
        ///
        /// ⚠ فرق عن AddAsync: هذه الدالّة **تبدأ بقراءة** ثم تكتب، فتُنشئ قفل قراءة
        /// يحتاج ترقية إلى قفل كتابة. وترقية القفل في SQLite تُعيد SQLITE_BUSY فوراً
        /// دون احترام busy_timeout. الضمان الذي يوفّره ترتيب «الكتابة أولاً» في
        /// AddAsync **لا ينطبق هنا**.
        ///
        /// غير مؤثر في نمط التشغيل الحالي (جهاز واحد، مستخدم واحد يعدّل شهادة).
        /// لو صار التعديل متزامناً فعلاً، فالعلاج معاملة IMMEDIATE صريحة عبر
        /// SqliteConnection.BeginTransaction(deferred: false) ثم Database.UseTransaction
        /// — لا إعادة ترتيب الخطوات، لأن القراءة هنا ضرورية قبل أي كتابة.
        /// </summary>
        public async Task<bool> UpdateAsync(Certificate certificate)
        {
            if (certificate == null) throw new ArgumentNullException(nameof(certificate));

            using var context = await _contextFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();

            var stored = await context.Certificates
                .Include(c => c.NuclideSummaries)
                .Include(c => c.CalibrationResults)
                .Include(c => c.UncertaintyComponents)
                .Include(c => c.FunctionalChecks)
                .FirstOrDefaultAsync(c => c.Id == certificate.Id);

            if (stored == null)
            {
                throw new InvalidOperationException(
                    $"لا توجد شهادة بالمعرّف {certificate.Id}.");
            }

            var validation = CertificateDateRules.Validate(
                certificate.CalibrationDate,
                certificate.IssueDate,
                DateTime.Today,
                stored.CertificateNumber);

            if (!validation.IsValid)
            {
                throw new InvalidOperationException(validation.ErrorMessage);
            }

            string? oldCode = stored.VerifyCode;
            string? oldVersion = stored.SignaturePayloadVersion;

            // الرقم لا يتغير بالتعديل أبداً — ولا التواريخ النظامية.
            // ويُعاد فرض الرمز والحمولة والإصدار المخزَّنة على الكائن الوارد قبل
            // نسخ القيم: كائن مفصول قد يحمل رمزاً قديماً أو خالياً، ونسخه بلا
            // إعادة فرض كان سيُتلف الرمز الحالي حين لا يقع تدوير.
            certificate.CertificateNumber = stored.CertificateNumber;
            certificate.VerifyCode = stored.VerifyCode;
            certificate.QrPayload = stored.QrPayload;
            certificate.SignaturePayloadVersion = stored.SignaturePayloadVersion;
            certificate.CreatedAt = stored.CreatedAt;
            certificate.IssuedAt = stored.IssuedAt;
            certificate.FirstPrintedAt = stored.FirstPrintedAt;
            certificate.AmendedAt = stored.AmendedAt;
            certificate.DueDate = CertificateDateRules.ComputeDueDate(certificate.CalibrationDate);
            certificate.UpdatedAt = DateTime.UtcNow;
            NormalizeDerivedValues(certificate);

            context.Entry(stored).CurrentValues.SetValues(certificate);
            SyncChildren(context, stored, certificate);

            await context.SaveChangesAsync();

            stored.SignaturePayloadVersion = _signatureService.CurrentVersion;
            string newCode = _signatureService.ComputeVerifyCode(
                stored, stored.SignaturePayloadVersion);

            bool rotated = !CertificateSignatureService.CodesMatch(oldCode, newCode);

            if (rotated)
            {
                if (!string.IsNullOrWhiteSpace(oldCode))
                {
                    context.CertificateVerifyCodeHistory.Add(new CertificateVerifyCodeHistory
                    {
                        CertificateId = stored.Id,
                        VerifyCode = oldCode!,
                        SignaturePayloadVersion = oldVersion,
                        ReplacedAt = DateTime.UtcNow
                    });
                }

                stored.VerifyCode = newCode;
                stored.QrPayload = _signatureService.BuildQrPayload(stored);

                // تاريخ **أول** تعديل بعد الطباعة، لا آخره
                if (stored.FirstPrintedAt.HasValue && !stored.AmendedAt.HasValue)
                {
                    stored.AmendedAt = DateTime.UtcNow;
                }

                await context.SaveChangesAsync();
            }

            await transaction.CommitAsync();
            return rotated;
        }

        /// <summary>
        /// تسجيل أوّل طباعة/إصدار: يضبط FirstPrintedAt مرّةً واحدة (idempotent).
        ///
        /// مستدعاة من مسار التصدير الفعليّ — DeviceDetailViewModel.ExportPdfAsync
        /// بعد توليد ملفّ الـPDF مباشرةً (بوسم Phase 5-b).
        ///
        /// FirstPrintedAt بوّابة سلسلة وسم التعديل:
        ///     يُضبط هنا ⇐ شرط AmendedAt يتحقّق عند أوّل تعديل بعد الطباعة
        ///                  ⇐ حالة ⚠ «عُدّلت هذه الشهادة» تظهر.
        /// إزالة هذا الاستدعاء تُعطّل السلسلة صامتةً — فلا يُحذف من مسار التصدير.
        ///
        /// تغطية: CertificateAmendmentTests.
        /// </summary>
        public async Task MarkPrintedAsync(int certificateId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var certificate = await context.Certificates
                .FirstOrDefaultAsync(c => c.Id == certificateId);

            if (certificate != null && !certificate.FirstPrintedAt.HasValue)
            {
                certificate.FirstPrintedAt = DateTime.UtcNow;
                await context.SaveChangesAsync();
            }
        }

        public async Task<bool> SyncSignedCopyStateAsync(int certificateId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var certificate = await context.Certificates
                .FirstOrDefaultAsync(c => c.Id == certificateId && !c.IsDeleted);

            if (certificate == null)
                throw new InvalidOperationException($"الشهادة {certificateId} غير موجودة.");

            bool attached = await context.Attachments
                .AnyAsync(a => a.CertificateId == certificateId);

            certificate.IsSignedCopyAttached = attached;

            // تاريخ وصول النسخة الموقّعة، لا تاريخ آخر لمسة: يُضبط عند أوّل
            // إرفاق ويبقى، ولا يُعاد ضبطه بإضافة ملفّ ثانٍ إلى النسخة نفسها.
            if (attached)
            {
                certificate.SignedCopyConfirmedAt ??= DateTime.UtcNow;
            }
            else
            {
                certificate.SignedCopyConfirmedAt = null;
            }

            certificate.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();

            return attached;
        }

        public async Task<CertificateVerificationResult> VerifyByCodeAsync(string verifyCode)
        {
            string code = (verifyCode ?? string.Empty).Trim().ToUpperInvariant();

            if (code.Length == 0)
            {
                return new CertificateVerificationResult
                {
                    Status = CertificateVerificationStatus.NotFound
                };
            }

            using var context = await _contextFactory.CreateDbContextAsync();

            // ١. الرموز الحالية
            var current = await context.Certificates
                .AsNoTracking()
                .Include(c => c.NuclideSummaries)
                .Include(c => c.CalibrationResults)
                .Include(c => c.UncertaintyComponents)
                .Include(c => c.FunctionalChecks)
                .FirstOrDefaultAsync(c => c.VerifyCode == code && !c.IsDeleted);

            if (current != null)
            {
                return new CertificateVerificationResult
                {
                    Status = _signatureService.SupportsVersion(current.SignaturePayloadVersion)
                        ? CertificateVerificationStatus.Authentic
                        : CertificateVerificationStatus.UnsupportedVersion,
                    Certificate = current,
                    AmendedAt = current.AmendedAt
                };
            }

            // ٢. الرموز التاريخية — المسار الذي يمنع ظهور وثيقة أصلية كأنها مزوّرة
            var historical = await context.CertificateVerifyCodeHistory
                .AsNoTracking()
                .Where(h => h.VerifyCode == code)
                .OrderByDescending(h => h.ReplacedAt)
                .FirstOrDefaultAsync();

            if (historical != null)
            {
                var certificate = await context.Certificates
                    .AsNoTracking()
                    .Include(c => c.NuclideSummaries)
                    .Include(c => c.CalibrationResults)
                    .Include(c => c.UncertaintyComponents)
                    .Include(c => c.FunctionalChecks)
                    .FirstOrDefaultAsync(c => c.Id == historical.CertificateId && !c.IsDeleted);

                if (certificate != null)
                {
                    return new CertificateVerificationResult
                    {
                        Status = CertificateVerificationStatus.AuthenticAmended,
                        Certificate = certificate,
                        AmendedAt = certificate.AmendedAt ?? historical.ReplacedAt
                    };
                }
            }

            return new CertificateVerificationResult
            {
                Status = CertificateVerificationStatus.NotFound
            };
        }

        /// <summary>AE = |RE| نصياً على كل صف نتائج، قبل أي بناء لنص التوقيع.</summary>
        private static void NormalizeDerivedValues(Certificate certificate)
        {
            foreach (var result in certificate.CalibrationResults)
            {
                if (!string.IsNullOrWhiteSpace(result.RelativeError))
                {
                    result.AbsoluteRelativeError = MeasurementValueRules.AbsoluteOf(result.RelativeError);
                }
            }
        }

        /// <summary>
        /// مزامنة الصفوف الأبناء: حذف ما اختفى، تحديث ما بقي، إضافة ما جدّ.
        /// الصفوف الجديدة تأخذ Ids في SaveChanges التي تلي — ولذلك يقع حساب
        /// التوقيع بعدها لا قبلها.
        /// </summary>
        private static void SyncChildren(CalQrDbContext context, Certificate stored, Certificate incoming)
        {
            SyncCollection(
                context,
                stored.NuclideSummaries.ToList(),
                incoming.NuclideSummaries.ToList(),
                stored.NuclideSummaries,
                n => n.Id,
                (target, source) => context.Entry(target).CurrentValues.SetValues(source));

            SyncCollection(
                context,
                stored.CalibrationResults.ToList(),
                incoming.CalibrationResults.ToList(),
                stored.CalibrationResults,
                r => r.Id,
                (target, source) => context.Entry(target).CurrentValues.SetValues(source));

            SyncCollection(
                context,
                stored.UncertaintyComponents.ToList(),
                incoming.UncertaintyComponents.ToList(),
                stored.UncertaintyComponents,
                u => u.Id,
                (target, source) => context.Entry(target).CurrentValues.SetValues(source));

            SyncCollection(
                context,
                stored.FunctionalChecks.ToList(),
                incoming.FunctionalChecks.ToList(),
                stored.FunctionalChecks,
                f => f.Id,
                (target, source) => context.Entry(target).CurrentValues.SetValues(source));
        }

        private static void SyncCollection<T>(
            CalQrDbContext context,
            List<T> storedRows,
            List<T> incomingRows,
            ICollection<T> storedCollection,
            Func<T, int> idOf,
            Action<T, T> copyValues) where T : class
        {
            foreach (var storedRow in storedRows)
            {
                var match = incomingRows.FirstOrDefault(r => idOf(r) == idOf(storedRow) && idOf(r) != 0);
                if (match == null)
                {
                    context.Remove(storedRow);
                    storedCollection.Remove(storedRow);
                }
                else
                {
                    copyValues(storedRow, match);
                }
            }

            foreach (var incomingRow in incomingRows.Where(r => idOf(r) == 0))
            {
                storedCollection.Add(incomingRow);
            }
        }
    }
}
