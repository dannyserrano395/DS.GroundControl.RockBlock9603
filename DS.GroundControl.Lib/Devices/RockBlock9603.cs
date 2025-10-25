using System.Text;
using System.IO.Ports;
using DS.GroundControl.Lib.Exceptions;
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

        public async Task ConnectAsync(int baudRate, int dataBits, Parity parity, StopBits stopBits)
        {
            await SemaphoreSlim.WaitAsync();
            try
            {
                ThrowIfAnyTransitionCompleted();
                foreach (var name in SerialPort.GetPortNames())
                {
                    try
                    {
                        SerialPort = new SerialPort()
                        {
                            PortName = name,
                            BaudRate = baudRate,
                            DataBits = dataBits,
                            Parity = parity,
                            StopBits = stopBits
                        };
                        SerialPort.Open();
                        await ValidateConnectionAsync();
                        TryTransitionToConnected();
                        break;
                    }
                    catch 
                    {
                        SerialPort?.Dispose();
                    }
                }              
                ThrowIfNotConnected();
            }
            catch
            {
                TryTransitionToFaulted();
                SerialPort?.Dispose();
                throw;
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }
        public async Task ConnectAsync(string portName, int baudRate, int dataBits, Parity parity, StopBits stopBits)
        {
            await SemaphoreSlim.WaitAsync();
            try
            {
                ThrowIfAnyTransitionCompleted();
                SerialPort = new SerialPort()
                {
                    PortName = portName,
                    BaudRate = baudRate,
                    DataBits = dataBits,
                    Parity = parity,
                    StopBits = stopBits
                };
                SerialPort.Open();
                await ValidateConnectionAsync();
                TryTransitionToConnected();
                ThrowIfNotConnected();
            }
            catch
            {
                TryTransitionToFaulted();
                SerialPort?.Dispose();
                throw;
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }
        public async Task<(string Command, string Response, string Result)> ExecuteCommandAsync(string command)
        {
            await SemaphoreSlim.WaitAsync();
            try
            {
                ThrowIfNotConnected();
                var (func, timeout) = CommandMap[NormalizeCommand(command)];
                var output = await func(SerialPort.BaseStream, command).WaitAsync(timeout);
                return output;
            }
            catch
            {
                if (IsConnected())
                {
                    TryTransitionToFaulted();
                    SerialPort?.Dispose();
                }
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
                ThrowIfNotConnected();
                var output = await ReadyStateTextCommandAsync(SerialPort.BaseStream, command).WaitAsync(TimeSpan.FromSeconds(3));
                return output;
            }
            catch
            {
                if (IsConnected())
                {
                    TryTransitionToFaulted();
                    SerialPort?.Dispose();
                }
                throw;
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }
        public async Task<(string Command, string Response, string Result)> ExecuteReadyStateBase64CommandAsync(string command)
        {
            await SemaphoreSlim.WaitAsync();
            try
            {
                ThrowIfNotConnected();
                var output = await ReadyStateBase64CommandAsync(SerialPort.BaseStream, command).WaitAsync(TimeSpan.FromSeconds(3));
                return output;
            }
            catch
            {
                if (IsConnected())
                {
                    TryTransitionToFaulted();
                    SerialPort?.Dispose();
                }
                throw;
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }
        private async Task ValidateConnectionAsync()
        {
            try
            {
                var (func, timeout) = CommandMap[NormalizeCommand("AT")];
                var output = await func(SerialPort.BaseStream, "AT").WaitAsync(timeout);
                if (output is { Command: "AT", Response: "", Result: "OK" or "0" })
                {
                    return;
                }
            }
            catch { }
            throw new DeviceConnectionException();
        }
        private bool TryTransitionToConnected()
        {
            return !Faulted.IsCompleted && !Disconnected.IsCompleted && ConnectedSource.TrySetResult();
        }
        private bool TryTransitionToFaulted()
        {
            if (FaultedSource.TrySetResult())
            {
                TryTransitionToDisconnected();
                return true;
            }
            return false;
        }
        private bool TryTransitionToDisconnected()
        {
            return Connected.IsCompletedSuccessfully && DisconnectedSource.TrySetResult();
        }
        private void CancelIncompleteTransitions()
        {
            if (!Connected.IsCompleted) { ConnectedSource.TrySetCanceled(); }
            if (!Disconnected.IsCompleted)
            {
                if (!TryTransitionToDisconnected())
                {
                    DisconnectedSource.TrySetCanceled();
                }
            }
            if (!Faulted.IsCompleted) { FaultedSource.TrySetCanceled(); }
        }
        private void ThrowIfAnyTransitionCompleted()
        {
            if (Connected.IsCompleted || Faulted.IsCompleted || Disconnected.IsCompleted)
            {
                throw new DeviceConnectionException();
            }
        }
        private void ThrowIfNotConnected()
        {
            if (!IsConnected())
            {
                throw new DeviceConnectionException();
            }
        }
        private bool IsConnected()
        {
            return Connected.IsCompletedSuccessfully && !Faulted.IsCompleted && !Disconnected.IsCompleted;
        }  

        #region static
        private static IReadOnlyDictionary<string, (Func<Stream, string, Task<(string Command, string Response, string Result)>>, TimeSpan)> CommandMap { get; } =
            new Dictionary<string, (Func<Stream, string, Task<(string Command, string Response, string Result)>>, TimeSpan)>
        {
            { "AT+CCLK?",  (CCLKSettingsAsync, TimeSpan.FromSeconds(5)) },
            { "AT+SBDRB",  (ReadBinaryAsync, TimeSpan.FromSeconds(5)) },
            { "AT+SBDRT",  (ReadTextAsync, TimeSpan.FromSeconds(5)) },
            { "AT+SBDWT",  (WriteTextAsync, TimeSpan.FromSeconds(5)) },
            { "AT+SBDWB=", (WriteBinaryAsync, TimeSpan.FromSeconds(5)) },
            { "AT&V",      (AndVAsync, TimeSpan.FromSeconds(3)) },
            { "AT+GMR",    (GMRAsync, TimeSpan.FromSeconds(3)) },
            { "AT+CGMR",   (CGMRAsync, TimeSpan.FromSeconds(3)) },
            { "AT%R",      (PercentRAsync, TimeSpan.FromSeconds(10)) },
            { "AT+CGMI",   (ResponseWithPayloadAsync, TimeSpan.FromSeconds(3)) },
            { "AT+CGMM",   (ResponseWithPayloadAsync, TimeSpan.FromSeconds(3)) },
            { "AT+CGSN",   (ResponseWithPayloadAsync, TimeSpan.FromSeconds(3)) },
            { "AT+CIER=?", (ResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT+CIER?",  (ResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT+CRIS",   (ResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT+CRISX",  (ResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT+CSQ",    (ResponseWithPayloadAsync, TimeSpan.FromSeconds(3)) },
            { "AT+CSQ=?",  (ResponseWithPayloadAsync, TimeSpan.FromSeconds(3)) },
            { "AT+CSQF",   (ResponseWithPayloadAsync, TimeSpan.FromSeconds(3)) },
            { "AT+CULK?",  (ResponseWithPayloadAsync, TimeSpan.FromSeconds(3)) },
            { "AT+GMI",    (ResponseWithPayloadAsync, TimeSpan.FromSeconds(3)) },
            { "AT+GMM",    (ResponseWithPayloadAsync, TimeSpan.FromSeconds(3)) },
            { "AT+GSN",    (ResponseWithPayloadAsync, TimeSpan.FromSeconds(3)) },
            { "AT+IPR=?",  (ResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT+IPR?",   (ResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT+SBDLOE", (ResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT+SBDAREG=?", (ResponseWithPayloadAsync, TimeSpan.FromSeconds(3)) },
            { "AT+SBDAREG?",  (ResponseWithPayloadAsync, TimeSpan.FromSeconds(3)) },
            { "AT+SBDC",   (ResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT+SBDD0",  (ResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT+SBDD1",  (ResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT+SBDD2",  (ResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT+SBDDSC?", (ResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT+SBDGW",  (ResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT+SBDGWN", (ResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT+SBDI",   (ResponseWithPayloadAsync, TimeSpan.FromSeconds(75)) },
            { "AT+SBDIX",  (ResponseWithPayloadAsync, TimeSpan.FromSeconds(75)) },
            { "AT+SBDIXA", (ResponseWithPayloadAsync, TimeSpan.FromSeconds(90)) },
            { "AT+SBDMTA?", (ResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT+SBDMTA=?", (ResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT+SBDREG?", (ResponseWithPayloadAsync, TimeSpan.FromSeconds(3)) },
            { "AT+SBDS",  (ResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT+SBDST?", (ResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT+SBDSX",  (ResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT+SBDTC",  (ResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT-MSGEOS", (ResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT-MSGEO",  (ResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT-MSSTM",  (ResponseWithPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "ATI0",      (ResponseWithPayloadAsync, TimeSpan.FromSeconds(2)) },
            { "ATI1",      (ResponseWithPayloadAsync, TimeSpan.FromSeconds(2)) },
            { "ATI2",      (ResponseWithPayloadAsync, TimeSpan.FromSeconds(2)) },
            { "ATI3",      (ResponseWithPayloadAsync, TimeSpan.FromSeconds(2)) },
            { "ATI4",      (ResponseWithPayloadAsync, TimeSpan.FromSeconds(2)) },
            { "ATI5",      (ResponseWithPayloadAsync, TimeSpan.FromSeconds(2)) },
            { "ATI6",      (ResponseWithPayloadAsync, TimeSpan.FromSeconds(2)) },
            { "ATI7",      (ResponseWithPayloadAsync, TimeSpan.FromSeconds(2)) },
            { "AT+SBDWT=", (ResponseWithoutPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT&Y0",     (ResponseWithoutPayloadAsync, TimeSpan.FromSeconds(2)) },
            { "AT&K0",     (ResponseWithoutPayloadAsync, TimeSpan.FromSeconds(2)) },
            { "AT&K3",     (ResponseWithoutPayloadAsync, TimeSpan.FromSeconds(2)) },
            { "AT*R1",     (ResponseWithoutPayloadAsync, TimeSpan.FromSeconds(2)) },
            { "AT*F",      (ResponseWithoutPayloadAsync, TimeSpan.FromSeconds(2)) },
            { "AT+SBDMTA=0", (ResponseWithoutPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "AT+SBDMTA=1", (ResponseWithoutPayloadAsync, TimeSpan.FromSeconds(5)) },
            { "ATE1",      (ResponseWithoutPayloadAsync, TimeSpan.FromSeconds(2)) },
            { "ATQ0",      (ResponseWithoutPayloadAsync, TimeSpan.FromSeconds(2)) },
            { "AT",        (ResponseWithoutPayloadAsync, TimeSpan.FromSeconds(2)) },
            { "ATV1",      (ResponseWithoutPayloadAsync, TimeSpan.FromSeconds(2)) },
            { "ATV0",      (ResponseWithoutPayloadAsync, TimeSpan.FromSeconds(2)) }
        };

        private static async Task<(string Command, string Response, string Result)> ReadyStateTextCommandAsync(Stream stream, string command)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await stream.WriteAsync(bytes, 0, bytes.Length);

            var cmd = await stream.ReadToAsync("\r");
            if (cmd == command)
            {
                await stream.ReadToAsync("\n");
                var response = await stream.ReadToAsync("\r\n");
                await stream.ReadToAsync("\r\n");
                var result = await stream.ReadToAsync("\r\n");
                return (cmd, response, result);
            }
            else
            {
                var response = cmd[^1].ToString();
                cmd = cmd.Substring(0, cmd.Length - 1);
                await stream.ReadToAsync("\n");
                var result = await stream.ReadToAsync("\r");
                return (cmd, response, result);
            }
        }
        private static async Task<(string Command, string Response, string Result)> ReadyStateBase64CommandAsync(Stream stream, string command)
        {
            var base64 = Convert.FromBase64String(command);
            var cks = CalculateChecksum(base64);
            var bytes = base64.Concat(cks).ToArray();
            await stream.WriteAsync(bytes, 0, bytes.Length);

            var next = await stream.ReadByteAsync();
            if (IsCarriageReturn(next))
            {
                var response = await stream.ReadToAsync("\r\n");
                await stream.ReadToAsync("\r\n");
                response = response[^1].ToString();
                var result = await stream.ReadToAsync("\r\n");
                return (string.Empty, response, result);
            }
            else
            {
                var response = ((char)next).ToString();
                await stream.ReadToAsync("\r\n");
                var result = await stream.ReadToAsync("\r");
                return (string.Empty, response, result);
            }
        }
        private static async Task<(string Command, string Response, string Result)> ReadBinaryAsync(Stream stream, string command)
        {          
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await stream.WriteAsync(bytes, 0, bytes.Length);
            while (true)
            {
                var cmd = await stream.ReadToAsync("\r");
                if (cmd == command)
                {
                    var len = new byte[2];
                    await stream.ReadExactlyAsync(len, 0, 2);
                    var msg = new byte[(len[0] << 8) | len[1]];
                    await stream.ReadExactlyAsync(msg, 0, msg.Length);
                    var cks = new byte[2];
                    await stream.ReadExactlyAsync(cks, 0, 2);

                    var next = await stream.ReadByteAsync();
                    if (IsCarriageReturn(next))
                    {
                        await stream.ReadToAsync("\n");
                        var result = await stream.ReadToAsync("\r\n");
                        var response = Convert.ToBase64String(len.Concat(msg).Concat(cks).ToArray());
                        return (cmd, response, result);
                    }
                    else if (IsAscii(next))
                    {
                        var result = ((char)next).ToString();
                        result += await stream.ReadToAsync("\r");
                        var response = Convert.ToBase64String(len.Concat(msg).Concat(cks).ToArray());
                        return (cmd, response, result);
                    }
                }
                await UnsolicitedResultCodeAsync(stream, cmd);
            }
        }
        private static async Task<(string Command, string Response, string Result)> ReadTextAsync(Stream stream, string command)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await stream.WriteAsync(bytes, 0, bytes.Length);
            while (true)
            {
                var cmd = await stream.ReadToAsync("\r");
                if (cmd == command)
                {
                    var next = await stream.ReadByteAsync();
                    if (IsCarriageReturn(next))
                    {
                        await stream.ReadToAsync("\n");
                        var response = await stream.ReadToAsync("\r\n");
                        response += await stream.ReadToAsync("\r\n");
                        var result = await stream.ReadToAsync("\r\n");
                        return (cmd, response, result);
                    }
                    else if (IsAscii(next))
                    {
                        var response = ((char)next).ToString();
                        response += await stream.ReadToAsync("\r\n");
                        response += await stream.ReadToAsync("\r");
                        var result = response[^1].ToString();
                        response = response.Remove(response.Length - 1);
                        return (cmd, response, result);
                    }
                }
                await UnsolicitedResultCodeAsync(stream, cmd);
            }
        }
        private static async Task<(string Command, string Response, string Result)> WriteBinaryAsync(Stream stream, string command)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await stream.WriteAsync(bytes, 0, bytes.Length);
            while (true)
            {
                var cmd = await stream.ReadToAsync("\r");
                if (cmd == command)
                {
                    var next = await stream.ReadByteAsync();
                    if (IsCarriageReturn(next))
                    {
                        await stream.ReadToAsync("\n");
                        var response = await stream.ReadToAsync("\r\n");
                        return (cmd, response, string.Empty);
                    }
                    else if (IsAscii(next))
                    {
                        var response = ((char)next).ToString();
                        response += await stream.ReadToAsync("\r\n");
                        return (cmd, response, string.Empty);
                    }
                }
                await UnsolicitedResultCodeAsync(stream, cmd);
            }
        }
        private static async Task<(string Command, string Response, string Result)> WriteTextAsync(Stream stream, string command)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await stream.WriteAsync(bytes, 0, bytes.Length);
            while (true)
            {
                var cmd = await stream.ReadToAsync("\r");
                if (cmd == command)
                {
                    var next = await stream.ReadByteAsync();
                    if (IsCarriageReturn(next))
                    {
                        await stream.ReadToAsync("\n");
                        var response = await stream.ReadToAsync("\r\n");
                        return (cmd, response, string.Empty);
                    }
                    else if (IsAscii(next))
                    {
                        var response = ((char)next).ToString();
                        response += await stream.ReadToAsync("\r\n");
                        return (cmd, response, string.Empty);
                    }
                }
                await UnsolicitedResultCodeAsync(stream, cmd);
            }
        }
        private static async Task<(string Command, string Response, string Result)> GMRAsync(Stream stream, string command)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await stream.WriteAsync(bytes, 0, bytes.Length);
            while (true)
            {
                var cmd = await stream.ReadToAsync("\r");
                if (cmd == command)
                {
                    var result = string.Empty;
                    var response = string.Empty;
                    var next = await stream.ReadByteAsync();
                    if (IsCarriageReturn(next))
                    {
                        int i = 0;
                        int verboseLineCount = 7;
                        await stream.ReadToAsync("\n");
                        while (true)
                        {
                            result = await stream.ReadToAsync("\r\n");
                            if (i == verboseLineCount)
                            {
                                break;
                            }
                            await stream.ReadToAsync("\n");
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
                            result = await stream.ReadToAsync("\r\n");
                            if (i == numericLineCount)
                            {
                                result = await stream.ReadToAsync("\r");
                                break;
                            }
                            response += result;
                            i++;
                        }
                    }
                    return (cmd, response, result);
                }
                await UnsolicitedResultCodeAsync(stream, cmd);
            }
        }
        private static async Task<(string Command, string Response, string Result)> CGMRAsync(Stream stream, string command)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await stream.WriteAsync(bytes, 0, bytes.Length);
            while (true)
            {
                var cmd = await stream.ReadToAsync("\r");
                if (cmd == command)
                {
                    var result = string.Empty;
                    var response = string.Empty;
                    var next = await stream.ReadByteAsync();
                    if (IsCarriageReturn(next))
                    {
                        int i = 0;
                        int verboseLineCount = 7;
                        await stream.ReadToAsync("\n");
                        while (true)
                        {
                            result = await stream.ReadToAsync("\r\n");
                            if (i == verboseLineCount)
                            {
                                break;
                            }
                            await stream.ReadToAsync("\n");
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
                            result = await stream.ReadToAsync("\r\n");
                            if (i == numericLineCount)
                            {
                                result = await stream.ReadToAsync("\r");
                                break;
                            }
                            response += result;
                            i++;
                        }
                    }
                    return (cmd, response, result);
                }
                await UnsolicitedResultCodeAsync(stream, cmd);
            }
        }
        private static async Task<(string Command, string Response, string Result)> CCLKSettingsAsync(Stream stream, string command)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await stream.WriteAsync(bytes, 0, bytes.Length);
            while (true)
            {
                var cmd = await stream.ReadToAsync("\r");
                if (cmd == command)
                {
                    var next = await stream.ReadByteAsync();
                    if (IsCarriageReturn(next))
                    {
                        await stream.ReadToAsync("\n");
                        var response = await stream.ReadToAsync("\n\r\n");
                        await stream.ReadToAsync("\r\n");
                        var result = await stream.ReadToAsync("\r\n");
                        return (cmd, response, result);
                    }
                    else if (IsAscii(next))
                    {
                        var response = ((char)next).ToString();
                        response += await stream.ReadToAsync("\n\r\n");
                        var result = await stream.ReadToAsync("\r");
                        return (cmd, response, result);
                    }
                }
                await UnsolicitedResultCodeAsync(stream, cmd);
            }
        }
        private static async Task<(string Command, string Response, string Result)> AndVAsync(Stream stream, string command)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await stream.WriteAsync(bytes, 0, bytes.Length);
            while (true)
            {             
                var cmd = await stream.ReadToAsync("\r");
                if (cmd == command)
                {
                    var result = string.Empty;
                    var response = string.Empty;
                    var next = await stream.ReadByteAsync();
                    if (IsCarriageReturn(next))
                    {
                        int i = 0;
                        int verboseLineCount = 10;
                        await stream.ReadToAsync("\n");
                        while (true)
                        {
                            result = await stream.ReadToAsync("\r\n");
                            if (i == verboseLineCount)
                            {
                                break;
                            }
                            await stream.ReadToAsync("\n");
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
                            result = await stream.ReadToAsync("\r\n");
                            if (i == numericLineCount)
                            {
                                result = await stream.ReadToAsync("\r");
                                break;
                            }
                            response += result;
                            i++;
                        }
                    }
                    return (cmd, response, result);
                }
                await UnsolicitedResultCodeAsync(stream, cmd);
            }
        }
        private static async Task<(string Command, string Response, string Result)> PercentRAsync(Stream stream, string command)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await stream.WriteAsync(bytes, 0, bytes.Length);
            while (true)
            {
                var cmd = await stream.ReadToAsync("\r");
                if (cmd == command)
                {
                    var result = string.Empty;
                    var response = string.Empty;
                    var next = await stream.ReadByteAsync();
                    if (IsCarriageReturn(next))
                    {
                        int i = 0;
                        int verboseLineCount = 66;
                        await stream.ReadToAsync("\n");
                        while (true)
                        {
                            result = await stream.ReadToAsync("\r\n");
                            if (i == verboseLineCount)
                            {
                                break;
                            }
                            await stream.ReadToAsync("\n");
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
                            result = await stream.ReadToAsync("\r\n");
                            if (i == numericLineCount)
                            {
                                result = await stream.ReadToAsync("\r");
                                break;
                            }
                            response += result;
                            i++;
                        }
                    }
                    return (cmd, response, result);
                }
                await UnsolicitedResultCodeAsync(stream, cmd);
            }
        }
        private static async Task<(string Command, string Response, string Result)> ResponseWithPayloadAsync(Stream stream, string command)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await stream.WriteAsync(bytes, 0, bytes.Length);
            while (true)
            {
                var cmd = await stream.ReadToAsync("\r");
                if (cmd == command)
                {
                    var next = await stream.ReadByteAsync();
                    if (IsCarriageReturn(next))
                    {
                        await stream.ReadToAsync("\n");
                        var response = await stream.ReadToAsync("\r\n\r\n");
                        var result = await stream.ReadToAsync("\r\n");
                        return (cmd, response, result);
                    }
                    else if (IsAscii(next))
                    {
                        var response = ((char)next).ToString();
                        response += await stream.ReadToAsync("\r\n");
                        var result = await stream.ReadToAsync("\r");
                        return (cmd, response, result);
                    }
                }
                await UnsolicitedResultCodeAsync(stream, cmd);
            }
        }
        private static async Task<(string Command, string Response, string Result)> ResponseWithoutPayloadAsync(Stream stream, string command)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await stream.WriteAsync(bytes, 0, bytes.Length);
            while (true)
            {
                var cmd = await stream.ReadToAsync("\r");
                if (cmd == command)
                {
                    var next = await stream.ReadByteAsync();
                    if (IsCarriageReturn(next))
                    {
                        await stream.ReadToAsync("\n");
                        var result = await stream.ReadToAsync("\r\n");
                        return (cmd, string.Empty, result);
                    }
                    else if (IsAscii(next))
                    {
                        var result = ((char)next).ToString();
                        result += await stream.ReadToAsync("\r");
                        return (cmd, string.Empty, result);
                    }
                }
                await UnsolicitedResultCodeAsync(stream, cmd);
            }
        }
        private static async Task UnsolicitedResultCodeAsync(Stream stream, string code)
        {
            if (code != "126")
            {
                await stream.ReadToAsync("\n");
                code = await stream.ReadToAsync("\r\n");
            }
            if (code is "126" or "SBDRING")
            {
                
            }
        }
        private static string NormalizeCommand(string command)
        {
            if (command.StartsWith("AT+SBDWT=")) return "AT+SBDWT=";
            else if (command.StartsWith("AT+SBDWB=")) return "AT+SBDWB=";
            return command;
        }
        private static byte[] CalculateChecksum(byte[] bytes)
        {
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
                        CancelIncompleteTransitions();
                    }
                    finally
                    {
                        SemaphoreSlim.Release();
                    }
                }
                else
                {
                    CancelIncompleteTransitions();
                }
                SerialPort?.Dispose();
                SemaphoreSlim.Dispose();
            }
        }
        #endregion
    }
}