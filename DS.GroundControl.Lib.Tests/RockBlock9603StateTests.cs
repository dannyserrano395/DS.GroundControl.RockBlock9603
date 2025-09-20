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
        public async Task Verify_Transitions_To_Faulted_When_ConnectAsync_Called_Twice()
        {
            using var rb = new RockBlock9603();
            var connect = rb.ConnectAsync();
            await connect;

            Assert.ThrowsAsync<DeviceConnectionException>(async () => await rb.ConnectAsync());

            Assert.Multiple(() =>
            {
                Assert.That(rb.Connected.IsCompletedSuccessfully, Is.True);
                Assert.That(rb.Faulted.IsCompleted, Is.True);
                Assert.That(rb.Disconnected.IsCompleted, Is.True);
            });
        }
        [Test]
        public async Task Verify_Transitions_To_Connected_After_ConnectAsync()
        {
            using var rb = new RockBlock9603();
            var connect = rb.ConnectAsync();
            await connect;
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
            var rb = new RockBlock9603();
            var connect = rb.ConnectAsync();
            await connect;
            rb.Dispose();
            Assert.Multiple(() =>
            {
                Assert.That(rb.Connected.IsCompletedSuccessfully, Is.True);
                Assert.That(rb.Faulted.IsCanceled, Is.True);
                Assert.That(rb.Disconnected.IsCompletedSuccessfully, Is.True);
            });
        }
    }
}