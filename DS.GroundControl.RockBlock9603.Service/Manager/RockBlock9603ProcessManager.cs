using DS.GroundControl.Lib.Extensions;
using DS.GroundControl.RockBlock9603.Service.Factories;

namespace DS.GroundControl.RockBlock9603.Service.Manager
{
    public class RockBlock9603ProcessManager : IRockBlock9603ProcessManager
    {
        private IRockBlock9603ProcessFactory RockBlock9603ProcessFactory { get; }
        private IRockBlock9603Process RockBlock9603Process { get; set; }
        private CancellationTokenSource CanceledSource { get; }
        private CancellationTokenSource StartedSource { get; }
        private CancellationTokenSource StoppedSource { get; }
        public CancellationToken Canceled { get; }
        public CancellationToken Started { get; }
        public CancellationToken Stopped { get; }

        public RockBlock9603ProcessManager(
            IRockBlock9603ProcessFactory rockBlock9603ProcessFactory)
        {
            RockBlock9603ProcessFactory = rockBlock9603ProcessFactory;
            CanceledSource = new CancellationTokenSource();
            StartedSource = new CancellationTokenSource();
            StoppedSource = new CancellationTokenSource();
            Canceled = CanceledSource.Token;
            Started = CanceledSource.Token;
            Stopped = StoppedSource.Token;
        }

        public async Task StartAsync()
        {
            _ = StartedSource.CancelAsync();
            try
            {
                while (true)
                {
                    await RockBlock9603ProcessStartAsync();
                    await RockBlock9603ProcessRunningAsync();
                    await RockBlock9603ProcessStopAsync();
                    await Task.Delay(TimeSpan.FromSeconds(10), Canceled);
                }
            }
            catch { }
            _ = StoppedSource.CancelAsync();
        }
        public async Task StopAsync()
        {
            _ = CanceledSource.CancelAsync();
            if (Started.IsCancellationRequested)
            {
                await Stopped.WhenCanceledAsync();
            }
            CanceledSource.Dispose();
            StartedSource.Dispose();
            StoppedSource.Dispose();
        }
        private async Task RockBlock9603ProcessStartAsync()
        {
            RockBlock9603Process = RockBlock9603ProcessFactory.Create();
            _ = RockBlock9603Process.StartAsync();

            var running = new TaskCompletionSource();
            var stopped = new TaskCompletionSource();
            using var run = RockBlock9603Process.Running.Register(() => running.SetResult());
            using var stp = RockBlock9603Process.Stopped.Register(() => stopped.SetResult());
            await Task.WhenAny(running.Task, stopped.Task);
        }
        private async Task RockBlock9603ProcessRunningAsync()
        {
            var canceled = new TaskCompletionSource();
            var stopped = new TaskCompletionSource();
            using var cnl = Canceled.Register(() => canceled.SetResult());
            using var stp = RockBlock9603Process.Stopped.Register(() => stopped.SetResult());
            await Task.WhenAny(canceled.Task, stopped.Task);
        }
        private async Task RockBlock9603ProcessStopAsync()
        {
            await RockBlock9603Process.StopAsync();
        }
    }
}