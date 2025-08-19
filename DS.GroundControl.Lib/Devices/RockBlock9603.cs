using System.Text;
using System.IO.Ports;
using DS.GroundControl.Lib.Extensions;

namespace DS.GroundControl.Lib.Devices
{
    public class RockBlock9603 : IRockBlock9603
    {
        private SerialPort SerialPort { get; set; }
        private SemaphoreSlim SemaphoreSlim { get; }
        private TaskCompletionSource ConnectedSource { get; }
        private TaskCompletionSource DisconnectedSource { get; }
        private TaskCompletionSource FaultedSource { get; }
        public Task Connected { get; }
        public Task Disconnected { get; }
        public Task Faulted { get; }

        public RockBlock9603()
        {
            SemaphoreSlim = new SemaphoreSlim(1, 1);
            ConnectedSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            DisconnectedSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            FaultedSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Connected = ConnectedSource.Task;
            Disconnected = DisconnectedSource.Task;
            Faulted = FaultedSource.Task;
        }

        public async Task ConnectAsync()
        {
            await SemaphoreSlim.WaitAsync();
            try
            {
                if (EnsureCanConnect())
                {
                    SerialPort = await TryLocateAndConnectAsync();
                    if (SerialPort != null && TryTransitionToConnected())
                    {
                        return;
                    }
                }
                TryTransitionToFaulted();
                SerialPort?.Dispose();
                SerialPort = null;
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }
        public async Task<(string Command, string Response, string Result)> ExecuteAsync(string command)
        {
            await SemaphoreSlim.WaitAsync();
            try
            {
                return await ExecuteAsync(SerialPort.BaseStream, command);
            }
            catch
            {
                TryTransitionToFaulted();
                SerialPort?.Dispose();
                SerialPort = null;
                throw;
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }
        public async Task<(string Command, string Response, string Result)> ExecuteReadyStateTextCommandAsync(string command)
        {
            await SemaphoreSlim.WaitAsync();
            try
            {
                return await ExecuteReadyStateTextCommandAsync(SerialPort.BaseStream, command);
            }
            catch
            {
                TryTransitionToFaulted();
                SerialPort?.Dispose();
                SerialPort = null;
                throw;
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }
        public async Task<(string Command, string Response, string Result)> ExecuteReadyStateBinaryCommandAsync(string command)
        {
            await SemaphoreSlim.WaitAsync();
            try
            {
                return await ExecuteReadyStateBinaryCommandAsync(SerialPort.BaseStream, command);
            }
            catch
            {
                TryTransitionToFaulted();
                SerialPort?.Dispose();
                SerialPort = null;
                throw;
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }        
        private bool TryTransitionToConnected()
        {
            if (!Faulted.IsCompleted)
            {
                return ConnectedSource.TrySetResult();
            }
            return false;
        }
        private bool TryTransitionToFaulted()
        {
            if (FaultedSource.TrySetResult())
            {
                if (Connected.IsCompletedSuccessfully)
                {
                    TryTransitionToDisconnected();
                }
                return true;
            }
            return false;
        }
        private bool TryTransitionToDisconnected()
        {
            return DisconnectedSource.TrySetResult();
        }
        private void CompleteLifecycleSignalsCooperatively()
        {
            if (!Connected.IsCompleted) { ConnectedSource.TrySetCanceled(); }
            if (!Disconnected.IsCompleted)
            {
                if (Connected.IsCompletedSuccessfully) { TryTransitionToDisconnected(); }
                else { DisconnectedSource.TrySetCanceled(); }
            }
            if (!Faulted.IsCompleted) { FaultedSource.TrySetCanceled(); }
        }
        private void CancelLifecycleSignalsBestEffort()
        {
            if (!Connected.IsCompleted) { ConnectedSource.TrySetCanceled(); }
            if (!Disconnected.IsCompleted) { DisconnectedSource.TrySetCanceled(); }
            if (!Faulted.IsCompleted) { FaultedSource.TrySetCanceled(); }
        }
        private bool EnsureCanConnect()
        {
            return !Connected.IsCompleted && !Faulted.IsCompleted;
        }

        #region static
        private static IReadOnlyDictionary<string, (Func<Stream, string, CancellationToken, Task<(string Command, string Response, string Result)>> Handler, TimeSpan Timeout)> CommandMap{ get; } =
            new Dictionary<string, (Func<Stream, string, CancellationToken, Task<(string Command, string Response, string Result)>>, TimeSpan)>
        {
            { "AT+CCLK?",  (ExecuteCCLKCurrentSettingsAsync, TimeSpan.FromSeconds(5)) },
            { "AT+SBDRB",  (ExecuteSBDRBAsync, TimeSpan.FromSeconds(5)) },
            { "AT+SBDRT",  (ExecuteSBDRTAsync, TimeSpan.FromSeconds(5)) },
            { "AT+SBDWT",  (ExecuteSBDWTAsync, TimeSpan.FromSeconds(5)) },
            { "AT+SBDWB=", (ExecuteSBDWBAsync, TimeSpan.FromSeconds(5)) },
            { "AT&V",      (ExecuteATAndVAsync, TimeSpan.FromSeconds(3)) },
            { "AT+GMR",    (ExecuteGMRAsync, TimeSpan.FromSeconds(3)) },
            { "AT+CGMR",   (ExecuteCGMRAsync, TimeSpan.FromSeconds(3)) },
            { "AT%R",      (ExecuteATPercentRAsync, TimeSpan.FromSeconds(10)) },
            { "AT+CGMI",   (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(3)) },
            { "AT+CGMM",   (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(3)) },
            { "AT+CGSN",   (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(3)) },
            { "AT+CIER=?", (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT+CIER?",  (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT+CRIS",   (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT+CRISX",  (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT+CSQ",    (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(3)) },
            { "AT+CSQ=?",  (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(3)) },
            { "AT+CSQF",   (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(3)) },
            { "AT+CULK?",  (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(3)) },
            { "AT+GMI",    (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(3)) },
            { "AT+GMM",    (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(3)) },
            { "AT+GSN",    (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(3)) },
            { "AT+IPR=?",  (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT+IPR?",   (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT+SBDLOE", (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT+SBDAREG=?", (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(3)) },
            { "AT+SBDAREG?",  (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(3)) },
            { "AT+SBDC",   (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT+SBDD0",  (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT+SBDD1",  (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT+SBDD2",  (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT+SBDDSC?", (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT+SBDGW",  (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT+SBDGWN", (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT+SBDI",   (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(75)) },
            { "AT+SBDIX",  (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(75)) },
            { "AT+SBDIXA", (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(90)) },
            { "AT+SBDMTA?", (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT+SBDMTA=?", (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT+SBDREG?", (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(3)) },
            { "AT+SBDS",  (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT+SBDST?", (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT+SBDSX",  (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT+SBDTC",  (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT-MSGEOS", (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT-MSGEO",  (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT-MSSTM",  (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "ATI0",      (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(2)) },
            { "ATI1",      (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(2)) },
            { "ATI2",      (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(2)) },
            { "ATI3",      (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(2)) },
            { "ATI4",      (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(2)) },
            { "ATI5",      (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(2)) },
            { "ATI6",      (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(2)) },
            { "ATI7",      (ExecuteResponseWithPayloadAsync, TimeSpan.FromSeconds(2)) },
            { "AT+SBDWT=", (ExecuteResponseWithoutPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT&Y0",     (ExecuteResponseWithoutPayloadAsync, TimeSpan.FromSeconds(2)) },
            { "AT&K0",     (ExecuteResponseWithoutPayloadAsync, TimeSpan.FromSeconds(2)) },
            { "AT&K3",     (ExecuteResponseWithoutPayloadAsync, TimeSpan.FromSeconds(2)) },
            { "AT*R1",     (ExecuteResponseWithoutPayloadAsync, TimeSpan.FromSeconds(2)) },
            { "AT*F",      (ExecuteResponseWithoutPayloadAsync, TimeSpan.FromSeconds(2)) },
            { "AT+SBDMTA=0", (ExecuteResponseWithoutPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT+SBDMTA=1", (ExecuteResponseWithoutPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "ATE1",      (ExecuteResponseWithoutPayloadAsync, TimeSpan.FromSeconds(2)) },
            { "ATQ0",      (ExecuteResponseWithoutPayloadAsync, TimeSpan.FromSeconds(2)) },
            { "AT",        (ExecuteResponseWithoutPayloadAsync, TimeSpan.FromSeconds(2)) },
            { "ATV1",      (ExecuteResponseWithoutPayloadAsync, TimeSpan.FromSeconds(2)) },
            { "ATV0",      (ExecuteResponseWithoutPayloadAsync, TimeSpan.FromSeconds(2)) }
        };

        private static async Task<SerialPort> TryLocateAndConnectAsync()
        {
            try
            {
                foreach (var name in SerialPort.GetPortNames())
                {
                    var sp = new SerialPort();
                    try
                    {
                        sp.PortName = name;
                        sp.BaudRate = 19200;
                        sp.DataBits = 8;
                        sp.Parity = Parity.None;
                        sp.StopBits = StopBits.One;
                        sp.Open();
                        if (await TryValidateConnectionAsync(sp.BaseStream))
                        {
                            return sp;
                        }
                    }
                    catch { }
                    sp.Dispose();
                }
            }
            catch { }
            return null;
        }
        private static async Task<bool> TryValidateConnectionAsync(Stream stream)
        {
            try
            {
                var output = await ExecuteAsync(stream, "AT");
                if (output is { Command: "AT", Response: "", Result: "OK" or "0" })
                {
                    return true;
                }
            }
            catch { }
            return false;
        }
        private static async Task<(string Command, string Response, string Result)> ExecuteAsync(Stream stream, string command)
        {
            var (func, timeout) = CommandMap[NormalizeCommand(command)];
            using var cts = new CancellationTokenSource(timeout);
            return await func(stream, command, cts.Token);
        }
        private static async Task<(string Command, string Response, string Result)> ExecuteReadyStateTextCommandAsync(Stream stream, string command)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await stream.WriteAsync(bytes, 0, bytes.Length, cts.Token);

            var cmd = await stream.ReadToAsync("\r", cts.Token);
            if (cmd == command) // ATV1 + AT+SBDWT + message
            {
                await stream.ReadToAsync("\n", cts.Token);
                var response = await stream.ReadToAsync("\r\n", cts.Token);
                await stream.ReadToAsync("\r\n", cts.Token);
                var result = await stream.ReadToAsync("\r\n", cts.Token);
                return (cmd, response, result);
            }
            else // ATV0 + AT+SBDWT + message 
            {
                var response = cmd[^1].ToString();
                cmd = cmd.Substring(0, cmd.Length - 1);
                await stream.ReadToAsync("\n", cts.Token);
                var result = await stream.ReadToAsync("\r", cts.Token);
                return (cmd, response, result);
            }
        }
        private static async Task<(string Command, string Response, string Result)> ExecuteReadyStateBinaryCommandAsync(Stream stream, string command)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var cks = CalculateChecksum(command);
            var bytes = Encoding.ASCII.GetBytes(command).Concat(cks).ToArray();
            await stream.WriteAsync(bytes, 0, bytes.Length, cts.Token);

            var next = await stream.ReadByteAsync(cts.Token);
            if (IsCarriageReturn(next)) // ATV1 + AT+SBDWB= + message
            {
                var response = await stream.ReadToAsync("\r\n", cts.Token);
                await stream.ReadToAsync("\r\n", cts.Token);
                response = response[^1].ToString();
                var result = await stream.ReadToAsync("\r\n", cts.Token);
                return (string.Empty, response, result);
            }
            else // ATV0 + AT+SBDWB= + message
            {
                var response = ((char)next).ToString();
                await stream.ReadToAsync("\r\n", cts.Token);
                var result = await stream.ReadToAsync("\r", cts.Token);           
                return (string.Empty, response, result);
            }
        }
        private static async Task<(string Command, string Response, string Result)> ExecuteCCLKCurrentSettingsAsync(Stream stream, string command, CancellationToken token)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await stream.WriteAsync(bytes, 0, bytes.Length, token);
            while (true)
            {
                var cmd = await stream.ReadToAsync("\r", token);
                if (cmd == command)
                {
                    var next = await stream.ReadByteAsync(token);
                    if (IsCarriageReturn(next))
                    {
                        await stream.ReadToAsync("\n", token);
                        var response = await stream.ReadToAsync("\n\r\n", token);
                        await stream.ReadToAsync("\r\n", token);
                        var result = await stream.ReadToAsync("\r\n", token);
                        return (cmd, response, result);
                    }
                    else if (IsAscii(next))
                    {
                        var response = ((char)next).ToString();
                        response += await stream.ReadToAsync("\n\r\n", token);
                        var result = await stream.ReadToAsync("\r", token);
                        return (cmd, response, result);
                    }
                }
                if (cmd != "126")
                {
                    await stream.ReadToAsync("\n", token);
                    cmd = await stream.ReadToAsync("\r\n", token);
                }
                if (cmd is "126" or "SBDRING")
                {

                }
            }
        }
        private static async Task<(string Command, string Response, string Result)> ExecuteSBDRBAsync(Stream stream, string command, CancellationToken token)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await stream.WriteAsync(bytes, 0, bytes.Length, token);
            while (true)
            {
                var cmd = await stream.ReadToAsync("\r", token);
                if (cmd == command)
                {
                    var len = new byte[2];
                    await stream.ReadExactlyAsync(len, 0, 2, token);
                    var msg = new byte[(len[0] << 8) | len[1]];
                    await stream.ReadExactlyAsync(msg, 0, msg.Length, token);
                    var cks = new byte[2];
                    await stream.ReadExactlyAsync(cks, 0, 2, token);

                    var next = await stream.ReadByteAsync(token);
                    if (IsCarriageReturn(next))
                    {
                        await stream.ReadToAsync("\n", token);
                        var result = await stream.ReadToAsync("\r\n", token);
                        var response = Encoding.ASCII.GetString(len.Concat(msg).Concat(cks).ToArray());
                        return (cmd, response, result);
                    }
                    else if (IsAscii(next))
                    {
                        var result = ((char)next).ToString();
                        result += await stream.ReadToAsync("\r", token);
                        var response = Encoding.ASCII.GetString(len.Concat(msg).Concat(cks).ToArray());
                        return (cmd, response, result);
                    }
                }
                if (cmd != "126")
                {
                    await stream.ReadToAsync("\n", token);
                    cmd = await stream.ReadToAsync("\r\n", token);
                }
                if (cmd is "126" or "SBDRING")
                {

                }
            }
        }
        private static async Task<(string Command, string Response, string Result)> ExecuteSBDRTAsync(Stream stream, string command, CancellationToken token)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await stream.WriteAsync(bytes, 0, bytes.Length, token);
            while (true)
            {
                var cmd = await stream.ReadToAsync("\r", token);
                if (cmd == command)
                {
                    var next = await stream.ReadByteAsync(token);
                    if (IsCarriageReturn(next))
                    {
                        await stream.ReadToAsync("\n", token);
                        var response = await stream.ReadToAsync("\r\n", token);
                        response += await stream.ReadToAsync("\r\n", token);
                        var result = await stream.ReadToAsync("\r\n", token);
                        return (cmd, response, result);
                    }
                    else if (IsAscii(next))
                    {
                        var response = ((char)next).ToString();
                        response += await stream.ReadToAsync("\r\n", token);
                        response += await stream.ReadToAsync("\r", token);
                        var result = response[^1].ToString();
                        response = response.Remove(response.Length - 1);
                        return (cmd, response, result);
                    }
                }
                if (cmd != "126")
                {
                    await stream.ReadToAsync("\n", token);
                    cmd = await stream.ReadToAsync("\r\n", token);
                }
                if (cmd is "126" or "SBDRING")
                {

                }
            }
        }
        private static async Task<(string Command, string Response, string Result)> ExecuteSBDWTAsync(Stream stream, string command, CancellationToken token)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await stream.WriteAsync(bytes, 0, bytes.Length, token);
            while (true)
            {
                var cmd = await stream.ReadToAsync("\r", token);
                if (cmd == command)
                {
                    var next = await stream.ReadByteAsync(token);
                    if (IsCarriageReturn(next))
                    {
                        await stream.ReadToAsync("\n", token);
                        var response = await stream.ReadToAsync("\r\n", token);
                        return (cmd, response, string.Empty);
                    }
                    else if (IsAscii(next))
                    {
                        var response = ((char)next).ToString();
                        response += await stream.ReadToAsync("\r\n", token);
                        return (cmd, response, string.Empty);
                    }
                }
                if (cmd != "126")
                {
                    await stream.ReadToAsync("\n", token);
                    cmd = await stream.ReadToAsync("\r\n", token);
                }
                if (cmd is "126" or "SBDRING")
                {

                }
            }
        }
        private static async Task<(string Command, string Response, string Result)> ExecuteSBDWBAsync(Stream stream, string command, CancellationToken token)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await stream.WriteAsync(bytes, 0, bytes.Length, token);
            while (true)
            {
                var cmd = await stream.ReadToAsync("\r", token);
                if (cmd == command)
                {
                    var next = await stream.ReadByteAsync(token);
                    if (IsCarriageReturn(next))
                    {
                        await stream.ReadToAsync("\n", token);
                        var response = await stream.ReadToAsync("\r\n", token);
                        return (cmd, response, string.Empty);
                    }
                    else if (IsAscii(next))
                    {
                        var response = ((char)next).ToString();
                        response += await stream.ReadToAsync("\r\n", token);
                        return (cmd, response, string.Empty);
                    }
                }
                if (cmd != "126")
                {
                    await stream.ReadToAsync("\n", token);
                    cmd = await stream.ReadToAsync("\r\n", token);
                }
                if (cmd is "126" or "SBDRING")
                {

                }
            }
        }
        private static async Task<(string Command, string Response, string Result)> ExecuteATAndVAsync(Stream stream, string command, CancellationToken token)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await stream.WriteAsync(bytes, 0, bytes.Length, token);
            while (true)
            {
                var result = string.Empty;
                var response = string.Empty;
                var cmd = await stream.ReadToAsync("\r", token);
                if (cmd == command)
                {
                    var next = await stream.ReadByteAsync(token);
                    if (IsCarriageReturn(next))
                    {
                        int i = 0;
                        int verboseLineCount = 10;
                        await stream.ReadToAsync("\n", token);
                        while (true)
                        {
                            result = await stream.ReadToAsync("\r\n", token);
                            if (i == verboseLineCount)
                            {
                                break;
                            }
                            await stream.ReadToAsync("\n", token);
                            response += result;
                            i++;
                        }
                    }
                    else if (IsAscii(next))
                    {
                        int i = 0;
                        int numericLineCount = 9;
                        response += (char)next;
                        while (true)
                        {
                            result = await stream.ReadToAsync("\r\n", token);
                            if (i == numericLineCount)
                            {
                                result = await stream.ReadToAsync("\r", token);
                                break;
                            }
                            response += result;
                            i++;
                        }
                    }
                    return (cmd, response, result);
                }
                if (cmd != "126")
                {
                    await stream.ReadToAsync("\n", token);
                    cmd = await stream.ReadToAsync("\r\n", token);
                }
                if (cmd is "126" or "SBDRING")
                {

                }
            }
        }
        private static async Task<(string Command, string Response, string Result)> ExecuteGMRAsync(Stream stream, string command, CancellationToken token)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await stream.WriteAsync(bytes, 0, bytes.Length, token);
            while (true)
            {
                var result = string.Empty;
                var response = string.Empty;
                var cmd = await stream.ReadToAsync("\r", token);
                if (cmd == command)
                {
                    var next = await stream.ReadByteAsync(token);
                    if (IsCarriageReturn(next))
                    {
                        int i = 0;
                        int verboseLineCount = 7;
                        await stream.ReadToAsync("\n", token);
                        while (true)
                        {
                            result = await stream.ReadToAsync("\r\n", token);
                            if (i == verboseLineCount)
                            {
                                break;
                            }
                            await stream.ReadToAsync("\n", token);
                            response += result;
                            i++;
                        }
                    }
                    else if (IsAscii(next))
                    {
                        int i = 0;
                        int numericLineCount = 6;
                        response += (char)next;
                        while (true)
                        {
                            result = await stream.ReadToAsync("\r\n", token);
                            if (i == numericLineCount)
                            {
                                result = await stream.ReadToAsync("\r", token);
                                break;
                            }
                            response += result;
                            i++;
                        }
                    }
                    return (cmd, response, result);
                }
                if (cmd != "126")
                {
                    await stream.ReadToAsync("\n", token);
                    cmd = await stream.ReadToAsync("\r\n", token);
                }
                if (cmd is "126" or "SBDRING")
                {

                }
            }
        }
        private static async Task<(string Command, string Response, string Result)> ExecuteCGMRAsync(Stream stream, string command, CancellationToken token)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await stream.WriteAsync(bytes, 0, bytes.Length, token);
            while (true)
            {
                var result = string.Empty;
                var response = string.Empty;
                var cmd = await stream.ReadToAsync("\r", token);
                if (cmd == command)
                {
                    var next = await stream.ReadByteAsync(token);
                    if (IsCarriageReturn(next))
                    {
                        int i = 0;
                        int verboseLineCount = 7;
                        await stream.ReadToAsync("\n", token);
                        while (true)
                        {
                            result = await stream.ReadToAsync("\r\n", token);
                            if (i == verboseLineCount)
                            {
                                break;
                            }
                            await stream.ReadToAsync("\n", token);
                            response += result;
                            i++;
                        }
                    }
                    else if (IsAscii(next))
                    {
                        int i = 0;
                        int numericLineCount = 6;
                        response += (char)next;
                        while (true)
                        {
                            result = await stream.ReadToAsync("\r\n", token);
                            if (i == numericLineCount)
                            {
                                result = await stream.ReadToAsync("\r", token);
                                break;
                            }
                            response += result;
                            i++;
                        }
                    }
                    return (cmd, response, result);
                }
                if (cmd != "126")
                {
                    await stream.ReadToAsync("\n", token);
                    cmd = await stream.ReadToAsync("\r\n", token);
                }
                if (cmd is "126" or "SBDRING")
                {

                }
            }
        }
        private static async Task<(string Command, string Response, string Result)> ExecuteATPercentRAsync(Stream stream, string command, CancellationToken token)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await stream.WriteAsync(bytes, 0, bytes.Length, token);
            while (true)
            {
                var result = string.Empty;
                var response = string.Empty;
                var cmd = await stream.ReadToAsync("\r", token);
                if (cmd == command)
                {
                    var next = await stream.ReadByteAsync(token);
                    if (IsCarriageReturn(next))
                    {                       
                        int i = 0;
                        int verboseLineCount = 66;
                        await stream.ReadToAsync("\n", token);
                        while (true)
                        {
                            result = await stream.ReadToAsync("\r\n", token);
                            if (i == verboseLineCount)
                            {
                                break;
                            }
                            await stream.ReadToAsync("\n", token);
                            response += result;
                            i++;
                        }
                    }
                    else if (IsAscii(next))
                    {
                        int i = 0;
                        int numericLineCount = 65;
                        response += (char)next;
                        while (true)
                        {
                            result = await stream.ReadToAsync("\r\n", token);
                            if (i == numericLineCount)
                            {
                                result = await stream.ReadToAsync("\r", token);
                                break;
                            }
                            response += result;
                            i++;
                        }
                    }
                    return (cmd, response, result);
                }
                if (cmd != "126")
                {
                    await stream.ReadToAsync("\n", token);
                    cmd = await stream.ReadToAsync("\r\n", token);
                }
                if (cmd is "126" or "SBDRING")
                {

                }
            }
        }
        private static async Task<(string Command, string Response, string Result)> ExecuteResponseWithPayloadAsync(Stream stream, string command, CancellationToken token)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await stream.WriteAsync(bytes, 0, bytes.Length, token);
            while (true)
            {
                var cmd = await stream.ReadToAsync("\r", token);
                if (cmd == command)
                {
                    var next = await stream.ReadByteAsync(token);
                    if (IsCarriageReturn(next))
                    {
                        await stream.ReadToAsync("\n", token);
                        var response = await stream.ReadToAsync("\r\n\r\n", token);
                        var result = await stream.ReadToAsync("\r\n", token);
                        return (cmd, response, result);
                    }
                    else if (IsAscii(next))
                    {
                        var response = ((char)next).ToString();
                        response += await stream.ReadToAsync("\r\n", token);
                        var result = await stream.ReadToAsync("\r", token);
                        return (cmd, response, result);
                    }
                }
                if (cmd != "126")
                {
                    await stream.ReadToAsync("\n", token);
                    cmd = await stream.ReadToAsync("\r\n", token);
                }
                if (cmd is "126" or "SBDRING")
                {

                }
            }
        }
        private static async Task<(string Command, string Response, string Result)> ExecuteResponseWithoutPayloadAsync(Stream stream, string command, CancellationToken token)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await stream.WriteAsync(bytes, 0, bytes.Length, token);
            while (true)
            {
                var cmd = await stream.ReadToAsync("\r", token);
                if (cmd == command)
                {
                    var next = await stream.ReadByteAsync(token);
                    if (IsCarriageReturn(next))
                    {
                        await stream.ReadToAsync("\n", token);
                        var result = await stream.ReadToAsync("\r\n", token);
                        return (cmd, string.Empty, result);
                    }
                    else if (IsAscii(next))
                    {
                        var result = ((char)next).ToString();
                        result += await stream.ReadToAsync("\r", token);
                        return (cmd, string.Empty, result);
                    }
                }
                if (cmd != "126")
                {
                    await stream.ReadToAsync("\n", token);
                    cmd = await stream.ReadToAsync("\r\n", token);
                }
                if (cmd is "126" or "SBDRING")
                {

                }
            }
        }
        private static string NormalizeCommand(string command)
        {
            if (command.StartsWith("AT+SBDWT=")) return "AT+SBDWT=";
            else if (command.StartsWith("AT+SBDWB=")) return "AT+SBDWB=";
            return command;
        }
        private static byte[] CalculateChecksum(string payload)
        {
            var bytes = Encoding.ASCII.GetBytes(payload);
            uint sum = 0;
            for (int i = 0; i < bytes.Length; i++)
            {
                sum += bytes[i];
            }
            ushort cks = (ushort)(sum & 0xFFFF);
            return new[] { (byte)(cks >> 8), (byte)(cks & 0xFF) };
        }
        private static bool IsAscii(int c) => (uint)c <= '\x007f';
        private static bool IsCarriageReturn(int c) => (uint)c == '\x000d';
        #endregion

        #region idisposable
        private int _isDisposed;
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref _isDisposed, 1, 0) == 0)
            {
                if (SemaphoreSlim.Wait(0))
                {
                    try
                    {
                        CompleteLifecycleSignalsCooperatively();
                    }
                    finally
                    {
                        SemaphoreSlim.Release();
                    }
                }
                else
                {
                    CancelLifecycleSignalsBestEffort();
                }
                SerialPort?.Dispose();
                SemaphoreSlim.Dispose();
            }
        }
        #endregion
    }
}