namespace DS.GroundControl.Lib.Extensions
{
    public static class StreamExtensions
    {
        public static async Task<string> ReadToEndAsync(this Stream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
    }
}