namespace DS.GroundControl.Lib.Exceptions
{
    public class DeviceNotRunningException : Exception
    {
        public DeviceNotRunningException() { }
        public DeviceNotRunningException(string message) : base(message) { }
    }
}