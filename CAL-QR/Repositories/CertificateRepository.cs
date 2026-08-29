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
            // الترتيب إلزاميّ: SanitizeUserText قبل NormalizeDerivedValues.
            // NormalizeDerivedValues تشتقّ AE من RE عبر MeasurementValueRules.AbsoluteOf،
            // وتلك تُسقط علامات الاتّجاه حين تجد إشارة سالب وتُبقيها حين لا تجدها.
            // تنظيفٌ بعدها يترك RE وAE بنظافتين مختلفتين.
            SanitizeUserText(certificate);
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
            // حالة النسخة الموقّعة مشتقّة من صفوف Attachments، ولا يعرفها نموذج
            // الشهادة. بلا إنقاذها هنا يكتب SetValues القيمة الافتراضيّة (false/null)
            // فوق المخزَّن، فتعود شهادة نسختها الموقّعة على القرص إلى حالة
            // «بانتظار النسخة الموقّعة» عند كلّ تعديل.
            certificate.IsSignedCopyAttached = stored.IsSignedCopyAttached;
            certificate.SignedCopyConfirmedAt = stored.SignedCopyConfirmedAt;
            certificate.DueDate = CertificateDateRules.ComputeDueDate(certificate.CalibrationDate);
            certificate.UpdatedAt = DateTime.UtcNow;
            // الترتيب إلزاميّ: SanitizeUserText قبل NormalizeDerivedValues.
            // NormalizeDerivedValues تشتقّ AE من RE عبر MeasurementValueRules.AbsoluteOf،
            // وتلك تُسقط علامات الاتّجاه حين تجد إشارة سالب وتُبقيها حين لا تجدها.
            // تنظيفٌ بعدها يترك RE وAE بنظافتين مختلفتين.
            //
            // الموضع هنا على الكائن الوارد (certificate) قبل SetValues وSyncChildren
            // عمداً — لا يُنقل: فينتشر الأثر إلى المخزَّن (عبر SetValues) وإلى
            // الصفوف الجديدة (عبر SyncChildren) معاً.
            SanitizeUserText(certificate);
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

                // تاريخ **أوّل** تعديل بعد الإصدار، لا آخره.
                //
                // المرساة هي IsSignedCopyAttached لا FirstPrintedAt، لأنّ الإصدار
                // الفعليّ هو وصول النسخة الموقّعة والمختومة لا خروج ورقة من الطابعة
                // (قرار رضا). التصدير للمراجعة الداخليّة كان يضبط FirstPrintedAt،
                // فيُوسم كلّ تعديل بعده «تعديلًا على شهادة صادرة» زورًا.
                //
                // الثمن مقبول وواعٍ: شهادة وُقّعت وسُلّمت ولم تُمسح نسختها بعد، ثمّ
                // عُدّلت — لا تُوسم. لكنّها تظهر «بانتظار النسخة الموقّعة» في القائمة،
                // فالتقصير مرئيّ لا صامت. لا تُضِف FirstPrintedAt إلى الشرط لسدّ هذه
                // الفجوة: ذلك يُعيد الوسم الزائف من الباب الخلفيّ.
                if (stored.IsSignedCopyAttached && !stored.AmendedAt.HasValue)
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
        /// FirstPrintedAt سجلّ تاريخيّ فقط: متى خرجت أوّل نسخة مطبوعة.
        /// لم يعد بوّابةً لوسم التعديل — تلك المرساة صارت IsSignedCopyAttached
        /// (انظر UpdateAsync). لا منطق يتفرّع عن هذا الحقل اليوم.
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

        /// <summary>
        /// تنظيف محارف الاتّجاه الخفيّة (تأتي مع اللصق من Word) من كل نصوص
        /// المستخدم قبل أي اشتقاق أو حفظ.
        ///
        /// يُستثنى ما يولّده النظام لا ما يكتبه المستخدم — خمسة حقول:
        /// CertificateNumber · QrPayload · QrPayloadVersion · VerifyCode ·
        /// SignaturePayloadVersion. المستخدم لا يلصق في هذه الخمسة من Word
        /// أصلاً، وتنظيفها يوهم القارئ أنّها مُدخَلات. من يضيف عموداً مولَّداً
        /// سادساً يعرف من هذا التعليق أين يضعه.
        /// </summary>
        private static void SanitizeUserText(Certificate certificate)
        {
            certificate.ReferenceNo = TextInputRules.Clean(certificate.ReferenceNo);
            certificate.FinancialReceiptNo = TextInputRules.Clean(certificate.FinancialReceiptNo);
            certificate.CertificateTemplateType = TextInputRules.Clean(certificate.CertificateTemplateType);
            certificate.ProcedureNo = TextInputRules.Clean(certificate.ProcedureNo);
            certificate.CalibrationLocation = TextInputRules.Clean(certificate.CalibrationLocation);
            certificate.Instrumentation = TextInputRules.Clean(certificate.Instrumentation);
            certificate.DetectorType = TextInputRules.Clean(certificate.DetectorType);
            certificate.ClientName = TextInputRules.Clean(certificate.ClientName) ?? string.Empty;
            certificate.ClientAddress = TextInputRules.Clean(certificate.ClientAddress);
            certificate.DeviceModel = TextInputRules.Clean(certificate.DeviceModel) ?? string.Empty;
            certificate.DeviceSerialNumber = TextInputRules.Clean(certificate.DeviceSerialNumber) ?? string.Empty;
            certificate.DeviceManufacturer = TextInputRules.Clean(certificate.DeviceManufacturer);
            certificate.SurveyMeterModel = TextInputRules.Clean(certificate.SurveyMeterModel);
            certificate.SurveyMeterSerialNumber = TextInputRules.Clean(certificate.SurveyMeterSerialNumber);
            certificate.MeasurementType = TextInputRules.Clean(certificate.MeasurementType);
            certificate.Distance = TextInputRules.Clean(certificate.Distance);
            certificate.CountingTime = TextInputRules.Clean(certificate.CountingTime);
            certificate.CountingUnit = TextInputRules.Clean(certificate.CountingUnit);
            certificate.CalibrationMode = TextInputRules.Clean(certificate.CalibrationMode);
            certificate.Temperature = TextInputRules.Clean(certificate.Temperature);
            certificate.RelativeHumidity = TextInputRules.Clean(certificate.RelativeHumidity);
            certificate.AtmosphericPressure = TextInputRules.Clean(certificate.AtmosphericPressure);
            certificate.CorrectedReadingFormula = TextInputRules.Clean(certificate.CorrectedReadingFormula);
            certificate.ComplianceVerdict = TextInputRules.Clean(certificate.ComplianceVerdict);
            certificate.CalibrationStandard = TextInputRules.Clean(certificate.CalibrationStandard);
            certificate.RadiationSource = TextInputRules.Clean(certificate.RadiationSource);
            certificate.ReferenceGeometry = TextInputRules.Clean(certificate.ReferenceGeometry);
            certificate.MethodologyText = TextInputRules.Clean(certificate.MethodologyText);
            certificate.TraceabilityReference = TextInputRules.Clean(certificate.TraceabilityReference);
            certificate.CombinedUncertainty = TextInputRules.Clean(certificate.CombinedUncertainty);
            certificate.ExpandedUncertainty = TextInputRules.Clean(certificate.ExpandedUncertainty);
            certificate.CoverageFactor = TextInputRules.Clean(certificate.CoverageFactor);
            certificate.AdditionalInformation = TextInputRules.Clean(certificate.AdditionalInformation);
            certificate.Notes = TextInputRules.Clean(certificate.Notes);
            certificate.Remarks = TextInputRules.Clean(certificate.Remarks);
            certificate.StatusReason = TextInputRules.Clean(certificate.StatusReason);
            certificate.CalibratedByName = TextInputRules.Clean(certificate.CalibratedByName);
            certificate.CalibratedByTitle = TextInputRules.Clean(certificate.CalibratedByTitle);
            certificate.ReviewedByName = TextInputRules.Clean(certificate.ReviewedByName);
            certificate.ReviewedByTitle = TextInputRules.Clean(certificate.ReviewedByTitle);
            certificate.ApprovedByName = TextInputRules.Clean(certificate.ApprovedByName);
            certificate.ApprovedByTitle = TextInputRules.Clean(certificate.ApprovedByTitle);
            certificate.AuthorizedByName = TextInputRules.Clean(certificate.AuthorizedByName);
            certificate.AuthorizedByTitle = TextInputRules.Clean(certificate.AuthorizedByTitle);

            foreach (var nuclide in certificate.NuclideSummaries)
            {
                nuclide.Radionuclide = TextInputRules.Clean(nuclide.Radionuclide) ?? string.Empty;
                nuclide.AverageCorrectionFactor = TextInputRules.Clean(nuclide.AverageCorrectionFactor);
            }

            foreach (var result in certificate.CalibrationResults)
            {
                result.SourceId = TextInputRules.Clean(result.SourceId);
                result.Radionuclide = TextInputRules.Clean(result.Radionuclide);
                result.Scale = TextInputRules.Clean(result.Scale);
                result.ReferenceDoseLevel = TextInputRules.Clean(result.ReferenceDoseLevel);
                result.ReferenceValue = TextInputRules.Clean(result.ReferenceValue);
                result.MeasuredReading = TextInputRules.Clean(result.MeasuredReading);
                result.CorrectionFactor = TextInputRules.Clean(result.CorrectionFactor);
                result.RelativeError = TextInputRules.Clean(result.RelativeError);
                result.AbsoluteRelativeError = TextInputRules.Clean(result.AbsoluteRelativeError);
                result.Unit = TextInputRules.Clean(result.Unit);
                result.Remarks = TextInputRules.Clean(result.Remarks);
            }

            foreach (var component in certificate.UncertaintyComponents)
            {
                component.ComponentName = TextInputRules.Clean(component.ComponentName) ?? string.Empty;
                component.EvaluationType = TextInputRules.Clean(component.EvaluationType);
                component.StandardUncertainty = TextInputRules.Clean(component.StandardUncertainty);
                component.ContributionPercent = TextInputRules.Clean(component.ContributionPercent);
                component.Distribution = TextInputRules.Clean(component.Distribution);
            }

            foreach (var check in certificate.FunctionalChecks)
            {
                check.CheckName = TextInputRules.Clean(check.CheckName) ?? string.Empty;
                check.Requirement = TextInputRules.Clean(check.Requirement);
                // Result: null مقصود («امسح الاختيار» في القائمة المنسدلة، وتقرير
                // الحالة يضبطه null عمداً). عودة Clean بـnull تحفظ ذلك.
                check.Result = TextInputRules.Clean(check.Result);
                check.Remarks = TextInputRules.Clean(check.Remarks);
            }
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
