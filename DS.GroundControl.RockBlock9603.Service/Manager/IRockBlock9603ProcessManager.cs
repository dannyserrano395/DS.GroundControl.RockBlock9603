namespace DS.GroundControl.RockBlock9603.Service.Manager
{
    public interface IRockBlock9603ProcessManager
    {
        Task StartAsync();
        Task StopAsync();
    }
}