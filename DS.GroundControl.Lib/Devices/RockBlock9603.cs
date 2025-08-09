using System.Text;
using System.IO.Ports;
using DS.GroundControl.Lib.Extensions;
using DS.GroundControl.Lib.Exceptions;

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
                    if (await TryValidateConnectionAsync(sp))
                    {
                        SerialPort = sp;
                        if (ConnectedSource.TrySetResult())
                        {
                            return;
                        }
                        SerialPort = null;
                        sp.Dispose();
                        break;
                    }
                }
                catch { }
                sp.Dispose();
            }
            FaultedSource.TrySetResult();
            if (Connected.IsCompletedSuccessfully)
            {
                DisconnectedSource.TrySetResult();
            }
        }
        public async Task<(string Command, string Response, string Result)> ExecuteAsync(string command)
        {
            try
            {
                if (Connected.IsCompletedSuccessfully && !Disconnected.IsCompleted && !Faulted.IsCompleted)
                {
                    await SemaphoreSlim.WaitAsync();
                    try
                    {
                        return await ExecuteAsync(SerialPort, command);
                    }
                    catch
                    {
                        if (FaultedSource.TrySetResult())
                        {
                            DisconnectedSource.TrySetResult();
                            SerialPort.Dispose();
                            SerialPort = null;
                        }
                    }
                    finally
                    {
                        SemaphoreSlim.Release();
                    }
                }
            }
            catch { }
            throw new DeviceNotConnectedException();
        }
        public async Task<(string Command, string Response, string Result)> ExecuteReadyStateTextCommandAsync(string command)
        {
            try
            {
                if (Connected.IsCompletedSuccessfully && !Disconnected.IsCompleted && !Faulted.IsCompleted)
                {
                    await SemaphoreSlim.WaitAsync();
                    try
                    {
                        return await ExecuteReadyStateTextCommandAsync(SerialPort, command);
                    }
                    catch
                    {
                        if (FaultedSource.TrySetResult())
                        {
                            DisconnectedSource.TrySetResult();
                            SerialPort.Dispose();
                            SerialPort = null;
                        }
                    }
                    finally
                    {
                        SemaphoreSlim.Release();
                    }
                }
            }
            catch { }
            throw new DeviceNotConnectedException();
        }
        public async Task<(string Command, string Response, string Result)> ExecuteReadyStateBinaryCommandAsync(string command)
        {
            try
            {
                if (Connected.IsCompletedSuccessfully && !Disconnected.IsCompleted && !Faulted.IsCompleted)
                {
                    await SemaphoreSlim.WaitAsync();
                    try
                    {
                        return await ExecuteReadyStateBinaryCommandAsync(SerialPort, command);
                    }
                    catch
                    {
                        if (FaultedSource.TrySetResult())
                        {
                            DisconnectedSource.TrySetResult();
                            SerialPort.Dispose();
                            SerialPort = null;
                        }
                    }
                    finally
                    {
                        SemaphoreSlim.Release();
                    }
                }
            }
            catch { }
            throw new DeviceNotConnectedException();
        }

        #region static
        private static Dictionary<string, Func<SerialPort, string, (string Command, string Response, string Result)>> Commands { get; } =
            new Dictionary<string, Func<SerialPort, string, (string Command, string Response, string Result)>>()
            {
                { "AT+CCLK?", ExecuteCCLKCurrentSettings },
                { "AT+SBDRB", ExecuteSBDRB },
                { "AT+SBDRT", ExecuteSBDRT },
                { "AT+SBDWT", ExecuteSBDWT },
                { "AT+SBDWB=", ExecuteSBDWB},
                { "AT&V", ExecuteATAndV },
                { "AT+GMR", ExecuteGMR },
                { "AT+CGMR", ExecuteCGMR },
                { "AT%R", ExecuteATPercentR },               
                { "AT+CGMI", ExecuteResponseWithPayload },
                { "AT+CGMM", ExecuteResponseWithPayload },
                { "AT+CGSN", ExecuteResponseWithPayload },
                { "AT+CIER=?", ExecuteResponseWithPayload },
                { "AT+CIER?", ExecuteResponseWithPayload },
                { "AT+CRIS", ExecuteResponseWithPayload },
                { "AT+CRISX", ExecuteResponseWithPayload },
                { "AT+CSQ", ExecuteResponseWithPayload },
                { "AT+CSQ=?", ExecuteResponseWithPayload },
                { "AT+CSQF", ExecuteResponseWithPayload },
                { "AT+CULK?", ExecuteResponseWithPayload },
                { "AT+GMI", ExecuteResponseWithPayload },
                { "AT+GMM", ExecuteResponseWithPayload },
                { "AT+GSN", ExecuteResponseWithPayload },
                { "AT+IPR=?", ExecuteResponseWithPayload },
                { "AT+IPR?", ExecuteResponseWithPayload },
                { "AT+SBDLOE", ExecuteResponseWithPayload },
                { "AT+SBDAREG=?", ExecuteResponseWithPayload },
                { "AT+SBDAREG?", ExecuteResponseWithPayload },
                { "AT+SBDC", ExecuteResponseWithPayload },
                { "AT+SBDD0", ExecuteResponseWithPayload },
                { "AT+SBDD1", ExecuteResponseWithPayload },
                { "AT+SBDD2", ExecuteResponseWithPayload },
                { "AT+SBDDSC?", ExecuteResponseWithPayload },
                { "AT+SBDGW", ExecuteResponseWithPayload },
                { "AT+SBDGWN", ExecuteResponseWithPayload },
                { "AT+SBDI", ExecuteResponseWithPayload },
                { "AT+SBDIX", ExecuteResponseWithPayload },
                { "AT+SBDIXA", ExecuteResponseWithPayload },
                { "AT+SBDMTA?", ExecuteResponseWithPayload },
                { "AT+SBDMTA=?", ExecuteResponseWithPayload },
                { "AT+SBDREG?", ExecuteResponseWithPayload },
                { "AT+SBDS", ExecuteResponseWithPayload },
                { "AT+SBDST?", ExecuteResponseWithPayload },
                { "AT+SBDSX", ExecuteResponseWithPayload },
                { "AT+SBDTC", ExecuteResponseWithPayload },
                { "AT-MSGEOS", ExecuteResponseWithPayload },
                { "AT-MSGEO", ExecuteResponseWithPayload },
                { "AT-MSSTM", ExecuteResponseWithPayload },
                { "ATI0", ExecuteResponseWithPayload },
                { "ATI1", ExecuteResponseWithPayload },
                { "ATI2", ExecuteResponseWithPayload },
                { "ATI3", ExecuteResponseWithPayload },
                { "ATI4", ExecuteResponseWithPayload },
                { "ATI5", ExecuteResponseWithPayload },
                { "ATI6", ExecuteResponseWithPayload },
                { "AT+SBDWT=", ExecuteResponseWithoutPayload },
                { "ATI7", ExecuteResponseWithoutPayload },
                { "AT&Y0", ExecuteResponseWithoutPayload },
                { "AT&K0", ExecuteResponseWithoutPayload },
                { "AT&K3", ExecuteResponseWithoutPayload },
                { "AT*R1", ExecuteResponseWithoutPayload },
                { "AT*F", ExecuteResponseWithoutPayload },
                { "AT+SBDMTA=0", ExecuteResponseWithoutPayload },
                { "AT+SBDMTA=1", ExecuteResponseWithoutPayload },
                { "ATE1", ExecuteResponseWithoutPayload },
                { "ATQ0", ExecuteResponseWithoutPayload },
                { "AT", ExecuteResponseWithoutPayload },
                { "ATV1", ExecuteResponseWithoutPayload },
                { "ATV0", ExecuteResponseWithoutPayload }
            };

        private static async Task<bool> TryValidateConnectionAsync(SerialPort sp)
        {
            try
            {
                var output = await ExecuteAsync(sp, "AT");
                if (output is { Command: "AT", Response: "", Result: "OK" or "0" })
                {
                    return true;
                }
            }
            catch { }
            return false;
        }
        private static async Task<(string Command, string Response, string Result)> ExecuteAsync(SerialPort sp, string command)
        {
            var execute = Task.Run(() =>
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
                return func(sp, command);
            });
            await execute.ThrowOnTimeoutAsync(TimeSpan.FromMinutes(1));
            return await execute;
        }
        private static async Task<(string Command, string Response, string Result)> ExecuteReadyStateTextCommandAsync(SerialPort sp, string command)
        {
            var execute = Task.Run(() =>
            {
                return ExecuteReadyStateTextCommand(sp, command);
            });
            await execute.ThrowOnTimeoutAsync(TimeSpan.FromMinutes(1));
            return await execute;
        }
        private static async Task<(string Command, string Response, string Result)> ExecuteReadyStateBinaryCommandAsync(SerialPort sp, string command)
        {
            var execute = Task.Run(() =>
            {
                return ExecuteReadyStateBinaryCommand(sp, command);
            });
            await execute.ThrowOnTimeoutAsync(TimeSpan.FromMinutes(1));
            return await execute;
        }
        private static (string Command, string Response, string Result) ExecuteReadyStateTextCommand(SerialPort sp, string command)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            sp.Write(bytes, 0, bytes.Length);

            var cmd = sp.ReadTo("\r");
            if (command.Length == cmd.Length) // ATV1 + AT+SBDWT + message
            {
                cmd = cmd.Substring(0, cmd.Length - 1);
                sp.ReadTo("\n");
                var response = sp.ReadTo("\r\n");
                sp.ReadTo("\r\n");
                var result = sp.ReadTo("\r\n");
                return (cmd, response, result);
            }
            else // ATV0 + AT+SBDWT + message 
            {
                var response = cmd[cmd.Length - 1].ToString();
                cmd = cmd.Substring(0, cmd.Length - 1);
                sp.ReadTo("\n");
                var result = sp.ReadTo("\r");
                return (cmd, response, result);
            }
        }
        private static (string Command, string Response, string Result) ExecuteReadyStateBinaryCommand(SerialPort sp, string command)
        {
            var cks = CalculateChecksum(command);
            var bytes = Encoding.ASCII.GetBytes(command).Concat(cks).ToArray();
            sp.Write(bytes, 0, bytes.Length);

            var next = sp.ReadChar();
            if (IsCarriageReturn(next)) // ATV1 + AT+SBDWB= + message
            {
                var response = sp.ReadTo("\r\n");
                sp.ReadTo("\r\n");
                response = response[response.Length - 1].ToString();
                var result = sp.ReadTo("\r\n");
                return (string.Empty, response, result);
            }
            else // ATV0 + AT+SBDWB= + message
            {
                var response = Convert.ToChar(next).ToString();
                sp.ReadTo("\r\n");
                var result = sp.ReadTo("\r");           
                return (string.Empty, response, result);
            }
        }
        private static (string Command, string Response, string Result) ExecuteCCLKCurrentSettings(SerialPort sp, string command)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            sp.Write(bytes, 0, bytes.Length);
            while (true)
            {
                var result = string.Empty;
                var response = string.Empty;
                var cmd = sp.ReadTo("\r");
                if (cmd == command)
                {
                    var next = sp.ReadChar();
                    if (IsCarriageReturn(next))
                    {
                        sp.ReadTo("\n");
                        response = sp.ReadTo("\n\r\n");
                        sp.ReadTo("\r\n");
                        result = sp.ReadTo("\r\n");
                    }
                    else if (IsAscii(next))
                    {
                        response += Convert.ToChar(next);
                        response += sp.ReadTo("\n\r\n");
                        result = sp.ReadTo("\r");
                    }
                }
                else
                {
                    if (cmd != "126")
                    {
                        sp.ReadTo("\n");
                        sp.ReadTo("\r\n");
                    }
                    continue;
                }
                return (cmd, response, result);
            }
        }
        private static (string Command, string Response, string Result) ExecuteSBDRB(SerialPort sp, string command)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            sp.Write(bytes, 0, bytes.Length);
            while (true)
            {
                var result = string.Empty;
                var response = string.Empty;
                var cmd = sp.ReadTo("\r");
                if (cmd == command)
                {
                    var len = new byte[2];
                    sp.ReadExactly(len, 0, 2);
                    var msg = new byte[(len[0] << 8) | len[1]];
                    sp.ReadExactly(msg, 0, msg.Length);
                    var cks = new byte[2];
                    sp.ReadExactly(cks, 0, 2);

                    var next = sp.ReadChar();
                    if (IsCarriageReturn(next))
                    {
                        sp.ReadTo("\n");
                        result = sp.ReadTo("\r\n");
                        response = Encoding.ASCII.GetString(len.Concat(msg).Concat(cks).ToArray());
                    }
                    else if (IsAscii(next))
                    {
                        result += Convert.ToChar(next);
                        result += sp.ReadTo("\r");
                        response = Encoding.ASCII.GetString(len.Concat(msg).Concat(cks).ToArray());
                    }
                }
                else
                {
                    if (cmd != "126")
                    {
                        sp.ReadTo("\n");
                        sp.ReadTo("\r\n");
                    }
                    continue;
                }
                return (cmd, response, result);
            }
        }
        private static (string Command, string Response, string Result) ExecuteSBDRT(SerialPort sp, string command)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            sp.Write(bytes, 0, bytes.Length);
            while (true)
            {
                var result = string.Empty;
                var response = string.Empty;
                var cmd = sp.ReadTo("\r");
                if (cmd == command)
                {
                    var next = sp.ReadChar();
                    if (IsCarriageReturn(next))
                    {
                        sp.ReadTo("\n");
                        response += sp.ReadTo("\r\n");
                        response += sp.ReadTo("\r\n");
                        result = sp.ReadTo("\r\n");
                    }
                    else if (IsAscii(next))
                    {
                        response += Convert.ToChar(next);
                        response += sp.ReadTo("\r\n");
                        response += sp.ReadTo("\r");
                        result = response[response.Length - 1].ToString();
                        response = response.Remove(response.Length - 1);
                    }
                }
                else
                {
                    if (cmd != "126")
                    {
                        sp.ReadTo("\n");
                        sp.ReadTo("\r\n");
                    }
                    continue;
                }
                return (cmd, response, result);
            }
        }
        private static (string Command, string Response, string Result) ExecuteSBDWT(SerialPort sp, string command)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            sp.Write(bytes, 0, bytes.Length);
            while (true)
            {
                var result = string.Empty;
                var response = string.Empty;
                var cmd = sp.ReadTo("\r");
                if (cmd == command)
                {
                    var next = sp.ReadChar();
                    if (IsCarriageReturn(next))
                    {
                        sp.ReadTo("\n");
                        response = sp.ReadTo("\r\n");
                    }
                    else if (IsAscii(next))
                    {
                        response += Convert.ToChar(next);
                        response += sp.ReadTo("\r\n");
                    }
                }
                else
                {
                    if (cmd != "126")
                    {
                        sp.ReadTo("\n");
                        sp.ReadTo("\r\n");
                    }
                    continue;
                }
                return (cmd, response, result);
            }
        }
        private static (string Command, string Response, string Result) ExecuteSBDWB(SerialPort sp, string command)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            sp.Write(bytes, 0, bytes.Length);
            while (true)
            {
                var result = string.Empty;
                var response = string.Empty;
                var cmd = sp.ReadTo("\r");
                if (cmd == command)
                {
                    var next = sp.ReadChar();
                    if (IsCarriageReturn(next))
                    {
                        sp.ReadTo("\n");
                        response = sp.ReadTo("\r\n");
                    }
                    else if (IsAscii(next))
                    {
                        response += Convert.ToChar(next);
                        response += sp.ReadTo("\r\n");
                    }
                }
                else
                {
                    if (cmd != "126")
                    {
                        sp.ReadTo("\n");
                        sp.ReadTo("\r\n");
                    }
                    continue;
                }
                return (cmd, response, result);
            }
        }
        private static (string Command, string Response, string Result) ExecuteATAndV(SerialPort sp, string command)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            sp.Write(bytes, 0, bytes.Length);
            while (true)
            {
                var result = string.Empty;
                var response = string.Empty;
                var cmd = sp.ReadTo("\r");
                if (cmd == command)
                {
                    var next = sp.ReadChar();
                    if (IsCarriageReturn(next))
                    {
                        sp.ReadTo("\n");
                        int i = 0;
                        while (true)
                        {
                            result = sp.ReadTo("\r\n");
                            if (i == 10)
                            {
                                break;
                            }
                            sp.ReadTo("\n");
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
                            result = sp.ReadTo("\r\n");
                            if (i == 9)
                            {
                                result = sp.ReadTo("\r");
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
                        sp.ReadTo("\n");
                        sp.ReadTo("\r\n");
                    }
                    continue;
                }
                return (cmd, response, result);
            }
        }
        private static (string Command, string Response, string Result) ExecuteGMR(SerialPort sp, string command)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            sp.Write(bytes, 0, bytes.Length);
            while (true)
            {
                var result = string.Empty;
                var response = string.Empty;
                var cmd = sp.ReadTo("\r");
                if (cmd == command)
                {
                    var next = sp.ReadChar();
                    if (IsCarriageReturn(next))
                    {
                        sp.ReadTo("\n");
                        int i = 0;
                        while (true)
                        {
                            result = sp.ReadTo("\r\n");
                            if (i == 7)
                            {
                                break;
                            }
                            sp.ReadTo("\n");
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
                            result = sp.ReadTo("\r\n");
                            if (i == 6)
                            {
                                result = sp.ReadTo("\r");
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
                        sp.ReadTo("\n");
                        sp.ReadTo("\r\n");
                    }
                    continue;
                }
                return (cmd, response, result);
            }
        }
        private static (string Command, string Response, string Result) ExecuteCGMR(SerialPort sp, string command)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            sp.Write(bytes, 0, bytes.Length);
            while (true)
            {
                var result = string.Empty;
                var response = string.Empty;
                var cmd = sp.ReadTo("\r");
                if (cmd == command)
                {
                    var next = sp.ReadChar();
                    if (IsCarriageReturn(next))
                    {
                        sp.ReadTo("\n");
                        int i = 0;
                        while (true)
                        {
                            result = sp.ReadTo("\r\n");
                            if (i == 7)
                            {
                                break;
                            }
                            sp.ReadTo("\n");
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
                            result = sp.ReadTo("\r\n");
                            if (i == 6)
                            {
                                result = sp.ReadTo("\r");
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
                        sp.ReadTo("\n");
                        sp.ReadTo("\r\n");
                    }
                    continue;
                }
                return (cmd, response, result);
            }
        }
        private static (string Command, string Response, string Result) ExecuteATPercentR(SerialPort sp, string command)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            sp.Write(bytes, 0, bytes.Length);
            while (true)
            {
                var result = string.Empty;
                var response = string.Empty;
                var cmd = sp.ReadTo("\r");
                if (cmd == command)
                {
                    var next = sp.ReadChar();
                    if (IsCarriageReturn(next))
                    {
                        sp.ReadTo("\n");
                        int i = 0;
                        while (true)
                        {
                            result = sp.ReadTo("\r\n");
                            if (i == 66)
                            {
                                break;
                            }
                            sp.ReadTo("\n");
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
                            result = sp.ReadTo("\r\n");
                            if (i == 65)
                            {
                                result = sp.ReadTo("\r");
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
                        sp.ReadTo("\n");
                        sp.ReadTo("\r\n");
                    }
                    continue;
                }
                return (cmd, response, result);
            }
        }
        private static (string Command, string Response, string Result) ExecuteResponseWithPayload(SerialPort sp, string command)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            sp.Write(bytes, 0, bytes.Length);
            while (true)
            {
                var result = string.Empty;
                var response = string.Empty;
                var cmd = sp.ReadTo("\r");
                if (cmd == command)
                {
                    var next = sp.ReadChar();
                    if (IsCarriageReturn(next))
                    {
                        sp.ReadTo("\n");
                        response = sp.ReadTo("\r\n\r\n");
                        result = sp.ReadTo("\r\n");
                    }
                    else if (IsAscii(next))
                    {
                        response += Convert.ToChar(next);
                        response += sp.ReadTo("\r\n");
                        result = sp.ReadTo("\r");
                    }
                }
                else
                {
                    if (cmd != "126")
                    {
                        sp.ReadTo("\n");
                        sp.ReadTo("\r\n");
                    }
                    continue;
                }
                return (cmd, response, result);
            }
        }
        private static (string Command, string Response, string Result) ExecuteResponseWithoutPayload(SerialPort sp, string command)
        {
            var bytes = Encoding.ASCII.GetBytes(command + '\r');
            sp.Write(bytes, 0, bytes.Length);
            while (true)
            {
                var result = string.Empty;
                var response = string.Empty;
                var cmd = sp.ReadTo("\r");
                if (cmd == command)
                {
                    var next = sp.ReadChar();
                    if (IsCarriageReturn(next))
                    {
                        sp.ReadTo("\n");
                        result = sp.ReadTo("\r\n");
                    }
                    else if (IsAscii(next))
                    {
                        result += Convert.ToChar(next);
                        result += sp.ReadTo("\r");
                    }
                }
                else
                {
                    if (cmd != "126")
                    {
                        sp.ReadTo("\n");
                        sp.ReadTo("\r\n");
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
        private static bool IsLineFeed(int c) => (uint)c == '\x000a';
        private static bool IsCarriageReturn(int c) => (uint)c == '\x000d';
        #endregion

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
                    SerialPort?.Dispose();
                    SemaphoreSlim?.Dispose();
                    if (!Connected.IsCompleted)
                    {
                        ConnectedSource.TrySetCanceled();
                    }
                    if (!Disconnected.IsCompleted)
                    {
                        if (Connected.IsCompleted)
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