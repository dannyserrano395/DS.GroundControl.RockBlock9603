namespace DS.GroundControl.RockBlock9603.Service.Manager
{
    public interface IRockBlock9603SessionManager
    {
        Task StartAsync();
        Task StopAsync();
    }
}