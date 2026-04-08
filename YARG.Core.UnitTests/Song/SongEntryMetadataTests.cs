using NUnit.Framework;
using YARG.Core.Song;

namespace YARG.Core.UnitTests.Song;

public class SongEntryMetadataTests
{
    [Test]
    public void TimingProperties_ExposeMillisecondValuesAndConvertToSeconds()
    {
        var entry = new TestSongEntry();
        entry.SetTimingMetadata(
            songLength: 12345,
            songOffset: -1500,
            preview: (2500, 4500),
            video: (5000, 7500));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(entry.SongLengthMilliseconds, Is.EqualTo(12345));
            Assert.That(entry.SongOffsetMilliseconds, Is.EqualTo(-1500));
            Assert.That(entry.PreviewStartMilliseconds, Is.EqualTo(2500));
            Assert.That(entry.PreviewEndMilliseconds, Is.EqualTo(4500));
            Assert.That(entry.VideoStartTimeMilliseconds, Is.EqualTo(5000));
            Assert.That(entry.VideoEndTimeMilliseconds, Is.EqualTo(7500));

            Assert.That(entry.SongLengthSeconds, Is.EqualTo(12.345).Within(0.000001));
            Assert.That(entry.SongOffsetSeconds, Is.EqualTo(-1.5).Within(0.000001));
            Assert.That(entry.PreviewStartSeconds, Is.EqualTo(2.5).Within(0.000001));
            Assert.That(entry.PreviewEndSeconds, Is.EqualTo(4.5).Within(0.000001));
            Assert.That(entry.VideoStartTimeSeconds, Is.EqualTo(5.0).Within(0.000001));
            Assert.That(entry.VideoEndTimeSeconds, Is.EqualTo(7.5).Within(0.000001));
        }
    }

    [Test]
    public void VideoEndTimeSeconds_ReturnsNegativeOneWhenVideoEndIsNegative()
    {
        var entry = new TestSongEntry();
        entry.SetTimingMetadata(video: (1000, -1));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(entry.VideoEndTimeMilliseconds, Is.EqualTo(-1));
            Assert.That(entry.VideoEndTimeSeconds, Is.EqualTo(-1));
        }
    }

    [Test]
    public void ToString_UsesOriginalArtistAndNameValues()
    {
        var entry = new TestSongEntry();
        entry.SetMetadata(name: "<b>The Song</b>", artist: "The Ártist");

        Assert.That(entry.ToString(), Is.EqualTo("The Ártist | <b>The Song</b>"));
    }

    [Test]
    public void VocalProperties_ExposeStoredMetadata()
    {
        var entry = new TestSongEntry();
        entry.SetVocalMetadata(vocalScrollSpeedScalingFactor: 0.85f, vocalGender: VocalGender.Female);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(entry.VocalScrollSpeedScalingFactor, Is.EqualTo(0.85f));
            Assert.That(entry.VocalGender, Is.EqualTo(VocalGender.Female));
        }
    }
}