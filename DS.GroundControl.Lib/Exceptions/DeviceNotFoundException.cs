namespace DS.GroundControl.Lib.Exceptions
{
    public class DeviceNotFoundException : Exception
    {
        public DeviceNotFoundException() { }
        public DeviceNotFoundException(string message) : base(message) { }
    }
}