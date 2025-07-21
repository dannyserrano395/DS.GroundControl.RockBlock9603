using DS.GroundControl.Lib.Devices;
using Microsoft.Extensions.DependencyInjection;

namespace DS.GroundControl.Lib.Factories
{
    public class RockBlock9603Factory : IRockBlock9603Factory
    {
        private IServiceProvider ServiceProvider { get; }

        public RockBlock9603Factory(IServiceProvider serviceProvider) => ServiceProvider = serviceProvider;

        public IRockBlock9603 Create() => ActivatorUtilities.CreateInstance<RockBlock9603>(ServiceProvider);
    }
}