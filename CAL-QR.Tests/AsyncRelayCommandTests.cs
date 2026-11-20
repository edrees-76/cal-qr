using System;
using System.IO;
using System.Threading.Tasks;
using CAL_QR.Services;
using CAL_QR.ViewModels.Base;
using Xunit;

namespace CAL_QR.Tests
{
    public class AsyncRelayCommandTests : IDisposable
    {
        private readonly string _originalLogDir = AppLog.LogDirectory;
        private readonly string _tempLogDir =
            Path.Combine(Path.GetTempPath(), "calqr-asynccmd-" + Guid.NewGuid().ToString("N"));

        public AsyncRelayCommandTests()
        {
            AppLog.LogDirectory = _tempLogDir;
        }

        public void Dispose()
        {
            AppLog.LogDirectory = _originalLogDir;
            try { if (Directory.Exists(_tempLogDir)) Directory.Delete(_tempLogDir, true); }
            catch (IOException) { /* تنظيف اختباريّ فقط */ }
        }

        [Fact]
        public async Task ExecuteAsync_RunsDelegate_AndPassesParameter()
        {
            object? received = null;
            var cmd = new AsyncRelayCommand(p => { received = p; return Task.CompletedTask; });

            await cmd.ExecuteAsync("x");

            Assert.Equal("x", received);
            Assert.False(cmd.IsExecuting);
        }

        [Fact]
        public async Task CanExecute_IsFalse_WhileRunning_AndTrueAfter()
        {
            var gate = new TaskCompletionSource();
            var cmd = new AsyncRelayCommand(() => gate.Task);

            var running = cmd.ExecuteAsync(null);

            Assert.True(cmd.IsExecuting);
            Assert.False(cmd.CanExecute(null));

            gate.SetResult();
            await running;

            Assert.False(cmd.IsExecuting);
            Assert.True(cmd.CanExecute(null));
        }

        [Fact]
        public async Task ExecuteAsync_WhileRunning_DoesNotReenter()
        {
            var gate = new TaskCompletionSource();
            int calls = 0;
            var cmd = new AsyncRelayCommand(async () => { calls++; await gate.Task; });

            var first = cmd.ExecuteAsync(null);
            await cmd.ExecuteAsync(null); // نقرة ثانية أثناء التنفيذ: تُتجاهل
            gate.SetResult();
            await first;

            Assert.Equal(1, calls);
        }

        [Fact]
        public async Task CanExecute_RespectsPredicate()
        {
            bool allowed = false;
            int calls = 0;
            var cmd = new AsyncRelayCommand(() => { calls++; return Task.CompletedTask; }, () => allowed);

            Assert.False(cmd.CanExecute(null));
            await cmd.ExecuteAsync(null);
            Assert.Equal(0, calls);

            allowed = true;
            await cmd.ExecuteAsync(null);
            Assert.Equal(1, calls);
        }

        [Fact]
        public async Task ExecuteAsync_CapturesException_CallsOnError_AndReenables()
        {
            Exception? caught = null;
            var cmd = new AsyncRelayCommand(
                () => throw new InvalidOperationException("boom"),
                onError: ex => caught = ex);

            await cmd.ExecuteAsync(null); // لا يجوز أن يرمي

            Assert.IsType<InvalidOperationException>(caught);
            Assert.False(cmd.IsExecuting);
            Assert.True(cmd.CanExecute(null));
        }

        [Fact]
        public async Task ExecuteAsync_AsyncException_IsLoggedToAppLog()
        {
            var cmd = new AsyncRelayCommand(async () =>
            {
                await Task.Yield();
                throw new InvalidOperationException("async-boom-marker");
            });

            await cmd.ExecuteAsync(null);

            var logs = Directory.GetFiles(_tempLogDir, "app-*.log");
            Assert.Single(logs);
            Assert.Contains("async-boom-marker", File.ReadAllText(logs[0]));
        }

        [Fact]
        public async Task ExecuteAsync_WithoutOnError_DoesNotThrow()
        {
            var cmd = new AsyncRelayCommand(() => Task.FromException(new Exception("x")));

            await cmd.ExecuteAsync(null);

            Assert.False(cmd.IsExecuting);
        }

        [Fact]
        public void Constructor_NullExecute_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new AsyncRelayCommand((Func<object?, Task>)null!));
            Assert.Throws<ArgumentNullException>(() => new AsyncRelayCommand((Func<Task>)null!));
        }
    }
}
