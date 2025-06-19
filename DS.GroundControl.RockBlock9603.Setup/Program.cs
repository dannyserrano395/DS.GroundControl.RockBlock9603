using log4net;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using ILoggerFactory = DS.GroundControl.RockBlock9603.Log4Net.ILoggerFactory;
using LoggerFactory = DS.GroundControl.RockBlock9603.Log4Net.LoggerFactory;

namespace DS.GroundControl.RockBlock9603.Setup
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            GlobalContext.Properties["startTime"] = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");
            var provider = InitializeServiceProvider();
            var app = provider.GetRequiredService<IApplication>();
            await app.StartAsync(args);
        }
        private static ServiceProvider InitializeServiceProvider()
        {
            var services = new ServiceCollection();
            services.AddSingleton<ILoggerFactory, LoggerFactory>();
            services.AddTransient(provider =>
            {
                var factory = provider.GetRequiredService<ILoggerFactory>();
                return factory.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
            });
            services.AddSingleton<IApplication, Application>();
            return services.BuildServiceProvider();
        }
        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = (Exception)e.ExceptionObject;
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType).Fatal(ex.Message, ex);
        }
    }
}