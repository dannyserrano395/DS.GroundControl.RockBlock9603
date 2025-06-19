using System.Text.Json;
using System.Text.Encodings.Web;
using System.CommandLine;
using DS.GroundControl.Lib.Devices;
using DS.GroundControl.Lib.Devices.Factories;

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
            var startOption = new Option<bool>(
                name: "--start",
                description: "Starts the RockBlock 9603 satellite transmitter")
            {            
                AllowMultipleArgumentsPerToken = false
            };

            var stopOption = new Option<bool>(
                name: "--stop",
                description: "Stops the RockBlock 9603 satellite transmitter")
            {
                AllowMultipleArgumentsPerToken = false
            };

            var statusOption = new Option<bool>(
                name: "--status",
                description: "Returns the RockBlock 9603 satellite transmitter status")
            {
                AllowMultipleArgumentsPerToken = false
            };

            var timeOption = new Option<bool>(
                name: "--time",
                description: "Returns the Iridium network time")
            {
                AllowMultipleArgumentsPerToken = false
            };

            var shutdownOption = new Option<bool>(
                name: "--shutdown",
                description: "Shutdowns the application")
            {
                AllowMultipleArgumentsPerToken = false
            };

            var crOption = new Option<string>(
                name: "--cr",
                description: "Appends a carriage return to the end of the command")
            {
                AllowMultipleArgumentsPerToken = true
            };

            var cksOption = new Option<string>(
                name: "--cks",
                description: "Appends a checksum to the end of the command")
            {
                AllowMultipleArgumentsPerToken = true
            };

            var rootCommand = new RootCommand("RockBlock 9603 satellite transmitter command-line")
            {
                startOption,
                stopOption,
                statusOption,
                timeOption,
                shutdownOption,
                crOption,
                cksOption,
            };
            rootCommand.AddValidator(result =>
            {
                if (result.Children.Count(s => s.Symbol == startOption || s.Symbol == stopOption || s.Symbol == timeOption ||
                s.Symbol == shutdownOption || s.Symbol == crOption || s.Symbol == cksOption || s.Symbol == statusOption) > 1)
                {
                    result.ErrorMessage = $"\"Option '--{result.Children[0].Symbol.Name}' is a mutually exclusive option.\"";
                }
            });
            rootCommand.SetHandler(async (start, stop, time, shutdown, cr, cks, status) =>
            {
                if (start)
                {
                    Console.WriteLine(await RockBlockStartAsync());
                }
                else if (stop)
                {
                    Console.WriteLine(await RockBlockStopAsync());
                }
                else if (time)
                {
                    Console.WriteLine(await RockBlockTimeAsync());
                }
                else if (shutdown)
                {
                    Console.WriteLine(await RockBlockStopAsync());
                    Environment.Exit(0);
                }
                else if (cr != null)
                {
                    Console.WriteLine(await AppendCarriageReturnAndWriteToRockBlockAsync(cr));
                }
                else if (cks != null)
                {
                    Console.WriteLine(await AppendChecksumAndWriteToRockBlockAsync(cks));
                }
                else if (status)
                {
                    Console.WriteLine(RockBlockStatus());
                }
            }, startOption, stopOption, timeOption, shutdownOption, crOption, cksOption, statusOption);

            while (true)
            {
                Console.Write("> ");
                await rootCommand.InvokeAsync(Console.ReadLine());
            }
        }
        private async Task<string> RockBlockStartAsync()
        {
            if (RockBlock9603 == null)
            {
                RockBlock9603 = RockBlock9603Factory.Create();
                _ = RockBlock9603.StartAsync();

                var running = new TaskCompletionSource();
                var stopped = new TaskCompletionSource();
                using var run = RockBlock9603.Running.Register(() => running.SetResult());
                using var stp = RockBlock9603.Stopped.Register(() => stopped.SetResult());
                await Task.WhenAny(running.Task, stopped.Task);
            }
            return RockBlockStatus();
        }
        private async Task<string> RockBlockStopAsync()
        {
            var tmp = RockBlock9603;
            if (RockBlock9603 != null)
            {
                await RockBlock9603.StopAsync();
                RockBlock9603 = null;
            }
            return ToJsonString(new
            {
                ISU = new
                {
                    Canceled = tmp?.Canceled.IsCancellationRequested,
                    Started = tmp?.Started.IsCancellationRequested,
                    Running = tmp?.Running.IsCancellationRequested,
                    Stopped = tmp?.Stopped.IsCancellationRequested,
                    Faulted = tmp?.Faulted.IsCancellationRequested
                }
            });
        }
        private async Task<string> RockBlockTimeAsync()
        {
            if (!(RockBlock9603?.Running.IsCancellationRequested is true &&
                RockBlock9603?.Stopped.IsCancellationRequested is false))
                return RockBlockStatus();

            var output = await RockBlock9603.AppendCarriageReturnAndWriteAsync("AT-MSSTM");

            var hex = output.Response.Split("-MSSTM: ").Last();
            var time = hex.All(char.IsAsciiHexDigit)
                ? CalculateIridiumTime(hex)
                : hex;

            return output != default
                ? ToJsonString(new
                {
                    ISU = new
                    {
                        Time = time
                    }
                })
                : RockBlockStatus();
        }
        private async Task<string> AppendCarriageReturnAndWriteToRockBlockAsync(string input)
        {
            if (!(RockBlock9603?.Running.IsCancellationRequested is true &&
                RockBlock9603?.Stopped.IsCancellationRequested is false))
                return RockBlockStatus();

            var output = await RockBlock9603.AppendCarriageReturnAndWriteAsync(input);

            return output != default
                ? ToJsonString(new
                {
                    ISU = new
                    {
                        output.Command,
                        output.Response,
                        output.Result
                    }
                })
                : RockBlockStatus();
        }
        private async Task<string> AppendChecksumAndWriteToRockBlockAsync(string input)
        {
            if (!(RockBlock9603?.Running.IsCancellationRequested is true &&
                RockBlock9603?.Stopped.IsCancellationRequested is false))
                return RockBlockStatus();

            var output = await RockBlock9603.AppendChecksumAndWriteAsync(input);

            return output != default
                ? ToJsonString(new
                {
                    ISU = new
                    {
                        output.Command,
                        output.Response,
                        output.Result
                    }
                })
                : RockBlockStatus();
        }
        private string RockBlockStatus()
        {
            return ToJsonString(new
            {
                ISU = new
                {
                    Canceled = RockBlock9603?.Canceled.IsCancellationRequested,
                    Started = RockBlock9603?.Started.IsCancellationRequested,
                    Running = RockBlock9603?.Running.IsCancellationRequested,
                    Stopped = RockBlock9603?.Stopped.IsCancellationRequested,
                    Faulted = RockBlock9603?.Faulted.IsCancellationRequested
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
// &Yn 
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

#region OTHER
// &Kn {"Command":"AT&K3","Response":"","Result":"OK"} 
// SHUTDOWN
// *F {"ISU":{"Command":"AT*F","Response":"","Result":"OK"}}
#endregion

#region NUMERIC
// %R {"ISU":{"Command":"AT%R
// &Dn 
// &Fn
// &V {"ISU":{"Command":"AT&V
// &Wn
// &Yn 
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