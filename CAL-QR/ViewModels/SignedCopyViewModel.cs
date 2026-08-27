using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using CAL_QR.ViewModels.Base;
using CAL_QR.Models;
using CAL_QR.Repositories;
using CAL_QR.Data;
using CAL_QR.Helpers;

namespace CAL_QR.ViewModels
{
    public class SignedCopyViewModel : BaseViewModel
    {
        private readonly ICertificateRepository _certificateRepository;
        private readonly IAttachmentRepository _attachmentRepository;
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;

        private int _certificateId;
        private int _calibrationRecordId;
        private string _certificateNumber = string.Empty;
        private bool _isSignedCopyAttached;
        private ObservableCollection<AttachmentItem> _signedCopies = new();

        public SignedCopyViewModel(
            ICertificateRepository certificateRepository,
            IAttachmentRepository attachmentRepository,
            IAuditLogRepository auditLogRepository,
            IDbContextFactory<CalQrDbContext> contextFactory)
        {
            _certificateRepository = certificateRepository;
            _attachmentRepository = attachmentRepository;
            _auditLogRepository = auditLogRepository;
            _contextFactory = contextFactory;

            AddSignedCopyCommand = new RelayCommand(async () => await AddSignedCopyAsync());
            RemoveSignedCopyCommand = new RelayCommand(async (param) => await RemoveSignedCopyAsync(param));
            CloseCommand = new RelayCommand(Close);
        }

        public string CertificateNumber
        {
            get => _certificateNumber;
            private set => SetProperty(ref _certificateNumber, value);
        }

        public bool IsSignedCopyAttached
        {
            get => _isSignedCopyAttached;
            private set => SetProperty(ref _isSignedCopyAttached, value);
        }

        public ObservableCollection<AttachmentItem> SignedCopies
        {
            get => _signedCopies;
            private set => SetProperty(ref _signedCopies, value);
        }

        public string StatusText => IsSignedCopyAttached
            ? "مكتملة ✓"
            : "بانتظار النسخة الموقّعة";

        public Action<bool>? CloseWindowAction { get; set; }

        public ICommand AddSignedCopyCommand { get; }
        public ICommand RemoveSignedCopyCommand { get; }
        public ICommand CloseCommand { get; }

        /// <summary>
        /// تُستدعى قبل ShowDialog. تُرجع false إن تعذّر التحميل — ويقع على
        /// المستدعي ألّا يعرض النافذة عندئذٍ. لا تُغلق النافذة بنفسها: إغلاق
        /// نافذة لم تُعرض بعد يجعل ShowDialog التالية ترمي
        /// InvalidOperationException.
        /// </summary>
        public async Task<bool> LoadAsync(int certificateId)
        {
            var certificate = await _certificateRepository.GetByIdAsync(certificateId);
            if (certificate == null)
            {
                MessageBox.Show($"الشهادة {certificateId} غير موجودة.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            _certificateId = certificate.Id;
            _calibrationRecordId = certificate.CalibrationRecordId;
            CertificateNumber = certificate.CertificateNumber;

            await RefreshAsync();
            return true;
        }

        private async Task RefreshAsync()
        {
            var attachments = await _attachmentRepository.GetByCertificateIdAsync(_certificateId);

            var items = new ObservableCollection<AttachmentItem>();
            foreach (var a in attachments)
            {
                items.Add(new AttachmentItem
                {
                    Id = a.Id,
                    FileName = a.FileName,
                    FullPath = a.FilePath,
                    IsSaved = true
                });
            }
            SignedCopies = items;

            IsSignedCopyAttached = SignedCopies.Count > 0;
            OnPropertyChanged(nameof(StatusText));
        }

        private async Task AddSignedCopyAsync()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "PDF و الصور|*.pdf;*.jpg;*.jpeg;*.png|كلّ الملفّات (*.*)|*.*",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() != true) return;

            string root = await AttachmentPaths.ResolveRootAsync(_contextFactory);
            string folder = AttachmentPaths.SignedFolder(root, _calibrationRecordId);
            FileHelper.EnsureDirectoryExists(folder);

            foreach (string filePath in openFileDialog.FileNames)
            {
                try
                {
                    string fileName = Path.GetFileName(filePath);
                    string destPath = UniqueDestinationPath(folder, fileName);
                    File.Copy(filePath, destPath, false);

                    var attachment = new Attachment
                    {
                        CalibrationRecordId = _calibrationRecordId,
                        CertificateId = _certificateId,
                        FileName = Path.GetFileName(destPath),
                        FilePath = destPath,
                        FileExtension = Path.GetExtension(destPath)
                    };
                    await _attachmentRepository.AddAsync(attachment);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"خطأ أثناء حفظ الملفّ {Path.GetFileName(filePath)}: {ex.Message}");
                }
            }

            await SyncAndLogAsync();
        }

        private async Task RemoveSignedCopyAsync(object? parameter)
        {
            if (parameter is not AttachmentItem item) return;

            var result = MessageBox.Show(
                $"هل تريد حذف النسخة الموقّعة '{item.FileName}'؟ ستعود الشهادة إلى حالة «بانتظار النسخة الموقّعة».",
                "تأكيد حذف النسخة الموقّعة",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            await _attachmentRepository.DeleteAsync(item.Id);
            await SyncAndLogAsync();
        }

        private async Task SyncAndLogAsync()
        {
            bool state = await _certificateRepository.SyncSignedCopyStateAsync(_certificateId);
            await RefreshAsync();

            try
            {
                await _auditLogRepository.LogAsync(
                    state ? "إرفاق نسخة موقّعة" : "إزالة النسخة الموقّعة",
                    "Certificate",
                    _certificateId.ToString(),
                    $"الشهادة {CertificateNumber} — {(state ? "تم الإرفاق" : "تم الإزالة")}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Audit] {ex.Message}");
            }
        }

        private void Close()
        {
            CloseWindowAction?.Invoke(true);
        }

        /// <summary>
        /// اسم وجهة فريد داخل المجلّد. File.Copy(overwrite: true) مع اسم
        /// مكرّر كان ينتج ملفًّا واحدًا وصفَّي Attachments يشيران إليه —
        /// نفس صنف العيب الذي أصلحه bb90ecc، مستوًى أدنى.
        /// </summary>
        private static string UniqueDestinationPath(string folder, string fileName)
        {
            string candidate = Path.Combine(folder, fileName);
            if (!File.Exists(candidate)) return candidate;

            string stem = Path.GetFileNameWithoutExtension(fileName);
            string ext = Path.GetExtension(fileName);

            for (int i = 2; i < 1000; i++)
            {
                candidate = Path.Combine(folder, $"{stem} ({i}){ext}");
                if (!File.Exists(candidate)) return candidate;
            }

            return Path.Combine(folder, $"{stem} ({Guid.NewGuid():N}){ext}");
        }
    }
}
