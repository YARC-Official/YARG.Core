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
