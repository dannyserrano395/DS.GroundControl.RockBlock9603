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
            var installOption = new Option<bool>(
            name: "--install",
            description: "Installs the RockBlock9603 service.");

            var uninstallOption = new Option<bool>(
            name: "--uninstall",
            description: "Uninstalls the RockBlock9603 service.");

            var rootCommand = new RootCommand("Setup application for the RockBlock9603 service.")
            {
                installOption,
                uninstallOption
            };
            rootCommand.AddValidator(result =>
            {
                if (result.Children.Count(s => s.Symbol == installOption || s.Symbol == uninstallOption) > 1)
                {
                    result.ErrorMessage = $"\"Option '--{result.Children[0].Symbol.Name}' is a mutually exclusive option.\"";
                }
            });
            rootCommand.SetHandler((install, uninstall) =>
            {
                if (install)
                {
                    ExecuteSetupCommand("--install");
                }
                else if (uninstall)
                {
                    ExecuteSetupCommand("--uninstall");
                }
            }, installOption, uninstallOption);

            return await rootCommand.InvokeAsync(args);
        }
        private void ExecuteSetupCommand(string option)
        {
            var serviceExePath = $"{AppContext.BaseDirectory}DS.GroundControl.RockBlock9603.Service.exe";
            var serviceSettingsPath = $"{AppContext.BaseDirectory}_config\\ServiceConfiguration.config";

            if (!File.Exists(serviceExePath))
                throw new FileNotFoundException($"The file \"{serviceExePath}\" was not found.");

            if (!File.Exists(serviceSettingsPath))
                throw new FileNotFoundException($"The file \"{serviceSettingsPath}\" was not found.");

            var serviceSettings = GetServiceSettings(serviceSettingsPath);

            switch (option.ToLower())
            {
                case "--install":
                    {
                        ExecuteInstallOption(serviceSettings, serviceExePath);
                        break;
                    }
                case "--uninstall":
                    {
                        ExecuteUninstallOption(serviceSettings);
                        break;
                    }
            }
        }
        private void ExecuteInstallOption((string ServiceName, string DisplayName, string Description) serviceSettings, string serviceExePath)
        {
            var cr = ExecuteCmdCommand($"sc.exe create \"{serviceSettings.ServiceName}\" binpath=\"{serviceExePath}\" start=\"auto\" displayname=\"{serviceSettings.DisplayName}\"");
            var create = DeserializeAnonymousType(cr, new { Command = "", ExitCode = 0, StandardOutput = "", StandardError = "" });

            if (create.StandardOutput != "[SC] CreateService SUCCESS\r\n")
            {
                Log.Info($"Installation failed. {ToJsonString(create)}");
                return;
            }

            var desc = ExecuteCmdCommand($"sc.exe description \"{serviceSettings.ServiceName}\" \"{serviceSettings.Description}\"");
            var description = DeserializeAnonymousType(desc, new { Command = "", ExitCode = 0, StandardOutput = "", StandardError = "" });

            if (description.StandardOutput != "[SC] ChangeServiceConfig2 SUCCESS\r\n")
            {
                Log.Info($"Installation completed with issues. {ToJsonString(description)}");
            }

            Log.Info("Installation complete. The application was successfully installed and is ready to use.");
        }
        private void ExecuteUninstallOption((string ServiceName, string DisplayName, string Description) serviceSettings)
        {
            var del = ExecuteCmdCommand($"sc.exe delete \"{serviceSettings.ServiceName}\"");
            var delete = DeserializeAnonymousType(del, new { Command = "", ExitCode = 0, StandardOutput = "", StandardError = "" });

            if (delete.StandardOutput != "[SC] DeleteService SUCCESS\r\n")
            {
                Log.Info($"Uninstallation failed. {ToJsonString(delete)}");
                return;
            }

            Log.Info("Uninstallation complete. The application was successfully uninstalled.");
        }
        private static (string ServiceName, string DisplayName, string Description) GetServiceSettings(string serviceSettingsPath)
        {
            var parentNode = new XmlDocument();
            parentNode.Load(serviceSettingsPath);

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
        private string ExecuteCmdCommand(string command)
        {
            using var process = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "C:\\windows\\system32\\cmd.exe",
                    Arguments = $"/c {command}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }
            };
            process.Start();

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();

            process.WaitForExit();

            return ToJsonString(new
            {
                Command = command,
                process.ExitCode,
                StandardOutput = standardOutput,
                StandardError = standardError,
            });
        }
        private static T DeserializeAnonymousType<T>(string json, T anonymousType) => JsonSerializer.Deserialize<T>(json);
        private string ToJsonString<T>(T value) => JsonSerializer.Serialize(value, JsonSerializerOptions);
    }
}