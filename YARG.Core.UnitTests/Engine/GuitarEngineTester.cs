using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Guitar.Engines;
using YARG.Core.Input;

namespace YARG.Core.UnitTests.Engine;

public class GuitarEngineTester : EngineTester
{
    [Test]
    public void TenSingleNotes_FcUsesCorrectMultiplierBoundary()
    {
        var notes = Enumerable.Range(1, 10)
            .Select(index => CreateNote(
                FiveFretGuitarFret.Green,
                NoteFlags.None,
                index * 0.5,
                (uint) index * 480))
            .ToList();
        LinkNotes(notes.ToArray());

        var difficulty = new InstrumentDifficulty<GuitarNote>(Instrument.FiveFretGuitar, Difficulty.Expert,
            notes, new(), new());
        var engineParams = new GuitarEngineParameters(
            CreateHitWindowSettings(),
            4,
            0,
            0,
            StarMultiplierThresholds,
            SoloBonusStarMultiplierThresholds,
            0.1,
            0.1,
            0.1,
            false,
            true,
            false,
            false,
            true);
        var engine = new YargFiveFretGuitarEngine(difficulty, CreateSyncTrack(), engineParams, true);

        engine.Update(notes[^1].Time + 0.5);

        using (Assert.EnterMultipleScope())
        {
            // 10 notes at 50 points each; chart base score assumes the pre-note combo (all 1x),
            // while an FC commits the 10th note at 2x
            Assert.That(engine.BaseNoteScore, Is.EqualTo(500));
            Assert.That(engine.BaseScore, Is.EqualTo(500));
            Assert.That(engine.EngineStats.CommittedScore, Is.EqualTo(550));
            Assert.That(engine.EngineStats.NoteScore, Is.EqualTo(500));
            Assert.That(engine.EngineStats.MultiplierScore, Is.EqualTo(50));
            Assert.That(engine.EngineStats.PendingScore, Is.Zero);
            Assert.That(engine.EngineStats.TotalNotes, Is.EqualTo(10));
            Assert.That(engine.EngineStats.NotesHit, Is.EqualTo(10));
            Assert.That(engine.EngineStats.NotesMissed, Is.Zero);
            Assert.That(engine.EngineStats.MaxCombo, Is.EqualTo(10));
            Assert.That(engine.EngineStats.Percent, Is.EqualTo(1f));
            Assert.That(engine.EngineStats.IsFullCombo, Is.True);
        }
    }

    [Test]
    public void MissedNote_PercentReflectsMissAndComboResets()
    {
        var notes = Enumerable.Range(1, 10)
            .Select(index => CreateNote(
                FiveFretGuitarFret.Green,
                NoteFlags.None,
                index * 0.5,
                (uint) index * 480))
            .ToList();
        LinkNotes(notes.ToArray());

        var difficulty = new InstrumentDifficulty<GuitarNote>(Instrument.FiveFretGuitar, Difficulty.Expert,
            notes, new(), new());
        var engineParams = new GuitarEngineParameters(
            CreateHitWindowSettings(),
            4,
            0,
            0,
            StarMultiplierThresholds,
            SoloBonusStarMultiplierThresholds,
            0.1,
            0.1,
            0.1,
            false,
            true,
            false,
            false,
            true);
        var engine = new YargFiveFretGuitarEngine(difficulty, CreateSyncTrack(), engineParams, false);

        // Hold green, strum every note except the 5th (at 2.5s)
        var fret = GameInput.Create(0, GuitarAction.GreenFret, true);
        engine.QueueInput(ref fret);
        for (int i = 0; i < notes.Count; i++)
        {
            if (i == 4)
            {
                continue;
            }

            Strum(engine, notes[i].Time);
        }

        engine.Update(notes[^1].Time + 0.5);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(engine.EngineStats.NotesHit, Is.EqualTo(9));
            Assert.That(engine.EngineStats.NotesMissed, Is.EqualTo(1));
            Assert.That(engine.EngineStats.MaxCombo, Is.EqualTo(5));
            Assert.That(engine.EngineStats.Percent, Is.EqualTo(0.9f));
            Assert.That(engine.EngineStats.IsFullCombo, Is.False);
            // Combo resets after the miss, so no note ever reaches 2x
            Assert.That(engine.EngineStats.CommittedScore, Is.EqualTo(450));
            Assert.That(engine.EngineStats.MultiplierScore, Is.Zero);
        }
    }

    [Test]
    public void DisjointChord_CountsSingleComboAndScoresEachSustainOnce()
    {
        var notes = Enumerable.Range(1, 8)
            .Select(index => CreateNote(
                FiveFretGuitarFret.Green,
                NoteFlags.None,
                index * 0.5,
                (uint) index * 480))
            .ToList();

        // Disjoint chord: green (no sustain) + red (one-beat sustain), same tick
        var disjointChord = new GuitarNote(FiveFretGuitarFret.Green, GuitarNoteType.Strum,
            GuitarNoteFlags.Disjoint, NoteFlags.None, 4.5, 0, 8 * 480, 0);
        disjointChord.AddChildNote(new GuitarNote(FiveFretGuitarFret.Red, GuitarNoteType.Strum,
            GuitarNoteFlags.None, NoteFlags.None, 4.5, 0.5, 8 * 480, 480));

        var lastNote = CreateNote(FiveFretGuitarFret.Green, NoteFlags.None, 5.0, 9 * 480);
        var allNotes = notes.Concat(new[] { disjointChord, lastNote }).ToList();
        LinkNotes(allNotes.ToArray());

        var difficulty = new InstrumentDifficulty<GuitarNote>(Instrument.FiveFretGuitar, Difficulty.Expert,
            allNotes, new(), new());
        var engineParams = new GuitarEngineParameters(
            CreateHitWindowSettings(),
            4,
            0,
            0,
            StarMultiplierThresholds,
            SoloBonusStarMultiplierThresholds,
            0.1,
            0.1,
            0.1,
            false,
            true,
            false,
            false,
            true);
        var engine = new YargFiveFretGuitarEngine(difficulty, CreateSyncTrack(), engineParams, true);

        engine.Update(5.5);

        using (Assert.EnterMultipleScope())
        {
            // 8 singles at 1x + chord (2 notes) at 1x + last single at 1x = 550,
            // plus the child sustain (480 ticks / 19.2 ticks per point) = 25
            //
            // The disjoint chord is hit with a single strum, so it must only
            // increment combo once (8 singles + chord + last note = 10 max combo).
            // If the chord's child notes incremented combo, the last note would
            // be scored at 2x and BaseScore would be 625 instead of 575.
            Assert.That(engine.BaseNoteScore, Is.EqualTo(575));
            Assert.That(engine.BaseScore, Is.EqualTo(575));
            Assert.That(engine.EngineStats.TotalNotes, Is.EqualTo(10));
            Assert.That(engine.EngineStats.NotesHit, Is.EqualTo(10));
            Assert.That(engine.EngineStats.MaxCombo, Is.EqualTo(10));
            Assert.That(engine.EngineStats.Percent, Is.EqualTo(1f));
            Assert.That(engine.EngineStats.IsFullCombo, Is.True);
        }
    }

    [Test]
    public void TopFretBeforeTrillStart_IsForgivenByGuitarGhostLeniency()
    {
        var (engine, notes) = CreateTrillProximityEngine();

        Assert.That(engine.IsGhostInputForgivenByTrill((int) GuitarAction.YellowFret, notes.Notes[1].Time - 0.2, 1),
            Is.True);
    }

    [Test]
    public void BottomFretBeforeTrillStart_IsForgivenByGuitarGhostLeniency()
    {
        var (engine, notes) = CreateTrillProximityEngine();

        Assert.That(engine.IsGhostInputForgivenByTrill((int) GuitarAction.RedFret, notes.Notes[1].Time - 0.2, 1),
            Is.True);
    }

    [Test]
    public void ExcludedFretBeforeChordTrillStart_IsNotForgivenByGuitarGhostLeniency()
    {
        var (engine, notes) = CreateTrillProximityEngine();

        Assert.That(engine.IsGhostInputForgivenByTrill((int) GuitarAction.OrangeFret, notes.Notes[1].Time - 0.2, 1),
            Is.False);
    }

    private static (TestFiveFretGuitarEngine Engine, InstrumentDifficulty<GuitarNote> Notes) CreateTrillProximityEngine()
    {
        var firstNote = CreateNote(FiveFretGuitarFret.Green, NoteFlags.None, 0.0, 0);
        var laneStart = CreateNote(FiveFretGuitarFret.Yellow, NoteFlags.Trill | NoteFlags.LaneStart, 1.0, 480);
        var laneEnd = CreateNote(FiveFretGuitarFret.Red, NoteFlags.Trill | NoteFlags.LaneEnd, 1.2, 576);

        LinkNotes(firstNote, laneStart, laneEnd);

        var notes = new InstrumentDifficulty<GuitarNote>(Instrument.FiveFretGuitar, Difficulty.Expert,
            [firstNote, laneStart, laneEnd], new(), new());
        var engineParams = new GuitarEngineParameters(
            CreateHitWindowSettings(),
            4,
            0,
            0,
            StarMultiplierThresholds,
            SoloBonusStarMultiplierThresholds,
            0.1,
            0.1,
            0.1,
            false,
            true,
            false,
            false,
            true);

        return (new TestFiveFretGuitarEngine(notes, CreateSyncTrack(), engineParams), notes);
    }

    private sealed class TestFiveFretGuitarEngine(
        InstrumentDifficulty<GuitarNote> chart,
        SyncTrack syncTrack,
        GuitarEngineParameters engineParameters)
        : YargFiveFretGuitarEngine(chart, syncTrack, engineParameters, false)
    {
        public bool IsGhostInputForgivenByTrill(int inputNote, double currentTime, int noteIndex)
        {
            CurrentTime = currentTime;
            NoteIndex = noteIndex;
            return IsGhostInTrillLeniencyWindow(inputNote);
        }
    }

    private static void Strum(YargFiveFretGuitarEngine engine, double time)
    {
        var strum = GameInput.Create(time, GuitarAction.StrumDown, true);
        engine.QueueInput(ref strum);
        engine.Update(time);
    }

    private static GuitarNote CreateNote(FiveFretGuitarFret fret, NoteFlags flags, double time, uint tick)
    {
        return new GuitarNote(fret, GuitarNoteType.Strum, GuitarNoteFlags.None, flags, time, 0, tick, 0);
    }

    private static void LinkNotes(params GuitarNote[] notes)
    {
        for (int i = 0; i < notes.Length; i++)
        {
            if (i > 0)
            {
                notes[i].PreviousNote = notes[i - 1];
            }

            if (i < notes.Length - 1)
            {
                notes[i].NextNote = notes[i + 1];
            }
        }
    }

    private static SyncTrack CreateSyncTrack()
    {
        var syncTrack = new SyncTrack(480);
        syncTrack.Tempos.Add(new TempoChange(120, 0, 0));
        return syncTrack;
    }

    private static HitWindowSettings CreateHitWindowSettings()
    {
        return new HitWindowSettings(0.1, 0.1, 1.0, false, 0, 1.0, 1.0, 0.15, 0.25);
    }
}
