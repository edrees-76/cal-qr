using Microsoft.EntityFrameworkCore;
using CAL_QR.Models;

namespace CAL_QR.Data
{
    public class CalQrDbContext : DbContext
    {
        public CalQrDbContext(DbContextOptions<CalQrDbContext> options) : base(options)
        {
        }

        /// <summary>
        /// فلتر الفهرس الفريد على CalibrationRecords.CertificateNumber.
        ///
        /// مصدر حقيقة واحد يقرأه موضعان: تعريف الفهرس في OnModelCreating أدناه،
        /// وجملة CREATE UNIQUE INDEX في DatabaseMigrator. صياغتان منفصلتان كانتا
        /// ستتباعدان بأول تعديل، فينشأ فهرسان مختلفان: واحد يظنّه EF قائماً وآخر
        /// موجود فعلاً في الملف.
        ///
        /// سببه: السجل الجديد يُحفظ برقم شهادة فارغ، لأن الرقم يُولّده إصدار
        /// الشهادة لا المستخدم. فهرس فريد غير مشروط كان سيسمح بسجل واحد فارغ في
        /// القاعدة كلها ويرفض الثاني. والفراغ مستثنى وحده — الأرقام غير الفارغة
        /// تبقى فريدة كما كانت.
        /// </summary>
        public const string CalibrationRecordCertificateNumberIndexFilter = @"""CertificateNumber"" <> ''";

        public DbSet<Owner> Owners { get; set; } = null!;
        public DbSet<DeviceType> DeviceTypes { get; set; } = null!;
        public DbSet<Device> Devices { get; set; } = null!;
        public DbSet<CalibrationRecord> CalibrationRecords { get; set; } = null!;
        public DbSet<Attachment> Attachments { get; set; } = null!;
        public DbSet<PaperTemplate> PaperTemplates { get; set; } = null!;
        public DbSet<AuditLog> AuditLogs { get; set; } = null!;
        public DbSet<AppSetting> AppSettings { get; set; } = null!;
        public DbSet<AcknowledgedExpiredDevice> AcknowledgedExpiredDevices { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Certificate> Certificates { get; set; } = null!;
        public DbSet<CertificateCalibrationResult> CertificateCalibrationResults { get; set; } = null!;
        public DbSet<CertificateUncertaintyComponent> CertificateUncertaintyComponents { get; set; } = null!;
        public DbSet<CertificateFunctionalCheck> CertificateFunctionalChecks { get; set; } = null!;
        public DbSet<CertificateNuclideSummary> CertificateNuclideSummaries { get; set; } = null!;
        public DbSet<DeviceTypeFunctionalCheckTemplate> DeviceTypeFunctionalCheckTemplates { get; set; } = null!;
        public DbSet<DeviceTypeUncertaintyComponentTemplate> DeviceTypeUncertaintyComponentTemplates { get; set; } = null!;
        public DbSet<CertificateSequence> CertificateSequence { get; set; } = null!;
        public DbSet<CertificateVerifyCodeHistory> CertificateVerifyCodeHistory { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Owner Indexes and constraints
            modelBuilder.Entity<Owner>(entity =>
            {
                entity.HasIndex(e => e.Name);
                entity.HasIndex(e => e.IsDeleted);
                entity.Property(e => e.Name).IsRequired();
            });

            // DeviceType Indexes and constraints
            modelBuilder.Entity<DeviceType>(entity =>
            {
                entity.HasIndex(e => e.Name);
                entity.HasIndex(e => e.IsDeleted);
                entity.Property(e => e.Name).IsRequired();

                // قالب الشهادة على مستوى النوع (المرحلة ٢)
                entity.Property(e => e.ProcedureNo).HasMaxLength(100);
                entity.Property(e => e.CalibrationLocation).HasMaxLength(300);
                entity.Property(e => e.ReferenceGeometry).HasMaxLength(200);
                entity.Property(e => e.CountingTime).HasMaxLength(50);
                entity.Property(e => e.CountingUnit).HasMaxLength(50);
                // 200 لا 50: القيمة المزروعة نصّ هندسة مطابق لـReferenceGeometry
                // ("Direct Contact Geometry")، فيتبع حدَّه لا حدَّ CountingTime.
                entity.Property(e => e.CalibrationMode).HasMaxLength(200);
                entity.Property(e => e.TraceabilityReference).HasMaxLength(300);
                entity.Property(e => e.ComplianceVerdict).HasMaxLength(300);
                entity.Property(e => e.CalibrationStandard).HasMaxLength(500);
                entity.Property(e => e.MeasurementType).HasMaxLength(200);
                entity.Property(e => e.Distance).HasMaxLength(100);
                entity.Property(e => e.CorrectedReadingFormula).HasMaxLength(200);
                entity.Property(e => e.DetectorType).HasMaxLength(200);
                entity.Property(e => e.Instrumentation).HasMaxLength(500);
                // MethodologyText و Notes و AdditionalInformation بلا حدّ طول،
                // أسوةً بنظائرها على Certificate: فقرات كاملة لا سطور.
            });

            // قوالب الفحوص الوظيفية على مستوى نوع الجهاز
            modelBuilder.Entity<DeviceTypeFunctionalCheckTemplate>(entity =>
            {
                entity.HasIndex(e => e.DeviceTypeId);

                entity.Property(e => e.CheckName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Requirement).HasMaxLength(300);
                entity.Property(e => e.DefaultResult).HasMaxLength(100);

                entity.HasOne(t => t.DeviceType)
                    .WithMany(p => p.FunctionalCheckTemplates)
                    .HasForeignKey(t => t.DeviceTypeId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // قوالب مكوّنات عدم اليقين على مستوى نوع الجهاز
            modelBuilder.Entity<DeviceTypeUncertaintyComponentTemplate>(entity =>
            {
                entity.HasIndex(e => e.DeviceTypeId);

                entity.Property(e => e.ComponentName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.EvaluationType).HasMaxLength(10);
                entity.Property(e => e.StandardUncertainty).HasMaxLength(50);
                entity.Property(e => e.ContributionPercent).HasMaxLength(50);
                entity.Property(e => e.Distribution).HasMaxLength(50);

                entity.HasOne(t => t.DeviceType)
                    .WithMany(p => p.UncertaintyComponentTemplates)
                    .HasForeignKey(t => t.DeviceTypeId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Device Indexes and constraints
            modelBuilder.Entity<Device>(entity =>
            {
                entity.HasIndex(e => e.SerialNumber);
                entity.HasIndex(e => e.OwnerId);
                entity.HasIndex(e => e.DeviceTypeId);
                entity.HasIndex(e => e.IsDeleted);
                entity.Property(e => e.Model).IsRequired();
                entity.Property(e => e.SerialNumber).IsRequired();

                entity.HasOne(d => d.Owner)
                    .WithMany(p => p.Devices)
                    .HasForeignKey(d => d.OwnerId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(d => d.DeviceType)
                    .WithMany(p => p.Devices)
                    .HasForeignKey(d => d.DeviceTypeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // CalibrationRecord Indexes and constraints
            modelBuilder.Entity<CalibrationRecord>(entity =>
            {
                entity.HasIndex(e => e.DeviceId);
                entity.HasIndex(e => e.ExpiryDate);
                entity.HasIndex(e => e.CertificateNumber)
                    .IsUnique()
                    .HasFilter(CalibrationRecordCertificateNumberIndexFilter);
                entity.HasIndex(e => e.IsDeleted);
                entity.Property(e => e.CertificateNumber).IsRequired();
                entity.Property(e => e.EngineerName).IsRequired();
                entity.Property(e => e.Result).IsRequired();
                entity.Property(e => e.HmacSignature).IsRequired();

                entity.HasOne(d => d.Device)
                    .WithMany(p => p.CalibrationRecords)
                    .HasForeignKey(d => d.DeviceId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Attachment constraints
            modelBuilder.Entity<Attachment>(entity =>
            {
                entity.Property(e => e.FileName).IsRequired();
                entity.Property(e => e.FilePath).IsRequired();

                entity.HasOne(d => d.CalibrationRecord)
                    .WithMany(p => p.Attachments)
                    .HasForeignKey(d => d.CalibrationRecordId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // PaperTemplate constraints
            modelBuilder.Entity<PaperTemplate>(entity =>
            {
                entity.Property(e => e.TemplateName).IsRequired();
            });

            // AppSetting constraints
            modelBuilder.Entity<AppSetting>(entity =>
            {
                entity.HasIndex(e => e.Key).IsUnique();
                entity.Property(e => e.Key).IsRequired();
            });

            // User configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(e => e.Username).IsUnique();
                entity.Property(e => e.Username).IsRequired();
                entity.Property(e => e.PasswordHash).IsRequired();
                entity.Property(e => e.FullName).IsRequired();
            });

            // AuditLog User relation
            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Certificate Indexes and constraints
            modelBuilder.Entity<Certificate>(entity =>
            {
                entity.HasIndex(e => e.CertificateNumber).IsUnique();
                // فهرس فريد مشروط يمنع على مستوى قاعدة البيانات وجود شهادتين غير محذوفتين
                // لسجل معايرة واحد، مع السماح بإصدار شهادة بديلة بعد الحذف الناعم للأولى
                entity.HasIndex(e => e.CalibrationRecordId)
                    .IsUnique()
                    .HasFilter("\"IsDeleted\" = 0");
                entity.HasIndex(e => e.IsDeleted);
                entity.HasIndex(e => e.IssuedAt);

                entity.Property(e => e.CertificateNumber).IsRequired().HasMaxLength(100);
                entity.Property(e => e.ReferenceNo).HasMaxLength(100);

                entity.Property(e => e.ClientName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.ClientAddress).HasMaxLength(300);
                entity.Property(e => e.DeviceModel).IsRequired().HasMaxLength(200);
                entity.Property(e => e.DeviceSerialNumber).IsRequired().HasMaxLength(100);
                entity.Property(e => e.DeviceManufacturer).HasMaxLength(200);
                entity.Property(e => e.SurveyMeterModel).HasMaxLength(200);
                entity.Property(e => e.SurveyMeterSerialNumber).HasMaxLength(100);

                entity.Property(e => e.MeasurementType).HasMaxLength(200);
                entity.Property(e => e.Distance).HasMaxLength(100);
                entity.Property(e => e.CountingTime).HasMaxLength(50);
                entity.Property(e => e.CountingUnit).HasMaxLength(50);
                entity.Property(e => e.CalibrationMode).HasMaxLength(200);

                entity.Property(e => e.Temperature).HasMaxLength(50);
                entity.Property(e => e.RelativeHumidity).HasMaxLength(50);
                entity.Property(e => e.AtmosphericPressure).HasMaxLength(50);

                entity.Property(e => e.CorrectedReadingFormula).HasMaxLength(200);
                entity.Property(e => e.ComplianceVerdict).HasMaxLength(300);
                entity.Property(e => e.CalibrationStandard).HasMaxLength(500);

                entity.Property(e => e.RadiationSource).HasMaxLength(200);
                entity.Property(e => e.ReferenceGeometry).HasMaxLength(200);
                entity.Property(e => e.TraceabilityReference).HasMaxLength(300);

                entity.Property(e => e.CombinedUncertainty).HasMaxLength(50);
                entity.Property(e => e.ExpandedUncertainty).HasMaxLength(50);
                entity.Property(e => e.CoverageFactor).HasMaxLength(20);

                entity.Property(e => e.CertificateTemplateType).HasMaxLength(200);
                entity.Property(e => e.ProcedureNo).HasMaxLength(100);
                entity.Property(e => e.CalibrationLocation).HasMaxLength(300);
                entity.Property(e => e.Instrumentation).HasMaxLength(500);
                entity.Property(e => e.DetectorType).HasMaxLength(200);

                entity.Property(e => e.QrPayloadVersion).HasMaxLength(20);
                entity.Property(e => e.VerifyCode).HasMaxLength(64);
                entity.Property(e => e.SignaturePayloadVersion).HasMaxLength(20);

                // فهرس البحث بالرمز — مسار «الكود السريع» يبحث به وحده.
                // غير فريد عمداً: تصادم رمز ١٦ خانة ممكن نظرياً، وفهرس فريد
                // كان سيُسقِط عملية إصدار مشروعة.
                entity.HasIndex(e => e.VerifyCode);

                entity.Property(e => e.CalibratedByName).HasMaxLength(200);
                entity.Property(e => e.CalibratedByTitle).HasMaxLength(200);
                entity.Property(e => e.ReviewedByName).HasMaxLength(200);
                entity.Property(e => e.ReviewedByTitle).HasMaxLength(200);
                entity.Property(e => e.ApprovedByName).HasMaxLength(200);
                entity.Property(e => e.ApprovedByTitle).HasMaxLength(200);
                entity.Property(e => e.AuthorizedByName).HasMaxLength(200);
                entity.Property(e => e.AuthorizedByTitle).HasMaxLength(200);

                entity.HasOne(c => c.CalibrationRecord)
                    .WithOne()
                    .HasForeignKey<Certificate>(c => c.CalibrationRecordId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ملخّص النويدات — الطبقة الوسطى التي يقرأ منها الملصق
            modelBuilder.Entity<CertificateNuclideSummary>(entity =>
            {
                entity.HasIndex(e => e.CertificateId);

                entity.Property(e => e.Radionuclide).IsRequired().HasMaxLength(100);
                entity.Property(e => e.AverageCorrectionFactor).HasMaxLength(100);

                entity.HasOne(d => d.Certificate)
                    .WithMany(p => p.NuclideSummaries)
                    .HasForeignKey(d => d.CertificateId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // CertificateCalibrationResult constraints
            modelBuilder.Entity<CertificateCalibrationResult>(entity =>
            {
                entity.HasIndex(e => e.CertificateId);

                entity.Property(e => e.SourceId).HasMaxLength(100);
                entity.Property(e => e.Radionuclide).HasMaxLength(100);
                entity.Property(e => e.Scale).HasMaxLength(100);
                entity.Property(e => e.ReferenceDoseLevel).HasMaxLength(100);
                entity.Property(e => e.ReferenceValue).HasMaxLength(100);
                entity.Property(e => e.MeasuredReading).HasMaxLength(100);
                entity.Property(e => e.CorrectionFactor).HasMaxLength(100);
                entity.Property(e => e.RelativeError).HasMaxLength(100);
                entity.Property(e => e.AbsoluteRelativeError).HasMaxLength(100);
                entity.Property(e => e.Unit).HasMaxLength(50);
                entity.Property(e => e.Remarks).HasMaxLength(300);

                entity.HasOne(d => d.Certificate)
                    .WithMany(p => p.CalibrationResults)
                    .HasForeignKey(d => d.CertificateId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // CertificateUncertaintyComponent constraints
            modelBuilder.Entity<CertificateUncertaintyComponent>(entity =>
            {
                entity.HasIndex(e => e.CertificateId);

                entity.Property(e => e.ComponentName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.EvaluationType).HasMaxLength(10);
                entity.Property(e => e.StandardUncertainty).HasMaxLength(50);
                entity.Property(e => e.ContributionPercent).HasMaxLength(50);
                entity.Property(e => e.Distribution).HasMaxLength(50);

                entity.HasOne(d => d.Certificate)
                    .WithMany(p => p.UncertaintyComponents)
                    .HasForeignKey(d => d.CertificateId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // CertificateFunctionalCheck constraints
            modelBuilder.Entity<CertificateFunctionalCheck>(entity =>
            {
                entity.HasIndex(e => e.CertificateId);

                entity.Property(e => e.CheckName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Requirement).HasMaxLength(300);
                entity.Property(e => e.Result).HasMaxLength(100);
                entity.Property(e => e.Remarks).HasMaxLength(300);

                entity.HasOne(d => d.Certificate)
                    .WithMany(p => p.FunctionalChecks)
                    .HasForeignKey(d => d.CertificateId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // عدّاد أرقام الشهادات
            modelBuilder.Entity<CertificateSequence>(entity =>
            {
                entity.ToTable("CertificateSequence");

                // السنة مفتاح أساسي صريح لا مولَّد: القيمة تأتي من IssueDate،
                // والمفتاح يمنع على مستوى المحرّك وجود صفّين لسنة واحدة.
                entity.HasKey(e => e.Year);
                entity.Property(e => e.Year).ValueGeneratedNever();
                entity.Property(e => e.LastNumber).IsRequired();
            });

            // أرشيف رموز التحقق المستبدلة
            modelBuilder.Entity<CertificateVerifyCodeHistory>(entity =>
            {
                entity.ToTable("CertificateVerifyCodeHistory");

                // غير فريد عمداً — انظر تعليق الكيان
                entity.HasIndex(e => e.VerifyCode);
                entity.HasIndex(e => e.CertificateId);

                entity.Property(e => e.VerifyCode).IsRequired().HasMaxLength(64);
                entity.Property(e => e.SignaturePayloadVersion).HasMaxLength(20);

                entity.HasOne(h => h.Certificate)
                    .WithMany()
                    .HasForeignKey(h => h.CertificateId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
