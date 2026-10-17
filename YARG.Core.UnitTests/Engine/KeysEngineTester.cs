using System.Collections.Generic;
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
    public void ReleaseDoesNotHitSingleNoteWhenOptionIsDisabled()
    {
        var note = CreateNote(FiveFretGuitarFret.Green, NoteFlags.None, 1.0, 480);
        var engine = CreateReleaseHitEngine(enabled: false, note);

        PressKey(engine, 0.5, ProKeysAction.GreenKey);
        ReleaseKey(engine, 1.0, ProKeysAction.GreenKey);

        Assert.That(note.WasHit, Is.False);
    }

    [Test]
    public void ReleaseHitsMatchingSingleNoteWhenOptionIsEnabled()
    {
        var note = CreateNote(FiveFretGuitarFret.Green, NoteFlags.None, 1.0, 480);
        var engine = CreateReleaseHitEngine(enabled: true, note);

        PressKey(engine, 0.5, ProKeysAction.GreenKey);
        ReleaseKey(engine, 1.0, ProKeysAction.GreenKey);

        Assert.That(note.WasHit, Is.True);
    }

    [Test]
    public void ReleaseWithoutMatchingNoteDoesNotHitOrOverhit()
    {
        var note = CreateNote(FiveFretGuitarFret.Green, NoteFlags.None, 1.0, 480);
        var engine = CreateReleaseHitEngine(enabled: true, note);

        ReleaseKey(engine, 1.0, ProKeysAction.BlueKey);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(note.WasHit, Is.False);
            Assert.That(engine.EngineStats.Overhits, Is.Zero);
        }
    }

    [Test]
    public void ReleaseOutsideHitWindowDoesNotHitSingleNote()
    {
        var note = CreateNote(FiveFretGuitarFret.Green, NoteFlags.None, 1.0, 480);
        var engine = CreateReleaseHitEngine(enabled: true, note);

        PressKey(engine, 0.5, ProKeysAction.GreenKey);
        ReleaseKey(engine, 1.11, ProKeysAction.GreenKey);

        Assert.That(note.WasHit, Is.False);
    }

    [Test]
    public void ReleaseModeChordHitsRepeatedChordEvenWhenPreviousChordWasMissed()
    {
        var previousChord = CreateChord(0.0, 0, FiveFretGuitarFret.Green, FiveFretGuitarFret.Red);
        var currentChord = CreateChord(1.0, 480, FiveFretGuitarFret.Green, FiveFretGuitarFret.Red);
        LinkNotes(previousChord, currentChord);
        var engine = CreateReleaseHitEngine(enabled: true, previousChord, currentChord);

        // Miss the first chord, then hold its lanes so they can be released for the repeated chord.
        engine.Update(0.2);
        PressKey(engine, 0.3, ProKeysAction.GreenKey);
        PressKey(engine, 0.31, ProKeysAction.RedKey);
        ReleaseKey(engine, 0.96, ProKeysAction.GreenKey);
        ReleaseKey(engine, 0.98, ProKeysAction.RedKey);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(previousChord.WasFullyMissed(), Is.True);
            Assert.That(currentChord.WasFullyHit(), Is.True);
        }
    }

    [Test]
    public void ReleaseModeChordStaggerHitsOnlyReleasedLanes()
    {
        var previousChord = CreateChord(0.0, 0, FiveFretGuitarFret.Green, FiveFretGuitarFret.Red);
        var currentChord = CreateChord(1.0, 480, FiveFretGuitarFret.Green, FiveFretGuitarFret.Red);
        LinkNotes(previousChord, currentChord);
        var engine = CreateReleaseHitEngine(enabled: true, previousChord, currentChord);

        engine.Update(0.2);
        PressKey(engine, 0.3, ProKeysAction.GreenKey);
        PressKey(engine, 0.31, ProKeysAction.RedKey);
        ReleaseKey(engine, 0.96, ProKeysAction.GreenKey);
        engine.Update(1.01);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(currentChord.WasHit, Is.True);
            Assert.That(currentChord.WasMissed, Is.False);
            Assert.That(currentChord.ChildNotes[0].WasHit, Is.False);
            Assert.That(currentChord.ChildNotes[0].WasMissed, Is.True);
        }
    }

    [Test]
    public void ReleaseCannotCompletePressModeChord()
    {
        var previousChord = CreateChord(0.0, 0, FiveFretGuitarFret.Green, FiveFretGuitarFret.Red);
        var currentChord = CreateChord(1.0, 480, FiveFretGuitarFret.Green, FiveFretGuitarFret.Red);
        LinkNotes(previousChord, currentChord);
        var engine = CreateReleaseHitEngine(enabled: true, previousChord, currentChord);

        engine.Update(0.2);
        PressKey(engine, 0.3, ProKeysAction.RedKey);
        PressKey(engine, 0.96, ProKeysAction.GreenKey);
        ReleaseKey(engine, 0.98, ProKeysAction.RedKey);
        engine.Update(1.01);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(currentChord.WasHit, Is.True);
            Assert.That(currentChord.ChildNotes[0].WasHit, Is.False);
            Assert.That(currentChord.ChildNotes[0].WasMissed, Is.True);
        }
    }

    [Test]
    public void PressCannotCompleteReleaseModeChord()
    {
        var previousChord = CreateChord(0.0, 0, FiveFretGuitarFret.Green, FiveFretGuitarFret.Red);
        var currentChord = CreateChord(1.0, 480, FiveFretGuitarFret.Green, FiveFretGuitarFret.Red);
        LinkNotes(previousChord, currentChord);
        var engine = CreateReleaseHitEngine(enabled: true, previousChord, currentChord);

        engine.Update(0.2);
        PressKey(engine, 0.3, ProKeysAction.GreenKey);
        PressKey(engine, 0.3, ProKeysAction.RedKey);
        ReleaseKey(engine, 0.96, ProKeysAction.GreenKey);
        PressKey(engine, 0.98, ProKeysAction.RedKey);
        engine.Update(1.01);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(currentChord.WasHit, Is.True);
            Assert.That(currentChord.ChildNotes[0].WasHit, Is.False);
            Assert.That(currentChord.ChildNotes[0].WasMissed, Is.True);
        }
    }

    [Test]
    public void ReleaseDoesNotHitChordWithoutMatchingPreviousChordMask()
    {
        var previousChord = CreateChord(0.0, 0, FiveFretGuitarFret.Green, FiveFretGuitarFret.Red);
        var currentChord = CreateChord(1.0, 480, FiveFretGuitarFret.Green, FiveFretGuitarFret.Blue);
        LinkNotes(previousChord, currentChord);
        var engine = CreateReleaseHitEngine(enabled: true, previousChord, currentChord);

        engine.Update(0.2);
        PressKey(engine, 0.3, ProKeysAction.GreenKey);
        PressKey(engine, 0.31, ProKeysAction.BlueKey);
        ReleaseKey(engine, 0.96, ProKeysAction.GreenKey);
        ReleaseKey(engine, 0.98, ProKeysAction.BlueKey);

        Assert.That(currentChord.WasHit, Is.False);
    }

    [Test]
    public void ReleaseDoesNotHitChordAfterSinglePreviousNote()
    {
        var previousNote = CreateNote(FiveFretGuitarFret.Green, NoteFlags.None, 0.0, 0);
        var currentChord = CreateChord(1.0, 480, FiveFretGuitarFret.Green, FiveFretGuitarFret.Red);
        LinkNotes(previousNote, currentChord);
        var engine = CreateReleaseHitEngine(enabled: true, previousNote, currentChord);

        engine.Update(0.2);
        PressKey(engine, 0.3, ProKeysAction.GreenKey);
        PressKey(engine, 0.31, ProKeysAction.RedKey);
        ReleaseKey(engine, 0.96, ProKeysAction.GreenKey);
        ReleaseKey(engine, 0.98, ProKeysAction.RedKey);

        Assert.That(currentChord.WasHit, Is.False);
    }

    [Test]
    public void ProKeysIgnoresReleaseHitParameter()
    {
        var note = new ProKeysNote(0, ProKeysNoteFlags.None, NoteFlags.None, 1.0, 0, 480, 0);
        var notes = new InstrumentDifficulty<ProKeysNote>(Instrument.ProKeys, Difficulty.Expert,
            [note], new(), new());
        var engineParameters = CreateKeysEngineParameters(releaseKeysCanHitNotes: true);
        var syncTrack = CreateSyncTrack();
        var engine = new YargProKeysEngine(notes, syncTrack, engineParameters, false);

        var release = GameInput.Create(1.0, ProKeysAction.Key1, false);
        engine.QueueInput(ref release);
        engine.Update(1.0);

        Assert.That(note.WasHit, Is.False);
    }

    [Test]
    public void MatchingKeyBeforeChordTremoloLaneStart_DoesNotOverhit()
    {
        var (engine, notes) = CreateChordTremoloLaneProximityEngine();

        PressKey(engine, notes.Notes[0].Time, ProKeysAction.GreenKey);
        ReleaseKey(engine, 0.1, ProKeysAction.GreenKey);
        PressKey(engine, notes.Notes[1].Time - 0.2, ProKeysAction.GreenKey);

        Assert.That(engine.EngineStats.Overhits, Is.Zero);
    }

    [Test]
    public void MismatchedKeyBeforeChordTremoloLaneStart_RecordsOverhit()
    {
        var (engine, notes) = CreateChordTremoloLaneProximityEngine();

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

    [Test]
    public void MatchingTopKeyBeforeTrillLaneStart_DoesNotOverhit()
    {
        var (engine, notes) = CreateTrillLaneProximityEngine();

        PressKey(engine, notes.Notes[0].Time, ProKeysAction.GreenKey);
        ReleaseKey(engine, 0.1, ProKeysAction.GreenKey);
        PressKey(engine, notes.Notes[1].Time - 0.2, ProKeysAction.OrangeKey);

        Assert.That(engine.EngineStats.Overhits, Is.Zero);
    }

    [Test]
    public void MatchingBottomKeyBeforeTrillLaneStart_DoesNotOverhit()
    {
        var (engine, notes) = CreateTrillLaneProximityEngine();

        PressKey(engine, notes.Notes[0].Time, ProKeysAction.GreenKey);
        ReleaseKey(engine, 0.1, ProKeysAction.GreenKey);
        PressKey(engine, notes.Notes[1].Time - 0.2, ProKeysAction.GreenKey);

        Assert.That(engine.EngineStats.Overhits, Is.Zero);
    }

    [Test]
    public void MismatchedKeyBeforeTrillLaneStart_DoesNotOverhit()
    {
        var (engine, notes) = CreateTrillLaneProximityEngine();

        int? overhitKey = null;
        engine.OnOverhit += key => overhitKey = key;

        PressKey(engine, notes.Notes[0].Time, ProKeysAction.GreenKey);
        ReleaseKey(engine, 0.1, ProKeysAction.GreenKey);
        PressKey(engine, notes.Notes[1].Time - 0.2, ProKeysAction.YellowKey);

        Assert.That(engine.EngineStats.Overhits, Is.EqualTo(1));
        Assert.That(overhitKey, Is.EqualTo((int) FiveLaneKeysEngine.FiveLaneKeysAction.YellowKey));
    }

    private static YargFiveLaneKeysEngine CreateReleaseHitEngine(bool enabled, params GuitarNote[] notes)
    {
        LinkNotes(notes);

        var difficulty = new InstrumentDifficulty<GuitarNote>(Instrument.Keys, Difficulty.Expert,
            new List<GuitarNote>(notes), new(), new());
        var engineParameters = CreateKeysEngineParameters(releaseKeysCanHitNotes: enabled);
        return new YargFiveLaneKeysEngine(difficulty, CreateSyncTrack(), engineParameters, false);
    }

    private static KeysEngineParameters CreateKeysEngineParameters(bool releaseKeysCanHitNotes)
    {
        return new KeysEngineParameters(
            CreateHitWindowSettings(),
            4,
            0,
            0,
            StarMultiplierThresholds,
            SoloBonusStarMultiplierThresholds,
            0.05,
            0,
            false,
            true,
            releaseKeysCanHitNotes);
    }

    private static GuitarNote CreateChord(double time, uint tick, params FiveFretGuitarFret[] frets)
    {
        var chord = CreateNote(frets[0], NoteFlags.None, time, tick);
        for (int i = 1; i < frets.Length; i++)
        {
            chord.AddChildNote(CreateNote(frets[i], NoteFlags.None, time, tick));
        }

        return chord;
    }

    private static (YargFiveLaneKeysEngine Engine, InstrumentDifficulty<GuitarNote> Notes) CreateChordTremoloLaneProximityEngine()
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

    private static (YargFiveLaneKeysEngine Engine, InstrumentDifficulty<GuitarNote> Notes) CreateTrillLaneProximityEngine()
    {
        var firstNote = CreateNote(FiveFretGuitarFret.Green, NoteFlags.None, 0.0, 0);
        var laneStart = CreateNote(FiveFretGuitarFret.Green, NoteFlags.Trill | NoteFlags.LaneStart, 1.0, 480);
        var laneEnd = CreateNote(FiveFretGuitarFret.Orange, NoteFlags.Trill | NoteFlags.LaneEnd, 1.2, 576);

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
