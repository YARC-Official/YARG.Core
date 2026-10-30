using NUnit.Framework;
using YARG.Core.Audio;

namespace YARG.Core.UnitTests.Audio;

public class MicDeviceConnectionTests
{
    // Checks the default connection status for implementations without hardware detection: connected until disposed.
    [Test]
    public void ExistingDevicesRemainConnectedUntilDisposed()
    {
        using var mic = new TestMic();
        Assert.That(mic.IsConnected, Is.True);
        mic.Dispose();
        Assert.That(mic.IsConnected, Is.False);
    }

    private sealed class TestMic : MicDevice
    {
        public TestMic() : base("test") { }
        public override int Reset() => 0;
        public override bool DequeueOutputFrame(out MicOutputFrame frame) { frame = default; return false; }
        public override void ClearOutputQueue() { }
        public override void SetMonitoringLevel(float volume) { }
        public override void SetReverbLevel(float wet) { }
        public override MicBufferInfo? GetBufferInfo() => null;
        public override SerializedMic Serialize() => new("test", 0);
    }
}
