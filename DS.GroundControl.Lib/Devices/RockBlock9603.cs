using System.IO.Ports;
using System.Text;
using System.Threading.Channels;
using DS.GroundControl.Lib.Extensions;

namespace DS.GroundControl.Lib.Devices
{
    public class RockBlock9603 : IRockBlock9603
    {
        private SerialPort SerialPort { get; set; }
        private Channel<byte[]> ModuleInput { get; }
        private Channel<(string Command, string Response, string Result)> ModuleOutput { get; }
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

        public RockBlock9603()
        {
            ModuleInput = Channel.CreateUnbounded<byte[]>();
            ModuleOutput = Channel.CreateUnbounded<(string Command, string Response, string Result)>();
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
            try
            {
                if (await ConnectModuleAsync())
                {
                    _ = RunningSource.CancelAsync();
                    while (await ModuleInput.Reader.WaitToReadAsync(Canceled))
                    {
                        var input = await ModuleInput.Reader.ReadAsync();
                        var module = await ModuleManagerAsync(input);
                        if (module.Faulted)
                        {
                            _ = FaultedSource.CancelAsync();
                            break;
                        }
                        await ModuleOutput.Writer.WriteAsync(module.Output);
                    }
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
            SerialPort?.Dispose();
            CanceledSource.Dispose();
            StartedSource.Dispose();
            RunningSource.Dispose();
            StoppedSource.Dispose();
            FaultedSource.Dispose();
        }
        private async Task<bool> ConnectModuleAsync()
        {
            foreach (var name in SerialPort.GetPortNames())
            {
                if (await ConnectModuleAsync(name))
                {
                    return true;
                }
            }
            return false;
        }
        private async Task<bool> ConnectModuleAsync(string portName)
        {
            try
            {
                SerialPort = new SerialPort()
                {
                    PortName = portName,
                    BaudRate = 19200,
                    DataBits = 8,
                    Parity = Parity.None,
                    StopBits = StopBits.One
                };
                SerialPort.Open();
                if (await ModuleManagerAsync(Encoding.ASCII.GetBytes("AT\r")) is { Output: { Command: "AT", Result: "OK" or "0" } })
                {
                    return true;
                }              
            }
            catch { }
            SerialPort?.Dispose();
            SerialPort = null;
            return false;
        }
        private async Task<(bool Faulted, (string Command, string Response, string Result) Output)> ModuleManagerAsync(byte[] input)
        {
            static bool IsRingAlert(string response)
            {
                return response is "SBDRING" or "126";
            }

            var ip = input;
            while (await ModuleAsync(ip) is { Faulted: false } module)
            {
                if (IsRingAlert(module.Output.Response))
                {
                    ip = null;
                    continue;
                }
                return module;
            }
            return (Faulted: true, Output: default);
        }
        private async Task<(bool Faulted, (string Command, string Response, string Result) Output)> ModuleAsync(byte[] input)
        {
            var module = Task.Run(() =>
            {
                static bool IsAscii(int c) => (uint)c <= '\x007f';
                static bool IsLineFeed(int c) => (uint)c == '\x000a';
                static bool IsCarriageReturn(int c) => (uint)c == '\x000d';

                var command = string.Empty;
                var response = string.Empty;
                var result = string.Empty;

                if (input != null)
                {
                    SerialPort.Write(input, 0, input.Length);
                }

                command += Convert.ToChar(SerialPort.ReadChar());
                command += Convert.ToChar(SerialPort.ReadChar());

                #region AT Commands
                if (command.StartsWith("AT", StringComparison.OrdinalIgnoreCase))
                {
                    command += SerialPort.ReadTo("\r");
                    switch (command.ToUpper())
                    {
                        case "AT+CCLK?":
                            {
                                var next = SerialPort.ReadChar();
                                if (IsCarriageReturn(next))
                                {
                                    SerialPort.ReadTo("\n");
                                    response = SerialPort.ReadTo("\n\r\n");
                                    SerialPort.ReadTo("\r\n");
                                    result = SerialPort.ReadTo("\r\n");
                                }
                                else if (IsAscii(next))
                                {
                                    response += Convert.ToChar(next);
                                    response += SerialPort.ReadTo("\n\r\n");
                                    result = SerialPort.ReadTo("\r");
                                }
                                break;
                            }
                        case "AT+SBDRB":
                            {
                                var len = new byte[2];
                                SerialPort.ReadExactly(len, 0, 2);
                                var msg = new byte[len[0] + len[1]];
                                SerialPort.ReadExactly(msg, 0, msg.Length);
                                var cks = new byte[2];
                                SerialPort.ReadExactly(cks, 0, 2);

                                var next = SerialPort.ReadChar();
                                if (IsCarriageReturn(next))
                                {
                                    SerialPort.ReadTo("\n");
                                    result = SerialPort.ReadTo("\r\n");
                                    response = Encoding.UTF8.GetString(len.Concat(msg).Concat(cks).ToArray());
                                }
                                else if (IsAscii(next))
                                {
                                    result += Convert.ToChar(next);
                                    result += SerialPort.ReadTo("\r");
                                    response = Encoding.UTF8.GetString(len.Concat(msg).Concat(cks).ToArray());
                                }                             
                                break;
                            }
                        case "AT+SBDRT":
                            {
                                var next = SerialPort.ReadChar();
                                if (IsCarriageReturn(next))
                                {
                                    SerialPort.ReadTo("\n");
                                    response += SerialPort.ReadTo("\r\n");
                                    response += SerialPort.ReadTo("\r\n");
                                    result = SerialPort.ReadTo("\r\n");
                                }
                                else if (IsAscii(next))
                                {
                                    response += Convert.ToChar(next);
                                    response += SerialPort.ReadTo("\r\n");
                                    response += SerialPort.ReadTo("\r");
                                    result = response[response.Length - 1].ToString();
                                    response = response.Remove(response.Length - 1);
                                }
                                break;
                            }
                        case "AT+SBDWT":
                        case var str when str.StartsWith("AT+SBDWB="):
                            {
                                var next = SerialPort.ReadChar();
                                if (IsCarriageReturn(next))
                                {
                                    SerialPort.ReadTo("\n");
                                    response = SerialPort.ReadTo("\r\n");
                                }
                                else if (IsAscii(next))
                                {
                                    response += Convert.ToChar(next);
                                    response += SerialPort.ReadTo("\r\n");
                                }
                                break;
                            }                        
                        case "AT&V":
                            {
                                var next = SerialPort.ReadChar();
                                if (IsCarriageReturn(next))
                                {
                                    SerialPort.ReadTo("\n");
                                    int i = 0;
                                    while (true)
                                    {
                                        result = SerialPort.ReadTo("\r\n");
                                        if (i == 10)
                                        {
                                            break;
                                        }
                                        SerialPort.ReadTo("\n");
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
                                        result = SerialPort.ReadTo("\r\n");
                                        if (i == 9)
                                        {
                                            result = SerialPort.ReadTo("\r");
                                            break;
                                        }
                                        response += result;
                                        i++;
                                    }
                                }
                                break;
                            }
                        case "AT+GMR":
                        case "AT+CGMR":
                            {
                                var next = SerialPort.ReadChar();
                                if (IsCarriageReturn(next))
                                {
                                    SerialPort.ReadTo("\n");
                                    int i = 0;
                                    while (true)
                                    {
                                        result = SerialPort.ReadTo("\r\n");
                                        if (i == 7)
                                        {
                                            break;
                                        }
                                        SerialPort.ReadTo("\n");
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
                                        result = SerialPort.ReadTo("\r\n");
                                        if (i == 6)
                                        {
                                            result = SerialPort.ReadTo("\r");
                                            break;
                                        }
                                        response += result;
                                        i++;
                                    }
                                }
                                break;
                            }
                        case "AT%R":
                            {
                                var next = SerialPort.ReadChar();
                                if (IsCarriageReturn(next))
                                {
                                    SerialPort.ReadTo("\n");
                                    int i = 0;
                                    while (true)
                                    {
                                        result = SerialPort.ReadTo("\r\n");
                                        if (i == 66)
                                        {
                                            break;
                                        }
                                        SerialPort.ReadTo("\n");
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
                                        result = SerialPort.ReadTo("\r\n");
                                        if (i == 65)
                                        {
                                            result = SerialPort.ReadTo("\r");
                                            break;
                                        }
                                        response += result;
                                        i++;
                                    }
                                }
                                break;
                            }
                        case "AT+CGMI":
                        case "AT+CGMM":
                        case "AT+CGSN":
                        case "AT+CIER=?":
                        case "AT+CIER?":
                        case "AT+CRIS":
                        case "AT+CRISX":
                        case "AT+CSQ":
                        case "AT+CSQ=?":
                        case "AT+CSQF":
                        case "AT+CULK?":
                        case "AT+GMI":
                        case "AT+GMM":
                        case "AT+GSN":
                        case "AT+IPR=?":
                        case "AT+IPR?":
                        case "AT+SBDLOE":
                        case "AT+SBDAREG=?":
                        case "AT+SBDAREG?":
                        case "AT+SBDC":
                        case "AT+SBDD0":
                        case "AT+SBDD1":
                        case "AT+SBDD2":
                        case "AT+SBDDSC?":
                        case "AT+SBDGW":
                        case "AT+SBDGWN":
                        case "AT+SBDI":
                        case "AT+SBDIX":
                        case "AT+SBDIXA":
                        case "AT+SBDMTA?":
                        case "AT+SBDMTA=?":
                        case "AT+SBDREG?":
                        case "AT+SBDS":
                        case "AT+SBDST?":
                        case "AT+SBDSX":
                        case "AT+SBDTC":
                        case "AT-MSGEOS":
                        case "AT-MSGEO":
                        case "AT-MSSTM":
                        case "ATI0":
                        case "ATI1":
                        case "ATI2":
                        case "ATI3":
                        case "ATI4":
                        case "ATI5":
                        case "ATI6":
                        case "ATI7":
                            {
                                var next = SerialPort.ReadChar();
                                if (IsCarriageReturn(next))
                                {
                                    SerialPort.ReadTo("\n");
                                    response = SerialPort.ReadTo("\r\n\r\n");
                                    result = SerialPort.ReadTo("\r\n");
                                }
                                else if (IsAscii(next))
                                {
                                    response += Convert.ToChar(next);
                                    response += SerialPort.ReadTo("\r\n");
                                    result = SerialPort.ReadTo("\r");
                                }
                                break;
                            }
                        case "AT&Y0":
                        case "AT&K0":
                        case "AT&K3":
                        case "AT*R1":
                        case "AT*F":
                        case "AT+SBDMTA=0":
                        case "AT+SBDMTA=1":
                        case "ATE1":
                        case "ATQ0":
                        case "AT":
                        case "ATV1":
                        case "ATV0":
                        case var str when str.StartsWith("AT+SBDWT="):
                            {
                                var next = SerialPort.ReadChar();
                                if (IsCarriageReturn(next))
                                {
                                    SerialPort.ReadTo("\n");
                                    result = SerialPort.ReadTo("\r\n");
                                }
                                else if (IsAscii(next))
                                {
                                    result += Convert.ToChar(next);
                                    result += SerialPort.ReadTo("\r");
                                }
                                break;
                            }
                    }
                }
                #endregion

                #region READY SBDRING
                else if (IsCarriageReturn(command[0]) && IsLineFeed(command[1]))
                {
                    response = SerialPort.ReadTo("\r\n");
                    if (response == "SBDRING")
                    {
                        
                    }
                    else
                    {
                        SerialPort.ReadTo("\r\n");
                        result = SerialPort.ReadTo("\r\n");
                    }
                    command = string.Empty;
                }
                #endregion

                #region READY
                else if (IsCarriageReturn(command[1]))
                {
                    response += command[0];
                    SerialPort.ReadTo("\n");
                    result = SerialPort.ReadTo("\r");
                    command = string.Empty;
                }
                #endregion

                #region READY SBDRING
                else
                {
                    command += SerialPort.ReadTo("\r");
                    if (command == "126")
                    {
                        response = command;
                        command = string.Empty;
                    }
                    else
                    {
                        SerialPort.ReadTo("\n");
                        if (input.Length - 1 == command.Length)
                        {
                            response = SerialPort.ReadTo("\r\n");
                            SerialPort.ReadTo("\r\n");
                            result = SerialPort.ReadTo("\r\n");
                        }
                        else
                        {
                            response = command[command.Length - 1].ToString();
                            command = command.Remove(command.Length - 1, 1);
                            result = SerialPort.ReadTo("\r");
                        }
                    }
                }
                #endregion

                return (command, response, result);
            });
            if (await module.TimeoutAfterAsync(TimeSpan.FromMinutes(1)))
            {
                if (module.IsCompletedSuccessfully)
                {
                    return (Faulted: false, Output: await module);
                }
            }
            SerialPort.Dispose();
            return (Faulted: true, Output: default);
        }
        private async Task<(string Command, string Response, string Result)> WriteAsync(byte[] input, byte[] delimiter)
        {
            var arr = new byte[input.Length + delimiter.Length];
            Buffer.BlockCopy(input, 0, arr, 0, input.Length);
            Buffer.BlockCopy(delimiter, 0, arr, input.Length, delimiter.Length);
            await ModuleInput.Writer.WriteAsync(arr, Stopped);
            return await ModuleOutput.Reader.ReadAsync(Stopped).AsTask();
        }
        public async Task<(string Command, string Response, string Result)> WriteWithCarriageReturnAsync(string input)
        {
            var delimiter = new byte[] { 0xD };
            return await WriteAsync(Encoding.ASCII.GetBytes(input), delimiter);
        }
        public async Task<(string Command, string Response, string Result)> WriteWithChecksumAsync(string input)
        {
            var delimiter = CalculateChecksum(input);
            return await WriteAsync(Encoding.ASCII.GetBytes(input), delimiter);
        }    
        private static string CalculateHexadecimalSummation(string hex)
        {
            int sum = 0;
            for (int i = 0; i < hex.Length; i += 2)
            {
                var value = hex[i].ToString() + hex[i + 1].ToString();
                sum += Convert.ToInt32(value, 16);
            }

            var result = sum.ToString("X");
            if (result.Length % 2 == 1)
            {
                result = "0" + result;
            }

            return result;
        }
        private static byte[] CalculateChecksum(string input)
        {
            var cks = new byte[2];
            var hex = Convert.ToHexString(Encoding.ASCII.GetBytes(input));
            var sum = CalculateHexadecimalSummation(hex);

            for (int i = 0, j = 0; i < sum.Length && i < 4; i += 2, j++)
            {
                var value = sum[i].ToString() + sum[i + 1].ToString();
                cks[j] = Convert.ToByte(value, 16);
            }

            return cks;
        }
    }
}