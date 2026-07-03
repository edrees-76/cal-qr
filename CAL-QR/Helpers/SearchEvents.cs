using System;

namespace CAL_QR.Helpers
{
    public static class SearchEvents
    {
        public static event Action<int>? NavigateToDevice;
        public static event Action<int>? NavigateToOwner;
        public static event Action<int>? NavigateToDeviceType;
        public static event Action<int>? NavigateToCalibrationRecord;

        public static void RaiseNavigateToDevice(int id) => NavigateToDevice?.Invoke(id);
        public static void RaiseNavigateToOwner(int id) => NavigateToOwner?.Invoke(id);
        public static void RaiseNavigateToDeviceType(int id) => NavigateToDeviceType?.Invoke(id);
        public static void RaiseNavigateToCalibrationRecord(int id) => NavigateToCalibrationRecord?.Invoke(id);
    }
}
