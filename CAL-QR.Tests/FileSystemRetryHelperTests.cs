using System;
using System.IO;
using Xunit;
using CAL_QR.Services;

namespace CAL_QR.Tests
{
    /// <summary>
    /// اختبارات مساعد الحذف بإعادة المحاولة. كل مسار هنا معزول تحت Path.GetTempPath()
    /// باسم فريد بـ GUID، فلا يتقاطع مع أي اختبار آخر في نفس التشغيل.
    /// </summary>
    public class FileSystemRetryHelperTests
    {
        private static string NewTempPath(string tag) =>
            Path.Combine(Path.GetTempPath(), $"cal_qr_fsretry_{tag}_{Guid.NewGuid():N}");

        [Fact]
        public void TryDeleteFile_ExistingFile_ReturnsTrueAndDeletes()
        {
            string path = NewTempPath("file") + ".txt";
            File.WriteAllText(path, "CONTENT");
            Assert.True(File.Exists(path));

            try
            {
                Assert.True(FileSystemRetryHelper.TryDeleteFile(path));
                Assert.False(File.Exists(path));
            }
            finally
            {
                try { File.Delete(path); } catch { }
            }
        }

        [Fact]
        public void TryDeleteFile_MissingFile_ReturnsFalse()
        {
            string path = NewTempPath("missingfile") + ".txt";
            Assert.False(File.Exists(path));

            // غير موجود أصلاً: لا شيء حُذف
            Assert.False(FileSystemRetryHelper.TryDeleteFile(path));
        }

        [Fact]
        public void TryDeleteDirectory_ExistingDirectoryWithContents_ReturnsTrueAndDeletes()
        {
            string dir = NewTempPath("dir");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "a.txt"), "A");
            File.WriteAllText(Path.Combine(dir, "b.txt"), "B");
            Assert.True(Directory.Exists(dir));

            try
            {
                Assert.True(FileSystemRetryHelper.TryDeleteDirectory(dir));
                Assert.False(Directory.Exists(dir));
            }
            finally
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }
        }

        [Fact]
        public void TryDeleteDirectory_MissingDirectory_ReturnsFalse()
        {
            string dir = NewTempPath("missingdir");
            Assert.False(Directory.Exists(dir));

            // غير موجود أصلاً: لا شيء حُذف
            Assert.False(FileSystemRetryHelper.TryDeleteDirectory(dir));
        }

        [Fact]
        public void TryDeleteDirectory_NestedDirectories_DeletesAll()
        {
            string root = NewTempPath("nested");
            string inner = Path.Combine(root, "inner");
            string innerFile = Path.Combine(inner, "deep.txt");

            Directory.CreateDirectory(inner);
            File.WriteAllText(innerFile, "DEEP");
            Assert.True(File.Exists(innerFile));

            try
            {
                Assert.True(FileSystemRetryHelper.TryDeleteDirectory(root));
                Assert.False(File.Exists(innerFile));
                Assert.False(Directory.Exists(inner));
                Assert.False(Directory.Exists(root));
            }
            finally
            {
                try { Directory.Delete(root, recursive: true); } catch { }
            }
        }
    }
}
