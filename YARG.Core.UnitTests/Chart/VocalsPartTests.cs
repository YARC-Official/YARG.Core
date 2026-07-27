using NUnit.Framework;
using YARG.Core.Chart;

namespace YARG.Core.UnitTests.Chart;

public class VocalsTrackTests
{
    [Test]
    public void CloneInTickRangeRemovesEventsOutsideRange()
    {
        var track = CreateVocalsTrack();

        var trimmed = track.CloneInTickRange(100, 200);
        var part = trimmed.Parts[0];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(part.NotePhrases.Select(e => e.Tick), Is.EqualTo(new uint[] { 100, 150 }));
            Assert.That(part.StaticLyricPhrases.Select(e => e.Tick), Is.EqualTo(new uint[] { 125, 175 }));
            Assert.That(part.OtherPhrases.Select(e => e.Tick), Is.EqualTo(new uint[] { 125 }));
            Assert.That(part.TextEvents.Select(e => e.Tick), Is.EqualTo(new uint[] { 100, 199 }));
        }
    }

    [Test]
    public void CloneInTickRangeKeepsStartAndExcludesEnd()
    {
        var track = new VocalsTrack(Instrument.Vocals, [
            new VocalsPart(false,
                CreateVocalPhrases(100, 200),
                CreateVocalPhrases(100, 200),
                CreatePhrases(100, 200),
                CreateTextEvents(100, 200))
        ], []);

        var trimmed = track.CloneInTickRange(100, 200).Parts[0];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(trimmed.NotePhrases.Select(e => e.Tick), Is.EqualTo(new uint[] { 100 }));
            Assert.That(trimmed.StaticLyricPhrases.Select(e => e.Tick), Is.EqualTo(new uint[] { 100 }));
            Assert.That(trimmed.OtherPhrases.Select(e => e.Tick), Is.EqualTo(new uint[] { 100 }));
            Assert.That(trimmed.TextEvents.Select(e => e.Tick), Is.EqualTo(new uint[] { 100 }));
        }
    }

    [Test]
    public void CloneInTickRangeKeepsVocalPhrasesWithChildEventsAtStart()
    {
        var track = new VocalsTrack(Instrument.Vocals, [
            new VocalsPart(false,
                [CreateVocalPhraseWithChildEvents(90, 100)],
                [CreateVocalPhraseWithChildEvents(90, 100)],
                [],
                [])
        ], []);

        var trimmed = track.CloneInTickRange(100, 200).Parts[0];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(trimmed.NotePhrases.Select(e => e.Tick), Is.EqualTo(new uint[] { 90 }));
            Assert.That(trimmed.StaticLyricPhrases.Select(e => e.Tick), Is.EqualTo(new uint[] { 90 }));
            Assert.That(trimmed.NotePhrases[0].PhraseParentNote.ChildNotes.Select(e => e.Tick), Is.EqualTo(new uint[] { 100 }));
            Assert.That(trimmed.NotePhrases[0].Lyrics.Select(e => e.Tick), Is.EqualTo(new uint[] { 100 }));
        }
    }

    [Test]
    public void CloneInTickRangeExcludesVocalPhrasesWithChildEventsOutsideRange()
    {
        var track = new VocalsTrack(Instrument.Vocals, [
            new VocalsPart(false,
                [CreateVocalPhraseWithChildEvents(80, 200), CreateVocalPhraseWithChildEvents(70, 90, 100), CreateVocalPhraseWithChildEvents(90, 191)],
                [CreateVocalPhraseWithChildEvents(80, 200), CreateVocalPhraseWithChildEvents(70, 90, 100), CreateVocalPhraseWithChildEvents(90, 191)],
                [],
                [])
        ], []);

        var trimmed = track.CloneInTickRange(100, 200).Parts[0];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(trimmed.NotePhrases, Is.Empty);
            Assert.That(trimmed.StaticLyricPhrases, Is.Empty);
        }
    }

    [Test]
    public void CloneInTickRangeDoesNotModifyOriginalPart()
    {
        var track = CreateVocalsTrack();

        _ = track.CloneInTickRange(100, 200);
        var part = track.Parts[0];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(part.NotePhrases.Select(e => e.Tick), Is.EqualTo(new uint[] { 50, 100, 150 }));
            Assert.That(part.StaticLyricPhrases.Select(e => e.Tick), Is.EqualTo(new uint[] { 75, 125, 175 }));
            Assert.That(part.OtherPhrases.Select(e => e.Tick), Is.EqualTo(new uint[] { 25, 125, 225 }));
            Assert.That(part.TextEvents.Select(e => e.Tick), Is.EqualTo(new uint[] { 99, 100, 199, 200 }));
        }
    }

    [Test]
    public void CloneInTickRangePreservesMostRecentRangeShiftBeforeStart()
    {
        var track = CreateVocalsTrack();

        var trimmed = track.CloneInTickRange(100, 200);

        Assert.That(trimmed.RangeShifts.Select(e => e.Tick), Is.EqualTo(new uint[] { 75, 125, 175 }));
    }

    [Test]
    public void CloneAsInstrumentDifficultyDoesNotShareNotesBetweenClones()
    {
        var part = CreatePartWithSungPhrase();

        var first = part.CloneAsInstrumentDifficulty();
        var second = part.CloneAsInstrumentDifficulty();

        first.Notes[0].SetHitState(true, true);
        first.Notes[0].SetMissState(true, true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(second.Notes[0], Is.Not.SameAs(first.Notes[0]));
            Assert.That(second.Notes[0].WasHit, Is.False);
            Assert.That(second.Notes[0].WasMissed, Is.False);
            Assert.That(part.NotePhrases[0].PhraseParentNote.WasHit, Is.False);
            Assert.That(part.NotePhrases[0].PhraseParentNote.WasMissed, Is.False);
        }
    }

    [Test]
    public void CloneAsInstrumentDifficultyPreservesPitchSlideChildNotes()
    {
        var part = CreatePartWithSungPhrase();

        var cloned = part.CloneAsInstrumentDifficulty();
        var phrase = cloned.Notes[0];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(phrase.ChildNotes, Has.Count.EqualTo(2));
            Assert.That(phrase.ChildNotes[1].ChildNotes, Has.Count.EqualTo(1));
            Assert.That(phrase.ChildNotes[1].ChildNotes[0].Pitch, Is.EqualTo(64));
        }
    }

    private static VocalsPart CreatePartWithSungPhrase()
    {
        var parentNote = new VocalNote(NoteFlags.None, false, 1.0, 0.4, 100, 40);
        parentNote.AddChildNote(new VocalNote(60, 0, VocalNoteType.Lyric, 1.0, 0.1, 100, 10));

        var slideNote = new VocalNote(62, 0, VocalNoteType.Lyric, 1.2, 0.1, 120, 10);
        parentNote.AddChildNote(slideNote);
        slideNote.AddChildNote(new VocalNote(64, 0, VocalNoteType.Lyric, 1.3, 0.1, 130, 10));

        var phrase = new VocalsPhrase(1.0, 0.4, 100, 40, parentNote, []);
        return new VocalsPart(false, [phrase], [], [], []);
    }

    private static VocalsTrack CreateVocalsTrack()
    {
        return new VocalsTrack(Instrument.Vocals, [
            new VocalsPart(false,
                CreateVocalPhrases(50, 100, 150),
                CreateVocalPhrases(75, 125, 175),
                CreatePhrases(25, 125, 225),
                CreateTextEvents(99, 100, 199, 200))
        ], CreateRangeShifts(75, 125, 175, 225));
    }

    private static List<VocalsPhrase> CreateVocalPhrases(params uint[] ticks)
    {
        return ticks.Select(tick =>
        {
            var parentNote = new VocalNote(NoteFlags.None, false, tick / 100.0, 0.1, tick, 10);
            return new VocalsPhrase(tick / 100.0, 0.1, tick, 10, parentNote, []);
        }).ToList();
    }

    private static VocalsPhrase CreateVocalPhraseWithChildEvents(uint phraseTick, params uint[] childTicks)
    {
        var tickLength = childTicks[^1] - phraseTick + 10;
        var parentNote = new VocalNote(NoteFlags.None, false, phraseTick / 100.0, tickLength / 100.0, phraseTick, tickLength);

        foreach (var childTick in childTicks)
        {
            parentNote.AddChildNote(new VocalNote(60, 0, VocalNoteType.Lyric, childTick / 100.0, 0.1, childTick, 10));
        }

        return new VocalsPhrase(phraseTick / 100.0, tickLength / 100.0, phraseTick, tickLength, parentNote,
            childTicks.Select(childTick => new LyricEvent(LyricSymbolFlags.None, childTick.ToString(), childTick / 100.0, childTick)).ToList());
    }

    private static List<Phrase> CreatePhrases(params uint[] ticks)
    {
        return ticks.Select(tick => new Phrase(PhraseType.StarPower, tick / 100.0, 0.1, tick, 10)).ToList();
    }

    private static List<TextEvent> CreateTextEvents(params uint[] ticks)
    {
        return ticks.Select(tick => new TextEvent(tick.ToString(), tick / 100.0, tick)).ToList();
    }

    private static List<VocalsRangeShift> CreateRangeShifts(params uint[] ticks)
    {
        return ticks.Select(tick => new VocalsRangeShift(60, 72, tick / 100.0, 0.1, tick, 10)).ToList();
    }
}