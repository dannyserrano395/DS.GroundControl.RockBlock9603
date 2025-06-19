namespace DS.GroundControl.RockBlock9603.Service.Configuration
{
    public interface IConfigurationManager
    {
        ServiceConfiguration ServiceConfiguration { get; }
        WorkerConfiguration WorkerConfiguration { get; }
    }
}