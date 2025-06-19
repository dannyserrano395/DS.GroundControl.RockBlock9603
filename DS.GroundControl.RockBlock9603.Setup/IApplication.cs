namespace DS.GroundControl.RockBlock9603.Setup
{
    public interface IApplication
    {
        Task<int> StartAsync(string[] args);
    }
}