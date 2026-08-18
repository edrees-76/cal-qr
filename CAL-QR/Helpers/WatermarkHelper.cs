using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace CAL_QR.Helpers
{
    /// <summary>
    /// Attached Property يعرض نص watermark عربي داخل TextBox فارغ غير مركَّز.
    /// يستخدم WPF AdornerLayer — مستقل تماماً عن MaterialDesign HintAssist.
    ///
    /// الاستخدام:
    ///   xmlns:helpers="clr-namespace:CAL_QR.Helpers"
    ///   helpers:WatermarkHelper.Watermark="الجهة المالكة للجهاز"
    /// </summary>
    public static class WatermarkHelper
    {
        public static readonly DependencyProperty WatermarkProperty =
            DependencyProperty.RegisterAttached(
                "Watermark",
                typeof(string),
                typeof(WatermarkHelper),
                new PropertyMetadata(null, OnWatermarkChanged));

        public static string GetWatermark(DependencyObject obj) =>
            (string)obj.GetValue(WatermarkProperty);

        public static void SetWatermark(DependencyObject obj, string value) =>
            obj.SetValue(WatermarkProperty, value);

        private static void OnWatermarkChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextBox textBox) return;

            // أزل الاشتراكات القديمة إن وُجدت (يمنع التكرار)
            textBox.Loaded -= OnLoaded;
            textBox.GotFocus -= OnFocusChanged;
            textBox.LostFocus -= OnFocusChanged;
            textBox.TextChanged -= OnTextChanged;
            textBox.IsVisibleChanged -= OnVisibilityChanged;

            if (e.NewValue is string text && !string.IsNullOrEmpty(text))
            {
                textBox.Loaded += OnLoaded;
                textBox.GotFocus += OnFocusChanged;
                textBox.LostFocus += OnFocusChanged;
                textBox.TextChanged += OnTextChanged;
                textBox.IsVisibleChanged += OnVisibilityChanged;

                // إذا كان الحقل محمّلاً فعلاً (تعديل ديناميكي)
                if (textBox.IsLoaded)
                    UpdateAdorner(textBox);
            }
            else
            {
                // أُزيل النص → أزل الـ adorner
                RemoveAdorner(textBox);
            }
        }

        private static void OnLoaded(object sender, RoutedEventArgs e) =>
            UpdateAdorner((TextBox)sender);

        private static void OnFocusChanged(object sender, RoutedEventArgs e) =>
            UpdateAdorner((TextBox)sender);

        private static void OnTextChanged(object sender, TextChangedEventArgs e) =>
            UpdateAdorner((TextBox)sender);

        // حقل يُخفى شرطيًّا (Collapsed) لا يُطلق أيًّا من الأحداث أعلاه، فيبقى
        // adorner الـwatermark عالقًا في الطبقة المشتركة ويتراكب فوق جاره بعد
        // إعادة تدفّق WrapPanel. تتبّع الرؤية يستدعي UpdateAdorner عند كل تغيّر،
        // وشرط IsVisible في UpdateAdorner يزيل الـadorner عند الإخفاء.
        private static void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) =>
            UpdateAdorner((TextBox)sender);

        private static void UpdateAdorner(TextBox textBox)
        {
            var layer = AdornerLayer.GetAdornerLayer(textBox);
            if (layer == null) return;

            // أزل أي adorner سابق
            RemoveAdornerFromLayer(layer, textBox);

            // أظهر فقط إذا: الحقل فارغ + غير مركَّز
            // IsVisible شرط لازم: حقل مخفيّ فارغ غير مركَّز كان سيمرّ بدونه فيُرسَم
            // له adorner شبح يتراكب فوق الحقول الظاهرة.
            bool shouldShow = string.IsNullOrEmpty(textBox.Text) && !textBox.IsFocused && textBox.IsVisible;
            if (shouldShow)
            {
                string watermarkText = GetWatermark(textBox);
                if (!string.IsNullOrEmpty(watermarkText))
                {
                    layer.Add(new WatermarkAdorner(textBox, watermarkText));
                }
            }
        }

        private static void RemoveAdorner(TextBox textBox)
        {
            var layer = AdornerLayer.GetAdornerLayer(textBox);
            if (layer != null)
                RemoveAdornerFromLayer(layer, textBox);
        }

        private static void RemoveAdornerFromLayer(AdornerLayer layer, TextBox textBox)
        {
            var adorners = layer.GetAdorners(textBox);
            if (adorners == null) return;
            foreach (var adorner in adorners)
            {
                if (adorner is WatermarkAdorner)
                    layer.Remove(adorner);
            }
        }
    }

    /// <summary>
    /// Adorner يعرض نص الـ watermark العربي داخل منطقة الحقل عبر TextBlock.
    /// لون باهت (#9E9E9E)، خط الحقل نفسه، لا يعترض الأحداث.
    /// </summary>
    internal sealed class WatermarkAdorner : Adorner
    {
        private readonly TextBlock _textBlock;

        public WatermarkAdorner(TextBox adornedElement, string watermarkText)
            : base(adornedElement)
        {
            IsHitTestVisible = false;

            _textBlock = new TextBlock
            {
                Text = watermarkText,
                FontFamily = adornedElement.FontFamily,
                FontSize = adornedElement.FontSize > 0 ? adornedElement.FontSize : 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E)),
                // LTR عمداً: النافذة أصلاً RTL فتعكسه تلقائياً — لو وضعنا RTL هنا
                // لتضاعف الانعكاس وظهر النص مقلوباً.
                FlowDirection = FlowDirection.LeftToRight,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.7
            };

            AddVisualChild(_textBlock);
        }

        protected override int VisualChildrenCount => 1;

        protected override Visual GetVisualChild(int index) => _textBlock;

        protected override Size MeasureOverride(Size constraint)
        {
            var textBox = (TextBox)AdornedElement;
            _textBlock.Measure(new Size(textBox.ActualWidth, textBox.ActualHeight));
            return textBox.RenderSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var textBox = (TextBox)AdornedElement;
            double left = textBox.BorderThickness.Left + textBox.Padding.Left + 4;
            double top = 0;
            double width = Math.Max(0, textBox.ActualWidth - left - textBox.BorderThickness.Right - textBox.Padding.Right - 4);
            double height = textBox.ActualHeight;

            _textBlock.Arrange(new Rect(left, top, width, height));
            return finalSize;
        }
    }
}
