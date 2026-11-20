using System;
using System.Threading.Tasks;
using System.Windows.Input;
using CAL_QR.Services;

namespace CAL_QR.ViewModels.Base
{
    /// <summary>
    /// أمر غير متزامن: يعطّل نفسه أثناء التنفيذ (يمنع النقر المزدوج وإعادة الدخول)،
    /// ويلتقط أيّ استثناء يخرج من المهمّة فيسجّله في AppLog ويمرّره إلى onError إن وُجد،
    /// فلا ينهار التطبيق بسبب async void ولا يضيع الخطأ صامتاً.
    /// </summary>
    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<object?, Task> _execute;
        private readonly Func<object?, bool>? _canExecute;
        private readonly Action<Exception>? _onError;
        private bool _isExecuting;

        public AsyncRelayCommand(Func<object?, Task> execute,
            Func<object?, bool>? canExecute = null,
            Action<Exception>? onError = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
            _onError = onError;
        }

        public AsyncRelayCommand(Func<Task> execute,
            Func<bool>? canExecute = null,
            Action<Exception>? onError = null)
        {
            if (execute == null) throw new ArgumentNullException(nameof(execute));
            _execute = _ => execute();
            _canExecute = canExecute != null ? _ => canExecute() : null;
            _onError = onError;
        }

        /// <summary>
        /// شبكة أمان للأوامر التي تتولّى دوالّها معالجة أخطائها المتوقَّعة بنفسها:
        /// تُظهر رسالة عامّة بالعربيّة (الخطأ نفسه سُجِّل في AppLog قبل الاستدعاء).
        /// </summary>
        public static void ShowUnexpectedError(Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"حدث خطأ غير متوقّع: {ex.Message}", "خطأ",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }

        public bool IsExecuting => _isExecuting;

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter)
        {
            return !_isExecuting && (_canExecute == null || _canExecute(parameter));
        }

        // ICommand.Execute لا يمكن أن يكون async Task؛ المنطق كلّه في ExecuteAsync
        // الذي يلتقط الاستثناءات، فلا يتسرّب أيّ استثناء من هذا الـvoid.
        public async void Execute(object? parameter)
        {
            await ExecuteAsync(parameter);
        }

        /// <summary>قابل للانتظار (للاختبارات). لا يرمي أبداً: الاستثناء يُسجَّل ويُمرَّر إلى onError.</summary>
        public async Task ExecuteAsync(object? parameter)
        {
            if (!CanExecute(parameter)) return;

            _isExecuting = true;
            RaiseCanExecuteChanged();
            try
            {
                await _execute(parameter);
            }
            catch (Exception ex)
            {
                AppLog.Error("AsyncRelayCommand", ex);
                _onError?.Invoke(ex);
            }
            finally
            {
                _isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
