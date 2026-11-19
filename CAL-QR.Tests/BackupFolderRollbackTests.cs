using System;
using System.IO;
using CAL_QR.Services;
using Xunit;

namespace CAL_QR.Tests
{
    public class BackupFolderRollbackTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "cal_qr_rollback_" + Guid.NewGuid().ToString("N"));

        public BackupFolderRollbackTests() => Directory.CreateDirectory(_dir);

        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch (IOException) { }
        }

        private string Make(string name, string fileName = "f.txt", string content = "x")
        {
            string path = Path.Combine(_dir, name);
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, fileName), content);
            return path;
        }

        [Fact]
        public void Rollback_InstalledWithoutPreRestore_DeletesRestoredFolder()
        {
            // المجلّد لم يكن موجودًا قبل الاستعادة: المستعاد يُحذف ولا يبقى يتيمًا.
            string live = Make("live");
            string pre = Path.Combine(_dir, "live_preRestore_x");

            BackupService.RollbackFolderSwap(live, pre, installed: true);

            Assert.False(Directory.Exists(live));
            Assert.False(Directory.Exists(pre));
        }

        [Fact]
        public void Rollback_InstalledWithPreRestore_RestoresOriginal()
        {
            string live = Make("live", "restored.txt");
            string pre = Make("live_preRestore_x", "original.txt");

            BackupService.RollbackFolderSwap(live, pre, installed: true);

            Assert.True(File.Exists(Path.Combine(live, "original.txt")));
            Assert.False(File.Exists(Path.Combine(live, "restored.txt")));
            Assert.False(Directory.Exists(pre));
        }

        [Fact]
        public void Rollback_OriginalMovedAwayButInstallFailed_BringsOriginalBack()
        {
            // نجح نقل الأصل إلى preRestore وفشل تثبيت المستعاد: لا مجلّد حيّ، والأصل في الأثر.
            string live = Path.Combine(_dir, "live");
            string pre = Make("live_preRestore_x", "original.txt");

            BackupService.RollbackFolderSwap(live, pre, installed: false);

            Assert.True(File.Exists(Path.Combine(live, "original.txt")));
            Assert.False(Directory.Exists(pre));
        }

        [Fact]
        public void Rollback_NothingTouched_LeavesLiveFolderAlone()
        {
            string live = Make("live", "keep.txt");
            string pre = Path.Combine(_dir, "live_preRestore_x");

            BackupService.RollbackFolderSwap(live, pre, installed: false);

            Assert.True(File.Exists(Path.Combine(live, "keep.txt")));
        }
    }
}
