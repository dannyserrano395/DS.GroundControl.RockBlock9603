using System.Text;

namespace DS.GroundControl.Lib.Extensions
{
    public static class StreamExtensions
    {
        public static async Task<string> ReadToAsync(this Stream stream, string value)
        {
            var buffer = new byte[1];
            var builder = new StringBuilder();
            while (true)
            {
                await stream.ReadExactlyAsync(buffer, 0, 1);
                builder.Append(buffer[0]);
                if (builder.EndsWith(value))
                {
                    return builder.ToString(0, builder.Length - value.Length);
                }
            }
        }
    }
}