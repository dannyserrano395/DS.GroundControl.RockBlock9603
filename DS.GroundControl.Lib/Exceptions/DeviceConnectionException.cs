namespace DS.GroundControl.Lib.Exceptions
{
    public class DeviceConnectionException : Exception
    {
        public DeviceConnectionException()
        {

        }
        public DeviceConnectionException(string message): base(message)
        {

        }
        public DeviceConnectionException(string message, Exception innerException) : base(message, innerException)
        {

        }
    }
}