using System.CommandLine;
using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Xml;
using ILogger = DS.GroundControl.RockBlock9603.Log4Net.ILogger;

namespace DS.GroundControl.RockBlock9603.Setup
{
    public class Application : IApplication
    {
        private ILogger Log { get; }
        private JsonSerializerOptions JsonSerializerOptions { get; }

        public Application(ILogger log)
        {
            Log = log;
            JsonSerializerOptions = new JsonSerializerOptions()
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            };
        }

        public async Task<int> StartAsync(string[] args)
        {
            var root = new RootCommand("Setup application for the RockBlock9603 service.");
            var install = new Command("install", "Installs the RockBlock9603 service.");
            var uninstall = new Command("uninstall", "Uninstalls the RockBlock9603 service.");

            install.SetHandler((context) => 
            {
                context.ExitCode = Install() ? 0 : 1;
            });
            uninstall.SetHandler((context) =>
            {
                context.ExitCode = Uninstall() ? 0 : 1;
            });

            root.AddCommand(install);
            root.AddCommand(uninstall);
            return await root.InvokeAsync(args);
        }
        private bool Install()
        {
            var svcExecutablePath = GetServiceExecutablePath();
            var svcSettingsPath = GetServiceSettingsPath();
            var svcSettings = GetServiceSettings(svcSettingsPath);

            var query = Execute($"sc.exe query \"{svcSettings.ServiceName}\"");
            if (!query.StandardOutput.StartsWith("[SC] EnumQueryServicesStatus:OpenService FAILED 1060:"))
            {
                Log.Info($"Installation canceled: service already exists. {ToJsonString(query)}");
                return false;
            }

            var create = Execute($"sc.exe create \"{svcSettings.ServiceName}\" binpath= \"{svcExecutablePath}\" start= auto displayname= \"{svcSettings.DisplayName}\"");
            if (!create.StandardOutput.StartsWith("[SC] CreateService SUCCESS"))
            {
                Log.Info($"Installation failed. {ToJsonString(create)}");
                return false;
            }

            var description = Execute($"sc.exe description \"{svcSettings.ServiceName}\" \"{svcSettings.Description}\"");
            if (!description.StandardOutput.StartsWith("[SC] ChangeServiceConfig2 SUCCESS"))
            {
                Log.Info($"Installation completed with issues. {ToJsonString(description)}");
            }

            Log.Info("Installation completed: service successfully installed.");
            return true;
        }
        private bool Uninstall()
        {
            var svcSettingsPath = GetServiceSettingsPath();
            var svcSettings = GetServiceSettings(svcSettingsPath);

            var query = Execute($"sc.exe query \"{svcSettings.ServiceName}\"");
            if (query.StandardOutput.StartsWith("[SC] EnumQueryServicesStatus:OpenService FAILED 1060:"))
            {
                Log.Info($"Uninstallation canceled: service does not exist. {ToJsonString(query)}");
                return false;
            }

            var stop = Execute($"sc.exe stop \"{svcSettings.ServiceName}\"");
            var delete = Execute($"sc.exe delete \"{svcSettings.ServiceName}\"");
            if (!delete.StandardOutput.StartsWith("[SC] DeleteService SUCCESS"))
            {
                Log.Info($"Uninstallation failed. {ToJsonString(delete)}");
                return false;
            }

            Log.Info("Uninstallation completed: service successfully uninstalled.");
            return true;
        }
        private static string GetServiceExecutablePath()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "DS.GroundControl.RockBlock9603.Service.exe");
            if (!File.Exists(path)) throw new FileNotFoundException($"The file \"{path}\" was not found.");
            return path;
        }
        private static string GetServiceSettingsPath()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "_config", "ServiceConfiguration.config");
            if (!File.Exists(path)) throw new FileNotFoundException($"The file \"{path}\" was not found.");
            return path;
        }
        private static (string ServiceName, string DisplayName, string Description) GetServiceSettings(string path)
        {
            var parentNode = new XmlDocument();
            parentNode.Load(path);

            var nodes = parentNode["serviceConfiguration"].ChildNodes;

            var n0 = nodes.Cast<XmlNode>()
                .Where(node =>
                node.NodeType == XmlNodeType.Element &&
                node.Attributes.GetNamedItem("key").Value == "ServiceName").FirstOrDefault();

            var n1 = nodes.Cast<XmlNode>()
                .Where(node =>
                node.NodeType == XmlNodeType.Element &&
                node.Attributes.GetNamedItem("key").Value == "DisplayName").FirstOrDefault();

            var n2 = nodes.Cast<XmlNode>()
                .Where(node =>
                node.NodeType == XmlNodeType.Element &&
                node.Attributes.GetNamedItem("key").Value == "Description").FirstOrDefault();

            var serviceName = n0?.Attributes.GetNamedItem("value")?.Value ?? string.Empty;
            var displayName = n1?.Attributes.GetNamedItem("value")?.Value ?? string.Empty;
            var description = n2?.Attributes.GetNamedItem("value")?.Value ?? string.Empty;

            return (serviceName, displayName, description);
        }
        private static (string Command, string ExitCode, string StandardOutput, string StandardError) Execute(string command)
        {
            using var process = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "C:\\windows\\system32\\cmd.exe",
                    Arguments = $"/c {command}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();

            process.WaitForExit();

            return (command, process.ExitCode.ToString(), standardOutput, standardError);
        }
        private string ToJsonString<T>(T value) => JsonSerializer.Serialize(value, JsonSerializerOptions);
    }
}