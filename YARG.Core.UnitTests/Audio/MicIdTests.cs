using NUnit.Framework;
using YARG.Core.Audio;
using YARG.Core.UnitTests.Song;

namespace YARG.Core.UnitTests.Audio
{
    public class MicIdTests
    {
        [TestCase(typeof(MicId), typeof(string), typeof(int))]
        [TestCase(typeof(InputDeviceInfo), typeof(int), typeof(string), typeof(int), typeof(int))]
        [TestCase(typeof(SerializedMic), typeof(string), typeof(int))]
        public void LegacyConstructor_IsAvailableToCompiledCallers(Type type, params Type[] parameterTypes)
        {
            Assert.That(type.GetConstructor(parameterTypes), Is.Not.Null);
        }

        [Test]
        public void GetInputDevice_UsesNameLookupForLegacyIdsAndPreservesQualifiedIds()
        {
            var manager = new InputLookupManager();
            var legacy = new MicId("USB Mic", 1);
            var qualified = new MicId("USB Mic", 1, "usb-1");

            manager.GetInputDevice(legacy);
            Assert.That(manager.NameLookup, Is.EqualTo(legacy));

            manager.GetInputDevice(qualified);
            Assert.That(manager.CreatedInput, Is.EqualTo(qualified));
        }

        [Test]
        public void MatchesInput_UsesSuppliedComparisonOnlyForLegacyNames()
        {
            var input = new MicId("USB Microphone", 1, "endpoint-A");
            var legacy = new MicId("usb microphone", 1);

            Assert.That(legacy.MatchesInput(input), Is.False);
            Assert.That(legacy.MatchesInput(input, StringComparison.OrdinalIgnoreCase), Is.True);
            var otherChannel = new MicId("usb microphone", 0);
            var otherEndpoint = new MicId("USB Microphone", 1, "endpoint-a");
            Assert.That(otherChannel.MatchesInput(input, StringComparison.OrdinalIgnoreCase), Is.False);
            Assert.That(otherEndpoint.MatchesInput(input, StringComparison.OrdinalIgnoreCase), Is.False);
        }

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

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(7)]
        public void IdenticalDevices_RetainIndependentChannelBindingsAcrossReenumeration(int count)
        {
            var owners = new System.Collections.Generic.Dictionary<MicId, int>();
            for (int i = 0; i < count; i++)
            {
                var input = new InputDeviceInfo(i, "Logitech USB Microphone", 0, 1, $"usb-{i}");
                owners.Add(new SerializedMic(input.Id).Id, i);
            }

            for (int i = 0; i < count; i++)
            {
                var input = new InputDeviceInfo(count - i, "Logitech USB Microphone", 0, 1, $"usb-{i}");
                Assert.That(owners[input.Id], Is.EqualTo(i));
            }

            var legacy = new SerializedMic("Logitech USB Microphone").Id;
            Assert.That(owners.ContainsKey(legacy), Is.False);
            Assert.That(legacy.MatchesInput(new MicId(legacy.BaseName, 0, "usb-0")), Is.True);
            Assert.That(new MicId(legacy.BaseName, 0, "usb-0").MatchesInput(
                new MicId(legacy.BaseName, 0, "usb-1")), Is.False);
            Assert.That(owners.ContainsKey(new MicId(legacy.BaseName, 1, "usb-0")), Is.False);
        }

        [Test]
        public void InputDeviceInfo_ExposesMatchingMicId()
        {
            var info = new InputDeviceInfo(1, "Focusrite", 0, 2);
            Assert.That(info.Id, Is.EqualTo(new MicId("Focusrite", 0)));
            Assert.That(default(InputDeviceInfo).Id, Is.EqualTo(new MicId(string.Empty, 0)));
        }

        [Test]
        public void SerializedMic_ExposesMatchingMicId()
        {
            var serialized = new SerializedMic("Focusrite", 1);
            Assert.That(serialized.Id, Is.EqualTo(new MicId("Focusrite", 1)));
        }

        private sealed class InputLookupManager : NullAudioManager
        {
            public MicId? NameLookup { get; private set; }
            public MicId? CreatedInput { get; private set; }

            protected internal override MicDevice? GetInputDevice(string baseName, int channel)
            {
                NameLookup = new MicId(baseName, channel);
                return null;
            }

            protected internal override MicDevice? CreateInputDevice(InputDeviceInfo device)
            {
                CreatedInput = device.Id;
                return null;
            }
        }
    }
}
