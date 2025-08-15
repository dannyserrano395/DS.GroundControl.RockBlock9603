using DS.GroundControl.Lib.Devices;

namespace DS.GroundControl.Lib.Tests
{
    public class RockBlock9603SendReceiveTests
    {
        private RockBlock9603 RockBlock9603;

        [OneTimeSetUp]
        public async Task Setup()
        {
            RockBlock9603 = new RockBlock9603();
            _ = RockBlock9603.ConnectAsync();
            await RockBlock9603.Connected;
        }

        [OneTimeTearDown]
        public void Teardown()
        {
            RockBlock9603.Dispose();
        }

        [Test]
        public async Task SendText_UsingSBDWTWithEqualsInVerboseMode_ReturnsOk()
        {
            var v1 = await RockBlock9603.ExecuteAsync("ATV1");
            Assert.That(v1.Result, Is.EqualTo("OK"));

            var sbdwt = await RockBlock9603.ExecuteAsync("AT+SBDWT=test");
            Assert.That(sbdwt.Result, Is.EqualTo("OK"));

            var sbdtc = await RockBlock9603.ExecuteAsync("AT+SBDTC");
            Assert.That(sbdtc.Result, Is.EqualTo("OK"));

            var sbdrt = await RockBlock9603.ExecuteAsync("AT+SBDRT");
            Assert.Multiple(() =>
            {
                Assert.That(sbdrt.Command, Is.EqualTo("AT+SBDRT"));
                Assert.That(sbdrt.Response, Is.EqualTo("+SBDRT:test"));
                Assert.That(sbdrt.Result, Is.EqualTo("OK"));
            });
        }
        [Test]
        public async Task SendText_UsingSBDWTWithEqualsInNumericMode_ReturnsZero()
        {
            var v0 = await RockBlock9603.ExecuteAsync("ATV0");
            Assert.That(v0.Result, Is.EqualTo("0"));

            var sbdwt = await RockBlock9603.ExecuteAsync("AT+SBDWT=test");
            Assert.That(sbdwt.Result, Is.EqualTo("0"));

            var sbdtc = await RockBlock9603.ExecuteAsync("AT+SBDTC");
            Assert.That(sbdtc.Result, Is.EqualTo("0"));

            var sbdrt = await RockBlock9603.ExecuteAsync("AT+SBDRT");
            Assert.Multiple(() =>
            {
                Assert.That(sbdrt.Command, Is.EqualTo("AT+SBDRT"));
                Assert.That(sbdrt.Response, Is.EqualTo("+SBDRT:test"));
                Assert.That(sbdrt.Result, Is.EqualTo("0"));
            });
        }
        [Test]
        public async Task SendText_UsingSBDWTWithoutEqualsInVerboseMode_ReturnsOk()
        {
            var v1 = await RockBlock9603.ExecuteAsync("ATV1");
            Assert.That(v1.Result, Is.EqualTo("OK"));

            var sbdwt = await RockBlock9603.ExecuteAsync("AT+SBDWT");
            Assert.That(sbdwt.Response, Is.EqualTo("READY"));

            var write = await RockBlock9603.ExecuteReadyStateTextCommandAsync("test");
            Assert.Multiple(() =>
            {
                Assert.That(write.Response, Is.EqualTo("0"));
                Assert.That(write.Result, Is.EqualTo("OK"));
            });

            var sbdtc = await RockBlock9603.ExecuteAsync("AT+SBDTC");
            Assert.That(sbdtc.Result, Is.EqualTo("OK"));

            var sbdrt = await RockBlock9603.ExecuteAsync("AT+SBDRT");
            Assert.Multiple(() =>
            {
                Assert.That(sbdrt.Command, Is.EqualTo("AT+SBDRT"));
                Assert.That(sbdrt.Response, Is.EqualTo("+SBDRT:test"));
                Assert.That(sbdrt.Result, Is.EqualTo("OK"));
            });
        }
        [Test]
        public async Task SendText_UsingSBDWTWithoutEqualsInNumericMode_ReturnsZero()
        {
            var v0 = await RockBlock9603.ExecuteAsync("ATV0");
            Assert.That(v0.Result, Is.EqualTo("0"));

            var sbdwt = await RockBlock9603.ExecuteAsync("AT+SBDWT");
            Assert.That(sbdwt.Response, Is.EqualTo("READY"));

            var write = await RockBlock9603.ExecuteReadyStateTextCommandAsync("test");
            Assert.Multiple(() =>
            {
                Assert.That(write.Response, Is.EqualTo("0"));
                Assert.That(write.Result, Is.EqualTo("0"));
            });

            var sbdtc = await RockBlock9603.ExecuteAsync("AT+SBDTC");
            Assert.That(sbdtc.Result, Is.EqualTo("0"));

            var sbdrt = await RockBlock9603.ExecuteAsync("AT+SBDRT");
            Assert.Multiple(() =>
            {
                Assert.That(sbdrt.Command, Is.EqualTo("AT+SBDRT"));
                Assert.That(sbdrt.Response, Is.EqualTo("+SBDRT:test"));
                Assert.That(sbdrt.Result, Is.EqualTo("0"));
            });
        }
        [Test]
        public async Task SendBinary_UsingSBDWBInVerboseMode_ReturnsOk()
        {
            var v1 = await RockBlock9603.ExecuteAsync("ATV1");
            Assert.That(v1.Result, Is.EqualTo("OK"));

            var sbdwb = await RockBlock9603.ExecuteAsync("AT+SBDWB=4");
            Assert.That(sbdwb.Response, Is.EqualTo("READY"));

            var write = await RockBlock9603.ExecuteReadyStateBinaryCommandAsync("test");
            Assert.Multiple(() =>
            {
                Assert.That(write.Response, Is.EqualTo("0"));
                Assert.That(write.Result, Is.EqualTo("OK"));
            });

            var sbdtc = await RockBlock9603.ExecuteAsync("AT+SBDTC");
            Assert.That(sbdtc.Result, Is.EqualTo("OK"));

            var sbdrb = await RockBlock9603.ExecuteAsync("AT+SBDRB");
            Assert.Multiple(() =>
            {
                Assert.That(sbdrb.Command, Is.EqualTo("AT+SBDRB"));
                Assert.That(sbdrb.Response, Is.EqualTo("\0\u0004test\u0001?"));
                Assert.That(sbdrb.Result, Is.EqualTo("OK"));
            });
        }
        [Test]
        public async Task SendBinary_UsingSBDWBInNumericMode_ReturnsZero()
        {
            var v0 = await RockBlock9603.ExecuteAsync("ATV0");
            Assert.That(v0.Result, Is.EqualTo("0"));

            var sbdwb = await RockBlock9603.ExecuteAsync("AT+SBDWB=4");
            Assert.That(sbdwb.Response, Is.EqualTo("READY"));

            var write = await RockBlock9603.ExecuteReadyStateBinaryCommandAsync("test");
            Assert.Multiple(() =>
            {
                Assert.That(write.Response, Is.EqualTo("0"));
                Assert.That(write.Result, Is.EqualTo("0"));
            });

            var sbdtc = await RockBlock9603.ExecuteAsync("AT+SBDTC");
            Assert.That(sbdtc.Result, Is.EqualTo("0"));

            var sbdrb = await RockBlock9603.ExecuteAsync("AT+SBDRB");
            Assert.Multiple(() =>
            {
                Assert.That(sbdrb.Command, Is.EqualTo("AT+SBDRB"));
                Assert.That(sbdrb.Response, Is.EqualTo("\0\u0004test\u0001?"));
                Assert.That(sbdrb.Result, Is.EqualTo("0"));
            });
        }
    }
}