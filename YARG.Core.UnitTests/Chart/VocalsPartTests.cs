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