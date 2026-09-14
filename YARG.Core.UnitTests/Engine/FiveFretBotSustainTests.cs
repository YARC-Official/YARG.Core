using System.Collections.Generic;
using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Guitar.Engines;

namespace YARG.Core.UnitTests.Engine;

/// <summary>
/// The stale-held-fret bug lived in the shared five-fret bot mask logic: a sustain
/// bit OR'd in for an extended sustain lingered after that sustain ended. It only
/// bites when the leftover fret is a higher fret than a still-held sustain (invalid
/// anchor), which is what this descending-overlap case reproduces.
/// </summary>
public class FiveFretBotSustainTests
{
    private const double TICKS_PER_SEC = 960.0; // 480 tpr * 120 bpm / 60

    private static GuitarNote Sus(FiveFretGuitarFret fret, uint tick, uint tickLen, bool extended)
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

    private class TestBotFiveFretEngine : YargFiveFretGuitarEngine
    {
        public List<string> Drops = new();
        public List<string> Misses = new();

        public TestBotFiveFretEngine(InstrumentDifficulty<GuitarNote> chart, SyncTrack syncTrack,
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

    /// <summary>
    /// Orange extended sustain (0..720), Blue sustain (480..1440). When the Orange
    /// sustain ends at 720, the bot's mask still holds Orange, which is a higher
    /// fret than Blue and therefore no valid anchor - the Blue sustain must survive.
    /// </summary>
    [Test]
    public void Bot_KeepsSustain_WhenPreviousExtendedSustainEndsOnSameTick()
    {
        var first = Sus(FiveFretGuitarFret.Orange, 0, 720, true);
        var second = Sus(FiveFretGuitarFret.Blue, 480, 960, false);
        LinkNotes(first, second);

        var difficulty = new InstrumentDifficulty<GuitarNote>(Instrument.FiveFretGuitar, Difficulty.Expert,
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

        var engine = new TestBotFiveFretEngine(difficulty, syncTrack, engineParams);

        for (double t = 0; t <= 2.0; t += 1.0 / 60.0)
            engine.Update(t);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(engine.Misses, Is.Empty);
            Assert.That(engine.Drops, Is.Empty);
        }
    }
}
