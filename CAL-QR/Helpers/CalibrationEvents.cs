using System;

namespace CAL_QR.Helpers
{
    public static class CalibrationEvents
    {
        public static event EventHandler? CalibrationChanged;

        public static void RaiseCalibrationChanged()
        {
            CalibrationChanged?.Invoke(null, EventArgs.Empty);
        }
    }
}
