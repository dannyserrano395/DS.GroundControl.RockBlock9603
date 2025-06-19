using Microsoft.Extensions.DependencyInjection;
using DS.GroundControl.Lib.Devices.Factories;

namespace DS.GroundControl.RockBlock9603.CommandLine
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            var provider = InitializeServiceProvider();
            var app = provider.GetRequiredService<IApplication>();
            await app.CommandLineAsync();
        }
        private static ServiceProvider InitializeServiceProvider()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IRockBlock9603Factory, RockBlock9603Factory>();
            services.AddSingleton<IApplication, Application>();
            return services.BuildServiceProvider();
        }
        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = (Exception)e.ExceptionObject;
            Console.WriteLine(ex.ToString());
        }
    }
}