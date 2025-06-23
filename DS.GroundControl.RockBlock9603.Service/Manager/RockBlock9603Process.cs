using System.Text.Json;
using System.Text.Encodings.Web;
using DS.GroundControl.Lib.Devices;
using DS.GroundControl.Lib.Extensions;
using DS.GroundControl.Lib.Devices.Factories;
using ILogger = DS.GroundControl.RockBlock9603.Service.Log4Net.ILogger;
using IConfigurationManager = DS.GroundControl.RockBlock9603.Service.Configuration.IConfigurationManager;

namespace DS.GroundControl.RockBlock9603.Service.Manager
{
    public class RockBlock9603Process : IRockBlock9603Process
    {
        private ILogger Log { get; }
        private IConfigurationManager ConfigurationManager { get; }
        private IRockBlock9603Factory RockBlock9603Factory { get; }
        private IRockBlock9603 RockBlock9603 { get; set; }
        private JsonSerializerOptions JsonSerializerOptions { get; }
        private CancellationTokenSource CanceledSource { get; }
        private CancellationTokenSource StartedSource { get; }
        private CancellationTokenSource RunningSource { get; }
        private CancellationTokenSource StoppedSource { get; }
        private CancellationTokenSource FaultedSource { get; }
        public CancellationToken Canceled { get; }
        public CancellationToken Started { get; }
        public CancellationToken Running { get; }
        public CancellationToken Stopped { get; }
        public CancellationToken Faulted { get; }

        public RockBlock9603Process(
            ILogger log,
            IConfigurationManager configurationManager,
            IRockBlock9603Factory rockBlock9603Factory)
        {
            Log = log;
            ConfigurationManager = configurationManager;
            RockBlock9603Factory = rockBlock9603Factory;
            JsonSerializerOptions = new JsonSerializerOptions()
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            CanceledSource = new CancellationTokenSource();
            StartedSource = new CancellationTokenSource();
            RunningSource = new CancellationTokenSource();
            StoppedSource = new CancellationTokenSource();
            FaultedSource = new CancellationTokenSource();
            Canceled = CanceledSource.Token;
            Started = StartedSource.Token;
            Running = RunningSource.Token;
            Stopped = StoppedSource.Token;
            Faulted = FaultedSource.Token;
        }

        public async Task StartAsync()
        {
            _ = StartedSource.CancelAsync();
            await RockBlock9603StartAsync();
            if (RockBlock9603.Running.IsCancellationRequested)
            {
                _ = RunningSource.CancelAsync();
                await RockBlock9603RunningAsync();
                if (RockBlock9603.Faulted.IsCancellationRequested)
                {
                    _ = FaultedSource.CancelAsync();
                }
            }
            await RockBlock9603StopAsync();
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
            RunningSource.Dispose();
            StoppedSource.Dispose();
            FaultedSource.Dispose();
        }       
        private async Task RockBlock9603StartAsync()
        {
            RockBlock9603 = RockBlock9603Factory.Create();
            _ = RockBlock9603.StartAsync();

            var running = new TaskCompletionSource();
            var stopped = new TaskCompletionSource();
            using var run = RockBlock9603.Running.Register(() => running.SetResult());
            using var stp = RockBlock9603.Stopped.Register(() => stopped.SetResult());
            await Task.WhenAny(running.Task, stopped.Task);
        }
        private async Task RockBlock9603RunningAsync()
        {         
            async Task<int> HandleIridiumSessionAsync(int i)
            {
                try
                {
                    var sessionArgs = await IridiumSessionAsync();
                    var session = new
                    {
                        MobileOriginatedStatus = sessionArgs[0],
                        MobileOriginatedMessageSequenceNumber = sessionArgs[1],
                        MobileOriginatedMessage = sessionArgs[2],
                        MobileTerminatedStatus = sessionArgs[3],
                        MobileTerminatedMessageSequenceNumber = sessionArgs[4],
                        MobileTerminatedLength = sessionArgs[5],
                        MobileTerminatedQueued = sessionArgs[6],
                        MobileTerminatedMessage = sessionArgs[7]
                    };
                    switch (session)
                    {
                        case { MobileOriginatedStatus: "0", MobileTerminatedStatus: "0", MobileTerminatedQueued: "0" }:
                            {
                                await Task.Delay(TimeSpan.FromMinutes(ConfigurationManager.WorkerConfiguration.IridiumSessionFreqMin), Canceled);
                                return 0;
                            }
                        case { MobileOriginatedStatus: "1" } or { MobileTerminatedStatus: "1" }:
                            {
                                await Task.Delay(TimeSpan.FromSeconds(10), Canceled);
                                return 0;
                            }
                        default:
                            {
                                if (i < 3)
                                {
                                    await Task.Delay(TimeSpan.FromMinutes(ConfigurationManager.WorkerConfiguration.IridiumSessionFreqMin), Canceled);
                                    return i + 1;
                                }
                                else
                                {
                                    await Task.Delay(TimeSpan.FromMinutes(ConfigurationManager.WorkerConfiguration.IridiumSessionFreqMin), Canceled);
                                    return 0;
                                }
                            }
                    }
                }
                catch { }
                return -1;
            }

            int i = 0;
            while (i != -1)
            {
                i = await HandleIridiumSessionAsync(i);
            }
        }
        private async Task RockBlock9603StopAsync()
        {
            await RockBlock9603.StopAsync();
        }
        private async Task<string[]> IridiumSessionAsync(string message = default)
        {
            try
            {
                string moMessage = null;
                if (message is not null or "")
                {
                    var sbdwb = await RockBlock9603.WriteWithCarriageReturnAsync($"AT+SBDWB={message.Length}");
                    if (sbdwb is not { Response: "READY" })
                    {
                        return null;
                    }
                    var sbdmsg = await RockBlock9603.WriteWithChecksumAsync(message);
                    if (sbdmsg is not { Response: "0", Result: "OK" or "0" })
                    {
                        return null;
                    }
                    moMessage = message;
                }

                var sbdi = await RockBlock9603.WriteWithCarriageReturnAsync("AT+SBDI");
                if (sbdi is not { Result: "OK" or "0" })
                {
                    return null;
                }

                var codes = sbdi.Response.RemoveSubstring("+SBDI: ").Replace(" ", "").Split(',');

                string mtMessage = null;
                if (codes[2] == "1")
                {
                    var sbdrb = await RockBlock9603.WriteWithCarriageReturnAsync("AT+SBDRB");
                    if (sbdrb is not { Result: "OK" or "0" })
                    {
                        return null;
                    }
                    var length = sbdrb.Response.Substring(0, 2);
                    mtMessage = sbdrb.Response.Substring(2, sbdrb.Response.Length - 4);
                    var checksum = sbdrb.Response.Substring(sbdrb.Response.Length - 2);
                }

                var sbdd = await RockBlock9603.WriteWithCarriageReturnAsync("AT+SBDD2");
                if (sbdd is not { Result: "OK" or "0" })
                {
                    return null;
                }

                return
                [
                    codes[0],
                    codes[1],
                    moMessage,
                    codes[2],
                    codes[3],
                    codes[4],
                    codes[5],
                    mtMessage
                ];
            }
            catch { }
            return null;
        }
        private async Task<string[]> IridiumTextSessionAsync(string message = default)
        {
            try
            {
                string moMessage = null;
                if (message is not null or "")
                {
                    var sbdwt = await RockBlock9603.WriteWithCarriageReturnAsync($"AT+SBDWT={message}");
                    if (sbdwt is not { Result: "OK" or "0" })
                    {
                        return null;
                    }
                    moMessage = message;
                }

                var sbdi = await RockBlock9603.WriteWithCarriageReturnAsync("AT+SBDI");
                if (sbdi is not { Result: "OK" or "0" })
                {
                    return null;
                }

                var codes = sbdi.Response.RemoveSubstring("+SBDI: ").Replace(" ", "").Split(',');

                string mtMessage = null;
                if (codes[2] == "1")
                {
                    var sbdrt = await RockBlock9603.WriteWithCarriageReturnAsync("AT+SBDRT");
                    if (sbdrt is not { Result: "OK" or "0" })
                    {
                        return null;
                    }
                    mtMessage = sbdrt.Response.RemoveSubstring("+SBDRT:");
                }

                var sbdd = await RockBlock9603.WriteWithCarriageReturnAsync("AT+SBDD2");
                if (sbdd is not { Result: "OK" or "0" })
                {
                    return null;
                }

                return
                [
                    codes[0],
                    codes[1],
                    moMessage,
                    codes[2],
                    codes[3],
                    codes[4],
                    codes[5],
                    mtMessage
                ];
            }
            catch { }
            return null;
        }       
        private string ToJsonString<T>(T value) => JsonSerializer.Serialize(value, JsonSerializerOptions);
    }
}

#region
// no upload or download
// success  0 , 0
// failed   2 , 2  

// upload and no download
// success  1 , 0
// failed   2 , 2

// downloand and no upload
// success  0 , 1
// failed   2 , 2    

// upload and download
// success  1 , 1 
// failed   2 , 2

// Log.Info(ToJsonString(session));
#endregion