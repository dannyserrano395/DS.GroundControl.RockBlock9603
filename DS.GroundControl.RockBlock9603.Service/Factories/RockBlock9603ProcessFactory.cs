using DS.GroundControl.RockBlock9603.Service.Manager;

namespace DS.GroundControl.RockBlock9603.Service.Factories
{
    public class RockBlock9603ProcessFactory : IRockBlock9603ProcessFactory
    {
        private IServiceProvider ServiceProvider { get; }

        public RockBlock9603ProcessFactory(IServiceProvider serviceProvider) => ServiceProvider = serviceProvider;

        public IRockBlock9603Session Create() => ActivatorUtilities.CreateInstance<RockBlock9603Session>(ServiceProvider);
    }
}