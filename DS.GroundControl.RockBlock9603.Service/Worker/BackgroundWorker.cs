using ILogger = DS.GroundControl.RockBlock9603.Service.Log4Net.ILogger;
using IConfigurationManager = DS.GroundControl.RockBlock9603.Service.Configuration.IConfigurationManager;

namespace DS.GroundControl.RockBlock9603.Service.Worker
{
    public class BackgroundWorker : BackgroundService
    {
        private IHostApplicationLifetime HostApplicationLifetime { get; }
        private ILogger Log { get; }       
        private IConfigurationManager ConfigurationManager { get; }

        public BackgroundWorker(
            IHostApplicationLifetime hostApplicationLifetime,
            ILogger log,
            IConfigurationManager configurationManager)
        {
            HostApplicationLifetime = hostApplicationLifetime;
            Log = log;
            ConfigurationManager = configurationManager;
        }

        private async Task<bool> WaitForAppStartedAsync(CancellationToken stoppingToken)
        {
            var started = new TaskCompletionSource();
            var stopped = new TaskCompletionSource();

            using var startedCtr = HostApplicationLifetime.ApplicationStarted.Register(() => started.SetResult());
            using var stoppedCtr = stoppingToken.Register(() => stopped.SetResult());

            return await Task.WhenAny(started.Task, stopped.Task) == started.Task;
        }
        protected async override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (await WaitForAppStartedAsync(stoppingToken))
            {
                Log.Info($"{ConfigurationManager.ServiceConfiguration.ServiceName} - Started");
                
            }
            return;
        }
        public async override Task StartAsync(CancellationToken cancellationToken)
        {
            Log.Info($"{ConfigurationManager.ServiceConfiguration.ServiceName} - Starting");

            HostApplicationLifetime.ApplicationStopped.Register(() =>
            {
                Log.Info($"{ConfigurationManager.ServiceConfiguration.ServiceName} - Stopped");
            });

            await base.StartAsync(cancellationToken);

            return;
        }
        public async override Task StopAsync(CancellationToken cancellationToken)
        {
            Log.Info($"{ConfigurationManager.ServiceConfiguration.ServiceName} - Stopping");

            
            await base.StopAsync(cancellationToken);

            return;
        }
    }
}