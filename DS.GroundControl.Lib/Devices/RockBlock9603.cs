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
        private static IReadOnlyDictionary<string, Func<Stream, string, Task<(string Command, string Response, string Result)>>> Commands { get; } =
            new Dictionary<string, Func<Stream, string, Task<(string Command, string Response, string Result)>>>()
            {
                { "AT+CCLK?", ExecuteCCLKCurrentSettingsAsync },
                { "AT+SBDRB", ExecuteSBDRBAsync },
                { "AT+SBDRT", ExecuteSBDRTAsync },
                { "AT+SBDWT", ExecuteSBDWTAsync },
                { "AT+SBDWB=", ExecuteSBDWBAsync},
                { "AT&V", ExecuteATAndVAsync },
                { "AT+GMR", ExecuteGMRAsync },
                { "AT+CGMR", ExecuteCGMRAsync },
                { "AT%R", ExecuteATPercentRAsync },               
                { "AT+CGMI", ExecuteResponseWithPayloadAsync },
                { "AT+CGMM", ExecuteResponseWithPayloadAsync },
                { "AT+CGSN", ExecuteResponseWithPayloadAsync },
                { "AT+CIER=?", ExecuteResponseWithPayloadAsync },
                { "AT+CIER?", ExecuteResponseWithPayloadAsync },
                { "AT+CRIS", ExecuteResponseWithPayloadAsync },
                { "AT+CRISX", ExecuteResponseWithPayloadAsync },
                { "AT+CSQ", ExecuteResponseWithPayloadAsync },
                { "AT+CSQ=?", ExecuteResponseWithPayloadAsync },
                { "AT+CSQF", ExecuteResponseWithPayloadAsync },
                { "AT+CULK?", ExecuteResponseWithPayloadAsync },
                { "AT+GMI", ExecuteResponseWithPayloadAsync },
                { "AT+GMM", ExecuteResponseWithPayloadAsync },
                { "AT+GSN", ExecuteResponseWithPayloadAsync },
                { "AT+IPR=?", ExecuteResponseWithPayloadAsync },
                { "AT+IPR?", ExecuteResponseWithPayloadAsync },
                { "AT+SBDLOE", ExecuteResponseWithPayloadAsync },
                { "AT+SBDAREG=?", ExecuteResponseWithPayloadAsync },
                { "AT+SBDAREG?", ExecuteResponseWithPayloadAsync },
                { "AT+SBDC", ExecuteResponseWithPayloadAsync },
                { "AT+SBDD0", ExecuteResponseWithPayloadAsync },
                { "AT+SBDD1", ExecuteResponseWithPayloadAsync },
                { "AT+SBDD2", ExecuteResponseWithPayloadAsync },
                { "AT+SBDDSC?", ExecuteResponseWithPayloadAsync },
                { "AT+SBDGW", ExecuteResponseWithPayloadAsync },
                { "AT+SBDGWN", ExecuteResponseWithPayloadAsync },
                { "AT+SBDI", ExecuteResponseWithPayloadAsync },
                { "AT+SBDIX", ExecuteResponseWithPayloadAsync },
                { "AT+SBDIXA", ExecuteResponseWithPayloadAsync },
                { "AT+SBDMTA?", ExecuteResponseWithPayloadAsync },
                { "AT+SBDMTA=?", ExecuteResponseWithPayloadAsync },
                { "AT+SBDREG?", ExecuteResponseWithPayloadAsync },
                { "AT+SBDS", ExecuteResponseWithPayloadAsync },
                { "AT+SBDST?", ExecuteResponseWithPayloadAsync },
                { "AT+SBDSX", ExecuteResponseWithPayloadAsync },
                { "AT+SBDTC", ExecuteResponseWithPayloadAsync },
                { "AT-MSGEOS", ExecuteResponseWithPayloadAsync },
                { "AT-MSGEO", ExecuteResponseWithPayloadAsync },
                { "AT-MSSTM", ExecuteResponseWithPayloadAsync },
                { "ATI0", ExecuteResponseWithPayloadAsync },
                { "ATI1", ExecuteResponseWithPayloadAsync },
                { "ATI2", ExecuteResponseWithPayloadAsync },
                { "ATI3", ExecuteResponseWithPayloadAsync },
                { "ATI4", ExecuteResponseWithPayloadAsync },
                { "ATI5", ExecuteResponseWithPayloadAsync },
                { "ATI6", ExecuteResponseWithPayloadAsync },
                { "ATI7", ExecuteResponseWithPayloadAsync },
                { "AT+SBDWT=", ExecuteResponseWithoutPayloadAsync },                
                { "AT&Y0", ExecuteResponseWithoutPayloadAsync },
                { "AT&K0", ExecuteResponseWithoutPayloadAsync },
                { "AT&K3", ExecuteResponseWithoutPayloadAsync },
                { "AT*R1", ExecuteResponseWithoutPayloadAsync },
                { "AT*F", ExecuteResponseWithoutPayloadAsync },
                { "AT+SBDMTA=0", ExecuteResponseWithoutPayloadAsync },
                { "AT+SBDMTA=1", ExecuteResponseWithoutPayloadAsync },
                { "ATE1", ExecuteResponseWithoutPayloadAsync },
                { "ATQ0", ExecuteResponseWithoutPayloadAsync },
                { "AT", ExecuteResponseWithoutPayloadAsync },
                { "ATV1", ExecuteResponseWithoutPayloadAsync },
                { "ATV0", ExecuteResponseWithoutPayloadAsync }
            };

        public static async Task<SerialPort> TryLocateAndConnectAsync()
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
            var cmd = command;
            if (command.StartsWith("AT+SBDWT="))
            {
                cmd = "AT+SBDWT=";
            }
            else if (command.StartsWith("AT+SBDWB="))
            {
                cmd = "AT+SBDWB=";
            }
            var func = Commands[cmd];
            return await func(stream, command);
        }
        private static async Task<(string Command, string Response, string Result)> ExecuteReadyStateTextCommandAsync(Stream stream, string command)
        {
            using var source = new CancellationTokenSource(TimeSpan.FromMinutes(1));

            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await stream.WriteAsync(bytes, 0, bytes.Length, source.Token);

            var cmd = await stream.ReadToAsync("\r", source.Token);
            if (command.Length == cmd.Length) // ATV1 + AT+SBDWT + message
            {
                cmd = cmd.Substring(0, cmd.Length - 1);
                await stream.ReadToAsync("\n", source.Token);
                var response = await stream.ReadToAsync("\r\n", source.Token);
                await stream.ReadToAsync("\r\n", source.Token);
                var result = await stream.ReadToAsync("\r\n", source.Token);
                return (cmd, response, result);
            }
            else // ATV0 + AT+SBDWT + message 
            {
                var response = cmd[cmd.Length - 1].ToString();
                cmd = cmd.Substring(0, cmd.Length - 1);
                await stream.ReadToAsync("\n", source.Token);
                var result = await stream.ReadToAsync("\r", source.Token);
                return (cmd, response, result);
            }
        }
        private static async Task<(string Command, string Response, string Result)> ExecuteReadyStateBinaryCommandAsync(Stream stream, string command)
        {
            using var source = new CancellationTokenSource(TimeSpan.FromMinutes(1));

            var cks = CalculateChecksum(command);
            var bytes = Encoding.ASCII.GetBytes(command).Concat(cks).ToArray();
            await stream.WriteAsync(bytes, 0, bytes.Length, source.Token);

            var next = await stream.ReadByteAsync(source.Token);
            if (IsCarriageReturn(next)) // ATV1 + AT+SBDWB= + message
            {
                var response = await stream.ReadToAsync("\r\n", source.Token);
                await stream.ReadToAsync("\r\n", source.Token);
                response = response[^1].ToString();
                var result = await stream.ReadToAsync("\r\n", source.Token);
                return (string.Empty, response, result);
            }
            else // ATV0 + AT+SBDWB= + message
            {
                var response = Convert.ToChar(next).ToString();
                await stream.ReadToAsync("\r\n", source.Token);
                var result = await stream.ReadToAsync("\r", source.Token);           
                return (string.Empty, response, result);
            }
        }
        private static async Task<(string Command, string Response, string Result)> ExecuteCCLKCurrentSettingsAsync(Stream stream, string command)
        {
            using var source = new CancellationTokenSource(TimeSpan.FromMinutes(1));

            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await stream.WriteAsync(bytes, 0, bytes.Length, source.Token);

            while (true)
            {
                var result = string.Empty;
                var response = string.Empty;
                var cmd = await stream.ReadToAsync("\r", source.Token);
                if (cmd == command)
                {
                    var next = await stream.ReadByteAsync(source.Token);
                    if (IsCarriageReturn(next))
                    {
                        await stream.ReadToAsync("\n", source.Token);
                        response = await stream.ReadToAsync("\n\r\n", source.Token);
                        await stream.ReadToAsync("\r\n", source.Token);
                        result = await stream.ReadToAsync("\r\n", source.Token);
                    }
                    else if (IsAscii(next))
                    {
                        response += Convert.ToChar(next);
                        response += await stream.ReadToAsync("\n\r\n", source.Token);
                        result = await stream.ReadToAsync("\r", source.Token);
                    }
                }
                else
                {
                    if (cmd != "126")
                    {
                        await stream.ReadToAsync("\n", source.Token);
                        await stream.ReadToAsync("\r\n", source.Token);
                    }
                    continue;
                }
                return (cmd, response, result);
            }
        }
        private static async Task<(string Command, string Response, string Result)> ExecuteSBDRBAsync(Stream stream, string command)
        {
            using var source = new CancellationTokenSource(TimeSpan.FromMinutes(1));

            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await stream.WriteAsync(bytes, 0, bytes.Length, source.Token);

            while (true)
            {
                var result = string.Empty;
                var response = string.Empty;
                var cmd = await stream.ReadToAsync("\r", source.Token);
                if (cmd == command)
                {
                    var len = new byte[2];
                    await stream.ReadExactlyAsync(len, 0, 2, source.Token);
                    var msg = new byte[(len[0] << 8) | len[1]];
                    await stream.ReadExactlyAsync(msg, 0, msg.Length, source.Token);
                    var cks = new byte[2];
                    await stream.ReadExactlyAsync(cks, 0, 2, source.Token);

                    var next = await stream.ReadByteAsync(source.Token);
                    if (IsCarriageReturn(next))
                    {
                        await stream.ReadToAsync("\n", source.Token);
                        result = await stream.ReadToAsync("\r\n", source.Token);
                        response = Encoding.ASCII.GetString(len.Concat(msg).Concat(cks).ToArray());
                    }
                    else if (IsAscii(next))
                    {
                        result += Convert.ToChar(next);
                        result += await stream.ReadToAsync("\r", source.Token);
                        response = Encoding.ASCII.GetString(len.Concat(msg).Concat(cks).ToArray());
                    }
                }
                else
                {
                    if (cmd != "126")
                    {
                        await stream.ReadToAsync("\n", source.Token);
                        await stream.ReadToAsync("\r\n", source.Token);
                    }
                    continue;
                }
                return (cmd, response, result);
            }
        }
        private static async Task<(string Command, string Response, string Result)> ExecuteSBDRTAsync(Stream stream, string command)
        {
            using var source = new CancellationTokenSource(TimeSpan.FromMinutes(1));

            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await stream.WriteAsync(bytes, 0, bytes.Length, source.Token);

            while (true)
            {
                var result = string.Empty;
                var response = string.Empty;
                var cmd = await stream.ReadToAsync("\r", source.Token);
                if (cmd == command)
                {
                    var next = await stream.ReadByteAsync(source.Token);
                    if (IsCarriageReturn(next))
                    {
                        await stream.ReadToAsync("\n", source.Token);
                        response += await stream.ReadToAsync("\r\n", source.Token);
                        response += await stream.ReadToAsync("\r\n", source.Token);
                        result = await stream.ReadToAsync("\r\n", source.Token);
                    }
                    else if (IsAscii(next))
                    {
                        response += Convert.ToChar(next);
                        response += await stream.ReadToAsync("\r\n", source.Token);
                        response += await stream.ReadToAsync("\r", source.Token);
                        result = response[^1].ToString();
                        response = response.Remove(response.Length - 1);
                    }
                }
                else
                {
                    if (cmd != "126")
                    {
                        await stream.ReadToAsync("\n", source.Token);
                        await stream.ReadToAsync("\r\n", source.Token);
                    }
                    continue;
                }
                return (cmd, response, result);
            }
        }
        private static async Task<(string Command, string Response, string Result)> ExecuteSBDWTAsync(Stream stream, string command)
        {
            using var source = new CancellationTokenSource(TimeSpan.FromMinutes(1));

            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await stream.WriteAsync(bytes, 0, bytes.Length, source.Token);

            while (true)
            {
                var result = string.Empty;
                var response = string.Empty;
                var cmd = await stream.ReadToAsync("\r", source.Token);
                if (cmd == command)
                {
                    var next = await stream.ReadByteAsync(source.Token);
                    if (IsCarriageReturn(next))
                    {
                        await stream.ReadToAsync("\n", source.Token);
                        response = await stream.ReadToAsync("\r\n", source.Token);
                    }
                    else if (IsAscii(next))
                    {
                        response += Convert.ToChar(next);
                        response += await stream.ReadToAsync("\r\n", source.Token);
                    }
                }
                else
                {
                    if (cmd != "126")
                    {
                        await stream.ReadToAsync("\n", source.Token);
                        await stream.ReadToAsync("\r\n", source.Token);
                    }
                    continue;
                }
                return (cmd, response, result);
            }
        }
        private static async Task<(string Command, string Response, string Result)> ExecuteSBDWBAsync(Stream stream, string command)
        {
            using var source = new CancellationTokenSource(TimeSpan.FromMinutes(1));

            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await stream.WriteAsync(bytes, 0, bytes.Length, source.Token);

            while (true)
            {
                var result = string.Empty;
                var response = string.Empty;
                var cmd = await stream.ReadToAsync("\r", source.Token);
                if (cmd == command)
                {
                    var next = await stream.ReadByteAsync(source.Token);
                    if (IsCarriageReturn(next))
                    {
                        await stream.ReadToAsync("\n", source.Token);
                        response = await stream.ReadToAsync("\r\n", source.Token);
                    }
                    else if (IsAscii(next))
                    {
                        response += Convert.ToChar(next);
                        response += await stream.ReadToAsync("\r\n", source.Token);
                    }
                }
                else
                {
                    if (cmd != "126")
                    {
                        await stream.ReadToAsync("\n", source.Token);
                        await stream.ReadToAsync("\r\n", source.Token);
                    }
                    continue;
                }
                return (cmd, response, result);
            }
        }
        private static async Task<(string Command, string Response, string Result)> ExecuteATAndVAsync(Stream stream, string command)
        {
            using var source = new CancellationTokenSource(TimeSpan.FromMinutes(1));

            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await stream.WriteAsync(bytes, 0, bytes.Length, source.Token);

            while (true)
            {
                var result = string.Empty;
                var response = string.Empty;
                var cmd = await stream.ReadToAsync("\r", source.Token);
                if (cmd == command)
                {
                    var next = await stream.ReadByteAsync(source.Token);
                    if (IsCarriageReturn(next))
                    {
                        await stream.ReadToAsync("\n", source.Token);
                        int i = 0;
                        while (true)
                        {
                            result = await stream.ReadToAsync("\r\n", source.Token);
                            if (i == 10)
                            {
                                break;
                            }
                            await stream.ReadToAsync("\n", source.Token);
                            response += result;
                            i++;
                        }
                    }
                    else if (IsAscii(next))
                    {
                        response += Convert.ToChar(next);
                        int i = 0;
                        while (true)
                        {
                            result = await stream.ReadToAsync("\r\n", source.Token);
                            if (i == 9)
                            {
                                result = await stream.ReadToAsync("\r", source.Token);
                                break;
                            }
                            response += result;
                            i++;
                        }
                    }
                }
                else
                {
                    if (cmd != "126")
                    {
                        await stream.ReadToAsync("\n", source.Token);
                        await stream.ReadToAsync("\r\n", source.Token);
                    }
                    continue;
                }
                return (cmd, response, result);
            }
        }
        private static async Task<(string Command, string Response, string Result)> ExecuteGMRAsync(Stream stream, string command)
        {
            using var source = new CancellationTokenSource(TimeSpan.FromMinutes(1));

            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await stream.WriteAsync(bytes, 0, bytes.Length, source.Token);

            while (true)
            {
                var result = string.Empty;
                var response = string.Empty;
                var cmd = await stream.ReadToAsync("\r", source.Token);
                if (cmd == command)
                {
                    var next = await stream.ReadByteAsync(source.Token);
                    if (IsCarriageReturn(next))
                    {
                        await stream.ReadToAsync("\n", source.Token);
                        int i = 0;
                        while (true)
                        {
                            result = await stream.ReadToAsync("\r\n", source.Token);
                            if (i == 7)
                            {
                                break;
                            }
                            await stream.ReadToAsync("\n", source.Token);
                            response += result;
                            i++;
                        }
                    }
                    else if (IsAscii(next))
                    {
                        response += Convert.ToChar(next);
                        int i = 0;
                        while (true)
                        {
                            result = await stream.ReadToAsync("\r\n", source.Token);
                            if (i == 6)
                            {
                                result = await stream.ReadToAsync("\r", source.Token);
                                break;
                            }
                            response += result;
                            i++;
                        }
                    }
                }
                else
                {
                    if (cmd != "126")
                    {
                        await stream.ReadToAsync("\n", source.Token);
                        await stream.ReadToAsync("\r\n", source.Token);
                    }
                    continue;
                }
                return (cmd, response, result);
            }
        }
        private static async Task<(string Command, string Response, string Result)> ExecuteCGMRAsync(Stream stream, string command)
        {
            using var source = new CancellationTokenSource(TimeSpan.FromMinutes(1));

            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await stream.WriteAsync(bytes, 0, bytes.Length, source.Token);

            while (true)
            {
                var result = string.Empty;
                var response = string.Empty;
                var cmd = await stream.ReadToAsync("\r", source.Token);
                if (cmd == command)
                {
                    var next = await stream.ReadByteAsync(source.Token);
                    if (IsCarriageReturn(next))
                    {
                        await stream.ReadToAsync("\n", source.Token);
                        int i = 0;
                        while (true)
                        {
                            result = await stream.ReadToAsync("\r\n", source.Token);
                            if (i == 7)
                            {
                                break;
                            }
                            await stream.ReadToAsync("\n", source.Token);
                            response += result;
                            i++;
                        }
                    }
                    else if (IsAscii(next))
                    {
                        response += Convert.ToChar(next);
                        int i = 0;
                        while (true)
                        {
                            result = await stream.ReadToAsync("\r\n", source.Token);
                            if (i == 6)
                            {
                                result = await stream.ReadToAsync("\r", source.Token);
                                break;
                            }
                            response += result;
                            i++;
                        }
                    }
                }
                else
                {
                    if (cmd != "126")
                    {
                        await stream.ReadToAsync("\n", source.Token);
                        await stream.ReadToAsync("\r\n", source.Token);
                    }
                    continue;
                }
                return (cmd, response, result);
            }
        }
        private static async Task<(string Command, string Response, string Result)> ExecuteATPercentRAsync(Stream stream, string command)
        {
            using var source = new CancellationTokenSource(TimeSpan.FromMinutes(1));

            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await stream.WriteAsync(bytes, 0, bytes.Length, source.Token);

            while (true)
            {
                var result = string.Empty;
                var response = string.Empty;
                var cmd = await stream.ReadToAsync("\r", source.Token);
                if (cmd == command)
                {
                    var next = await stream.ReadByteAsync(source.Token);
                    if (IsCarriageReturn(next))
                    {
                        await stream.ReadToAsync("\n", source.Token);
                        int i = 0;
                        while (true)
                        {
                            result = await stream.ReadToAsync("\r\n", source.Token);
                            if (i == 66)
                            {
                                break;
                            }
                            await stream.ReadToAsync("\n", source.Token);
                            response += result;
                            i++;
                        }
                    }
                    else if (IsAscii(next))
                    {
                        response += Convert.ToChar(next);
                        int i = 0;
                        while (true)
                        {
                            result = await stream.ReadToAsync("\r\n", source.Token);
                            if (i == 65)
                            {
                                result = await stream.ReadToAsync("\r", source.Token);
                                break;
                            }
                            response += result;
                            i++;
                        }
                    }
                }
                else
                {
                    if (cmd != "126")
                    {
                        await stream.ReadToAsync("\n", source.Token);
                        await stream.ReadToAsync("\r\n", source.Token);
                    }
                    continue;
                }
                return (cmd, response, result);
            }
        }
        private static async Task<(string Command, string Response, string Result)> ExecuteResponseWithPayloadAsync(Stream stream, string command)
        {
            using var source = new CancellationTokenSource(TimeSpan.FromMinutes(1));

            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await stream.WriteAsync(bytes, 0, bytes.Length, source.Token);

            while (true)
            {
                var result = string.Empty;
                var response = string.Empty;
                var cmd = await stream.ReadToAsync("\r", source.Token);
                if (cmd == command)
                {
                    var next = await stream.ReadByteAsync(source.Token);
                    if (IsCarriageReturn(next))
                    {
                        await stream.ReadToAsync("\n", source.Token);
                        response = await stream.ReadToAsync("\r\n\r\n", source.Token);
                        result = await stream.ReadToAsync("\r\n", source.Token);
                    }
                    else if (IsAscii(next))
                    {
                        response += Convert.ToChar(next);
                        response += await stream.ReadToAsync("\r\n", source.Token);
                        result = await stream.ReadToAsync("\r", source.Token);
                    }
                }
                else
                {
                    if (cmd != "126")
                    {
                        await stream.ReadToAsync("\n", source.Token);
                        await stream.ReadToAsync("\r\n", source.Token);
                    }
                    continue;
                }
                return (cmd, response, result);
            }
        }
        private static async Task<(string Command, string Response, string Result)> ExecuteResponseWithoutPayloadAsync(Stream stream, string command)
        {
            using var source = new CancellationTokenSource(TimeSpan.FromMinutes(1));

            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await stream.WriteAsync(bytes, 0, bytes.Length, source.Token);

            while (true)
            {
                var result = string.Empty;
                var response = string.Empty;
                var cmd = await stream.ReadToAsync("\r", source.Token);
                if (cmd == command)
                {
                    var next = await stream.ReadByteAsync(source.Token);
                    if (IsCarriageReturn(next))
                    {
                        await stream.ReadToAsync("\n", source.Token);
                        result = await stream.ReadToAsync("\r\n", source.Token);
                    }
                    else if (IsAscii(next))
                    {
                        result += Convert.ToChar(next);
                        result += await stream.ReadToAsync("\r", source.Token);
                    }
                }
                else
                {
                    if (cmd != "126")
                    {
                        await stream.ReadToAsync("\n", source.Token);
                        await stream.ReadToAsync("\r\n", source.Token);
                    }
                    continue;
                }
                return (cmd, response, result);
            }
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