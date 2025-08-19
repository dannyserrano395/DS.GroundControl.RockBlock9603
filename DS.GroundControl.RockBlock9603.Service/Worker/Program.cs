using log4net;
using System.Reflection;
using ILoggerFactory = DS.GroundControl.RockBlock9603.Service.Log4Net.ILoggerFactory;
using LoggerFactory = DS.GroundControl.RockBlock9603.Service.Log4Net.LoggerFactory;
using IConfigurationManager = DS.GroundControl.RockBlock9603.Service.Configuration.IConfigurationManager;
using ConfigurationManager = DS.GroundControl.RockBlock9603.Service.Configuration.ConfigurationManager;
using DS.GroundControl.Lib.Factories;

namespace DS.GroundControl.RockBlock9603.Service.Worker
{
    public class Program
    {
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            GlobalContext.Properties["startTime"] = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");
            InitializeHost(args).Run();
        }
        private static IHost InitializeHost(string[] args) =>
            Host
            .CreateDefaultBuilder(args)
            .UseWindowsService()
            .UseSystemd()
            .ConfigureServices(configure =>
            {
                configure
                .AddSingleton<ILoggerFactory, LoggerFactory>()
                .AddTransient(provider =>
                {
                    var factory = provider.GetRequiredService<ILoggerFactory>();
                    return factory.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
                })
                .AddSingleton<IConfigurationManager, ConfigurationManager>()
                .AddSingleton<IRockBlock9603Factory, RockBlock9603Factory>()
                .AddHostedService<BackgroundWorker>();
            })
            .ConfigureHostOptions(options =>
            {
                options.ShutdownTimeout = TimeSpan.FromSeconds(10);
            })
            .Build();
        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = (Exception)e.ExceptionObject;
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType).Fatal(ex.Message, ex);
        }
    }
}