using System.Text;
using System.IO.Ports;
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
            _ = RockBlock9603.ConnectAsync(19200, 8, Parity.None, StopBits.One);
            await RockBlock9603.Connected.WaitAsync(TimeSpan.FromSeconds(3));
        }

        [OneTimeTearDown]
        public void Teardown()
        {
            RockBlock9603.Dispose();
        }

        [Test]
        public async Task SendText_UsingSBDWTWithEqualsInVerboseMode_ReturnsOk()
        {
            var v1 = await RockBlock9603.ExecuteCommandAsync("ATV1");
            Assert.That(v1.Result, Is.EqualTo("OK"));

            var sbdwt = await RockBlock9603.ExecuteCommandAsync("AT+SBDWT=test");
            Assert.That(sbdwt.Result, Is.EqualTo("OK"));

            var sbdtc = await RockBlock9603.ExecuteCommandAsync("AT+SBDTC");
            Assert.That(sbdtc.Result, Is.EqualTo("OK"));

            var sbdrt = await RockBlock9603.ExecuteCommandAsync("AT+SBDRT");
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
            var v0 = await RockBlock9603.ExecuteCommandAsync("ATV0");
            Assert.That(v0.Result, Is.EqualTo("0"));

            var sbdwt = await RockBlock9603.ExecuteCommandAsync("AT+SBDWT=test");
            Assert.That(sbdwt.Result, Is.EqualTo("0"));

            var sbdtc = await RockBlock9603.ExecuteCommandAsync("AT+SBDTC");
            Assert.That(sbdtc.Result, Is.EqualTo("0"));

            var sbdrt = await RockBlock9603.ExecuteCommandAsync("AT+SBDRT");
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
            var v1 = await RockBlock9603.ExecuteCommandAsync("ATV1");
            Assert.That(v1.Result, Is.EqualTo("OK"));

            var sbdwt = await RockBlock9603.ExecuteCommandAsync("AT+SBDWT");
            Assert.That(sbdwt.Response, Is.EqualTo("READY"));

            var write = await RockBlock9603.ExecuteReadyStateTextCommandAsync("test");
            Assert.Multiple(() =>
            {
                Assert.That(write.Response, Is.EqualTo("0"));
                Assert.That(write.Result, Is.EqualTo("OK"));
            });

            var sbdtc = await RockBlock9603.ExecuteCommandAsync("AT+SBDTC");
            Assert.That(sbdtc.Result, Is.EqualTo("OK"));

            var sbdrt = await RockBlock9603.ExecuteCommandAsync("AT+SBDRT");
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
            var v0 = await RockBlock9603.ExecuteCommandAsync("ATV0");
            Assert.That(v0.Result, Is.EqualTo("0"));

            var sbdwt = await RockBlock9603.ExecuteCommandAsync("AT+SBDWT");
            Assert.That(sbdwt.Response, Is.EqualTo("READY"));

            var write = await RockBlock9603.ExecuteReadyStateTextCommandAsync("test");
            Assert.Multiple(() =>
            {
                Assert.That(write.Response, Is.EqualTo("0"));
                Assert.That(write.Result, Is.EqualTo("0"));
            });

            var sbdtc = await RockBlock9603.ExecuteCommandAsync("AT+SBDTC");
            Assert.That(sbdtc.Result, Is.EqualTo("0"));

            var sbdrt = await RockBlock9603.ExecuteCommandAsync("AT+SBDRT");
            Assert.Multiple(() =>
            {
                Assert.That(sbdrt.Command, Is.EqualTo("AT+SBDRT"));
                Assert.That(sbdrt.Response, Is.EqualTo("+SBDRT:test"));
                Assert.That(sbdrt.Result, Is.EqualTo("0"));
            });
        }
        [Test]
        public async Task SendBase64_UsingSBDWBInVerboseMode_ReturnsOk()
        {
            var v1 = await RockBlock9603.ExecuteCommandAsync("ATV1");
            Assert.That(v1.Result, Is.EqualTo("OK"));

            var sbdwb = await RockBlock9603.ExecuteCommandAsync("AT+SBDWB=4");
            Assert.That(sbdwb.Response, Is.EqualTo("READY"));

            var base64 = Convert.ToBase64String(Encoding.ASCII.GetBytes("test"));
            var write = await RockBlock9603.ExecuteReadyStateBase64CommandAsync(base64);
            Assert.Multiple(() =>
            {
                Assert.That(write.Response, Is.EqualTo("0"));
                Assert.That(write.Result, Is.EqualTo("OK"));
            });

            var sbdtc = await RockBlock9603.ExecuteCommandAsync("AT+SBDTC");
            Assert.That(sbdtc.Result, Is.EqualTo("OK"));

            var sbdrb = await RockBlock9603.ExecuteCommandAsync("AT+SBDRB");
            Assert.Multiple(() =>
            {
                Assert.That(sbdrb.Command, Is.EqualTo("AT+SBDRB"));
                Assert.That(sbdrb.Response, Is.EqualTo("AAR0ZXN0AcA="));
                Assert.That(sbdrb.Result, Is.EqualTo("OK"));
            });
        }
        [Test]
        public async Task SendBase64_UsingSBDWBInNumericMode_ReturnsZero()
        {
            var v0 = await RockBlock9603.ExecuteCommandAsync("ATV0");
            Assert.That(v0.Result, Is.EqualTo("0"));

            var sbdwb = await RockBlock9603.ExecuteCommandAsync("AT+SBDWB=4");
            Assert.That(sbdwb.Response, Is.EqualTo("READY"));

            var base64 = Convert.ToBase64String(Encoding.ASCII.GetBytes("test"));
            var write = await RockBlock9603.ExecuteReadyStateBase64CommandAsync(base64);
            Assert.Multiple(() =>
            {
                Assert.That(write.Response, Is.EqualTo("0"));
                Assert.That(write.Result, Is.EqualTo("0"));
            });

            var sbdtc = await RockBlock9603.ExecuteCommandAsync("AT+SBDTC");
            Assert.That(sbdtc.Result, Is.EqualTo("0"));

            var sbdrb = await RockBlock9603.ExecuteCommandAsync("AT+SBDRB");
            Assert.Multiple(() =>
            {
                Assert.That(sbdrb.Command, Is.EqualTo("AT+SBDRB"));
                Assert.That(sbdrb.Response, Is.EqualTo("AAR0ZXN0AcA="));
                Assert.That(sbdrb.Result, Is.EqualTo("0"));
            });
        }
    }
}