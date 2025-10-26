using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.CommandLine;
using System.IO.Ports;
using DS.GroundControl.Lib.Devices;
using DS.GroundControl.Lib.Factories;

namespace DS.GroundControl.RockBlock9603.CommandLine
{
    public class Application : IApplication
    {
        private IRockBlock9603Factory RockBlock9603Factory { get; }
        private IRockBlock9603 RockBlock9603 { get; set; }
        private JsonSerializerOptions JsonSerializerOptions { get; }

        public Application(IRockBlock9603Factory rockBlock9603Factory)
        {
            RockBlock9603Factory = rockBlock9603Factory;
            JsonSerializerOptions = new JsonSerializerOptions()
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            };
        }

        public async Task CommandLineAsync()
        {
            var rootCommand = new RootCommand("RockBlock 9603 satellite transmitter command-line");

            var startOption = new Option<bool>(
                name: "--start",
                description: "Starts the RockBlock 9603 satellite transmitter.")
            {
                AllowMultipleArgumentsPerToken = false
            };

            var stopOption = new Option<bool>(
                name: "--stop",
                description: "Stops the RockBlock 9603 satellite transmitter.")
            {
                AllowMultipleArgumentsPerToken = false
            };

            var statusOption = new Option<bool>(
                name: "--status",
                description: "Returns the RockBlock 9603 satellite transmitter status.")
            {
                AllowMultipleArgumentsPerToken = false
            };

            var shutdownOption = new Option<bool>(
                name: "--shutdown",
                description: "Shuts down the application.")
            {
                AllowMultipleArgumentsPerToken = false
            };

            var timeOption = new Option<bool>(
                name: "--time",
                description: "Returns the Iridium network time.")
            {
                AllowMultipleArgumentsPerToken = false
            };

            var xOption = new Option<string>(
                name: "--x",
                description: "Execute a raw AT command on the Iridium 9603 modem.")
            {
                Arity = ArgumentArity.ExactlyOne,
                AllowMultipleArgumentsPerToken = false
            };

            var xTextOption = new Option<string>(
                name: "--x-text",
                description: "Execute in ready-state TEXT mode (adds CR).")
            {
                Arity = ArgumentArity.ExactlyOne,
                AllowMultipleArgumentsPerToken = false
            };

            var xBinaryOption = new Option<string>(
                name: "--x-binary",
                description: "Execute in ready-state RAW BINARY mode (adds checksum).")
            {
                Arity = ArgumentArity.ExactlyOne,
                AllowMultipleArgumentsPerToken = false
            };

            var xBase64Option = new Option<string>(
                name: "--x-base64",
                description: "Execute in ready-state BASE64 mode (adds checksum).")
            {
                Arity = ArgumentArity.ExactlyOne,
                AllowMultipleArgumentsPerToken = false
            };

            rootCommand.AddOption(startOption);
            rootCommand.AddOption(stopOption);
            rootCommand.AddOption(statusOption);
            rootCommand.AddOption(shutdownOption);
            rootCommand.AddOption(timeOption);
            rootCommand.AddOption(xOption);
            rootCommand.AddOption(xTextOption);
            rootCommand.AddOption(xBinaryOption);
            rootCommand.AddOption(xBase64Option);
            rootCommand.SetHandler(async (context) =>
            {
                var start = context.ParseResult.GetValueForOption(startOption);
                var stop = context.ParseResult.GetValueForOption(stopOption);
                var shutdown = context.ParseResult.GetValueForOption(shutdownOption);
                var time = context.ParseResult.GetValueForOption(timeOption);
                var execute = context.ParseResult.GetValueForOption(xOption);
                var xText = context.ParseResult.GetValueForOption(xTextOption);
                var xBinary = context.ParseResult.GetValueForOption(xBinaryOption);  
                var xBase64 = context.ParseResult.GetValueForOption(xBase64Option);
                var status = context.ParseResult.GetValueForOption(statusOption);

                if (start) await Console.Out.WriteLineAsync(await RockBlockStartAsync());
                else if (stop) await Console.Out.WriteLineAsync(await RockBlockStopAsync());
                else if (shutdown)
                {
                    await Console.Out.WriteLineAsync(await RockBlockStopAsync());
                    Environment.Exit(0);
                }
                else if (time) await Console.Out.WriteLineAsync(await RockBlockTimeAsync());
                else if (execute != null) await Console.Out.WriteLineAsync(await ExecuteCommandAsync(execute));
                else if (xText != null) await Console.Out.WriteLineAsync(await ExecuteReadyStateTextCommandAsync(xText));
                else if (xBinary != null) await Console.Out.WriteLineAsync(await ExecuteReadyStateBinaryCommandAsync(xBinary));
                else if (xBase64 != null) await Console.Out.WriteLineAsync(await ExecuteReadyStateBase64CommandAsync(xBase64));
                else if (status) await Console.Out.WriteLineAsync(RockBlockStatus());
            });
            rootCommand.AddValidator(result =>
            {
                if (result.Children.Count(s => s.Symbol == startOption || s.Symbol == stopOption || s.Symbol == shutdownOption ||s.Symbol == timeOption ||
                s.Symbol == xOption || s.Symbol == xTextOption || s.Symbol == xBinaryOption || s.Symbol == xBase64Option || s.Symbol == statusOption) > 1)
                {
                    result.ErrorMessage = $"Option '--{result.Children[0].Symbol.Name}' is a mutually exclusive option.";
                }
            });

            while (true)
            {
                await Console.Out.WriteAsync("> ");
                var line = await Console.In.ReadLineAsync();
                if (line == null) break;
                await rootCommand.InvokeAsync(line);
            }
        }
        private async Task<string> RockBlockStartAsync()
        {
            try
            {
                if (RockBlock9603 == null)
                {
                    RockBlock9603 = RockBlock9603Factory.Create();
                    await RockBlock9603.ConnectAsync(19200, 8, Parity.None, StopBits.One);
                }
            }
            catch { }
            return RockBlockStatus();
        }
        private async Task<string> RockBlockStopAsync()
        {
            var rb = RockBlock9603;
            if (RockBlock9603 != null)
            {
                await RockBlock9603.DisconnectAsync();
                RockBlock9603.Dispose();
                RockBlock9603 = null;
            }
            return ToJsonString(new
            {
                ISU = new
                {
                    Connected = rb?.Connected.IsCompletedSuccessfully,
                    Disconnected = rb?.Disconnected.IsCompletedSuccessfully,
                    Faulted = rb?.Faulted.IsCompletedSuccessfully
                }
            });
        }
        private async Task<string> ExecuteCommandAsync(string command)
        {
            try
            {
                if (IsRockBlockConnected())
                {
                    var output = await RockBlock9603.ExecuteCommandAsync(command);
                    return ToJsonString(new
                    {
                        ISU = new
                        {
                            output.Command,
                            output.Response,
                            output.Result
                        }
                    });
                }
            }
            catch { }
            return RockBlockStatus();
        }
        private async Task<string> ExecuteReadyStateTextCommandAsync(string command)
        {
            try
            {
                if (IsRockBlockConnected())
                {
                    var output = await RockBlock9603.ExecuteReadyStateTextCommandAsync(command);
                    return ToJsonString(new
                    {
                        ISU = new
                        {
                            output.Command,
                            output.Response,
                            output.Result
                        }
                    });
                }
            }
            catch { }
            return RockBlockStatus();
        }
        private async Task<string> ExecuteReadyStateBinaryCommandAsync(string command)
        {
            try
            {
                if (IsRockBlockConnected())
                {
                    var base64 = Convert.ToBase64String(Encoding.ASCII.GetBytes(command));
                    var output = await RockBlock9603.ExecuteReadyStateBase64CommandAsync(base64);
                    return ToJsonString(new
                    {
                        ISU = new
                        {
                            output.Command,
                            output.Response,
                            output.Result
                        }
                    });
                }
            }
            catch { }
            return RockBlockStatus();
        }
        private async Task<string> ExecuteReadyStateBase64CommandAsync(string command)
        {
            try
            {
                if (IsRockBlockConnected())
                {
                    var output = await RockBlock9603.ExecuteReadyStateBase64CommandAsync(command);
                    return ToJsonString(new
                    {
                        ISU = new
                        {
                            output.Command,
                            output.Response,
                            output.Result
                        }
                    });
                }
            }
            catch { }
            return RockBlockStatus();
        }
        private async Task<string> RockBlockTimeAsync()
        {
            try
            {
                if (IsRockBlockConnected())
                {
                    var output = await RockBlock9603.ExecuteCommandAsync("AT-MSSTM");

                    var hex = output.Response.Split("-MSSTM: ").Last();
                    var time = hex.All(char.IsAsciiHexDigit) 
                        ? CalculateIridiumTime(hex) 
                        : hex;

                    return ToJsonString(new
                    {
                        ISU = new
                        {
                            Time = time
                        }
                    });
                }
            }
            catch { }
            return RockBlockStatus();
        }
        private string RockBlockStatus()
        {
            return ToJsonString(new
            {
                ISU = new
                {
                    Connected = RockBlock9603?.Connected.IsCompletedSuccessfully,
                    Disconnected = RockBlock9603?.Disconnected.IsCompletedSuccessfully,
                    Faulted = RockBlock9603?.Faulted.IsCompletedSuccessfully
                }
            });
        }
        private bool IsRockBlockConnected()
        {
            return RockBlock9603 != null && RockBlock9603.Connected.IsCompletedSuccessfully &&
                !RockBlock9603.Disconnected.IsCompleted && !RockBlock9603.Faulted.IsCompleted;
        }  
        private string ToJsonString<T>(T value) => JsonSerializer.Serialize(value, JsonSerializerOptions);
        private static string CalculateIridiumTime(string hex)
        {
            var era2Epoch = new DateTime(2014, 5, 11, 14, 23, 55);
            var intervalCount = Convert.ToInt64(hex, 16);
            var totalMilliseconds = intervalCount * 90;
            var timeDelta = TimeSpan.FromMilliseconds(totalMilliseconds);
            var decodedTime = era2Epoch + timeDelta;
            return decodedTime.ToString();
        }
    }
}

#region NUMERIC
//test , AT+SBDWB=4 , --x-base64 dGVzdA==

// %R {"ISU":{"Command":"AT%R
// &Dn 
// &Fn
// &V {"ISU":{"Command":"AT&V
// &Wn
// &Yn {"ISU":{"Command":"AT&Y0
// *Rn {"ISU":{"Command":"AT*R1
// +CCLK {"ISU":{"Command":"AT+CCLK?
// +CGMI {"ISU":{"Command":"AT+CGMI
// +CGMM {"ISU":{"Command":"AT+CGMM
// +CGMR {"ISU":{"Command":"AT+CGMR
// +CGSN {"ISU":{"Command":"AT+CGSN
// +CIER {"ISU":{"Command":"AT+CIER=?
// +CIER {"ISU":{"Command":"AT+CIER?
// +CRIS {"ISU":{"Command":"AT+CRIS
// +CRISX {"ISU":{"Command":"AT+CRISX
// +CSQ {"ISU":{"Command":"AT+CSQ
// +CSQ {"ISU":{"Command":"AT+CSQ=?
// +CSQF {"ISU":{"Command":"AT+CSQF
// +CULK {"ISU":{"Command":"AT+CULK?
// +GEMON 
// +GMI {"ISU":{"Command":"AT+GMI
// +GMM {"ISU":{"Command":"AT+GMM
// +GMR {"ISU":{"Command":"AT+GMR
// +GSN {"ISU":{"Command":"AT+GSN
// +IPR {"ISU":{"Command":"AT+IPR=?
// +IPR {"ISU":{"Command":"AT+IPR?
// +SBDLOE {"ISU":{"Command":"AT+SBDLOE
// +SBDAREG {"ISU":{"Command":"AT+SBDAREG=?
// +SBDAREG {"ISU":{"Command":"AT+SBDAREG?
// +SBDC {"ISU":{"Command":"AT+SBDC
// +SBDD {"ISU":{"Command":"AT+SBDD0
// +SBDD {"ISU":{"Command":"AT+SBDD1
// +SBDD {"ISU":{"Command":"AT+SBDD2
// +SBDDET 
// +SBDDSC {"ISU":{"Command":"AT+SBDDSC?
// +SBDGW {"ISU":{"Command":"AT+SBDGW
// +SBDGWN {"ISU":{"Command":"AT+SBDGWN
// +SBDI {"ISU":{"Command":"AT+SBDI
// +SBDIX 
// +SBDIXA
// +SBDMTA {"ISU":{"Command":"AT+SBDMTA=0
// +SBDMTA {"ISU":{"Command":"AT+SBDMTA=1
// +SBDMTA {"ISU":{"Command":"AT+SBDMTA?
// +SBDMTA {"ISU":{"Command":"AT+SBDMTA=?
// +SBDRB {"ISU":{"Command":"AT+SBDRB
// +SBDREG {"ISU":{"Command":"AT+SBDREG?
// +SBDRT {"ISU":{"Command":"AT+SBDRT
// +SBDS {"ISU":{"Command":"AT+SBDS
// +SBDST {"ISU":{"Command":"AT+SBDST?
// +SBDSX {"ISU":{"Command":"AT+SBDSX
// +SBDTC {"ISU":{"Command":"AT+SBDTC
// +SBDWB {"ISU":{"Command":"AT+SBDWB=5
// +SBDWT {"ISU":{"Command":"AT+SBDWT
// +SBDWT= {"ISU":{"Command":"AT+SBDWT=hellome
// -MSGEOS {"ISU":{"Command":"AT-MSGEOS
// -MSGEO {"ISU":{"Command":"AT-MSGEO
// -MSSTM {"ISU":{"Command":"AT-MSSTM
// A/ 
// AT {"ISU":{"Command":"AT
// En {"ISU":{"Command":"ATE1
// I0 {"ISU":{"Command":"ATI0
// I1 {"ISU":{"Command":"ATI1
// I2 {"ISU":{"Command":"ATI2
// I3 {"ISU":{"Command":"ATI3
// I4 {"ISU":{"Command":"ATI4
// I5 {"ISU":{"Command":"ATI5
// I6 {"ISU":{"Command":"ATI6
// I7 {"ISU":{"Command":"ATI7
// Qn 
// Vn {"ISU":{"Command":"ATV1
// Vn {"ISU":{"Command":"ATV0
// Zn 
#endregion

#region VERBOSE
//test , AT+SBDWB=4 , --x-base64 dGVzdA==

// %R {"ISU":{"Command":"AT%R
// &Dn 
// &Fn
// &V {"ISU":{"Command":"AT&V
// &Wn
// &Yn {"ISU":{"Command":"AT&Y0
// *Rn {"ISU":{"Command":"AT*R1
// +CCLK {"ISU":{"Command":"AT+CCLK?
// +CGMI {"ISU":{"Command":"AT+CGMI
// +CGMM {"ISU":{"Command":"AT+CGMM
// +CGMR {"ISU":{"Command":"AT+CGMR
// +CGSN {"ISU":{"Command":"AT+CGSN
// +CIER {"ISU":{"Command":"AT+CIER=?
// +CIER {"ISU":{"Command":"AT+CIER?
// +CRIS {"ISU":{"Command":"AT+CRIS
// +CRISX {"ISU":{"Command":"AT+CRISX
// +CSQ {"ISU":{"Command":"AT+CSQ
// +CSQ {"ISU":{"Command":"AT+CSQ=?
// +CSQF {"ISU":{"Command":"AT+CSQF
// +CULK {"ISU":{"Command":"AT+CULK?
// +GEMON 
// +GMI {"ISU":{"Command":"AT+GMI
// +GMM {"ISU":{"Command":"AT+GMM
// +GMR {"ISU":{"Command":"AT+GMR
// +GSN {"ISU":{"Command":"AT+GSN
// +IPR {"ISU":{"Command":"AT+IPR=?
// +IPR {"ISU":{"Command":"AT+IPR?
// +SBDLOE {"ISU":{"Command":"AT+SBDLOE
// +SBDAREG {"ISU":{"Command":"AT+SBDAREG=?
// +SBDAREG {"ISU":{"Command":"AT+SBDAREG?
// +SBDC {"ISU":{"Command":"AT+SBDC
// +SBDD {"ISU":{"Command":"AT+SBDD0
// +SBDD {"ISU":{"Command":"AT+SBDD1
// +SBDD {"ISU":{"Command":"AT+SBDD2
// +SBDDET 
// 
// +SBDDSC {"ISU":{"Command":"AT+SBDDSC?
// +SBDGW {"ISU":{"Command":"AT+SBDGW
// +SBDGWN {"ISU":{"Command":"AT+SBDGWN
// +SBDI {"ISU":{"Command":"AT+SBDI
// +SBDIX {"ISU":{"Command":"AT+SBDIX
// +SBDIXA {"ISU":{"Command":"AT+SBDIXA
// +SBDIXA
//
// +SBDMTA {"ISU":{"Command":"AT+SBDMTA=0
// +SBDMTA {"ISU":{"Command":"AT+SBDMTA=1
// +SBDMTA {"ISU":{"Command":"AT+SBDMTA?
// +SBDMTA {"ISU":{"Command":"AT+SBDMTA=?
// +SBDRB {"ISU":{"Command":"AT+SBDRB
// +SBDREG {"ISU":{"Command":"AT+SBDREG?
// +SBDRT {"ISU":{"Command":"AT+SBDRT
// +SBDS {"ISU":{"Command":"AT+SBDS
// +SBDST {"ISU":{"Command":"AT+SBDST?
// +SBDSX {"ISU":{"Command":"AT+SBDSX
// +SBDTC {"ISU":{"Command":"AT+SBDTC
// +SBDWB {"ISU":{"Command":"AT+SBDWB=5
// +SBDWT {"ISU":{"Command":"AT+SBDWT
// +SBDWT= {"ISU":{"Command":"AT+SBDWT=smith
// -MSGEOS {"ISU":{"Command":"AT-MSGEOS
// -MSGEO {"ISU":{"Command":"AT-MSGEO
// -MSSTM {"ISU":{"Command":"AT-MSSTM
// A/ 
// AT {"ISU":{"Command":"AT
// En {"ISU":{"Command":"ATE1
// I0 {"ISU":{"Command":"ATI0
// I1 {"ISU":{"Command":"ATI1
// I2 {"ISU":{"Command":"ATI2
// I3 {"ISU":{"Command":"ATI3
// I4 {"ISU":{"Command":"ATI4
// I5 {"ISU":{"Command":"ATI5
// I6 {"ISU":{"Command":"ATI6
// I7 {"ISU":{"Command":"ATI7
// Qn {"ISU":{"Command":"ATQ0
// Vn {"ISU":{"Command":"ATV1
// Vn {"ISU":{"Command":"ATV0
// Zn 
#endregion