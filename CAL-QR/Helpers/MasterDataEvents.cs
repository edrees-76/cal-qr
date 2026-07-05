using System;

namespace CAL_QR.Helpers
{
    public static class MasterDataEvents
    {
        public static event EventHandler? OwnerAdded;
        public static event EventHandler? DeviceTypeAdded;

        public static void RaiseOwnerAdded() => OwnerAdded?.Invoke(null, EventArgs.Empty);
        public static void RaiseDeviceTypeAdded() => DeviceTypeAdded?.Invoke(null, EventArgs.Empty);
    }
}
