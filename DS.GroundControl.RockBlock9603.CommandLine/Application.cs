using System.Text.Json;
using System.Text.Encodings.Web;
using System.CommandLine;
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
                description: "Execute in ready-state BINARY mode (adds checksum).")
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
            rootCommand.SetHandler(async (start, stop, shutdown, time, execute, xText, xBinary, status) =>
            {
                if (start)
                {
                    await Console.Out.WriteLineAsync(await RockBlockStartAsync());
                }
                else if (stop)
                {
                    await Console.Out.WriteLineAsync(RockBlockStop());
                }
                else if (shutdown)
                {
                    await Console.Out.WriteLineAsync(RockBlockStop());
                    Environment.Exit(0);
                }
                else if (time)
                {
                    await Console.Out.WriteLineAsync(await RockBlockTimeAsync());
                }
                else if (execute != null)
                {
                    await Console.Out.WriteLineAsync(await ExecuteAsync(execute));
                }
                else if (xText != null)
                {
                    await Console.Out.WriteLineAsync(await ExecuteReadyStateTextCommandAsync(xText));
                }
                else if (xBinary != null)
                {
                    await Console.Out.WriteLineAsync(await ExecuteReadyStateBinaryCommandAsync(xBinary));
                }
                else if (status)
                {
                    await Console.Out.WriteLineAsync(RockBlockStatus());
                }
            }, startOption, stopOption, shutdownOption, timeOption, xOption, xTextOption, xBinaryOption, statusOption);
            rootCommand.AddValidator(result =>
            {
                if (result.Children.Count(s => s.Symbol == startOption || s.Symbol == stopOption || s.Symbol == shutdownOption ||
                s.Symbol == timeOption || s.Symbol == xOption || s.Symbol == xTextOption || s.Symbol == xBinaryOption || s.Symbol == statusOption) > 1)
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
            if (RockBlock9603 == null)
            {
                RockBlock9603 = RockBlock9603Factory.Create();
                _ = RockBlock9603.ConnectAsync();

                var connected = new TaskCompletionSource();
                var faulted = new TaskCompletionSource();
                using var connect = RockBlock9603.Connected.Register(() => connected.SetResult());
                using var fault = RockBlock9603.Faulted.Register(() => faulted.SetResult());
                await Task.WhenAny(connected.Task, faulted.Task);
            }
            return RockBlockStatus();
        }       
        private async Task<string> ExecuteAsync(string command)
        {
            if (RockBlock9603 == null || !RockBlock9603.Connected.IsCancellationRequested ||
                RockBlock9603.Faulted.IsCancellationRequested)
            {
                return RockBlockStatus();
            }
            try
            {
                var output = await RockBlock9603.ExecuteAsync(command);
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
            catch { }
            return RockBlockStatus();
        }
        private async Task<string> ExecuteReadyStateTextCommandAsync(string command)
        {
            if (RockBlock9603 == null || !RockBlock9603.Connected.IsCancellationRequested ||
                RockBlock9603.Faulted.IsCancellationRequested)
            {
                return RockBlockStatus();
            }
            try
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
            catch { }
            return RockBlockStatus();
        }
        private async Task<string> ExecuteReadyStateBinaryCommandAsync(string command)
        {
            if (RockBlock9603 == null || !RockBlock9603.Connected.IsCancellationRequested ||
                RockBlock9603.Faulted.IsCancellationRequested)
            {
                return RockBlockStatus();
            }
            try
            {
                var output = await RockBlock9603.ExecuteReadyStateBinaryCommandAsync(command);
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
            catch { }
            return RockBlockStatus();
        }
        private async Task<string> RockBlockTimeAsync()
        {
            if (RockBlock9603 == null || !RockBlock9603.Connected.IsCancellationRequested ||
                RockBlock9603.Faulted.IsCancellationRequested)
            {
                return RockBlockStatus();
            }
            try
            {
                var output = await RockBlock9603.ExecuteAsync("AT-MSSTM");

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
            catch { }
            return RockBlockStatus();
        }
        private string RockBlockStatus()
        {
            return ToJsonString(new
            {
                ISU = new
                {
                    Connected = RockBlock9603?.Connected.IsCancellationRequested,
                    Faulted = RockBlock9603?.Faulted.IsCancellationRequested
                }
            });
        }
        private string RockBlockStop()
        {
            var rb = RockBlock9603;
            if (RockBlock9603 != null)
            {
                RockBlock9603.Dispose();
                RockBlock9603 = null;
            }
            return ToJsonString(new
            {
                ISU = new
                {
                    Connected = rb?.Connected.IsCancellationRequested,
                    Faulted = rb?.Faulted.IsCancellationRequested
                }
            });
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