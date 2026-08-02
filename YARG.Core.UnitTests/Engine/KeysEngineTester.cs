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

        var difficulty = new InstrumentDifficulty<GuitarNote>(Instrument.Keys, Difficulty.Expert,
            notes, new(), new());
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
        var engine = new YargFiveLaneKeysEngine(difficulty, CreateSyncTrack(), engineParams, true);

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

        var difficulty = new InstrumentDifficulty<GuitarNote>(Instrument.Keys, Difficulty.Expert,
            notes, new(), new());
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
        var engine = new YargFiveLaneKeysEngine(difficulty, CreateSyncTrack(), engineParams, false);

        // Hit every note except the 5th (at 2.5s)
        for (int i = 0; i < notes.Count; i++)
        {
            if (i == 4)
            {
                continue;
            }

            PressKey(engine, notes[i].Time, ProKeysAction.GreenKey);
            ReleaseKey(engine, notes[i].Time + 0.05, ProKeysAction.GreenKey);
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
    public void EmptyChart_ReportsFullPercentAndFullComboByDefinition()
    {
        // TotalNotes == 0 forces Percent to 1f (div-by-zero guard) and IsFullCombo to true
        var difficulty = new InstrumentDifficulty<GuitarNote>(Instrument.Keys, Difficulty.Expert,
            new List<GuitarNote>(), new(), new());
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
        var engine = new YargFiveLaneKeysEngine(difficulty, CreateSyncTrack(), engineParams, false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(engine.BaseScore, Is.Zero);
            Assert.That(engine.EngineStats.TotalNotes, Is.Zero);
            Assert.That(engine.EngineStats.NotesHit, Is.Zero);
            Assert.That(engine.EngineStats.NotesMissed, Is.Zero);
            Assert.That(engine.EngineStats.MaxCombo, Is.Zero);
            Assert.That(engine.EngineStats.Percent, Is.EqualTo(1f));
            Assert.That(engine.EngineStats.IsFullCombo, Is.True);
        }
    }

    [Test]
    public void SingleSustainNote_ChartScoreMatchesRuntimeScore()
    {
        // One green note with a one-beat sustain (480 ticks at 120bpm)
        var note = new GuitarNote(FiveFretGuitarFret.Green, GuitarNoteType.Strum, GuitarNoteFlags.None,
            NoteFlags.None, 0.0, 0.5, 0, 480);
        LinkNotes(note);

        var difficulty = new InstrumentDifficulty<GuitarNote>(Instrument.Keys, Difficulty.Expert,
            new List<GuitarNote> { note }, new(), new());
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
        var engine = new YargFiveLaneKeysEngine(difficulty, CreateSyncTrack(), engineParams, true);

        // Step through time so the bot keeps the key held through the sustain
        for (double t = 0; t <= 0.8; t += 0.01)
        {
            engine.Update(t);
        }

        using (Assert.EnterMultipleScope())
        {
            // 50 for the note + 25 for the one-beat sustain (480 ticks / 19.2 ticks per point)
            Assert.That(engine.BaseNoteScore, Is.EqualTo(75));
            Assert.That(engine.BaseScore, Is.EqualTo(75));
            Assert.That(engine.EngineStats.CommittedScore, Is.EqualTo(75));
            Assert.That(engine.EngineStats.NoteScore, Is.EqualTo(50));
            Assert.That(engine.EngineStats.SustainScore, Is.EqualTo(25));
            Assert.That(engine.EngineStats.MultiplierScore, Is.Zero);
            Assert.That(engine.EngineStats.PendingScore, Is.Zero);
            Assert.That(engine.EngineStats.MaxCombo, Is.EqualTo(1));
            Assert.That(engine.EngineStats.Percent, Is.EqualTo(1f));
            Assert.That(engine.EngineStats.IsFullCombo, Is.True);
        }
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
