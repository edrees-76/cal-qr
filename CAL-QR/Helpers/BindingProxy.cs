using System.Windows;

namespace CAL_QR.Helpers
{
    /// <summary>
    /// وسيط ربط قياسيّ لأعمدة DataGrid: DataGridColumn ليس في الشجرة المرئيّة ولا
    /// المنطقيّة فلا يرث DataContext ولا يرى ElementName. يُوضع هذا الكائن في
    /// Window.Resources (حيث يرث DataContext النافذة فعلاً) ثمّ يُربط عمود الرأس
    /// عبره بـStaticResource. Freezable لا DependencyObject عادي: Freezable وحده
    /// يملك DataContext من موروث شجرة الموارد رغم عدم وجوده في أيّ شجرة مرئيّة.
    /// </summary>
    public class BindingProxy : Freezable
    {
        protected override Freezable CreateInstanceCore() => new BindingProxy();

        public object Data
        {
            get => GetValue(DataProperty);
            set => SetValue(DataProperty, value);
        }

        public static readonly DependencyProperty DataProperty =
            DependencyProperty.Register(nameof(Data), typeof(object), typeof(BindingProxy), new UIPropertyMetadata(null));
    }
}
