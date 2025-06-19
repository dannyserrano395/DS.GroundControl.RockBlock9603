namespace DS.GroundControl.RockBlock9603.Service.Manager
{
    public interface IRockBlock9603Process
    {
        CancellationToken Canceled { get; }
        CancellationToken Started { get; }
        CancellationToken Running { get; }
        CancellationToken Stopped { get; }
        CancellationToken Faulted { get; }
        Task StartAsync();
        Task StopAsync();
    }
}