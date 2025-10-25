using System.IO.Ports;

namespace DS.GroundControl.Lib.Devices
{
    public interface IRockBlock9603 : IDisposable
    {
        Task Connected { get; }
        Task Disconnected { get; }
        Task Faulted { get; }
        Task ConnectAsync(int baudRate, int dataBits, Parity parity, StopBits stopBits);
        Task ConnectAsync(string portName, int baudRate, int dataBits, Parity parity, StopBits stopBits);
        Task<(string Command, string Response, string Result)> ExecuteCommandAsync(string command);
        Task<(string Command, string Response, string Result)> ExecuteReadyStateTextCommandAsync(string command);
        Task<(string Command, string Response, string Result)> ExecuteReadyStateBase64CommandAsync(string command);
    }
}