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
            InitializeCommandMap();
        }

        public async Task ConnectAsync(int baudRate, int dataBits, Parity parity, StopBits stopBits)
        {
            await SemaphoreSlim.WaitAsync();
            try
            {
                ThrowIfAnyTransitionCompleted();
                await InitializeSerialPortAsync(baudRate, dataBits, parity, stopBits);
                TryTransitionToConnected();
                ThrowIfNotConnected();
            }
            catch when (!IsFaulted() && !IsDisconnected())
            {
                TryTransitionToFaulted();
                TryTransitionToDisconnected();
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
                await InitializeSerialPortAsync(portName, baudRate, dataBits, parity, stopBits);
                TryTransitionToConnected();
                ThrowIfNotConnected();
            }
            catch when (!IsFaulted() && !IsDisconnected())
            {
                TryTransitionToFaulted();
                TryTransitionToDisconnected();
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
                return await CommandMapAsync(command);
            }
            catch when (IsConnected())
            {
                TryTransitionToFaulted();
                TryTransitionToDisconnected();
                SerialPort?.Dispose();
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
                return await ReadyStateTextCommandAsync(command).WaitAsync(TimeSpan.FromSeconds(3));
            }
            catch when (IsConnected())
            {
                TryTransitionToFaulted();
                TryTransitionToDisconnected();
                SerialPort?.Dispose();
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
                return await ReadyStateBase64CommandAsync(command).WaitAsync(TimeSpan.FromSeconds(3));
            }
            catch when (IsConnected())
            {
                TryTransitionToFaulted();
                TryTransitionToDisconnected();
                SerialPort?.Dispose();
                throw;
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }
        public async Task DisconnectAsync()
        {
            await SemaphoreSlim.WaitAsync();
            try
            {
                ThrowIfNeverConnected();
                if (TryTransitionToDisconnected())
                {
                    SerialPort?.Dispose();
                }
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }
        private async Task InitializeSerialPortAsync(int baudRate, int dataBits, Parity parity, StopBits stopBits)
        {
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
                    return;
                }
                catch
                {
                    SerialPort?.Dispose();
                }
            }
            throw new DeviceException();
        }
        private async Task InitializeSerialPortAsync(string portName, int baudRate, int dataBits, Parity parity, StopBits stopBits)
        {
            try
            {
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
                return;
            }
            catch 
            {
                SerialPort?.Dispose();
            }
            throw new DeviceException();
        }
        private async Task ValidateConnectionAsync()
        {
            var output = await CommandMapAsync("AT");
            if (output is not { Command: "AT", Response: "", Result: "OK" or "0" })
            {
                throw new DeviceException();
            }
        }
        private bool TryTransitionToConnected()
        {
            return !Faulted.IsCompleted && !Disconnected.IsCompleted && ConnectedSource.TrySetResult();
        }
        private bool TryTransitionToFaulted()
        {
            return FaultedSource.TrySetResult();
        }
        private bool TryTransitionToDisconnected()
        {
            return Connected.IsCompletedSuccessfully && DisconnectedSource.TrySetResult();
        }
        private void CancelIncompleteTransitions()
        {
            if (!Connected.IsCompleted)
                ConnectedSource.TrySetCanceled();

            if (!Disconnected.IsCompleted)
                DisconnectedSource.TrySetCanceled();

            if (!Faulted.IsCompleted)
                FaultedSource.TrySetCanceled();
        }
        private void ThrowIfAnyTransitionCompleted()
        {
            if (Connected.IsCompleted || Faulted.IsCompleted || Disconnected.IsCompleted)
            {
                throw new DeviceException();
            }
        }
        private void ThrowIfNeverConnected()
        {
            if (!Connected.IsCompletedSuccessfully)
            {
                throw new DeviceException();
            }
        }
        private void ThrowIfNotConnected()
        {
            if (!IsConnected())
            {
                throw new DeviceException();
            }
        }
        private bool IsConnected()
        {
            return Connected.IsCompletedSuccessfully && !Faulted.IsCompleted && !Disconnected.IsCompleted;
        }
        private bool IsFaulted()
        {
            return Faulted.IsCompletedSuccessfully;
        }
        private bool IsDisconnected()
        {
            return Disconnected.IsCompletedSuccessfully;
        }

        #region Iridium 9603 Module
        private IReadOnlyDictionary<string, (Func<string, Task<(string Command, string Response, string Result)>>, TimeSpan)> CommandMap { get; set; }

        private void InitializeCommandMap()
        {
            CommandMap = new Dictionary<string, (Func<string, Task<(string Command, string Response, string Result)>>, TimeSpan)>
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
        }
        private async Task<(string Command, string Response, string Result)> CommandMapAsync(string command)
        {
            try
            {
                var (func, timeout) = CommandMap[NormalizeCommand(command)];
                return await func(command).WaitAsync(timeout);
            }
            catch { }
            throw new DeviceException();
        }
        private async Task<(string Command, string Response, string Result)> ReadyStateTextCommandAsync(string command)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await SerialPort.BaseStream.WriteAsync(bytes, 0, bytes.Length);

            var cmd = await SerialPort.BaseStream.ReadToAsync("\r");
            if (cmd == command)
            {
                await SerialPort.BaseStream.ReadToAsync("\n");
                var response = await SerialPort.BaseStream.ReadToAsync("\r\n");
                await SerialPort.BaseStream.ReadToAsync("\r\n");
                var result = await SerialPort.BaseStream.ReadToAsync("\r\n");
                return (cmd, response, result);
            }
            else
            {
                var response = cmd[^1].ToString();
                cmd = cmd.Substring(0, cmd.Length - 1);
                await SerialPort.BaseStream.ReadToAsync("\n");
                var result = await SerialPort.BaseStream.ReadToAsync("\r");
                return (cmd, response, result);
            }
        }
        private async Task<(string Command, string Response, string Result)> ReadyStateBase64CommandAsync(string command)
        {
            var base64 = Convert.FromBase64String(command);
            var cks = CalculateChecksum(base64);
            var bytes = base64.Concat(cks).ToArray();
            await SerialPort.BaseStream.WriteAsync(bytes, 0, bytes.Length);

            var next = await SerialPort.BaseStream.ReadByteAsync();
            if (IsCarriageReturn(next))
            {
                var response = await SerialPort.BaseStream.ReadToAsync("\r\n");
                await SerialPort.BaseStream.ReadToAsync("\r\n");
                response = response[^1].ToString();
                var result = await SerialPort.BaseStream.ReadToAsync("\r\n");
                return (string.Empty, response, result);
            }
            else
            {
                var response = ((char)next).ToString();
                await SerialPort.BaseStream.ReadToAsync("\r\n");
                var result = await SerialPort.BaseStream.ReadToAsync("\r");
                return (string.Empty, response, result);
            }
        }
        private async Task<(string Command, string Response, string Result)> ReadBinaryAsync(string command)
        {          
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await SerialPort.BaseStream.WriteAsync(bytes, 0, bytes.Length);
            while (true)
            {
                var cmd = await SerialPort.BaseStream.ReadToAsync("\r");
                if (cmd == command)
                {
                    var len = new byte[2];
                    await SerialPort.BaseStream.ReadExactlyAsync(len, 0, 2);
                    var msg = new byte[(len[0] << 8) | len[1]];
                    await SerialPort.BaseStream.ReadExactlyAsync(msg, 0, msg.Length);
                    var cks = new byte[2];
                    await SerialPort.BaseStream.ReadExactlyAsync(cks, 0, 2);

                    var next = await SerialPort.BaseStream.ReadByteAsync();
                    if (IsCarriageReturn(next))
                    {
                        await SerialPort.BaseStream.ReadToAsync("\n");
                        var result = await SerialPort.BaseStream.ReadToAsync("\r\n");
                        var response = Convert.ToBase64String(len.Concat(msg).Concat(cks).ToArray());
                        return (cmd, response, result);
                    }
                    else if (IsAscii(next))
                    {
                        var result = ((char)next).ToString();
                        result += await SerialPort.BaseStream.ReadToAsync("\r");
                        var response = Convert.ToBase64String(len.Concat(msg).Concat(cks).ToArray());
                        return (cmd, response, result);
                    }
                }
                await UnsolicitedResultCodeAsync(cmd);
            }
        }
        private async Task<(string Command, string Response, string Result)> ReadTextAsync(string command)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await SerialPort.BaseStream.WriteAsync(bytes, 0, bytes.Length);
            while (true)
            {
                var cmd = await SerialPort.BaseStream.ReadToAsync("\r");
                if (cmd == command)
                {
                    var next = await SerialPort.BaseStream.ReadByteAsync();
                    if (IsCarriageReturn(next))
                    {
                        await SerialPort.BaseStream.ReadToAsync("\n");
                        var response = await SerialPort.BaseStream.ReadToAsync("\r\n");
                        response += await SerialPort.BaseStream.ReadToAsync("\r\n");
                        var result = await SerialPort.BaseStream.ReadToAsync("\r\n");
                        return (cmd, response, result);
                    }
                    else if (IsAscii(next))
                    {
                        var response = ((char)next).ToString();
                        response += await SerialPort.BaseStream.ReadToAsync("\r\n");
                        response += await SerialPort.BaseStream.ReadToAsync("\r");
                        var result = response[^1].ToString();
                        response = response.Remove(response.Length - 1);
                        return (cmd, response, result);
                    }
                }
                await UnsolicitedResultCodeAsync(cmd);
            }
        }
        private async Task<(string Command, string Response, string Result)> WriteBinaryAsync(string command)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await SerialPort.BaseStream.WriteAsync(bytes, 0, bytes.Length);
            while (true)
            {
                var cmd = await SerialPort.BaseStream.ReadToAsync("\r");
                if (cmd == command)
                {
                    var next = await SerialPort.BaseStream.ReadByteAsync();
                    if (IsCarriageReturn(next))
                    {
                        await SerialPort.BaseStream.ReadToAsync("\n");
                        var response = await SerialPort.BaseStream.ReadToAsync("\r\n");
                        return (cmd, response, string.Empty);
                    }
                    else if (IsAscii(next))
                    {
                        var response = ((char)next).ToString();
                        response += await SerialPort.BaseStream.ReadToAsync("\r\n");
                        return (cmd, response, string.Empty);
                    }
                }
                await UnsolicitedResultCodeAsync(cmd);
            }
        }
        private async Task<(string Command, string Response, string Result)> WriteTextAsync(string command)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await SerialPort.BaseStream.WriteAsync(bytes, 0, bytes.Length);
            while (true)
            {
                var cmd = await SerialPort.BaseStream.ReadToAsync("\r");
                if (cmd == command)
                {
                    var next = await SerialPort.BaseStream.ReadByteAsync();
                    if (IsCarriageReturn(next))
                    {
                        await SerialPort.BaseStream.ReadToAsync("\n");
                        var response = await SerialPort.BaseStream.ReadToAsync("\r\n");
                        return (cmd, response, string.Empty);
                    }
                    else if (IsAscii(next))
                    {
                        var response = ((char)next).ToString();
                        response += await SerialPort.BaseStream.ReadToAsync("\r\n");
                        return (cmd, response, string.Empty);
                    }
                }
                await UnsolicitedResultCodeAsync(cmd);
            }
        }
        private async Task<(string Command, string Response, string Result)> GMRAsync(string command)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await SerialPort.BaseStream.WriteAsync(bytes, 0, bytes.Length);
            while (true)
            {
                var cmd = await SerialPort.BaseStream.ReadToAsync("\r");
                if (cmd == command)
                {
                    var result = string.Empty;
                    var response = string.Empty;
                    var next = await SerialPort.BaseStream.ReadByteAsync();
                    if (IsCarriageReturn(next))
                    {
                        int i = 0;
                        int verboseLineCount = 7;
                        await SerialPort.BaseStream.ReadToAsync("\n");
                        while (true)
                        {
                            result = await SerialPort.BaseStream.ReadToAsync("\r\n");
                            if (i == verboseLineCount)
                            {
                                break;
                            }
                            await SerialPort.BaseStream.ReadToAsync("\n");
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
                            result = await SerialPort.BaseStream.ReadToAsync("\r\n");
                            if (i == numericLineCount)
                            {
                                result = await SerialPort.BaseStream.ReadToAsync("\r");
                                break;
                            }
                            response += result;
                            i++;
                        }
                    }
                    return (cmd, response, result);
                }
                await UnsolicitedResultCodeAsync(cmd);
            }
        }
        private async Task<(string Command, string Response, string Result)> CGMRAsync(string command)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await SerialPort.BaseStream.WriteAsync(bytes, 0, bytes.Length);
            while (true)
            {
                var cmd = await SerialPort.BaseStream.ReadToAsync("\r");
                if (cmd == command)
                {
                    var result = string.Empty;
                    var response = string.Empty;
                    var next = await SerialPort.BaseStream.ReadByteAsync();
                    if (IsCarriageReturn(next))
                    {
                        int i = 0;
                        int verboseLineCount = 7;
                        await SerialPort.BaseStream.ReadToAsync("\n");
                        while (true)
                        {
                            result = await SerialPort.BaseStream.ReadToAsync("\r\n");
                            if (i == verboseLineCount)
                            {
                                break;
                            }
                            await SerialPort.BaseStream.ReadToAsync("\n");
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
                            result = await SerialPort.BaseStream.ReadToAsync("\r\n");
                            if (i == numericLineCount)
                            {
                                result = await SerialPort.BaseStream.ReadToAsync("\r");
                                break;
                            }
                            response += result;
                            i++;
                        }
                    }
                    return (cmd, response, result);
                }
                await UnsolicitedResultCodeAsync(cmd);
            }
        }
        private async Task<(string Command, string Response, string Result)> CCLKSettingsAsync(string command)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await SerialPort.BaseStream.WriteAsync(bytes, 0, bytes.Length);
            while (true)
            {
                var cmd = await SerialPort.BaseStream.ReadToAsync("\r");
                if (cmd == command)
                {
                    var next = await SerialPort.BaseStream.ReadByteAsync();
                    if (IsCarriageReturn(next))
                    {
                        await SerialPort.BaseStream.ReadToAsync("\n");
                        var response = await SerialPort.BaseStream.ReadToAsync("\n\r\n");
                        await SerialPort.BaseStream.ReadToAsync("\r\n");
                        var result = await SerialPort.BaseStream.ReadToAsync("\r\n");
                        return (cmd, response, result);
                    }
                    else if (IsAscii(next))
                    {
                        var response = ((char)next).ToString();
                        response += await SerialPort.BaseStream.ReadToAsync("\n\r\n");
                        var result = await SerialPort.BaseStream.ReadToAsync("\r");
                        return (cmd, response, result);
                    }
                }
                await UnsolicitedResultCodeAsync(cmd);
            }
        }
        private async Task<(string Command, string Response, string Result)> AndVAsync(string command)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await SerialPort.BaseStream.WriteAsync(bytes, 0, bytes.Length);
            while (true)
            {             
                var cmd = await SerialPort.BaseStream.ReadToAsync("\r");
                if (cmd == command)
                {
                    var result = string.Empty;
                    var response = string.Empty;
                    var next = await SerialPort.BaseStream.ReadByteAsync();
                    if (IsCarriageReturn(next))
                    {
                        int i = 0;
                        int verboseLineCount = 10;
                        await SerialPort.BaseStream.ReadToAsync("\n");
                        while (true)
                        {
                            result = await SerialPort.BaseStream.ReadToAsync("\r\n");
                            if (i == verboseLineCount)
                            {
                                break;
                            }
                            await SerialPort.BaseStream.ReadToAsync("\n");
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
                            result = await SerialPort.BaseStream.ReadToAsync("\r\n");
                            if (i == numericLineCount)
                            {
                                result = await SerialPort.BaseStream.ReadToAsync("\r");
                                break;
                            }
                            response += result;
                            i++;
                        }
                    }
                    return (cmd, response, result);
                }
                await UnsolicitedResultCodeAsync(cmd);
            }
        }
        private async Task<(string Command, string Response, string Result)> PercentRAsync(string command)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await SerialPort.BaseStream.WriteAsync(bytes, 0, bytes.Length);
            while (true)
            {
                var cmd = await SerialPort.BaseStream.ReadToAsync("\r");
                if (cmd == command)
                {
                    var result = string.Empty;
                    var response = string.Empty;
                    var next = await SerialPort.BaseStream.ReadByteAsync();
                    if (IsCarriageReturn(next))
                    {
                        int i = 0;
                        int verboseLineCount = 66;
                        await SerialPort.BaseStream.ReadToAsync("\n");
                        while (true)
                        {
                            result = await SerialPort.BaseStream.ReadToAsync("\r\n");
                            if (i == verboseLineCount)
                            {
                                break;
                            }
                            await SerialPort.BaseStream.ReadToAsync("\n");
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
                            result = await SerialPort.BaseStream.ReadToAsync("\r\n");
                            if (i == numericLineCount)
                            {
                                result = await SerialPort.BaseStream.ReadToAsync("\r");
                                break;
                            }
                            response += result;
                            i++;
                        }
                    }
                    return (cmd, response, result);
                }
                await UnsolicitedResultCodeAsync(cmd);
            }
        }
        private async Task<(string Command, string Response, string Result)> ResponseWithPayloadAsync(string command)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await SerialPort.BaseStream.WriteAsync(bytes, 0, bytes.Length);
            while (true)
            {
                var cmd = await SerialPort.BaseStream.ReadToAsync("\r");
                if (cmd == command)
                {
                    var next = await SerialPort.BaseStream.ReadByteAsync();
                    if (IsCarriageReturn(next))
                    {
                        await SerialPort.BaseStream.ReadToAsync("\n");
                        var response = await SerialPort.BaseStream.ReadToAsync("\r\n\r\n");
                        var result = await SerialPort.BaseStream.ReadToAsync("\r\n");
                        return (cmd, response, result);
                    }
                    else if (IsAscii(next))
                    {
                        var response = ((char)next).ToString();
                        response += await SerialPort.BaseStream.ReadToAsync("\r\n");
                        var result = await SerialPort.BaseStream.ReadToAsync("\r");
                        return (cmd, response, result);
                    }
                }
                await UnsolicitedResultCodeAsync(cmd);
            }
        }
        private async Task<(string Command, string Response, string Result)> ResponseWithoutPayloadAsync(string command)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            await SerialPort.BaseStream.WriteAsync(bytes, 0, bytes.Length);
            while (true)
            {
                var cmd = await SerialPort.BaseStream.ReadToAsync("\r");
                if (cmd == command)
                {
                    var next = await SerialPort.BaseStream.ReadByteAsync();
                    if (IsCarriageReturn(next))
                    {
                        await SerialPort.BaseStream.ReadToAsync("\n");
                        var result = await SerialPort.BaseStream.ReadToAsync("\r\n");
                        return (cmd, string.Empty, result);
                    }
                    else if (IsAscii(next))
                    {
                        var result = ((char)next).ToString();
                        result += await SerialPort.BaseStream.ReadToAsync("\r");
                        return (cmd, string.Empty, result);
                    }
                }
                await UnsolicitedResultCodeAsync(cmd);
            }
        }
        private async Task UnsolicitedResultCodeAsync(string code)
        {
            if (code != "126")
            {
                await SerialPort.BaseStream.ReadToAsync("\n");
                code = await SerialPort.BaseStream.ReadToAsync("\r\n");
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
                CancelIncompleteTransitions();
                SerialPort?.Dispose();
                SemaphoreSlim.Dispose();
            }
        }
        #endregion
    }
}