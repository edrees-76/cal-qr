using System;
using System.IO;

namespace CAL_QR.Helpers
{
    public static class FileHelper
    {
        public static void EnsureDirectoryExists(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        public static bool HasEnoughDiskSpace(string path, long requiredBytes = 524288000) // 500 MB default
        {
            try
            {
                string root = Path.GetPathRoot(Path.GetFullPath(path)) ?? "C:\\";
                var driveInfo = new DriveInfo(root);
                return driveInfo.AvailableFreeSpace >= requiredBytes;
            }
            catch
            {
                return true;
            }
        }

        public static long GetAvailableFreeSpace(string path)
        {
            try
            {
                string root = Path.GetPathRoot(Path.GetFullPath(path)) ?? "C:\\";
                var driveInfo = new DriveInfo(root);
                return driveInfo.AvailableFreeSpace;
            }
            catch
            {
                return -1;
            }
        }
    }
}
