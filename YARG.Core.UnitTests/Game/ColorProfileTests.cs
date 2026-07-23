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
}
