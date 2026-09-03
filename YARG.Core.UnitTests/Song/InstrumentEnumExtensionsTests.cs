using NUnit.Framework;
using YARG.Core.Song;

namespace YARG.Core.UnitTests.Song;

public class InstrumentEnumExtensionsTests
{
    [TestCase(Instrument.FiveFretBass, GameMode.FiveFretGuitar)]
    [TestCase(Instrument.SixFretRhythm, GameMode.SixFretGuitar)]
    [TestCase(Instrument.ProDrums, GameMode.FourLaneDrums)]
    [TestCase(Instrument.FiveLaneDrums, GameMode.FiveLaneDrums)]
    [TestCase(Instrument.EliteDrums, GameMode.EliteDrums)]
    [TestCase(Instrument.ProBass_22Fret, GameMode.ProGuitar)]
    [TestCase(Instrument.ProKeys, GameMode.ProKeys)]
    [TestCase(Instrument.Harmony, GameMode.Vocals)]
    public void ToNativeGameMode_ReturnsExpectedGameMode(Instrument instrument, GameMode expectedGameMode)
    {
        Assert.That(instrument.ToNativeGameMode(), Is.EqualTo(expectedGameMode));
    }

    [Test]
    public void ToNativeGameMode_ThrowsForUnhandledInstrument()
    {
        Assert.That(
            () => ((Instrument) 99).ToNativeGameMode(),
            Throws.TypeOf<NotImplementedException>()
                .With.Message.EqualTo("Unhandled instrument 99!"));
    }

    [Test]
    public void PossibleInstrumentsForSong_EliteDrumsReturnsFiveLaneOnlyWhenSongHasFiveLaneDrums()
    {
        var parts = AvailableParts.Default;
        parts.FiveLaneDrums.ActivateSubtrack(0);

        var entry = new TestSongEntry();
        entry.SetParts(parts);

        var instruments = GameMode.EliteDrums.PossibleInstrumentsForSong(entry);

        Assert.That(instruments, Is.EqualTo([
            Instrument.FiveLaneDrums,
        ]));
    }

    [Test]
    public void PossibleInstrumentsForSong_EliteDrumsReturnsFourLaneAndProDrumsWhenSongDoesNotHaveFiveLaneDrums()
    {
        var parts = AvailableParts.Default;
        parts.FourLaneDrums.ActivateSubtrack(0);
        parts.ProDrums.ActivateSubtrack(0);

        var entry = new TestSongEntry();
        entry.SetParts(parts);

        var instruments = GameMode.EliteDrums.PossibleInstrumentsForSong(entry);

        Assert.That(instruments, Is.EqualTo([
            Instrument.FourLaneDrums,
            Instrument.ProDrums,
        ]));
    }

    [Test]
    public void PossibleInstrumentsForSong_UsesDefaultPossibleInstrumentsForNonEliteDrumsModes()
    {
        var entry = new TestSongEntry();

        var possible = GameMode.Vocals.PossibleInstruments();
        var forSong = GameMode.Vocals.PossibleInstrumentsForSong(entry);

        Assert.That(forSong, Is.EqualTo(possible));
    }

    [Test]
    public void PossibleInstruments_ReturnsExpectedOrderForProKeys()
    {
        var instruments = GameMode.ProKeys.PossibleInstruments();

        Assert.That(instruments, Is.EqualTo(new[]
        {
            Instrument.ProKeys,
            Instrument.Keys,
            Instrument.FiveFretGuitar,
            Instrument.FiveFretBass,
            Instrument.FiveFretRhythm,
            Instrument.FiveFretCoopGuitar,
        }));
    }

    [Test]
    public void DifficultyMaskConversion_RoundTripsSingleDifficulty()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Difficulty.Expert.ToDifficultyMask(), Is.EqualTo(DifficultyMask.Expert));
            Assert.That(DifficultyMask.Expert.ToDifficulty(), Is.EqualTo(Difficulty.Expert));
        }
    }

    [Test]
    public void ToDifficulty_ThrowsWhenMaskDoesNotRepresentSingleDifficulty()
    {
        Assert.That(
            () => (DifficultyMask.Easy | DifficultyMask.Medium).ToDifficulty(),
            Throws.ArgumentException.With.Message.EqualTo(
                $"Cannot convert difficulty mask {DifficultyMask.Easy | DifficultyMask.Medium} into a single difficulty!"));
    }
}
