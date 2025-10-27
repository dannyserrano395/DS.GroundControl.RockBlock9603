using System.IO.Ports;
using DS.GroundControl.Lib.Devices;
using DS.GroundControl.Lib.Exceptions;

namespace DS.GroundControl.Lib.Tests
{
    public class RockBlock9603StateTests
    {
        [Test]
        public void Verify_Transitions_To_Canceled_After_Dispose_Without_Usage()
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
        public void Verify_Transitions_To_Faulted_When_ConnectAsync_Called_With_Bad_PortName()
        {
            using var rb = new RockBlock9603();

            Assert.ThrowsAsync<ArgumentException>(async () => await rb.ConnectAsync("BADPORTNAME", 19200, 8, Parity.None, StopBits.One));

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

            Assert.ThrowsAsync<DeviceConnectionException>(async () => await rb.ConnectAsync(19200, 8, Parity.None, StopBits.One));

            Assert.Multiple(() =>
            {
                Assert.That(rb.Connected.IsCompletedSuccessfully, Is.True);
                Assert.That(rb.Faulted.IsCompletedSuccessfully, Is.True);
                Assert.That(rb.Disconnected.IsCompletedSuccessfully, Is.True);
            });
        }
        [Test]
        public async Task Verify_Transitions_To_Connected_After_ConnectAsync()
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
        public async Task Verify_Transitions_Between_Connected_And_Disconnected_Without_Faulting()
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
        public void Verify_No_Transition_When_Not_Connected()
        {
            using var rb = new RockBlock9603();

            Assert.ThrowsAsync<DeviceConnectionException>(async () => await rb.ExecuteCommandAsync("AT"));
            Assert.ThrowsAsync<DeviceConnectionException>(async () => await rb.ExecuteReadyStateTextCommandAsync("test"));
            Assert.ThrowsAsync<DeviceConnectionException>(async () => await rb.ExecuteReadyStateBase64CommandAsync("dGVzdA=="));

            Assert.Multiple(() =>
            {
                Assert.That(rb.Connected.IsCompleted, Is.False);
                Assert.That(rb.Faulted.IsCompleted, Is.False);
                Assert.That(rb.Disconnected.IsCompleted, Is.False);
            });
        }
    }
}