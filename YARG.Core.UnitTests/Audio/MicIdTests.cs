using NUnit.Framework;
using YARG.Core.Audio;

namespace YARG.Core.UnitTests.Audio
{
    public class MicIdTests
    {
        [Test]
        public void Equality_SameValues_AreEqual()
        {
            var id1 = new MicId("Focusrite", 0);
            var id2 = new MicId("Focusrite", 0);

            Assert.That(id1 == id2, Is.True);
            Assert.That(id1.Equals(id2), Is.True);
            Assert.That(id1.GetHashCode(), Is.EqualTo(id2.GetHashCode()));
        }

        [Test]
        public void Equality_DifferentValues_AreNotEqual()
        {
            var id1 = new MicId("Focusrite", 0);
            var id2 = new MicId("Focusrite", 1);
            var id3 = new MicId("Different", 0);

            Assert.That(id1 == id2, Is.False);
            Assert.That(id1 != id2, Is.True);
            Assert.That(id1 == id3, Is.False);
        }

        [Test]
        public void InputDeviceInfo_ExposesMatchingMicId()
        {
            var info = new InputDeviceInfo(1, "Focusrite", 0, 2);
            Assert.That(info.Id, Is.EqualTo(new MicId("Focusrite", 0)));
        }

        [Test]
        public void SerializedMic_ExposesMatchingMicId()
        {
            var serialized = new SerializedMic("Focusrite", 1);
            Assert.That(serialized.Id, Is.EqualTo(new MicId("Focusrite", 1)));
        }
    }
}
