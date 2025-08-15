using System.Text;
using DS.GroundControl.Lib.Extensions;

namespace DS.GroundControl.Lib.Tests
{
    public class StreamReadToExtensionsTests
    {
        private static MemoryStream MakeStream(string data) => new MemoryStream(Encoding.ASCII.GetBytes(data));

        [TestCase("AT+SBDWT\rAT\r\n", "\r\n", "AT+SBDWT\rAT")]
        [TestCase("\r\n", "\r\n", "")]
        [TestCase("OK\r\n", "\r\n", "OK")]
        [TestCase("\nOK\r\n", "\r", "\nOK")]
        [TestCase("\rOK\r\n", "\n", "\rOK\r")]
        public async Task ReadToAsync_Returns_Text_Before_Delimiter(string str, string value, string expected)
        {
            using var s = MakeStream(str);
            var result = await s.ReadToAsync(value);
            Assert.That(result, Is.EqualTo(expected));
        }
        [Test]
        public void ReadToAsync_Throws_EndOfStream_When_Delimiter_Not_Found()
        {
            using var stream = MakeStream("NO DELIMITER HERE");
            Assert.That(async () => await stream.ReadToAsync("\r\n"),
                Throws.TypeOf<EndOfStreamException>());
        }
        [Test]
        public void EndsWith_MatchAtEnd_ReturnsTrue()
        {
            var list = new List<byte>(Encoding.ASCII.GetBytes("AT+SBDWB"));
            var value = Encoding.ASCII.GetBytes("WB");
            Assert.That(list.EndsWith(value), Is.True);
        }
        [Test]
        public void EndsWith_NoMatchAtEnd_ReturnsFalse()
        {
            var list = new List<byte>(Encoding.ASCII.GetBytes("AT+SBDWB"));
            var value = Encoding.ASCII.GetBytes("WT");
            Assert.That(list.EndsWith(value), Is.False);
        }
        [Test]
        public void EndsWith_ListShorterThanValue_ReturnsFalse()
        {
            var list = new List<byte>(Encoding.ASCII.GetBytes("AT"));
            var value = Encoding.ASCII.GetBytes("AT+SBDWB");
            Assert.That(list.EndsWith(value), Is.False);
        }
        [Test]
        public void EndsWith_EmptyPattern_ReturnsTrue()
        {
            var list = new List<byte>(Encoding.ASCII.GetBytes("ABC"));
            var value = Array.Empty<byte>();
            Assert.That(list.EndsWith(value), Is.True);
        }
        [Test]
        public async Task ReadByteAsync_ShouldReturnByteValue_WhenStreamContainsData()
        {
            using var s = MakeStream("a");
            var result = await s.ReadByteAsync();
            Assert.That(result, Is.EqualTo(97));
        }
        [Test]
        public async Task ReadByteAsync_ShouldReturnMinusOne_WhenStreamHasNoData()
        {
            using var s = MakeStream("");
            var result = await s.ReadByteAsync();
            Assert.That(result, Is.EqualTo(-1));
        }
    }
}