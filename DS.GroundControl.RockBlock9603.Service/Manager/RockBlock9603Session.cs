using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;
using DS.GroundControl.Lib.Devices;
using DS.GroundControl.Lib.Extensions;
using ILogger = DS.GroundControl.RockBlock9603.Service.Log4Net.ILogger;
using IConfigurationManager = DS.GroundControl.RockBlock9603.Service.Configuration.IConfigurationManager;

namespace DS.GroundControl.RockBlock9603.Service.Manager
{
    public class RockBlock9603Session : IRockBlock9603Session
    {
        private ILogger Log { get; }
        private IConfigurationManager ConfigurationManager { get; }
        private IRockBlock9603 RockBlock9603 { get; set; }
        private JsonSerializerOptions JsonSerializerOptions { get; }
        private TaskCompletionSource ConnectedSource { get; }
        private TaskCompletionSource DisconnectedSource { get; }
        private TaskCompletionSource FaultedSource { get; }
        public Task Connected { get; }
        public Task Disconnected { get; }
        public Task Faulted { get; }

        public RockBlock9603Session(
            ILogger log,
            IConfigurationManager configurationManager)
        {
            Log = log;
            ConfigurationManager = configurationManager;
            JsonSerializerOptions = new JsonSerializerOptions()
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            ConnectedSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            DisconnectedSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            FaultedSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Connected = ConnectedSource.Task;
            Disconnected = DisconnectedSource.Task;
            Faulted = FaultedSource.Task;
        }

        public async Task StartAsync()
        {
            await RockBlock9603.ConnectAsync();
            if (RockBlock9603.Connected.IsCompletedSuccessfully)
            {
                if (ConnectedSource.TrySetResult())
                {
                    await RockBlock9603RunningAsync();
                }
            }
            else if (RockBlock9603.Faulted.IsCompletedSuccessfully)
            {
                FaultedSource.TrySetResult();
            }
        }
        private async Task RockBlock9603RunningAsync()
        {         
            async Task<int> HandleIridiumSessionAsync(int i)
            {
                try
                {
                    var codes = await IridiumBinarySessionAsync();
                    if (codes != null)
                    {
                        var session = new
                        {
                            MobileOriginatedStatus = codes[0],
                            MobileOriginatedMessageSequenceNumber = codes[1],
                            MobileOriginatedMessage = codes[2],
                            MobileTerminatedStatus = codes[3],
                            MobileTerminatedMessageSequenceNumber = codes[4],
                            MobileTerminatedLength = codes[5],
                            MobileTerminatedQueued = codes[6],
                            MobileTerminatedMessage = codes[7]
                        };
                        switch (session)
                        {
                            case { MobileOriginatedStatus: "0", MobileTerminatedStatus: "0", MobileTerminatedQueued: "0" }:
                                {
                                    await Task.Delay(TimeSpan.FromMinutes(ConfigurationManager.WorkerConfiguration.IridiumSessionFreqMin));
                                    return 0;
                                }
                            case { MobileOriginatedStatus: "1" } or { MobileTerminatedStatus: "1" }:
                                {
                                    await Task.Delay(TimeSpan.FromSeconds(10));
                                    return 0;
                                }
                            default:
                                {
                                    if (i < 3)
                                    {
                                        await Task.Delay(TimeSpan.FromMinutes(ConfigurationManager.WorkerConfiguration.IridiumSessionFreqMin));
                                        return i + 1;
                                    }
                                    else
                                    {
                                        await Task.Delay(TimeSpan.FromMinutes(ConfigurationManager.WorkerConfiguration.IridiumSessionFreqMin));
                                        return 0;
                                    }
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
        private async Task<string[]> IridiumBinarySessionAsync(string message = default)
        {
            try
            {
                string moMessage = null;
                if (!string.IsNullOrEmpty(message))
                {
                    var sbdwb = await RockBlock9603.ExecuteAsync($"AT+SBDWB={message.Length}");
                    if (sbdwb is not { Response: "READY" })
                    {
                        return null;
                    }
                    var sbdmsg = await RockBlock9603.ExecuteReadyStateBinaryCommandAsync(message);
                    if (sbdmsg is not { Response: "0", Result: "OK" or "0" })
                    {
                        return null;
                    }
                    moMessage = message;
                }

                var sbdi = await RockBlock9603.ExecuteAsync("AT+SBDI");
                if (sbdi is not { Result: "OK" or "0" })
                {
                    return null;
                }

                var codes = sbdi.Response.RemoveSubstring("+SBDI: ").Replace(" ", "").Split(',');

                string mtMessage = null;
                if (codes[2] == "1")
                {
                    var sbdrb = await RockBlock9603.ExecuteAsync("AT+SBDRB");
                    if (sbdrb is not { Result: "OK" or "0" })
                    {
                        return null;
                    }
                    var bytes = Encoding.ASCII.GetBytes(sbdrb.Response);
                    int len = (bytes[0] << 8) | bytes[1];
                    var payload = bytes.Skip(2).Take(len).ToArray();
                    var msg = Encoding.ASCII.GetString(payload);
                    var cks = bytes.Skip(2 + len).Take(2).ToArray();
                    mtMessage = msg;
                }

                var sbdd = await RockBlock9603.ExecuteAsync("AT+SBDD2");
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
                if (!string.IsNullOrEmpty(message))
                {
                    var sbdwt = await RockBlock9603.ExecuteAsync($"AT+SBDWT={message}");
                    if (sbdwt is not { Result: "OK" or "0" })
                    {
                        return null;
                    }
                    moMessage = message;
                }

                var sbdi = await RockBlock9603.ExecuteAsync("AT+SBDI");
                if (sbdi is not { Result: "OK" or "0" })
                {
                    return null;
                }

                var codes = sbdi.Response.RemoveSubstring("+SBDI: ").Replace(" ", "").Split(',');

                string mtMessage = null;
                if (codes[2] == "1")
                {
                    var sbdrt = await RockBlock9603.ExecuteAsync("AT+SBDRT");
                    if (sbdrt is not { Result: "OK" or "0" })
                    {
                        return null;
                    }
                    mtMessage = sbdrt.Response.RemoveSubstring("+SBDRT:");
                }

                var sbdd = await RockBlock9603.ExecuteAsync("AT+SBDD2");
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

        #region idisposable
        private bool _isDisposed;
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    RockBlock9603.Dispose();
                    if (!Connected.IsCompleted)
                    {
                        ConnectedSource.TrySetCanceled();
                    }
                    if (!Disconnected.IsCompleted)
                    {
                        if (Connected.IsCompletedSuccessfully)
                        {
                            DisconnectedSource.TrySetResult();
                        }
                        else
                        {
                            DisconnectedSource.TrySetCanceled();
                        }
                    }
                    if (!Faulted.IsCompleted)
                    {
                        FaultedSource.TrySetCanceled();
                    }
                }
                _isDisposed = true;
            }
        }
        #endregion
    }
}

#region AT+SBDI
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
#endregion

#region AT+SBDIX
// no upload or download
// success  0 , 0
// failed   32 , 2

// upload and no download
// success  0 , 0 
// failed   (32 , 2) , (18 , 2)

// downloand and no upload
// success  1 , 0
// failed   2 , 32    

// upload and download
// success  0 , 1
// failed   (32 , 2) , (18 , 2)

//var len = sbdrb.Response.Substring(0, 2);
//var msg = sbdrb.Response.Substring(2, sbdrb.Response.Length - 4);
//var cks = sbdrb.Response.Substring(sbdrb.Response.Length - 2);
#endregion