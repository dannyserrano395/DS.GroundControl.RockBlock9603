namespace DS.GroundControl.RockBlock9603.Service.Manager
{
    public interface IRockBlock9603Session : IDisposable
    {
        Task Connected { get; }
        Task Disconnected { get; }
        Task Faulted { get; }
        Task StartAsync();
    }
}