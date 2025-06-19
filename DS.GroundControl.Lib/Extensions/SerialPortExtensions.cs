using System.IO.Ports;

namespace DS.GroundControl.Lib.Extensions
{
    public static class SerialPortExtensions
    {
        public static void ReadExactly(this SerialPort serialPort, byte[] buffer, int offset, int count)
        {
            for (int i = offset; i < count; i++)
            {
                buffer[i] = (byte)serialPort.ReadByte();
            }
        }
    }
}