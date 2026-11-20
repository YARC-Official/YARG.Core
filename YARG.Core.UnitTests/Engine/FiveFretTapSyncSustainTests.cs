using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Guitar.Engines;
using YARG.Core.Input;

namespace YARG.Core.UnitTests.Engine;

/// Two defects in the sustain-cutting path of `YargFiveFretGuitarEngine.HitNote`
/// (`YargFiveFretGuitarEngine.cs:509-518`), the loop that ends the active sustains whose
/// frets overlap the note that was just hit. This is how a chart cuts a sustain: the next
/// note on the same fret ("tap sync").
///
/// 1. `EndSustain` (`BaseEngine.Generic.cs:1025-1032`) only removes the sustain and raises
///    `OnSustainEnd`. Sustain points reach the score from two call sites only: the burst/end/drop
///    branch of `UpdateSustains` (`BaseEngine.Generic.cs:959-1001`, `AddScore` +
///    `EngineStats.SustainScore`) and `Overstrum` (`GuitarEngine.cs:176-186`, which calls
///    `CalculateSustainPoints` itself). The between-passes branch (`:993-1000`) only fills
///    `EngineStats.PendingScore`, which is UI-only. The cut path scores nowhere, so every point
///    the sustain had earned is discarded.
/// 2. The loop indexes `ActiveSustains[i]` and `EndSustain` removes at `i`, but the loop then does
///    `i++`, so the sustain that shifts into `i` is never tested. `UpdateSustains` pairs its removal
///    with the matching `i--` (`BaseEngine.Generic.cs:1003-1008`), and `Overstrum` does the same
///    (`GuitarEngine.cs:176-180`); the cut loop is the only removal site that misses it. So when two
///    active sustains both overlap the note, the second one is skipped: it keeps scoring, keeps
///    masking its fret in `CanNoteBeHit` (`YargFiveFretGuitarEngine.cs:358-390`), and for a star
///    power sustain keeps `IsStarPowerSustainActive` true after the note that should have cut it.
public class FiveFretTapSyncSustainTests
{
    // SyncTrack resolution 480 @ 120 bpm, so 960 ticks per second and 19.2 ticks per sustain
    // point (POINTS_PER_BEAT is 25).
    private const double TICKS_PER_SEC = 960.0;

    private class TestEngine : YargFiveFretGuitarEngine
    {
        public readonly List<(double time, FiveFretGuitarFret fret, bool scored)> SustainEnds = new();
        public int Hits;

        public TestEngine(InstrumentDifficulty<GuitarNote> chart, SyncTrack syncTrack,
            GuitarEngineParameters parameters) : base(chart, syncTrack, parameters, false)
        {
            OnSustainEnd += (note, time, scored) =>
                SustainEnds.Add((time, (FiveFretGuitarFret) note.Fret, scored));
            OnNoteHit += (_, _) => Hits++;
        }

        public int ActiveSustainCount => ActiveSustains.Count;
        public readonly List<(double time, int count)> SustainCountOverTime = new();
        public int Score => EngineStats.CommittedScore;
        public int SustainScore => EngineStats.SustainScore;
    }

    private static GuitarNote Note(FiveFretGuitarFret fret, GuitarNoteType type, double time,
        uint tickLength = 0, GuitarNoteFlags flags = GuitarNoteFlags.None)
    {
        uint tick = (uint) Math.Round(time * TICKS_PER_SEC);
        return new GuitarNote(fret, type, flags, NoteFlags.None, time, 0, tick, tickLength);
    }

    private static void Link(List<GuitarNote> notes)
    {
        for (int i = 1; i < notes.Count; i++)
        {
            notes[i - 1].NextNote = notes[i];
            notes[i].PreviousNote = notes[i - 1];
        }
    }

    private static GuitarEngineParameters Parameters()
    {
        var window = new HitWindowSettings(
            maxWindow: 0.12, minWindow: 0.04, frontToBackRatio: 1.0, isDynamic: true,
            dwSlope: 0.93, dwScale: 1.0, dwGamma: 1.5,
            laneAutohitWindow: 0.160, laneProximityProtectionWindow: 0.080);

        var thresholds = new[] { 0.06f, 0.12f, 0.2f, 0.45f, 0.75f, 1.09f };

        return new GuitarEngineParameters(window, 4, 0.25, 0.025, thresholds, thresholds,
            hopoLeniency: 0.08, strumLeniency: 0.04, strumLeniencySmall: 0.02,
            infiniteFrontEnd: false, antiGhosting: true, soloTaps: false,
            noStarPowerOverlap: false, enableLanes: false);
    }

    private static TestEngine Run(List<GuitarNote> notes,
        IReadOnlyList<(double time, GuitarAction action, bool button)> inputs, double? until = null)
    {
        Link(notes);

        var difficulty = new InstrumentDifficulty<GuitarNote>(Instrument.FiveFretGuitar, Difficulty.Expert,
            notes, new(), new());
        var engine = new TestEngine(difficulty, SyncTrack(), Parameters());

        foreach (var (time, action, button) in inputs)
        {
            var input = GameInput.Create(time, action, button);
            engine.QueueInput(ref input);
        }

        for (double time = 0.4; time <= (until ?? notes[^1].Time + 1.5); time += 1.0 / 240.0)
        {
            engine.Update(time);
            engine.SustainCountOverTime.Add((time, engine.ActiveSustainCount));
        }

        return engine;
    }

    private static string First(List<(double time, int count)> list) =>
        list.Count == 0 ? "none" : $"{list[0].time:F3} s: {list[0].count} sustain(s) active";

    private static SyncTrack SyncTrack()
    {
        var track = new SyncTrack(480);
        track.Tempos.Add(new TempoChange(120, 0, 0));
        return track;
    }

    // The player strums the green sustain at 1.0 s and holds it; the tap on the same fret at
    // 2.0 s cuts it, which is the chart's intent. One second of a held sustain is 960 ticks,
    // worth 50 points.
    private static readonly (double, GuitarAction, bool)[] HoldGreen =
    {
        (0.95, GuitarAction.GreenFret, true),
        (1.00, GuitarAction.StrumDown, true),
    };

    [Test]
    public void TapNote_CutsSustain_AndKeepsThePointsEarnedSoFar()
    {
        var notes = new List<GuitarNote>
        {
            Note(FiveFretGuitarFret.Green, GuitarNoteType.Strum, 1.0, 1440,
                GuitarNoteFlags.ExtendedSustain),
            Note(FiveFretGuitarFret.Green, GuitarNoteType.Tap, 2.0),
        };

        var engine = Run(notes, HoldGreen);

        // Precondition: both notes were played, and the cut itself works.
        Assert.That(engine.Hits, Is.EqualTo(2), "both notes should have been hit");
        Assert.That(engine.SustainEnds, Has.Count.EqualTo(1),
            "the tap note should end the sustain it is charted on");
        Assert.That(engine.SustainEnds[0].time, Is.LessThan(2.05),
            "the sustain should be cut by the tap, not run to its natural end at 2.5 s");

        // A sustain held from 1.0 s to the cut at its front end (~1.94 s, the window saturates at
        // MaxWindow 0.12) has earned ~902/19.2 = 47 of its 75 points. Pinned as a range so that a
        // fix which pays the *whole* sustain instead of the earned part also fails this test.
        Assert.That(engine.SustainScore, Is.InRange(45, 50),
            $"the ~47 points earned before the cut should have been committed " +
            $"(SustainScore was {engine.SustainScore}, CommittedScore {engine.Score})");
    }

    [Test]
    public void SustainNotCut_PaysItsPointsNormally_Control()
    {
        // Same chart without the cutting tap: everything the sustain earns is committed at burst.
        var notes = new List<GuitarNote>
        {
            Note(FiveFretGuitarFret.Green, GuitarNoteType.Strum, 1.0, 1440,
                GuitarNoteFlags.ExtendedSustain),
            Note(FiveFretGuitarFret.Red, GuitarNoteType.Tap, 2.0),
        };

        var engine = Run(notes, HoldGreen);

        Assert.That(engine.SustainEnds, Has.Count.EqualTo(1));
        Assert.That(engine.SustainEnds[0].time, Is.InRange(2.4, 2.6),
            "control: the sustain should reach its natural end at 2.5 s, nothing cut it");

        // 1440 ticks / 19.2 ticks per point at multiplier 1.
        Assert.That(engine.SustainScore, Is.EqualTo(75),
            "control: an uncut sustain pays its full 75 points at burst");
    }

    [Test]
    public void TapNote_OverOneSustain_EndsIt_Control()
    {
        var notes = new List<GuitarNote>
        {
            Note(FiveFretGuitarFret.Green, GuitarNoteType.Strum, 1.0, 1440,
                GuitarNoteFlags.ExtendedSustain),
            Note(FiveFretGuitarFret.Green, GuitarNoteType.Tap, 2.0),
            Note(FiveFretGuitarFret.Yellow, GuitarNoteType.Tap, 2.6),
        };

        var engine = Run(notes, HoldGreen);

        Assert.That(engine.Hits, Is.EqualTo(2), "the sustain and the cutting tap should have been hit");
        Assert.That(engine.SustainEnds, Has.Count.EqualTo(1));
        Assert.That(engine.SustainEnds[0].time, Is.LessThan(2.05),
            "control: with a single overlapping sustain the cut happens at the tap");
        Assert.That(engine.ActiveSustainCount, Is.Zero);
    }

    [Test]
    public void TapChord_OverTwoSustains_EndsBothOfThem()
    {
        // Green sustain 1.0-2.5 s, red sustain 1.5-3.0 s, so both are active from 1.5 s. The
        // green+red tap chord at 2.0 s overlaps both, and should cut both.
        var green = Note(FiveFretGuitarFret.Green, GuitarNoteType.Strum, 1.0, 1440,
            GuitarNoteFlags.ExtendedSustain);
        var red = Note(FiveFretGuitarFret.Red, GuitarNoteType.Strum, 1.5, 1440,
            GuitarNoteFlags.ExtendedSustain);
        var chord = Note(FiveFretGuitarFret.Green, GuitarNoteType.Tap, 2.0);
        chord.AddChildNote(Note(FiveFretGuitarFret.Red, GuitarNoteType.Tap, 2.0));

        var notes = new List<GuitarNote> { green, red, chord };

        var inputs = new (double, GuitarAction, bool)[]
        {
            (0.95, GuitarAction.GreenFret, true),
            (1.00, GuitarAction.StrumDown, true),
            (1.45, GuitarAction.RedFret, true),
            (1.50, GuitarAction.StrumUp, true),
        };

        // Stop at 2.4 s: after the chord (~1.94 s) and the green sustain's cut, but before the red
        // sustain's burst (2.875 s) or its natural end (3.0 s). So a sustain that is still in the
        // list here is one the chord failed to cut, not one that ended by itself.
        var engine = Run(notes, inputs, until: 2.4);

        // Precondition: all three notes were played, i.e. the chord really was hit.
        Assert.That(engine.Hits, Is.EqualTo(3), "the chord should have been hit");

        // Both sustains overlap the chord's frets, so both must have been cut by it. With the
        // loop-index skip only the green one is, and it is the first of the two in list order.
        Assert.That(engine.SustainEnds, Has.Count.EqualTo(2),
            $"both sustains should have been cut by the chord; ends were " +
            $"[{string.Join(", ", engine.SustainEnds)}]");
        Assert.That(engine.SustainEnds.Select(e => e.fret),
            Is.EquivalentTo(new[] { FiveFretGuitarFret.Green, FiveFretGuitarFret.Red }),
            "both frets' sustains should have been cut, in any order");
        Assert.That(engine.SustainEnds[1].time, Is.LessThan(2.05),
            "both cuts should happen at the chord, not later");

        // Same fact from the other side: the red sustain sits in the active list for the whole
        // span after the chord, so it keeps scoring and keeps masking its fret.
        var stillActive = engine.SustainCountOverTime.FindAll(s => s.time > 2.15 && s.count > 0);
        Assert.That(stillActive, Is.Empty,
            "no sustain should still be active after the chord that overlaps both its frets " +
            $"(found {stillActive.Count} updates with an active sustain, e.g. {First(stillActive)})");
    }
}
