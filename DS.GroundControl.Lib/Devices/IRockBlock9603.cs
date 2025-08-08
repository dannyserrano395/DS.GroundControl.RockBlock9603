namespace DS.GroundControl.Lib.Devices
{
    public interface IRockBlock9603 : IDisposable
    {
        CancellationToken Connected { get; }
        CancellationToken Faulted { get; }
        Task ConnectAsync();
        Task<(string Command, string Response, string Result)> ExecuteAsync(string command);
        Task<(string Command, string Response, string Result)> ExecuteReadyStateTextCommandAsync(string command);
        Task<(string Command, string Response, string Result)> ExecuteReadyStateBinaryCommandAsync(string command);
    }
}