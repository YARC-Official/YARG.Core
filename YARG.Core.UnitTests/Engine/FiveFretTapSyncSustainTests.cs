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

/// <summary>
/// Regression tests for the sustain-cutting loop in <see cref="YargFiveFretGuitarEngine.HitNote"/>
/// (inherited by <see cref="YargSixFretGuitarEngine"/>): cutting every overlapping active sustain
/// without index skips, committing earned points (including burst-window handling), and avoiding
/// double-scoring after burst.
/// </summary>
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
        public int StarPowerScore => EngineStats.StarPowerScore;
    }

    private class TestSixFretEngine : YargSixFretGuitarEngine
    {
        public readonly List<(double time, SixFretGuitarFret fret, bool scored)> SustainEnds = new();
        public int Hits;

        public TestSixFretEngine(InstrumentDifficulty<GuitarNote> chart, SyncTrack syncTrack,
            GuitarEngineParameters parameters) : base(chart, syncTrack, parameters, false)
        {
            OnSustainEnd += (note, time, scored) =>
                SustainEnds.Add((time, (SixFretGuitarFret) note.Fret, scored));
            OnNoteHit += (_, _) => Hits++;
        }

        public int ActiveSustainCount => ActiveSustains.Count;
        public int Score => EngineStats.CommittedScore;
        public int SustainScore => EngineStats.SustainScore;
    }

    private static GuitarNote Note(FiveFretGuitarFret fret, GuitarNoteType type, double time,
        uint tickLength = 0, GuitarNoteFlags flags = GuitarNoteFlags.None,
        NoteFlags noteFlags = NoteFlags.None)
    {
        uint tick = (uint) Math.Round(time * TICKS_PER_SEC);
        return new GuitarNote(fret, type, flags, noteFlags, time, tickLength / TICKS_PER_SEC, tick, tickLength);
    }

    private static GuitarNote SixFretNote(SixFretGuitarFret fret, GuitarNoteType type, double time,
        uint tickLength = 0, GuitarNoteFlags flags = GuitarNoteFlags.None)
    {
        uint tick = (uint) Math.Round(time * TICKS_PER_SEC);
        return new GuitarNote(fret, type, flags, NoteFlags.None, time, tickLength / TICKS_PER_SEC, tick, tickLength);
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
        track.TimeSignatures.Add(new TimeSignatureChange(4, 4, 0.0, 0, 0, 0, 0, 0));
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
        Assert.That(engine.SustainEnds[0].scored, Is.False,
            "an early cut before burst should not report the sustain as fully scored");

        // A sustain held from 1.0 s to the cut at its front end (~1.94 s, the window saturates at
        // MaxWindow 0.12) has earned ~902/19.2 = 47 of its 75 points. Pinned as a range so that a
        // fix which pays the *whole* sustain instead of the earned part also fails this test.
        Assert.That(engine.SustainScore, Is.InRange(45, 50),
            $"the ~47 points earned before the cut should have been committed " +
            $"(SustainScore was {engine.SustainScore}, CommittedScore {engine.Score})");
        Assert.That(engine.Score, Is.EqualTo(100 + engine.SustainScore),
            "CommittedScore should match the two notes (100) plus the committed sustain points");
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
        Assert.That(engine.SustainEnds[0].scored, Is.True);

        // 1440 ticks / 19.2 ticks per point at multiplier 1.
        Assert.That(engine.SustainScore, Is.EqualTo(75),
            "control: an uncut sustain pays its full 75 points at burst");
        Assert.That(engine.Score, Is.EqualTo(125));
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
            Is.EqualTo(new[] { FiveFretGuitarFret.Green, FiveFretGuitarFret.Red }),
            "both frets' sustains should have been cut in FIFO order");
        Assert.That(engine.SustainEnds.Select(e => e.time), Has.All.LessThan(2.05),
            "both cuts should happen at the chord, not later");
        Assert.That(engine.SustainEnds.Select(e => e.scored), Has.All.False);

        // Green held ~0.94 s (~47 pts) + Red held ~0.44 s (~22 pts) = ~69 pts; 4 note heads (G, R, G+R) = 200 pts.
        Assert.That(engine.SustainScore, Is.InRange(65, 73));
        Assert.That(engine.Score, Is.EqualTo(200 + engine.SustainScore));

        // Same fact from the other side: the red sustain sits in the active list for the whole
        // span after the chord, so it keeps scoring and keeps masking its fret.
        var stillActive = engine.SustainCountOverTime.FindAll(s => s.time > 2.15 && s.count > 0);
        Assert.That(stillActive, Is.Empty,
            "no sustain should still be active after the chord that overlaps both its frets " +
            $"(found {stillActive.Count} updates with an active sustain, e.g. {First(stillActive)})");
    }

    [Test]
    public void StrumNote_AtExactBurstTime_PaysFullSustainAndMarksScored()
    {
        // Green sustain 1.0-2.5 s (ticks 960..2400). SustainBurstThreshold is 480 / 4 = 120 ticks,
        // so burstTime is tick 2280 (2.375 s). Strumming a same-fret note at exact 2.375 s runs
        // CheckForNoteHit before UpdateSustains on that frame; the cut must still honor burst.
        var notes = new List<GuitarNote>
        {
            Note(FiveFretGuitarFret.Green, GuitarNoteType.Strum, 1.0, 1440,
                GuitarNoteFlags.ExtendedSustain),
            Note(FiveFretGuitarFret.Green, GuitarNoteType.Strum, 2.375),
        };

        var inputs = new (double, GuitarAction, bool)[]
        {
            (0.95, GuitarAction.GreenFret, true),
            (1.00, GuitarAction.StrumDown, true),
            (2.375, GuitarAction.StrumUp, true),
        };

        var engine = Run(notes, inputs);

        Assert.That(engine.Hits, Is.EqualTo(2));
        Assert.That(engine.SustainEnds, Has.Count.EqualTo(1));
        Assert.That(engine.SustainEnds[0].time, Is.EqualTo(2.375).Within(1e-6));
        Assert.That(engine.SustainEnds[0].scored, Is.True,
            "a cut at or past burstTime must mark the sustain as fully scored");
        Assert.That(engine.SustainScore, Is.EqualTo(75),
            "burst threshold should pay the full 75 points");
        Assert.That(engine.Score, Is.EqualTo(175));
    }

    [Test]
    public void TapNote_AfterBurst_DoesNotDoubleScoreSustain()
    {
        // Green sustain 1.0-2.5 s bursts at 2.375 s. The tap at 2.48 s hits at its front end (~2.42 s),
        // after burst has already committed all 75 points but before TickEnd (2.5 s).
        var notes = new List<GuitarNote>
        {
            Note(FiveFretGuitarFret.Green, GuitarNoteType.Strum, 1.0, 1440,
                GuitarNoteFlags.ExtendedSustain),
            Note(FiveFretGuitarFret.Green, GuitarNoteType.Tap, 2.48),
        };

        var engine = Run(notes, HoldGreen);

        Assert.That(engine.Hits, Is.EqualTo(2));
        Assert.That(engine.SustainEnds, Has.Count.EqualTo(1));
        Assert.That(engine.SustainEnds[0].time, Is.LessThan(2.47));
        Assert.That(engine.SustainEnds[0].scored, Is.True);
        Assert.That(engine.SustainScore, Is.EqualTo(75),
            "a sustain already committed at burst must not be scored a second time when cut");
        Assert.That(engine.Score, Is.EqualTo(175));
    }

    [Test]
    public void TapNote_CutsSustain_WithMultiplierAndStarPower()
    {
        // Build a 10-note streak (2x multiplier) with two SP phrases (half bar), activate SP (4x total),
        // then hold a green sustain 2.0-3.5 s and cut it with a green tap at 3.0 s.
        var notes = new List<GuitarNote>();
        var inputs = new List<(double, GuitarAction, bool)>
        {
            (0.45, GuitarAction.GreenFret, true),
        };

        for (int i = 0; i < 10; i++)
        {
            double t = 0.5 + i * 0.1;
            var spFlags = i switch
            {
                0 or 2 => NoteFlags.StarPower | NoteFlags.StarPowerStart,
                1 or 3 => NoteFlags.StarPower | NoteFlags.StarPowerEnd,
                _ => NoteFlags.None,
            };
            notes.Add(Note(FiveFretGuitarFret.Green, GuitarNoteType.Strum, t, noteFlags: spFlags));
            inputs.Add((t, i % 2 == 0 ? GuitarAction.StrumDown : GuitarAction.StrumUp, true));
        }

        inputs.Add((1.55, GuitarAction.StarPower, true));
        inputs.Add((1.60, GuitarAction.StarPower, false));

        notes.Add(Note(FiveFretGuitarFret.Green, GuitarNoteType.Strum, 2.0, 1440,
            GuitarNoteFlags.ExtendedSustain));
        inputs.Add((2.0, GuitarAction.StrumDown, true));

        notes.Add(Note(FiveFretGuitarFret.Green, GuitarNoteType.Tap, 3.0));

        var engine = Run(notes, inputs, until: 3.6);

        Assert.That(engine.Hits, Is.EqualTo(12));
        Assert.That(engine.SustainEnds, Has.Count.EqualTo(1));
        Assert.That(engine.SustainEnds[0].time, Is.LessThan(3.05));
        Assert.That(engine.SustainEnds[0].scored, Is.False);

        // ~47 base sustain points at 2x combo multiplier -> ~94 in SustainScore (SP excluded),
        // and 2 * SustainScore (~188) in CommittedScore (4x total with SP active).
        // Note score: 9 notes @ 1x (450) + 1 note @ 2x (100) + 2 notes @ 4x SP (400) = 950.
        Assert.That(engine.SustainScore, Is.InRange(90, 100));
        Assert.That(engine.Score, Is.EqualTo(950 + 2 * engine.SustainScore));
        Assert.That(engine.StarPowerScore, Is.EqualTo(200 + engine.SustainScore));
    }

    [Test]
    public void TapChord_OverDisjointAndNonOverlappingSustains_CutsOnlyOverlapping()
    {
        // Green sustain (1.0-3.0 s, lower anchor) plus disjoint red+yellow sustain (1.5-3.0 s).
        // A red+yellow tap chord at 2.0 s must cut both disjoint sustains while leaving green intact.
        var green = Note(FiveFretGuitarFret.Green, GuitarNoteType.Strum, 1.0, 1920,
            GuitarNoteFlags.ExtendedSustain);

        var disjoint = Note(FiveFretGuitarFret.Red, GuitarNoteType.Strum, 1.5, 1440,
            GuitarNoteFlags.ExtendedSustain | GuitarNoteFlags.Disjoint);
        disjoint.AddChildNote(Note(FiveFretGuitarFret.Yellow, GuitarNoteType.Strum, 1.5, 1440,
            GuitarNoteFlags.ExtendedSustain | GuitarNoteFlags.Disjoint));

        var chord = Note(FiveFretGuitarFret.Red, GuitarNoteType.Tap, 2.0);
        chord.AddChildNote(Note(FiveFretGuitarFret.Yellow, GuitarNoteType.Tap, 2.0));

        var notes = new List<GuitarNote> { green, disjoint, chord };
        var inputs = new (double, GuitarAction, bool)[]
        {
            (0.95, GuitarAction.GreenFret, true),
            (1.00, GuitarAction.StrumDown, true),
            (1.45, GuitarAction.RedFret, true),
            (1.45, GuitarAction.YellowFret, true),
            (1.50, GuitarAction.StrumUp, true),
        };

        var engine = Run(notes, inputs, until: 3.3);

        Assert.That(engine.Hits, Is.EqualTo(3));
        Assert.That(engine.SustainEnds, Has.Count.EqualTo(3));

        var cutEnds = engine.SustainEnds.Where(e => e.time < 2.05).ToList();
        var naturalEnds = engine.SustainEnds.Where(e => e.time > 2.9).ToList();

        Assert.That(cutEnds.Select(e => e.fret),
            Is.EqualTo(new[] { FiveFretGuitarFret.Red, FiveFretGuitarFret.Yellow }));
        Assert.That(cutEnds.Select(e => e.scored), Has.All.False);

        Assert.That(naturalEnds, Has.Count.EqualTo(1));
        Assert.That(naturalEnds[0].fret, Is.EqualTo(FiveFretGuitarFret.Green));
        Assert.That(naturalEnds[0].scored, Is.True);

        // Full green sustain (1920 ticks = 100 pts) + two disjoint sustains (~0.44 s -> ~22 pts each = ~44) = ~144.
        Assert.That(engine.SustainScore, Is.InRange(140, 150));
        Assert.That(engine.Score, Is.EqualTo(250 + engine.SustainScore));
    }

    [Test]
    public void SixFret_TapChord_OverTwoSustains_EndsAndScoresBoth()
    {
        var b1 = SixFretNote(SixFretGuitarFret.Black1, GuitarNoteType.Strum, 1.0, 1440,
            GuitarNoteFlags.ExtendedSustain);
        var b2 = SixFretNote(SixFretGuitarFret.Black2, GuitarNoteType.Strum, 1.5, 1440,
            GuitarNoteFlags.ExtendedSustain);
        var chord = SixFretNote(SixFretGuitarFret.Black1, GuitarNoteType.Tap, 2.0);
        chord.AddChildNote(SixFretNote(SixFretGuitarFret.Black2, GuitarNoteType.Tap, 2.0));

        var notes = new List<GuitarNote> { b1, b2, chord };
        Link(notes);

        var difficulty = new InstrumentDifficulty<GuitarNote>(Instrument.SixFretGuitar, Difficulty.Expert,
            notes, new(), new());
        var engine = new TestSixFretEngine(difficulty, SyncTrack(), Parameters());

        var inputs = new (double, GuitarAction, bool)[]
        {
            (0.95, GuitarAction.Black1Fret, true),
            (1.00, GuitarAction.StrumDown, true),
            (1.45, GuitarAction.Black2Fret, true),
            (1.50, GuitarAction.StrumUp, true),
        };

        foreach (var (time, action, button) in inputs)
        {
            var input = GameInput.Create(time, action, button);
            engine.QueueInput(ref input);
        }

        for (double time = 0.4; time <= 2.4; time += 1.0 / 240.0)
        {
            engine.Update(time);
        }

        Assert.That(engine.Hits, Is.EqualTo(3));
        Assert.That(engine.SustainEnds, Has.Count.EqualTo(2));
        Assert.That(engine.SustainEnds.Select(e => e.fret),
            Is.EqualTo(new[] { SixFretGuitarFret.Black1, SixFretGuitarFret.Black2 }));
        Assert.That(engine.SustainEnds.Select(e => e.time), Has.All.LessThan(2.05));
        Assert.That(engine.SustainEnds.Select(e => e.scored), Has.All.False);
        Assert.That(engine.ActiveSustainCount, Is.Zero);
        Assert.That(engine.SustainScore, Is.InRange(65, 73));
        Assert.That(engine.Score, Is.EqualTo(200 + engine.SustainScore));
    }
}
