using System;
using System.Windows;

namespace CAL_QR.Helpers
{
    /// <summary>
    /// نوافذ الحوار لها أحجام ثابتة (مثلاً 750×1200) قد تتجاوز مساحة العمل على شاشة 1366×768، فيُقصّ
    /// أسفلها (أزرار الحفظ). يُصغَّر الحجم ليناسب المساحة المتاحة فقط؛ الشاشات الأكبر لا تتأثّر.
    /// </summary>
    public static class WindowSizing
    {
        /// <summary>أصغر من الحجم المطلوب والمساحة المتاحة (بهامش صغير)، دون تغيير إن كان يناسب.</summary>
        public static double Fit(double requested, double available, double margin = 16)
        {
            if (double.IsNaN(requested) || available <= 0) return requested;
            double limit = Math.Max(0, available - margin);
            return requested > limit ? limit : requested;
        }

        public static void ClampToWorkArea(Window window)
        {
            var area = SystemParameters.WorkArea;

            double height = Fit(window.Height, area.Height);
            if (height != window.Height)
            {
                window.MinHeight = Math.Min(window.MinHeight, height);
                window.Height = height;
            }

            double width = Fit(window.Width, area.Width);
            if (width != window.Width)
            {
                window.MinWidth = Math.Min(window.MinWidth, width);
                window.Width = width;
            }
        }
    }
}
