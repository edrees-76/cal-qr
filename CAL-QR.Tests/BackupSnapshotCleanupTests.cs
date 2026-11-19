using System;
using System.IO;
using CAL_QR.Services;
using Xunit;

namespace CAL_QR.Tests
{
    /// <summary>
    /// تنظيف مجلّدات لقطات النسخ الاحتياطيّ المتروكة (CalQrBackup_*) بعد انقطاع مفاجئ.
    /// كلّ مسار معزول تحت جذر مؤقّت خاصّ بالاختبار، لا يُمسّ %TEMP% الحقيقيّ.
    /// </summary>
    public class BackupSnapshotCleanupTests : IDisposable
    {
        private readonly string _root;

        public BackupSnapshotCleanupTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "calqr_snapcleanup_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        private string MakeFolder(string name, TimeSpan age)
        {
            string dir = Path.Combine(_root, name);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "cal-qr.db"), "x");
            Directory.SetLastWriteTimeUtc(dir, DateTime.UtcNow - age);
            return dir;
        }

        [Fact]
        public void CleanupStale_RemovesOldSnapshotFolder()
        {
            string stale = MakeFolder(BackupService.SnapshotFolderPrefix + "old", TimeSpan.FromHours(5));

            int removed = BackupService.CleanupStaleSnapshotFolders(_root);

            Assert.Equal(1, removed);
            Assert.False(Directory.Exists(stale));
        }

        [Fact]
        public void CleanupStale_KeepsRecentSnapshotFolder()
        {
            string recent = MakeFolder(BackupService.SnapshotFolderPrefix + "running", TimeSpan.FromMinutes(5));

            int removed = BackupService.CleanupStaleSnapshotFolders(_root);

            Assert.Equal(0, removed);
            Assert.True(Directory.Exists(recent));
        }

        [Fact]
        public void CleanupStale_IgnoresFoldersWithOtherNames()
        {
            string other = MakeFolder("SomeoneElse_old", TimeSpan.FromDays(3));

            int removed = BackupService.CleanupStaleSnapshotFolders(_root);

            Assert.Equal(0, removed);
            Assert.True(Directory.Exists(other));
        }

        [Fact]
        public void CleanupStale_MissingRoot_ReturnsZeroWithoutThrowing()
        {
            int removed = BackupService.CleanupStaleSnapshotFolders(Path.Combine(_root, "nope"));

            Assert.Equal(0, removed);
        }

        [Fact]
        public void CleanupStale_RespectsCustomMinAge()
        {
            string dir = MakeFolder(BackupService.SnapshotFolderPrefix + "mid", TimeSpan.FromMinutes(30));

            Assert.Equal(0, BackupService.CleanupStaleSnapshotFolders(_root));
            Assert.True(Directory.Exists(dir));

            Assert.Equal(1, BackupService.CleanupStaleSnapshotFolders(_root, TimeSpan.FromMinutes(10)));
            Assert.False(Directory.Exists(dir));
        }
    }
}
