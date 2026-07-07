using Microsoft.EntityFrameworkCore;
using CAL_QR.Models;

namespace CAL_QR.Data
{
    public class CalQrDbContext : DbContext
    {
        public CalQrDbContext(DbContextOptions<CalQrDbContext> options) : base(options)
        {
        }

        public DbSet<Owner> Owners { get; set; } = null!;
        public DbSet<DeviceType> DeviceTypes { get; set; } = null!;
        public DbSet<Device> Devices { get; set; } = null!;
        public DbSet<CalibrationRecord> CalibrationRecords { get; set; } = null!;
        public DbSet<Attachment> Attachments { get; set; } = null!;
        public DbSet<PaperTemplate> PaperTemplates { get; set; } = null!;
        public DbSet<AuditLog> AuditLogs { get; set; } = null!;
        public DbSet<AppSetting> AppSettings { get; set; } = null!;
        public DbSet<AcknowledgedExpiredDevice> AcknowledgedExpiredDevices { get; set; } = null!;

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
                entity.HasIndex(e => e.CertificateNumber).IsUnique();
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
        }
    }
}
