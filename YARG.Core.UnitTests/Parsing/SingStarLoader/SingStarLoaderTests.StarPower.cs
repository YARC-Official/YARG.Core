using NUnit.Framework;
using YARG.Core.Chart;

namespace YARG.Core.UnitTests.Parsing.SingStarLoader;

internal class SingStarLoaderTests_StarPower : SingStarLoaderTests
{
    [Test]
    public void ParseBonusNote()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Golden\" Bonus=\"Yes\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);
        var phrase = track.Parts[0].NotePhrases[0];

        Assert.That(phrase.IsStarPower, Is.True);
    }

    [Test]
    public void ParseMultipleBonusNotes()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Normal\"/>",
            "</SENTENCE>",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Golden\" Bonus=\"Yes\"/>",
            "</SENTENCE>",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Normal2\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);

        // First phrase - normal
        Assert.That(track.Parts[0].NotePhrases[0].IsStarPower, Is.False);
        // Second phrase - golden (StarPower)
        Assert.That(track.Parts[0].NotePhrases[1].IsStarPower, Is.True);
        // Third phrase - normal
        Assert.That(track.Parts[0].NotePhrases[2].IsStarPower, Is.False);
    }

    [Test]
    public void StarPowerOtherPhrasesAdded()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Golden1\" Bonus=\"Yes\"/>",
            "</SENTENCE>",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Golden2\" Bonus=\"Yes\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);
        var otherPhrases = track.Parts[0].OtherPhrases;

        // Should have 2 StarPower phrases in OtherPhrases
        Assert.That(otherPhrases, Has.Count.EqualTo(2));
        Assert.That(otherPhrases[0].Type, Is.EqualTo(PhraseType.StarPower));
        Assert.That(otherPhrases[1].Type, Is.EqualTo(PhraseType.StarPower));
    }

    [Test]
    public void StarPowerPhraseTiming()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"8\" Lyric=\"LongGolden\" Bonus=\"Yes\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);
        var otherPhrase = track.Parts[0].OtherPhrases[0];

        Assert.That(otherPhrase.TickLength, Is.GreaterThan(0));
        Assert.That(otherPhrase.TimeLength, Is.GreaterThan(0));
    }

    [Test]
    public void MultipleStarPowerPhrasesSeparate()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"SP1\" Bonus=\"Yes\"/>",
            "</SENTENCE>",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"SP2\" Bonus=\"Yes\"/>",
            "</SENTENCE>",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"SP3\" Bonus=\"Yes\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);
        var otherPhrases = track.Parts[0].OtherPhrases;

        Assert.That(otherPhrases, Has.Count.EqualTo(3));

        // Each should have different tick positions
        Assert.That(otherPhrases[0].Tick, Is.LessThan(otherPhrases[1].Tick));
        Assert.That(otherPhrases[1].Tick, Is.LessThan(otherPhrases[2].Tick));
    }

    [Test]
    public void NormalNotesAfterStarPowerNotAffected()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Golden\" Bonus=\"Yes\"/>",
            "</SENTENCE>",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Normal\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);

        // Second phrase should NOT be StarPower
        Assert.That(track.Parts[0].NotePhrases[1].IsStarPower, Is.False);
    }

    [Test]
    public void StarPowerWithPitch()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"72\" Duration=\"4\" Lyric=\"GoldenHigh\" Bonus=\"Yes\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);
        var note = track.Parts[0].NotePhrases[0].PhraseParentNote.ChildNotes[0];

        // Pitch should be absolute MIDI
        Assert.That(note.Pitch, Is.EqualTo(72f));
        Assert.That(track.Parts[0].NotePhrases[0].IsStarPower, Is.True);
    }

    [Test]
    public void GoldenNoteInDuet()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"2\" Tempo=\"120\" Duet=\"Yes\">",
            "<TRACK Name=\"Player1\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Golden\" Bonus=\"Yes\"/>",
            "</SENTENCE>",
            "</TRACK>",
            "<TRACK Name=\"Player2\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Normal\"/>",
            "</SENTENCE>",
            "</TRACK>",
            "</MELODY>"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Harmony);

        // Part 1 has StarPower
        Assert.That(track.Parts[0].NotePhrases[0].IsStarPower, Is.True);
        Assert.That(track.Parts[0].OtherPhrases, Has.Count.EqualTo(1));

        // Part 2 has no StarPower
        Assert.That(track.Parts[1].NotePhrases[0].IsStarPower, Is.False);
        Assert.That(track.Parts[1].OtherPhrases, Has.Count.EqualTo(0));
    }
}