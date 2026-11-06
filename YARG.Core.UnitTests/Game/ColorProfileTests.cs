// pattern: Imperative Shell

using System.Drawing;
using System.IO;
using System.Text;
using NUnit.Framework;
using YARG.Core.Game;

namespace YARG.Core.UnitTests.Game;

public class ColorProfileTests
{
    [Test]
    public void BinarySerialization_PreservesFiveFretGuitarAppearanceFields()
    {
        var source = new ColorProfile("Source");
        source.FiveFretGuitar.TapStripEmission = 37.5f;
        source.FiveFretGuitar.OpenHopoNote = Color.FromArgb(255, 12, 34, 56);
        source.FiveFretGuitar.OpenHopoNoteStarPower = Color.FromArgb(255, 78, 90, 12);

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            source.Serialize(writer);
        }

        stream.Position = 0;
        var restored = new ColorProfile("Restored");
        using (var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true))
        {
            restored.Deserialize(reader);
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(restored.FiveFretGuitar.TapStripEmission,
                Is.EqualTo(source.FiveFretGuitar.TapStripEmission));
            Assert.That(restored.FiveFretGuitar.OpenHopoNote,
                Is.EqualTo(source.FiveFretGuitar.OpenHopoNote));
            Assert.That(restored.FiveFretGuitar.OpenHopoNoteStarPower,
                Is.EqualTo(source.FiveFretGuitar.OpenHopoNoteStarPower));
        }
    }

    [Test]
    public void BinarySerialization_PreservesVocalsColors()
    {
        var source = new ColorProfile("Source");
        source.Vocals.LeadVocals = Color.FromArgb(255, 1, 2, 3);
        source.Vocals.Harmony1 = Color.FromArgb(255, 4, 5, 6);
        source.Vocals.Harmony2 = Color.FromArgb(255, 7, 8, 9);
        source.Vocals.Harmony3 = Color.FromArgb(255, 10, 11, 12);

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            source.Serialize(writer);
        }

        stream.Position = 0;
        var restored = new ColorProfile("Restored");
        using (var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true))
        {
            restored.Deserialize(reader);
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(restored.Vocals.LeadVocals, Is.EqualTo(source.Vocals.LeadVocals));
            Assert.That(restored.Vocals.Harmony1, Is.EqualTo(source.Vocals.Harmony1));
            Assert.That(restored.Vocals.Harmony2, Is.EqualTo(source.Vocals.Harmony2));
            Assert.That(restored.Vocals.Harmony3, Is.EqualTo(source.Vocals.Harmony3));
        }
    }

    [Test]
    public void CopyWithNewName_PreservesVocalsColors()
    {
        var source = new ColorProfile("Source");
        source.Vocals.Harmony2 = Color.FromArgb(255, 12, 34, 56);

        var copy = (ColorProfile) source.CopyWithNewName("Copy");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(copy.Name, Is.EqualTo("Copy"));
            Assert.That(copy.Vocals.Harmony2, Is.EqualTo(source.Vocals.Harmony2));
            Assert.That(copy.Vocals, Is.Not.SameAs(source.Vocals));
        }
    }

    [Test]
    public void BinaryDeserialization_VersionTwoUsesDefaultVocalsColors()
    {
        var source = new ColorProfile("Version 2");
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(2);
            writer.Write(source.Name);
            source.FiveFretGuitar.Serialize(writer);
            source.SixFretGuitar.Serialize(writer);
            source.FourLaneDrums.Serialize(writer);
            source.FiveLaneDrums.Serialize(writer);
            source.ProKeys.Serialize(writer);
        }

        stream.Position = 0;
        var restored = new ColorProfile("Restored");
        using (var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true))
        {
            restored.Deserialize(reader);
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(restored.Vocals.LeadVocals, Is.EqualTo(Color.FromArgb(255, 0, 204, 255)));
            Assert.That(restored.Vocals.Harmony1, Is.EqualTo(Color.FromArgb(255, 0, 204, 255)));
            Assert.That(restored.Vocals.Harmony2, Is.EqualTo(Color.FromArgb(255, 255, 133, 0)));
            Assert.That(restored.Vocals.Harmony3, Is.EqualTo(Color.FromArgb(255, 255, 219, 0)));
        }
    }
}
