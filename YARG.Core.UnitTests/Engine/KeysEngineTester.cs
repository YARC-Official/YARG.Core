using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Keys;
using YARG.Core.Engine.Keys.Engines;
using YARG.Core.Input;

namespace YARG.Core.UnitTests.Engine;

public class KeysEngineTester : EngineTester
{
    [Test]
    public void MatchingKeyBeforeChordLaneStart_DoesNotOverhit()
    {
        var (engine, notes) = CreateLaneProximityEngine();

        PressKey(engine, notes.Notes[0].Time, ProKeysAction.GreenKey);
        ReleaseKey(engine, 0.1, ProKeysAction.GreenKey);
        PressKey(engine, notes.Notes[1].Time - 0.2, ProKeysAction.GreenKey);

        Assert.That(engine.EngineStats.Overhits, Is.Zero);
    }

    [Test]
    public void MismatchedKeyBeforeChordLaneStart_RecordsOverhit()
    {
        var (engine, notes) = CreateLaneProximityEngine();

        int? overhitKey = null;
        engine.OnOverhit += key => overhitKey = key;

        PressKey(engine, notes.Notes[0].Time, ProKeysAction.GreenKey);
        ReleaseKey(engine, 0.1, ProKeysAction.GreenKey);
        PressKey(engine, notes.Notes[1].Time - 0.2, ProKeysAction.BlueKey);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(engine.EngineStats.Overhits, Is.EqualTo(1));
            Assert.That(overhitKey, Is.EqualTo((int) FiveLaneKeysEngine.FiveLaneKeysAction.BlueKey));
        }
    }

    private static (YargFiveLaneKeysEngine Engine, InstrumentDifficulty<GuitarNote> Notes) CreateLaneProximityEngine()
    {
        var firstNote = CreateNote(FiveFretGuitarFret.Green, NoteFlags.None, 0.0, 0);
        var laneStart = CreateNote(FiveFretGuitarFret.Green, NoteFlags.Tremolo | NoteFlags.LaneStart, 1.0, 480);
        laneStart.AddChildNote(CreateNote(FiveFretGuitarFret.Red, NoteFlags.Tremolo | NoteFlags.LaneStart, 1.0, 480));
        var laneEnd = CreateNote(FiveFretGuitarFret.Green, NoteFlags.Tremolo | NoteFlags.LaneEnd, 1.2, 576);
        laneEnd.AddChildNote(CreateNote(FiveFretGuitarFret.Red, NoteFlags.Tremolo | NoteFlags.LaneEnd, 1.2, 576));

        LinkNotes(firstNote, laneStart, laneEnd);

        var notes = new InstrumentDifficulty<GuitarNote>(Instrument.Keys, Difficulty.Expert,
            [firstNote, laneStart, laneEnd], new(), new());
        var engineParams = new KeysEngineParameters(
            CreateHitWindowSettings(),
            4,
            0,
            0,
            StarMultiplierThresholds,
            SoloBonusStarMultiplierThresholds,
            0.05,
            0,
            false,
            true);

        return (new YargFiveLaneKeysEngine(notes, CreateSyncTrack(), engineParams, false), notes);
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

    private static void PressKey(YargFiveLaneKeysEngine engine, double time, ProKeysAction action)
    {
        QueueInput(engine, GameInput.Create(time, action, true));
    }

    private static void ReleaseKey(YargFiveLaneKeysEngine engine, double time, ProKeysAction action)
    {
        QueueInput(engine, GameInput.Create(time, action, false));
    }

    private static void QueueInput(YargFiveLaneKeysEngine engine, GameInput input)
    {
        engine.QueueInput(ref input);
        engine.Update(input.Time);
    }
}
