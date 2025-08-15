using System.Text;

namespace DS.GroundControl.Lib.Extensions
{
    public static class StreamExtensions
    {
        public static async Task<string> ReadToAsync(this Stream stream, string value, CancellationToken token = default)
        {
            var delimiter = Encoding.ASCII.GetBytes(value);
            var result = new List<byte>();
            var buffer = new byte[1];
            
            while (await stream.ReadAsync(buffer, 0, 1, token) != 0)
            {
                result.Add(buffer[0]);
                if (result.Count >= delimiter.Length && result.EndsWith(delimiter))
                {
                    return Encoding.ASCII.GetString(result.GetRange(0, result.Count - delimiter.Length).ToArray());
                }
            }
            throw new EndOfStreamException();
        }
        public static async Task<int> ReadByteAsync(this Stream stream, CancellationToken token = default)
        {
            var buffer = new byte[1];
            var b = await stream.ReadAsync(buffer, 0, 1, token);
            return b == 1 ? buffer[0] : -1;
        }
    }
}