namespace DS.GroundControl.Lib.Devices
{
    public interface IRockBlock9603
    {
        CancellationToken Canceled { get; }
        CancellationToken Started { get; }
        CancellationToken Running { get; }
        CancellationToken Stopped { get; }
        CancellationToken Faulted { get; }
        Task StartAsync();
        Task StopAsync();
        Task<(string Command, string Response, string Result)> AppendCarriageReturnAndWriteAsync(string input);
        Task<(string Command, string Response, string Result)> AppendChecksumAndWriteAsync(string input);
    }
}