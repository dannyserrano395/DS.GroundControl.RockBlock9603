﻿namespace DS.GroundControl.Lib.Exceptions
{
    public class DeviceException : Exception
    {
        public DeviceException()
        {

        }
        public DeviceException(string message): base(message)
        {

        }
        public DeviceException(string message, Exception innerException) : base(message, innerException)
        {

        }
    }
}