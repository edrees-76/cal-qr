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
            catch (System.Exception swallowEx1)
            {
                CAL_QR.Services.AppLog.Error("FileHelper.cs:25", swallowEx1);
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
            catch (System.Exception swallowEx2)
            {
                CAL_QR.Services.AppLog.Error("FileHelper.cs:39", swallowEx2);
                return -1;
            }
        }
    }
}
