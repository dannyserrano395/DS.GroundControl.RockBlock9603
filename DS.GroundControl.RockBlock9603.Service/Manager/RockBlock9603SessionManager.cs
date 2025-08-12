using DS.GroundControl.Lib.Extensions;
using DS.GroundControl.RockBlock9603.Service.Factories;

namespace DS.GroundControl.RockBlock9603.Service.Manager
{
    public class RockBlock9603SessionManager : IRockBlock9603SessionManager
    {
        private IRockBlock9603ProcessFactory RockBlock9603ProcessFactory { get; }
        private IRockBlock9603Session RockBlock9603Process { get; set; }
        private CancellationTokenSource CanceledSource { get; }
        private CancellationTokenSource StartedSource { get; }
        private CancellationTokenSource StoppedSource { get; }
        public CancellationToken Canceled { get; }
        public CancellationToken Started { get; }
        public CancellationToken Stopped { get; }

        public RockBlock9603SessionManager(IRockBlock9603ProcessFactory rockBlock9603ProcessFactory)
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
                    using (RockBlock9603Process = RockBlock9603ProcessFactory.Create())
                    {
                        await RockBlock9603Process.StartAsync();
                    }
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
    }
}