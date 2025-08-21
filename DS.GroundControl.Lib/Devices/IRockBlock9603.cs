namespace DS.GroundControl.Lib.Devices
{
    public interface IRockBlock9603 : IDisposable
    {
        Task Connected { get; }
        Task Disconnected { get; }
        Task Faulted { get; }
        Task ConnectAsync();
        Task<(string Command, string Response, string Result)> ExecuteCommandAsync(string command);
        Task<(string Command, string Response, string Result)> ExecuteReadyStateTextCommandAsync(string command);
        Task<(string Command, string Response, string Result)> ExecuteReadyStateBinaryCommandAsync(string command);
    }
}