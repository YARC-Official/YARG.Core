using System.Collections.Generic;
using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Keys;
using YARG.Core.Engine.Keys.Engines;
using YARG.Core.Game;

namespace YARG.Core.UnitTests.Engine;

// Regression tests for the ProKeysNote constructor asymmetry: the primary (parsed-note)
// constructor previously never set DisjointMask (only the copy constructor did), so any
// engine built from un-cloned parsed notes could never hold a sustain (CanSustainHold
// checked `(KeyMask & DisjointMask) != 0` against 0) and the chord-stagger key-hold
// enforcement was dead code. The game always clones charts before play, which masked this;
// these tests pin the un-cloned path so it cannot silently break again.
public class ProKeysSustainTests : EngineTester
{
    private const int RES = 480;

    // 120 bpm → 0.5s per beat
    private static double TimeOf(uint tick) => tick / (double) RES * 0.5;

    private static KeysEngineParameters KeysParams() =>
        new(new HitWindowSettings(0.1, 0.1, 1.0, false, 0, 1.0, 1.0, 0.15, 0.25),
            4, 0, 0, StarMultiplierThresholds, SoloBonusStarMultiplierThresholds, 0.05, 0, false, true);

    private static YargProKeysEngine BuildEngine()
    {
        var notes = new List<ProKeysNote>
        {
            // Sustained note: key 0, one beat (480 ticks / 0.5s)
            new(0, ProKeysNoteFlags.None, NoteFlags.None, TimeOf(0), TimeOf(480), 0, 480),
            // Trailing note keeps the engine moving past the sustain end
            new(4, ProKeysNoteFlags.None, NoteFlags.None, TimeOf(960), 0, 960, 0),
        };

        for (int i = 1; i < notes.Count; i++)
        {
            notes[i].PreviousNote = notes[i - 1];
            notes[i - 1].NextNote = notes[i];
        }

        var diff = new InstrumentDifficulty<ProKeysNote>(Instrument.ProKeys, Difficulty.Expert, notes, new List<Phrase>(), new());

        var syncTrack = new SyncTrack(RES);
        syncTrack.Tempos.Add(new TempoChange(120, 0, 0));

        return new YargProKeysEngine(diff, syncTrack, KeysParams(), isBot: true);
    }

    [Test]
    public void ParsedNotes_BotRun_SustainScores()
    {
        var engine = BuildEngine();

        double endTime = TimeOf(960) + 1.0;
        for (double t = 0; t <= endTime; t += 0.01)
        {
            engine.Update(t);
        }

        Assert.That(engine.EngineStats.NotesHit, Is.EqualTo(2), "both notes should be hit");
        Assert.That(engine.EngineStats.NotesMissed, Is.EqualTo(0));
        // One beat of sustain = 480 / 19.2 = 25 points, paid at combo 1 (1x)
        Assert.That(engine.EngineStats.SustainScore, Is.EqualTo(25));
    }
}
