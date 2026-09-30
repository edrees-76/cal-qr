using System;
using System.IO;
using Xunit;
using CAL_QR.Helpers;

namespace CAL_QR.Tests
{
    public class AppPathsTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "calqr_paths_" + Guid.NewGuid().ToString("N"));
        private readonly string _app;
        private readonly string _data;

        public AppPathsTests()
        {
            _app = Path.Combine(_root, "app");
            _data = Path.Combine(_root, "data");
            Directory.CreateDirectory(_app);
        }

        public void Dispose()
        {
            try { Directory.Delete(_root, true); }
            catch (IOException) { /* ملفّ مؤقّت؛ لا يضرّ */ }
        }

        [Fact]
        public void DataRoot_IsUnderProgramData_AndNamedCalQr()
        {
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            Assert.Equal(Path.Combine(programData, "CAL-QR"), AppPaths.DataRoot);
        }

        [Fact]
        public void DefaultDbPath_FreshInstall_UsesDataRoot()
        {
            Assert.Equal(Path.GetFullPath(Path.Combine(_data, "cal-qr.db")), AppPaths.DefaultDbPath(_app, _data));
        }

        [Fact]
        public void DefaultDbPath_LegacyDbBesideProgram_KeepsUsingIt()
        {
            string legacy = Path.Combine(_app, "cal-qr-simulation.db");
            File.WriteAllText(legacy, "x");

            Assert.Equal(Path.GetFullPath(legacy), AppPaths.DefaultDbPath(_app, _data));
        }

        [Fact]
        public void FindPointerFile_NoneLegacyOnlyAndBoth_FollowsPriority()
        {
            Assert.Null(AppPaths.FindPointerFile(_app, _data));

            string legacy = Path.Combine(_app, "db_path.txt");
            File.WriteAllText(legacy, "x");
            Assert.Equal(legacy, AppPaths.FindPointerFile(_app, _data));

            Directory.CreateDirectory(_data);
            string current = Path.Combine(_data, "db_path.txt");
            File.WriteAllText(current, "y");
            Assert.Equal(current, AppPaths.FindPointerFile(_app, _data));
        }

        [Fact]
        public void PointerFileForWrite_IsAlwaysInDataRoot_NeverBesideProgram()
        {
            Assert.Equal(Path.Combine(_data, "db_path.txt"), AppPaths.PointerFileForWrite(_data));
        }

        [Fact]
        public void DefaultAttachmentsAndQr_UseLegacyFolderIfPresent_ElseDataRoot()
        {
            Assert.Equal(Path.GetFullPath(Path.Combine(_data, "Attachments")), AppPaths.DefaultAttachmentsFolder(_app, _data));
            Assert.Equal(Path.GetFullPath(Path.Combine(_data, "poster")), AppPaths.DefaultQrFolder(_app, _data));

            Directory.CreateDirectory(Path.Combine(_app, "Attachments"));
            Directory.CreateDirectory(Path.Combine(_app, "poster"));

            Assert.Equal(Path.GetFullPath(Path.Combine(_app, "Attachments")), AppPaths.DefaultAttachmentsFolder(_app, _data));
            Assert.Equal(Path.GetFullPath(Path.Combine(_app, "poster")), AppPaths.DefaultQrFolder(_app, _data));
        }

        [Fact]
        public void EnsureDirectory_CreatesNestedFolders_AndIgnoresEmpty()
        {
            string nested = Path.Combine(_data, "a", "b");
            AppPaths.EnsureDirectory(nested);
            Assert.True(Directory.Exists(nested));

            AppPaths.EnsureDirectory(null);
            AppPaths.EnsureDirectory("   ");
        }

        [Fact]
        public void EnsureDirectory_WhenPathCannotBeCreated_ThrowsReadableArabicMessage()
        {
            string file = Path.Combine(_app, "iam_a_file.txt");
            File.WriteAllText(file, "x");

            var ex = Assert.Throws<InvalidOperationException>(() => AppPaths.EnsureDirectory(Path.Combine(file, "sub")));

            Assert.Contains("تعذّر", ex.Message);
            Assert.NotNull(ex.InnerException);
        }

        [Fact]
        public void QrPaths_DefaultFolder_DelegatesToAppPaths()
        {
            Assert.Equal(AppPaths.DefaultQrFolder(), QrPaths.DefaultFolder());
        }
    }
}
