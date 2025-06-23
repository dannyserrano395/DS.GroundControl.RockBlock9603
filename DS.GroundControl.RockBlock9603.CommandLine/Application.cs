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
                    Console.WriteLine(await WriteWithCarriageReturnAsync(cr));
                }
                else if (cks != null)
                {
                    Console.WriteLine(await WriteWithChecksumAsync(cks));
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

            var output = await RockBlock9603.WriteWithCarriageReturnAsync("AT-MSSTM");

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
        private async Task<string> WriteWithCarriageReturnAsync(string input)
        {
            if (!(RockBlock9603?.Running.IsCancellationRequested is true &&
                RockBlock9603?.Stopped.IsCancellationRequested is false))
                return RockBlockStatus();

            try
            {
                var output = await RockBlock9603.WriteWithCarriageReturnAsync(input);
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
        private async Task<string> WriteWithChecksumAsync(string input)
        {
            if (!(RockBlock9603?.Running.IsCancellationRequested is true &&
                RockBlock9603?.Stopped.IsCancellationRequested is false))
                return RockBlockStatus();

            try
            {
                var output = await RockBlock9603.WriteWithChecksumAsync(input);
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
// %R {"ISU":{"Command":"AT%R","Response":"REG  DEC HEX  REG  DEC HEXS000 000 00H  S001 000 00HS002 043 2bH  S003 013 0dHS004 010 0aH  S005 008 08HS006 004 04H  S007 050 32HS008 004 04H  S009 006 06HS010 014 0eH  S011 000 00HS012 050 32H  S013 049 31HS014 162 a2H  S015 000 00HS016 000 00H  S017 000 00HS018 000 00H  S019 000 00HS020 000 00H  S021 048 30HS022 246 f6H  S023 012 0cHS024 000 00H  S025 005 05HS026 000 00H  S027 009 09HS028 000 00H  S029 000 00HS030 000 00H  S031 000 00HS032 017 11H  S033 019 13HS034 000 00H  S035 007 07HS036 000 00H  S037 000 00HS038 000 00H  S039 000 00HS040 104 68H  S041 000 00HS042 016 10H  S043 032 20HS044 004 04H  S045 000 00HS046 000 00H  S047 000 00HS048 000 00H  S049 001 01HS050 000 00H  S051 002 02HS052 000 00H  S053 000 00HS054 006 06H  S055 000 00HS056 000 00H  S057 000 00HS058 003 03H  S059 000 00HS060 000 00H  S061 000 00HS062 000 00H  S063 000 00HS064 000 00H  S065 000 00HS066 000 00H  S067 000 00HS068 000 00H  S069 000 00HS070 000 00H  S071 000 00HS072 000 00H  S073 000 00HS074 000 00H  S075 000 00HS076 000 00H  S077 000 00HS078 000 00H  S079 000 00HS080 000 00H  S081 000 00HS082 000 00H  S083 000 00HS084 000 00H  S085 000 00HS086 000 00H  S087 000 00HS088 000 00H  S089 000 00HS090 000 00H  S091 000 00HS092 000 00H  S093 000 00HS094 000 00H  S095 000 00HS096 000 00H  S097 000 00HS098 105 69H  S099 105 69HS100 015 0fH  S101 000 00HS102 030 1eH  S103 010 0aHS104 025 19H  S105 000 00HS106 010 0aH  S107 010 0aHS108 000 00H  S109 000 00HS110 000 00H  S111 000 00HS112 000 00H  S113 000 00HS114 000 00H  S115 000 00HS116 000 00H  S117 000 00HS118 000 00H  S119 000 00HS120 000 00H  S121 001 01HS122 001 01H  S123 008 08HS124 015 0fH  S125 010 0aHS126 002 02H  S127 000 00H","Result":"0"}}
// &Dn 
// &Fn
// &V {"ISU":{"Command":"AT&V","Response":"ACTIVE PROFILE:E1 Q0 V0 &D2 &K0 S003:013 S004:010 S005:008 S013:049 S014:162 S021:048 S023:012 S039:000 S121:001 S122:001 STORED PROFILE 0:E1 Q0 V1 &D2 &K0 S013:049 S014:170 S021:048 S023:012 S039:000 S121:000 S122:001 STORED PROFILE 1:E1 Q0 V1 &D2 &K3 ","Result":"0"}}
// &Wn
// &Yn 
// *Rn {"ISU":{"Command":"AT*R1","Response":"","Result":"0"}}
// +CCLK {"ISU":{"Command":"AT+CCLK?","Response":"+CCLK:25/06/23,03:14:08","Result":"0"}}
// +CGMI {"ISU":{"Command":"AT+CGMI","Response":"Iridium","Result":"0"}}
// +CGMM {"ISU":{"Command":"AT+CGMM","Response":"IRIDIUM 9600 Family SBD Transceiver","Result":"0"}}
// +CGMR {"ISU":{"Command":"AT+CGMR","Response":"Call Processor Version: TA16005Modem DSP Version: 1.7 svn: 2358DBB Version: 0x0001 (ASIC)RFA Version: 0x0007 (SRFA2)NVM Version: KVSHardware Version: BOOT07d2/9602NrvA-D/04/RAW0d","Result":"0"}}
// +CGSN {"ISU":{"Command":"AT+CGSN","Response":"300234066586340","Result":"0"}}
// +CIER {"ISU":{"Command":"AT+CIER=?","Response":"+CIER:(0-1),(0-1),(0-1),(0-1),(0-1)","Result":"0"}}
// +CIER {"ISU":{"Command":"AT+CIER?","Response":"+CIER:0,0,0,0,0","Result":"0"}}
// +CRIS {"ISU":{"Command":"AT+CRIS","Response":"+CRIS:000,000","Result":"0"}}
// +CRISX {"ISU":{"Command":"AT+CRISX","Response":"+CRISX:000,000,00000000","Result":"0"}}
// +CSQ {"ISU":{"Command":"AT+CSQ","Response":"+CSQ:0","Result":"0"}}
// +CSQ {"ISU":{"Command":"AT+CSQ=?","Response":"+CSQ:(0-5)","Result":"0"}}
// +CSQF {"ISU":{"Command":"AT+CSQF","Response":"+CSQF:1","Result":"0"}}
// +CULK {"ISU":{"Command":"AT+CULK?","Response":"+CULK:0","Result":"0"}}
// +GEMON 
// +GMI {"ISU":{"Command":"AT+GMI","Response":"Iridium","Result":"0"}}
// +GMM {"ISU":{"Command":"AT+GMM","Response":"IRIDIUM 9600 Family SBD Transceiver","Result":"0"}}
// +GMR {"ISU":{"Command":"AT+GMR","Response":"Call Processor Version: TA16005Modem DSP Version: 1.7 svn: 2358DBB Version: 0x0001 (ASIC)RFA Version: 0x0007 (SRFA2)NVM Version: KVSHardware Version: BOOT07d2/9602NrvA-D/04/RAW0d","Result":"0"}}
// +GSN {"ISU":{"Command":"AT+GSN","Response":"300234066586340","Result":"0"}}
// +IPR {"ISU":{"Command":"AT+IPR=?","Response":"+IPR:(001-009)","Result":"0"}}
// +IPR {"ISU":{"Command":"AT+IPR?","Response":"+IPR:006","Result":"0"}}
// +SBDLOE {"ISU":{"Command":"AT+SBDLOE","Response":"+SBDLOE:0,0","Result":"0"}}
// +SBDAREG {"ISU":{"Command":"AT+SBDAREG=?","Response":"+SBDAREG:(0-4)","Result":"0"}}
// +SBDAREG {"ISU":{"Command":"AT+SBDAREG?","Response":"+SBDAREG:0","Result":"0"}}
// +SBDC {"ISU":{"Command":"AT+SBDC","Response":"0","Result":"0"}}
// +SBDD {"ISU":{"Command":"AT+SBDD0","Response":"0","Result":"0"}}
// +SBDD {"ISU":{"Command":"AT+SBDD1","Response":"0","Result":"0"}}
// +SBDD {"ISU":{"Command":"AT+SBDD2","Response":"0","Result":"0"}}
// +SBDDET 
// +SBDDSC {"ISU":{"Command":"AT+SBDDSC?","Response":"0","Result":"0"}}
// +SBDGW {"ISU":{"Command":"AT+SBDGW","Response":"+SBDGW: EMSS","Result":"0"}}
// +SBDGWN {"ISU":{"Command":"AT+SBDGWN","Response":"+SBDGWN: 1","Result":"0"}}
// +SBDI {"ISU":{"Command":"AT+SBDI
// +SBDIX 
// +SBDIXA
// +SBDMTA {"ISU":{"Command":"AT+SBDMTA=0","Response":"","Result":"0"}}
// +SBDMTA {"ISU":{"Command":"AT+SBDMTA=1","Response":"","Result":"0"}}
// +SBDMTA {"ISU":{"Command":"AT+SBDMTA?","Response":"+SBDMTA:1","Result":"0"}}
// +SBDMTA {"ISU":{"Command":"AT+SBDMTA=?","Response":"+SBDMTA:(0-1)","Result":"0"}}
// +SBDRB {"ISU":{"Command":"AT+SBDRB","Response":"\u0000\u0005danny\u0002\u001A","Result":"0"}}
// +SBDREG {"ISU":{"Command":"AT+SBDREG?","Response":"+SBDREG:0","Result":"0"}}
// +SBDRT {"ISU":{"Command":"AT+SBDRT","Response":"+SBDRT:danny","Result":"0"}}
// +SBDS {"ISU":{"Command":"AT+SBDS","Response":"+SBDS: 0, 0, 0, -1","Result":"0"}}
// +SBDST {"ISU":{"Command":"AT+SBDST?","Response":"+SBDST:0","Result":"0"}}
// +SBDSX {"ISU":{"Command":"AT+SBDSX","Response":"+SBDSX: 0, 0, 0, -1, 0, 0","Result":"0"}}
// +SBDTC {"ISU":{"Command":"AT+SBDTC","Response":"SBDTC: Outbound SBD Copied to Inbound SBD: size = 5","Result":"0"}}
// +SBDWB {"ISU":{"Command":"AT+SBDWB=5","Response":"READY","Result":""}}
// +SBDWT {"ISU":{"Command":"AT+SBDWT","Response":"READY","Result":""}}
// +SBDWT= {"ISU":{"Command":"AT+SBDWT=hellome","Response":"","Result":"0"}}
// -MSGEOS {"ISU":{"Command":"AT-MSGEOS","Response":"-MSGEO: -2424,-4736,3500,e7d1e277","Result":"0"}}
// -MSGEO {"ISU":{"Command":"AT-MSGEO","Response":"-MSGEO: -2424,-4736,3500,e7d1e277","Result":"0"}}
// -MSSTM {"ISU":{"Command":"AT-MSSTM","Response":"-MSSTM: e858a224","Result":"0"}}
// A/ 
// AT {"ISU":{"Command":"AT","Response":"","Result":"0"}}
// En {"ISU":{"Command":"ATE1","Response":"","Result":"0"}}
// I0 {"ISU":{"Command":"ATI0","Response":"2400","Result":"0"}}
// I1 {"ISU":{"Command":"ATI1","Response":"0000","Result":"0"}}
// I2 {"ISU":{"Command":"ATI2","Response":"OK","Result":"0"}}
// I3 {"ISU":{"Command":"ATI3","Response":"TA16005","Result":"0"}}
// I4 {"ISU":{"Command":"ATI4","Response":"IRIDIUM 9600 Family","Result":"0"}}
// I5 {"ISU":{"Command":"ATI5","Response":"8816","Result":"0"}}
// I6 {"ISU":{"Command":"ATI6","Response":"13X","Result":"0"}}
// I7 {"ISU":{"Command":"ATI7","Response":"BOOT07d2/9602NrvA-D/04/RAW0d","Result":"0"}}
// Qn 
// Vn {"ISU":{"Command":"ATV1","Response":"","Result":"OK"}}
// Vn {"ISU":{"Command":"ATV0","Response":"","Result":"0"}}
// Zn 
#endregion

#region VERBOSE
// %R {"ISU":{"Command":"AT%R","Response":"REG  DEC HEX  REG  DEC HEXS000 000 00H  S001 000 00HS002 043 2bH  S003 013 0dHS004 010 0aH  S005 008 08HS006 004 04H  S007 050 32HS008 004 04H  S009 006 06HS010 014 0eH  S011 000 00HS012 050 32H  S013 049 31HS014 170 aaH  S015 000 00HS016 000 00H  S017 000 00HS018 000 00H  S019 000 00HS020 000 00H  S021 048 30HS022 246 f6H  S023 012 0cHS024 000 00H  S025 005 05HS026 000 00H  S027 009 09HS028 000 00H  S029 000 00HS030 000 00H  S031 000 00HS032 017 11H  S033 019 13HS034 000 00H  S035 007 07HS036 000 00H  S037 000 00HS038 000 00H  S039 000 00HS040 104 68H  S041 000 00HS042 016 10H  S043 032 20HS044 004 04H  S045 000 00HS046 000 00H  S047 000 00HS048 000 00H  S049 001 01HS050 000 00H  S051 002 02HS052 000 00H  S053 000 00HS054 006 06H  S055 000 00HS056 000 00H  S057 000 00HS058 003 03H  S059 000 00HS060 000 00H  S061 000 00HS062 000 00H  S063 000 00HS064 000 00H  S065 000 00HS066 000 00H  S067 000 00HS068 000 00H  S069 000 00HS070 000 00H  S071 000 00HS072 000 00H  S073 000 00HS074 000 00H  S075 000 00HS076 000 00H  S077 000 00HS078 000 00H  S079 000 00HS080 000 00H  S081 000 00HS082 000 00H  S083 000 00HS084 000 00H  S085 000 00HS086 000 00H  S087 000 00HS088 000 00H  S089 000 00HS090 000 00H  S091 000 00HS092 000 00H  S093 000 00HS094 000 00H  S095 000 00HS096 000 00H  S097 000 00HS098 105 69H  S099 105 69HS100 015 0fH  S101 000 00HS102 030 1eH  S103 010 0aHS104 025 19H  S105 000 00HS106 010 0aH  S107 010 0aHS108 000 00H  S109 000 00HS110 000 00H  S111 000 00HS112 000 00H  S113 000 00HS114 000 00H  S115 000 00HS116 000 00H  S117 000 00HS118 000 00H  S119 000 00HS120 000 00H  S121 000 00HS122 001 01H  S123 008 08HS124 015 0fH  S125 010 0aHS126 002 02H  S127 000 00HS128 000 00H","Result":"OK"}}
// &Dn 
// &Fn
// &V {"ISU":{"Command":"AT&V","Response":"ACTIVE PROFILE:E1 Q0 V1 &D2 &K0 S003:013 S004:010 S005:008 S013:049 S014:170 S021:048 S023:012 S039:000 S121:000 S122:001 STORED PROFILE 0:E1 Q0 V1 &D2 &K0 S013:049 S014:170 S021:048 S023:012 S039:000 S121:000 S122:001 STORED PROFILE 1:E1 Q0 V1 &D2 &K3 S013:049 S014:170 S021:048 S023:012 S039:003 S121:001 S122:001 ","Result":"OK"}}
// &Wn
// &Yn {"ISU":{"Command":"AT&Y0","Response":"","Result":"OK"}}
// *Rn {"ISU":{"Command":"AT*R1","Response":"","Result":"OK"}}
// +CCLK {"ISU":{"Command":"AT+CCLK?","Response":"+CCLK:25/06/23,02:41:31","Result":"OK"}}
// +CGMI {"ISU":{"Command":"AT+CGMI","Response":"Iridium","Result":"OK"}}
// +CGMM {"ISU":{"Command":"AT+CGMM","Response":"IRIDIUM 9600 Family SBD Transceiver","Result":"OK"}}
// +CGMR {"ISU":{"Command":"AT+CGMR","Response":"Call Processor Version: TA16005Modem DSP Version: 1.7 svn: 2358DBB Version: 0x0001 (ASIC)RFA Version: 0x0007 (SRFA2)NVM Version: KVSHardware Version: BOOT07d2/9602NrvA-D/04/RAW0dBOOT Version: TA16005 (rev exported)","Result":"OK"}}
// +CGSN {"ISU":{"Command":"AT+CGSN","Response":"300234066586340","Result":"OK"}}
// +CIER {"ISU":{"Command":"AT+CIER=?","Response":"+CIER:(0-1),(0-1),(0-1),(0-1),(0-1)","Result":"OK"}}
// +CIER {"ISU":{"Command":"AT+CIER?","Response":"+CIER:0,0,0,0,0","Result":"OK"}}
// +CRIS {"ISU":{"Command":"AT+CRIS","Response":"+CRIS:000,000","Result":"OK"}}
// +CRISX {"ISU":{"Command":"AT+CRISX","Response":"+CRISX:000,000,00000000","Result":"OK"}}
// +CSQ {"ISU":{"Command":"AT+CSQ","Response":"+CSQ:0","Result":"OK"}}
// +CSQ {"ISU":{"Command":"AT+CSQ=?","Response":"+CSQ:(0-5)","Result":"OK"}}
// +CSQF {"ISU":{"Command":"AT+CSQF","Response":"+CSQF:0","Result":"OK"}}
// +CULK {"ISU":{"Command":"AT+CULK?","Response":"+CULK:0","Result":"OK"}}
// +GEMON 
// +GMI {"ISU":{"Command":"AT+GMI","Response":"Iridium","Result":"OK"}}
// +GMM {"ISU":{"Command":"AT+GMM","Response":"IRIDIUM 9600 Family SBD Transceiver","Result":"OK"}}
// +GMR {"ISU":{"Command":"AT+GMR","Response":"Call Processor Version: TA16005Modem DSP Version: 1.7 svn: 2358DBB Version: 0x0001 (ASIC)RFA Version: 0x0007 (SRFA2)NVM Version: KVSHardware Version: BOOT07d2/9602NrvA-D/04/RAW0dBOOT Version: TA16005 (rev exported)","Result":"OK"}}
// +GSN {"ISU":{"Command":"AT+GSN","Response":"300234066586340","Result":"OK"}}
// +IPR {"ISU":{"Command":"AT+IPR=?","Response":"+IPR:(001-009)","Result":"OK"}}
// +IPR {"ISU":{"Command":"AT+IPR?","Response":"+IPR:006","Result":"OK"}}
// +SBDLOE {"ISU":{"Command":"AT+SBDLOE","Response":"+SBDLOE:0,0","Result":"OK"}}
// +SBDAREG {"ISU":{"Command":"AT+SBDAREG=?","Response":"+SBDAREG:(0-4)","Result":"OK"}}
// +SBDAREG {"ISU":{"Command":"AT+SBDAREG?","Response":"+SBDAREG:0","Result":"OK"}}
// +SBDC {"ISU":{"Command":"AT+SBDC","Response":"0","Result":"OK"}}
// +SBDD {"ISU":{"Command":"AT+SBDD0","Response":"0","Result":"OK"}}
// +SBDD {"ISU":{"Command":"AT+SBDD1","Response":"0","Result":"OK"}}
// +SBDD {"ISU":{"Command":"AT+SBDD2","Response":"0","Result":"OK"}}
// +SBDDET 
// 
// +SBDDSC {"ISU":{"Command":"AT+SBDDSC?","Response":"0","Result":"OK"}}
// +SBDGW {"ISU":{"Command":"AT+SBDGW","Response":"+SBDGW: EMSS","Result":"OK"}}
// +SBDGWN {"ISU":{"Command":"AT+SBDGWN","Response":"+SBDGWN: 1","Result":"OK"}}
// +SBDI {"ISU":{"Command":"AT+SBDI
// +SBDIX {"ISU":{"Command":"AT+SBDIX
// +SBDIXA {"ISU":{"Command":"AT+SBDIXA
// +SBDIXA
//
// +SBDMTA {"ISU":{"Command":"AT+SBDMTA=0","Response":"","Result":"OK"}}
// +SBDMTA {"ISU":{"Command":"AT+SBDMTA=1","Response":"","Result":"OK"}}
// +SBDMTA {"ISU":{"Command":"AT+SBDMTA?","Response":"+SBDMTA:1","Result":"OK"}}
// +SBDMTA {"ISU":{"Command":"AT+SBDMTA=?","Response":"+SBDMTA:(0-1)","Result":"OK"}}
// +SBDRB {"ISU":{"Command":"AT+SBDRB","Response":"\u0000\u0005danny\u0002\u001A","Result":"OK"}}
// +SBDREG {"ISU":{"Command":"AT+SBDREG?","Response":"+SBDREG:0","Result":"OK"}}
// +SBDRT {"ISU":{"Command":"AT+SBDRT","Response":"+SBDRT:danny","Result":"OK"}}
// +SBDS {"ISU":{"Command":"AT+SBDS","Response":"+SBDS: 0, 0, 0, -1","Result":"OK"}}
// +SBDST {"ISU":{"Command":"AT+SBDST?","Response":"+SBDST:0","Result":"OK"}}
// +SBDSX {"ISU":{"Command":"AT+SBDSX","Response":"+SBDSX: 0, 0, 0, -1, 0, 0","Result":"OK"}}
// +SBDTC {"ISU":{"Command":"AT+SBDTC","Response":"SBDTC: Outbound SBD Copied to Inbound SBD: size = 5","Result":"OK"}}
// +SBDWB {"ISU":{"Command":"AT+SBDWB=5","Response":"READY","Result":""}}
// +SBDWT {"ISU":{"Command":"AT+SBDWT","Response":"READY","Result":""}}
// +SBDWT= {"ISU":{"Command":"AT+SBDWT=smith","Response":"","Result":"OK"}}
// -MSGEOS {"ISU":{"Command":"AT-MSGEOS","Response":"-MSGEO: -2424,-4736,3500,e7d1e277","Result":"OK"}}
// -MSGEO {"ISU":{"Command":"AT-MSGEO","Response":"-MSGEO: -2424,-4736,3500,e7d1e277","Result":"OK"}}
// -MSSTM {"ISU":{"Command":"AT-MSSTM","Response":"-MSSTM: e8585069","Result":"OK"}}
// A/ 
// AT {"ISU":{"Command":"AT","Response":"","Result":"OK"}}
// En {"ISU":{"Command":"ATE1","Response":"","Result":"OK"}}
// I0 {"ISU":{"Command":"ATI0","Response":"2400","Result":"OK"}}
// I1 {"ISU":{"Command":"ATI1","Response":"0000","Result":"OK"}}
// I2 {"ISU":{"Command":"ATI2","Response":"OK","Result":"OK"}}
// I3 {"ISU":{"Command":"ATI3","Response":"TA16005","Result":"OK"}}
// I4 {"ISU":{"Command":"ATI4","Response":"IRIDIUM 9600 Family","Result":"OK"}}
// I5 {"ISU":{"Command":"ATI5","Response":"8816","Result":"OK"}}
// I6 {"ISU":{"Command":"ATI6","Response":"13X","Result":"OK"}}
// I7 {"ISU":{"Command":"ATI7","Response":"BOOT07d2/9602NrvA-D/04/RAW0d","Result":"OK"}}
// Qn {"ISU":{"Command":"ATQ0","Response":"","Result":"OK"}}
// Vn {"ISU":{"Command":"ATV1","Response":"","Result":"OK"}}
// Vn {"ISU":{"Command":"ATV0","Response":"","Result":"0"}}
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