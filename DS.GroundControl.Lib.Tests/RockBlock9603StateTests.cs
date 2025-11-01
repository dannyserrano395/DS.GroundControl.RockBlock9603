using System.IO.Ports;
using DS.GroundControl.Lib.Devices;
using DS.GroundControl.Lib.Exceptions;

namespace DS.GroundControl.Lib.Tests
{
    public class RockBlock9603StateTests
    {
        [Test]
        public void Verify_Transitions_To_Canceled_When_Only_Dispose_Called()
        {
            var rb = new RockBlock9603();
            rb.Dispose();
            Assert.Multiple(() =>
            {
                Assert.That(rb.Connected.IsCanceled, Is.True);
                Assert.That(rb.Faulted.IsCanceled, Is.True);
                Assert.That(rb.Disconnected.IsCanceled, Is.True);
            });
        }
        [Test]
        public void Verify_Transitions_To_Faulted_When_ConnectAsync_Called_With_Invalid_Argument()
        {
            using var rb = new RockBlock9603();

            Assert.ThrowsAsync<ArgumentException>(async () => await rb.ConnectAsync("INVALIDPORTNAME", 19200, 8, Parity.None, StopBits.One));

            Assert.Multiple(() =>
            {
                Assert.That(rb.Connected.IsCompleted, Is.False);
                Assert.That(rb.Faulted.IsCompletedSuccessfully, Is.True);
                Assert.That(rb.Disconnected.IsCompleted, Is.False);
            });
        }
        [Test]
        public async Task Verify_Transitions_To_Faulted_When_ConnectAsync_Called_Twice()
        {
            using var rb = new RockBlock9603();
            await rb.ConnectAsync(19200, 8, Parity.None, StopBits.One);

            Assert.ThrowsAsync<DeviceException>(async () => await rb.ConnectAsync(19200, 8, Parity.None, StopBits.One));

            Assert.Multiple(() =>
            {
                Assert.That(rb.Connected.IsCompletedSuccessfully, Is.True);
                Assert.That(rb.Faulted.IsCompletedSuccessfully, Is.True);
                Assert.That(rb.Disconnected.IsCompletedSuccessfully, Is.True);
            });
        }
        [Test]
        public async Task Verify_Transitions_To_Faulted_When_ExecuteCommandAsync_Called_With_Invalid_Command()
        {
            using var rb = new RockBlock9603();
            await rb.ConnectAsync(19200, 8, Parity.None, StopBits.One);

            Assert.CatchAsync(async () => await rb.ExecuteCommandAsync("INVALIDCOMMAND"));

            Assert.Multiple(() =>
            {
                Assert.That(rb.Connected.IsCompletedSuccessfully, Is.True);
                Assert.That(rb.Faulted.IsCompletedSuccessfully, Is.True);
                Assert.That(rb.Disconnected.IsCompletedSuccessfully, Is.True);
            });
        }
        [Test]
        public async Task Verify_Transitions_To_Faulted_When_ExecuteReadyStateTextCommandAsync_Called_Unexpectedly()
        {
            using var rb = new RockBlock9603();
            await rb.ConnectAsync(19200, 8, Parity.None, StopBits.One);

            Assert.CatchAsync(async () => await rb.ExecuteReadyStateTextCommandAsync("TEST"));

            Assert.Multiple(() =>
            {
                Assert.That(rb.Connected.IsCompletedSuccessfully, Is.True);
                Assert.That(rb.Faulted.IsCompletedSuccessfully, Is.True);
                Assert.That(rb.Disconnected.IsCompletedSuccessfully, Is.True);
            });
        }
        [Test]
        public async Task Verify_Transitions_To_Faulted_When_ExecuteReadyStateBase64CommandAsync_Called_Unexpectedly()
        {
            using var rb = new RockBlock9603();
            await rb.ConnectAsync(19200, 8, Parity.None, StopBits.One);

            Assert.CatchAsync(async () => await rb.ExecuteReadyStateBase64CommandAsync("VEVTVA=="));

            Assert.Multiple(() =>
            {
                Assert.That(rb.Connected.IsCompletedSuccessfully, Is.True);
                Assert.That(rb.Faulted.IsCompletedSuccessfully, Is.True);
                Assert.That(rb.Disconnected.IsCompletedSuccessfully, Is.True);
            });
        }
        [Test]
        public async Task Verify_Transitions_To_Connected_When_ConnectAsync_Called()
        {
            using var rb = new RockBlock9603();
            await rb.ConnectAsync(19200, 8, Parity.None, StopBits.One);
            Assert.Multiple(() =>
            {
                Assert.That(rb.Connected.IsCompletedSuccessfully, Is.True);
                Assert.That(rb.Faulted.IsCompleted, Is.False);
                Assert.That(rb.Disconnected.IsCompleted, Is.False);
            });
        }
        [Test]
        public async Task Verify_Transitions_To_Disconnected_When_DisconnectAsync_Called()
        {
            using var rb = new RockBlock9603();
            await rb.ConnectAsync(19200, 8, Parity.None, StopBits.One);
            await rb.DisconnectAsync();
            Assert.Multiple(() =>
            {
                Assert.That(rb.Connected.IsCompletedSuccessfully, Is.True);
                Assert.That(rb.Faulted.IsCompleted, Is.False);
                Assert.That(rb.Disconnected.IsCompletedSuccessfully, Is.True);
            });
        }
        [Test]
        public void Verify_No_Transition_When_ConnectAsync_Never_Called()
        {
            using var rb = new RockBlock9603();

            Assert.ThrowsAsync<DeviceException>(async () => await rb.ExecuteCommandAsync("AT"));
            Assert.ThrowsAsync<DeviceException>(async () => await rb.ExecuteReadyStateTextCommandAsync("test"));
            Assert.ThrowsAsync<DeviceException>(async () => await rb.ExecuteReadyStateBase64CommandAsync("dGVzdA=="));
            Assert.ThrowsAsync<DeviceException>(async () => await rb.DisconnectAsync());

            Assert.Multiple(() =>
            {
                Assert.That(rb.Connected.IsCompleted, Is.False);
                Assert.That(rb.Faulted.IsCompleted, Is.False);
                Assert.That(rb.Disconnected.IsCompleted, Is.False);
            });
        }
    }
}