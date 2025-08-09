namespace DS.GroundControl.Lib.Exceptions
{
    public class DeviceNotConnectedException : Exception
    {
        public DeviceNotConnectedException() { }
        public DeviceNotConnectedException(string message) : base(message) { }
    }
}