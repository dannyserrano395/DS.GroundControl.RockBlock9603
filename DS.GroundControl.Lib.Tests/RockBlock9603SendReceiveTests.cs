using DS.GroundControl.Lib.Devices;
using DS.GroundControl.Lib.Extensions;

namespace DS.GroundControl.Lib.Tests
{
    public class RockBlock9603SendReceiveTests
    {
        private IRockBlock9603 RockBlock9603;

        [OneTimeSetUp]
        public void Setup()
        {
            RockBlock9603 = new RockBlock9603();
            RockBlock9603.StartAsync();

            var running = RockBlock9603.Running.WhenCanceledAsync();
            if (running.TimeoutAfter(TimeSpan.FromSeconds(10)))
            {
                Assert.Fail();
            }
        }

        [OneTimeTearDown]
        public void Teardown()
        {
            RockBlock9603.StopAsync();
            var stopped = RockBlock9603.Stopped.WhenCanceledAsync();
            stopped.TimeoutAfter(TimeSpan.FromSeconds(10));
        }

        [Test]
        public async Task SendText_UsingSBDWTWithEquals_ReturnsOkOrZero()
        {
            var sbdwt = await RockBlock9603.WriteWithCarriageReturnAsync("AT+SBDWT=test");
            Assert.That(sbdwt.Result, Is.EqualTo("OK").Or.EqualTo("0"));

            var sbdtc = await RockBlock9603.WriteWithCarriageReturnAsync("AT+SBDTC");
            Assert.That(sbdtc.Result, Is.EqualTo("OK").Or.EqualTo("0"));

            var sbdrt = await RockBlock9603.WriteWithCarriageReturnAsync("AT+SBDRT");
            Assert.Multiple(() =>
            {
                Assert.That(sbdrt.Command, Is.EqualTo("AT+SBDRT"));
                Assert.That(sbdrt.Response, Is.EqualTo("+SBDRT:test"));
                Assert.That(sbdrt.Result, Is.EqualTo("OK").Or.EqualTo("0"));
            });
        }
        [Test]
        public async Task SendText_UsingSBDWTWithoutEquals_ReturnsOkOrZero()
        {
            var sbdwt = await RockBlock9603.WriteWithCarriageReturnAsync("AT+SBDWT");
            Assert.That(sbdwt.Response, Is.EqualTo("READY"));

            var write = await RockBlock9603.WriteWithCarriageReturnAsync("test");
            Assert.Multiple(() =>
            {
                Assert.That(write.Response, Is.EqualTo("0"));
                Assert.That(write.Result, Is.EqualTo("OK").Or.EqualTo("0"));
            });

            var sbdtc = await RockBlock9603.WriteWithCarriageReturnAsync("AT+SBDTC");
            Assert.That(sbdtc.Result, Is.EqualTo("OK").Or.EqualTo("0"));

            var sbdrt = await RockBlock9603.WriteWithCarriageReturnAsync("AT+SBDRT");
            Assert.Multiple(() =>
            {
                Assert.That(sbdrt.Command, Is.EqualTo("AT+SBDRT"));
                Assert.That(sbdrt.Response, Is.EqualTo("+SBDRT:test"));
                Assert.That(sbdrt.Result, Is.EqualTo("OK").Or.EqualTo("0"));
            });
        }
        [Test]
        public async Task SendBinary_UsingSBDWB_ReturnsOkOrZero()
        {
            var sbdwb = await RockBlock9603.WriteWithCarriageReturnAsync("AT+SBDWB=4");
            Assert.That(sbdwb.Response, Is.EqualTo("READY"));

            var write = await RockBlock9603.WriteWithChecksumAsync("test");
            Assert.Multiple(() =>
            {
                Assert.That(write.Response, Is.EqualTo("0"));
                Assert.That(write.Result, Is.EqualTo("OK").Or.EqualTo("0"));
            });

            var sbdtc = await RockBlock9603.WriteWithCarriageReturnAsync("AT+SBDTC");
            Assert.That(sbdtc.Result, Is.EqualTo("OK").Or.EqualTo("0"));

            var sbdrb = await RockBlock9603.WriteWithCarriageReturnAsync("AT+SBDRB");
            Assert.Multiple(() =>
            {
                Assert.That(sbdrb.Command, Is.EqualTo("AT+SBDRB"));
                Assert.That(sbdrb.Response, Is.EqualTo("\0\u0004test\u0001?"));
                Assert.That(sbdrb.Result, Is.EqualTo("OK").Or.EqualTo("0"));
            });
        }
    }
}