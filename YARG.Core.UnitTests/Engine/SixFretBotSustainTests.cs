using System.Collections.Generic;
using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Guitar.Engines;

namespace YARG.Core.UnitTests.Engine;

/// <summary>
/// Regression for the six-fret bot stale-held-fret bug (shared with five-fret in
/// YargFiveFretGuitarEngine.UpdateBot): when an extended sustain ended on the exact
/// tick a new sustain on the same fret number in the other row was pressed, the bot
/// kept holding the finished sustain's fret until its next note press. The stale fret
/// is not a valid six-fret anchor, so the fresh sustain was judged dropped the instant
/// it started.
/// </summary>
public class SixFretBotSustainTests
{
    private const double TICKS_PER_SEC = 960.0; // 480 tpr * 120 bpm / 60

    private static GuitarNote Sus(SixFretGuitarFret fret, uint tick, uint tickLen, bool extended)
    {
        return new GuitarNote(fret, GuitarNoteType.Strum,
            extended ? GuitarNoteFlags.ExtendedSustain : GuitarNoteFlags.None,
            NoteFlags.None,
            tick / TICKS_PER_SEC, tickLen / TICKS_PER_SEC, tick, tickLen);
    }

    private static void LinkNotes(params GuitarNote[] notes)
    {
        for (int i = 0; i < notes.Length; i++)
        {
            if (i > 0) notes[i].PreviousNote = notes[i - 1];
            if (i < notes.Length - 1) notes[i].NextNote = notes[i + 1];
        }
    }

    private class TestBotSixFretEngine : YargSixFretGuitarEngine
    {
        public List<string> Drops = new();
        public List<string> Misses = new();

        public TestBotSixFretEngine(InstrumentDifficulty<GuitarNote> chart, SyncTrack syncTrack,
            GuitarEngineParameters engineParameters)
            : base(chart, syncTrack, engineParameters, true)
        {
            OnSustainEnd += (note, time, finished) =>
            {
                if (!finished)
                    Drops.Add($"sustain tick={note.Tick} dropped at {time:F3}");
            };
            OnNoteMissed += (index, note) => Misses.Add($"note index={index} tick={note.Tick} missed");
        }
    }

    [Test]
    public void Bot_KeepsSustain_WhenPreviousExtendedSustainEndsOnSameTick()
    {
        // B3 extended sustain (0..720), W3 sustain (480..1440) - W3 is not extended.
        var first = Sus(SixFretGuitarFret.Black3, 0, 720, true);
        var second = Sus(SixFretGuitarFret.White3, 480, 960, false);
        LinkNotes(first, second);

        var difficulty = new InstrumentDifficulty<GuitarNote>(Instrument.SixFretGuitar, Difficulty.Expert,
            new(new[] { first, second }), new(), new());

        var engineParams = new GuitarEngineParameters(
            new HitWindowSettings(0.1, 0.1, 1.0, false, 0, 1.0, 1.0, 0.15, 0.25),
            4, 0, 0.05,
            new[] { 0.08f, 0.17f, 0.28f, 0.41f, 0.55f, 0.71f, 0.88f, 1.0f },
            new[] { 0.08f, 0.17f, 0.28f, 0.41f, 0.55f, 0.71f, 0.88f, 1.0f },
            0.1, 0.1, 0.1,
            false, true, false, false, true);

        var syncTrack = new SyncTrack(480);
        syncTrack.Tempos.Add(new TempoChange(120, 0, 0));

        var engine = new TestBotSixFretEngine(difficulty, syncTrack, engineParams);

        for (double t = 0; t <= 2.0; t += 1.0 / 60.0)
            engine.Update(t);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(engine.Misses, Is.Empty);
            Assert.That(engine.Drops, Is.Empty);
        }
    }
}
