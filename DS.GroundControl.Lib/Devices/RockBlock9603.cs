using System.IO.Ports;
using System.Text;
using System.Threading.Channels;
using DS.GroundControl.Lib.Extensions;
using DS.GroundControl.Lib.Exceptions;

namespace DS.GroundControl.Lib.Devices
{
    public class RockBlock9603 : IRockBlock9603
    {
        private SerialPort SerialPort { get; set; }
        private Channel<byte[]> Input { get; }
        private Channel<(string Command, string Response, string Result)> Output { get; }
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
            Input = Channel.CreateUnbounded<byte[]>();
            Output = Channel.CreateUnbounded<(string Command, string Response, string Result)>();
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
                using (SerialPort = await ConnectAsync())
                {
                    _ = RunningSource.CancelAsync();
                    while (true)
                    {
                        var input = await Input.Reader.ReadAsync(Canceled);
                        var output = await WriteToRockBlockAsync(SerialPort, input);
                        await Output.Writer.WriteAsync(output);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception)
            {
                _ = FaultedSource.CancelAsync();
            }
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
        private static async Task<SerialPort> ConnectAsync()
        {
            foreach (var portName in SerialPort.GetPortNames())
            {
                try
                {
                    var serialPort = new SerialPort()
                    {
                        PortName = portName,
                        BaudRate = 19200,
                        DataBits = 8,
                        Parity = Parity.None,
                        StopBits = StopBits.One
                    };
                    serialPort.Open();
                    if (await IsConnectedAsync(serialPort))
                    {
                        return serialPort;
                    }
                    serialPort.Dispose();
                }
                catch { }
            }
            throw new DeviceNotFoundException();
        }
        private static async Task<bool> IsConnectedAsync(SerialPort serialPort)
        {
            try
            {
                var at = await RockBlockAsync(serialPort, Encoding.ASCII.GetBytes("AT\r"));
                if (at is { Command: "AT", Response: "", Result: "OK" or "0" })
                {
                    return true;
                }
            }
            catch { }
            return false;
        }
        private static async Task<(string Command, string Response, string Result)> RockBlockAsync(SerialPort serialPort, byte[] input)
        {
            static Task<(string Command, string Response, string Result)> RockBlockAsync(SerialPort serialPort, byte[] input)
            {
                return Task.Run(() =>
                {
                    static bool IsAscii(int c) => (uint)c <= '\x007f';
                    static bool IsLineFeed(int c) => (uint)c == '\x000a';
                    static bool IsCarriageReturn(int c) => (uint)c == '\x000d';

                    var command = string.Empty;
                    var response = string.Empty;
                    var result = string.Empty;

                    if (input != null)
                    {
                        serialPort.Write(input, 0, input.Length);
                    }

                    command += Convert.ToChar(serialPort.ReadChar());
                    command += Convert.ToChar(serialPort.ReadChar());

                    #region AT Commands
                    if (command.StartsWith("AT", StringComparison.OrdinalIgnoreCase))
                    {
                        command += serialPort.ReadTo("\r");
                        switch (command.ToUpper())
                        {
                            case "AT+CCLK?":
                                {
                                    var next = serialPort.ReadChar();
                                    if (IsCarriageReturn(next))
                                    {
                                        serialPort.ReadTo("\n");
                                        response = serialPort.ReadTo("\n\r\n");
                                        serialPort.ReadTo("\r\n");
                                        result = serialPort.ReadTo("\r\n");
                                    }
                                    else if (IsAscii(next))
                                    {
                                        response += Convert.ToChar(next);
                                        response += serialPort.ReadTo("\n\r\n");
                                        result = serialPort.ReadTo("\r");
                                    }
                                    break;
                                }
                            case "AT+SBDRB":
                                {
                                    var len = new byte[2];
                                    serialPort.ReadExactly(len, 0, 2);
                                    var msg = new byte[len[0] + len[1]];
                                    serialPort.ReadExactly(msg, 0, msg.Length);
                                    var cks = new byte[2];
                                    serialPort.ReadExactly(cks, 0, 2);

                                    var next = serialPort.ReadChar();
                                    if (IsCarriageReturn(next))
                                    {
                                        serialPort.ReadTo("\n");
                                        result = serialPort.ReadTo("\r\n");
                                        response = Encoding.ASCII.GetString(len.Concat(msg).Concat(cks).ToArray());
                                    }
                                    else if (IsAscii(next))
                                    {
                                        result += Convert.ToChar(next);
                                        result += serialPort.ReadTo("\r");
                                        response = Encoding.ASCII.GetString(len.Concat(msg).Concat(cks).ToArray());
                                    }
                                    break;
                                }
                            case "AT+SBDRT":
                                {
                                    var next = serialPort.ReadChar();
                                    if (IsCarriageReturn(next))
                                    {
                                        serialPort.ReadTo("\n");
                                        response += serialPort.ReadTo("\r\n");
                                        response += serialPort.ReadTo("\r\n");
                                        result = serialPort.ReadTo("\r\n");
                                    }
                                    else if (IsAscii(next))
                                    {
                                        response += Convert.ToChar(next);
                                        response += serialPort.ReadTo("\r\n");
                                        response += serialPort.ReadTo("\r");
                                        result = response[response.Length - 1].ToString();
                                        response = response.Remove(response.Length - 1);
                                    }
                                    break;
                                }
                            case "AT+SBDWT":
                            case var str when str.StartsWith("AT+SBDWB="):
                                {
                                    var next = serialPort.ReadChar();
                                    if (IsCarriageReturn(next))
                                    {
                                        serialPort.ReadTo("\n");
                                        response = serialPort.ReadTo("\r\n");
                                    }
                                    else if (IsAscii(next))
                                    {
                                        response += Convert.ToChar(next);
                                        response += serialPort.ReadTo("\r\n");
                                    }
                                    break;
                                }
                            case "AT&V":
                                {
                                    var next = serialPort.ReadChar();
                                    if (IsCarriageReturn(next))
                                    {
                                        serialPort.ReadTo("\n");
                                        int i = 0;
                                        while (true)
                                        {
                                            result = serialPort.ReadTo("\r\n");
                                            if (i == 10)
                                            {
                                                break;
                                            }
                                            serialPort.ReadTo("\n");
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
                                            result = serialPort.ReadTo("\r\n");
                                            if (i == 9)
                                            {
                                                result = serialPort.ReadTo("\r");
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
                                    var next = serialPort.ReadChar();
                                    if (IsCarriageReturn(next))
                                    {
                                        serialPort.ReadTo("\n");
                                        int i = 0;
                                        while (true)
                                        {
                                            result = serialPort.ReadTo("\r\n");
                                            if (i == 7)
                                            {
                                                break;
                                            }
                                            serialPort.ReadTo("\n");
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
                                            result = serialPort.ReadTo("\r\n");
                                            if (i == 6)
                                            {
                                                result = serialPort.ReadTo("\r");
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
                                    var next = serialPort.ReadChar();
                                    if (IsCarriageReturn(next))
                                    {
                                        serialPort.ReadTo("\n");
                                        int i = 0;
                                        while (true)
                                        {
                                            result = serialPort.ReadTo("\r\n");
                                            if (i == 66)
                                            {
                                                break;
                                            }
                                            serialPort.ReadTo("\n");
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
                                            result = serialPort.ReadTo("\r\n");
                                            if (i == 65)
                                            {
                                                result = serialPort.ReadTo("\r");
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
                                    var next = serialPort.ReadChar();
                                    if (IsCarriageReturn(next))
                                    {
                                        serialPort.ReadTo("\n");
                                        response = serialPort.ReadTo("\r\n\r\n");
                                        result = serialPort.ReadTo("\r\n");
                                    }
                                    else if (IsAscii(next))
                                    {
                                        response += Convert.ToChar(next);
                                        response += serialPort.ReadTo("\r\n");
                                        result = serialPort.ReadTo("\r");
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
                                    var next = serialPort.ReadChar();
                                    if (IsCarriageReturn(next))
                                    {
                                        serialPort.ReadTo("\n");
                                        result = serialPort.ReadTo("\r\n");
                                    }
                                    else if (IsAscii(next))
                                    {
                                        result += Convert.ToChar(next);
                                        result += serialPort.ReadTo("\r");
                                    }
                                    break;
                                }
                        }
                    }
                    #endregion

                    #region READY SBDRING
                    else if (IsCarriageReturn(command[0]) && IsLineFeed(command[1]))
                    {
                        response = serialPort.ReadTo("\r\n");
                        if (response != "SBDRING")
                        {
                            serialPort.ReadTo("\r\n");
                            result = serialPort.ReadTo("\r\n");
                        }
                        command = string.Empty;
                    }
                    #endregion

                    #region READY
                    else if (IsCarriageReturn(command[1]))
                    {
                        response += command[0];
                        serialPort.ReadTo("\n");
                        result = serialPort.ReadTo("\r");
                        command = string.Empty;
                    }
                    #endregion

                    #region READY SBDRING
                    else
                    {
                        command += serialPort.ReadTo("\r");
                        if (command == "126")
                        {
                            response = command;
                            command = string.Empty;
                        }
                        else
                        {
                            serialPort.ReadTo("\n");
                            if (input.Length - 1 == command.Length)
                            {
                                response = serialPort.ReadTo("\r\n");
                                serialPort.ReadTo("\r\n");
                                result = serialPort.ReadTo("\r\n");
                            }
                            else
                            {
                                response = command[command.Length - 1].ToString();
                                command = command.Remove(command.Length - 1, 1);
                                result = serialPort.ReadTo("\r");
                            }
                        }
                    }
                    #endregion

                    return (command, response, result);
                });
            }

            var output = RockBlockAsync(serialPort, input);
            await output.ThrowOnTimeoutAsync(TimeSpan.FromMinutes(1));
            return await output;
        }
        private static async Task<(string Command, string Response, string Result)> WriteToRockBlockAsync(SerialPort serialPort, byte[] input)
        {
            static bool IsRingAlert(string response)
            {
                return response is "SBDRING" or "126";
            }

            while (true)
            {
                var output = await RockBlockAsync(serialPort, input);
                if (IsRingAlert(output.Response))
                {
                    input = null;
                    continue;
                }
                return output;
            }
        }
        public async Task<(string Command, string Response, string Result)> WriteWithCarriageReturnAsync(string input)
        {
            var arr = Encoding.ASCII.GetBytes(input + "\r");
            await Input.Writer.WriteAsync(arr, Stopped);
            return await Output.Reader.ReadAsync(Stopped);
        }
        public async Task<(string Command, string Response, string Result)> WriteWithChecksumAsync(string input)
        {
            var cks = CalculateChecksum(input);
            var arr = Encoding.ASCII.GetBytes(input).Concat(cks).ToArray();
            await Input.Writer.WriteAsync(arr, Stopped);
            return await Output.Reader.ReadAsync(Stopped);
        }
        private static string CalculateHexadecimalSummation(string input)
        {
            var hex = Convert.ToHexString(Encoding.ASCII.GetBytes(input));

            int result = 0;
            for (int i = 0; i < hex.Length; i += 2)
            {
                var value = hex[i].ToString() + hex[i + 1].ToString();
                result += Convert.ToInt32(value, 16);
            }

            var sum = result.ToString("X");
            if (sum.Length % 2 == 1)
            {
                sum = "0" + sum;
            }

            return sum;
        }
        private static byte[] CalculateChecksum(string input)
        {
            var cks = new byte[2];
            var sum = CalculateHexadecimalSummation(input);

            for (int i = 0, j = 0; i < sum.Length && i < 4; i += 2, j++)
            {
                var value = sum[i].ToString() + sum[i + 1].ToString();
                cks[j] = Convert.ToByte(value, 16);
            }

            return cks;
        }       
    }
}