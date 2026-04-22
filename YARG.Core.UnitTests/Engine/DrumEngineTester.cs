using Melanchall.DryWetMidi.Core;
using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Drums.Engines;
using YARG.Core.Game;
using YARG.Core.Input;

namespace YARG.Core.UnitTests.Engine;

public class DrumEngineTester : EngineTester
{
    // This should probably be in some parent class of the tester, but right now there's only drums tests so it's fine

    private readonly DrumsEngineParameters _engineParams =
        EnginePreset.Default.Drums.Create(StarMultiplierThresholds, SoloBonusStarMultiplierThresholds, DrumsEngineParameters.DrumMode.ProFourLane);

    [Test]
    public void DrumSoloThatEndsInChord_ShouldWorkCorrectly()
    {
        var (engine, notes) = CreateEngine(isBot: true);

        RunEngineToEnd(engine, notes);

        Assert.That(engine.EngineStats.SoloBonuses, Is.EqualTo(3900));
    }

    [Test]
    public void DrumTrackWithKickDrumRemoved_ShouldWorkCorrectly()
    {
        var (engine, notes) = CreateEngine(isBot: true);

        notes.RemoveKickDrumNotes();
        RunEngineToEnd(engine, notes);

        Assert.That(engine.EngineStats.NotesHit, Is.EqualTo(notes.GetTotalNoteCount()));
    }

    [Test]
    public void BotRun_HitsAllNotesWithoutOverhitsAndEndsFullCombo()
    {
        var (engine, notes) = CreateEngine(isBot: true);

        RunEngineToEnd(engine, notes);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(engine.EngineStats.NotesHit, Is.EqualTo(notes.GetTotalNoteCount()));
            Assert.That(engine.EngineStats.Overhits, Is.Zero);
            Assert.That(engine.EngineStats.IsFullCombo, Is.True);
            Assert.That(engine.EngineStats.Percent, Is.EqualTo(1f));
        }
    }

    [Test]
    public void Reset_ClearsRuntimeDrumStatsButPreservesChartTotals()
    {
        var (engine, notes) = CreateEngine(isBot: true);

        RunEngineToEnd(engine, notes);

        int totalNotes = engine.EngineStats.TotalNotes;
        int totalAccents = engine.EngineStats.TotalAccents;
        int totalGhosts = engine.EngineStats.TotalGhosts;

        engine.Reset();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(engine.EngineStats.NotesHit, Is.Zero);
            Assert.That(engine.EngineStats.Combo, Is.Zero);
            Assert.That(engine.EngineStats.Overhits, Is.Zero);
            Assert.That(engine.EngineStats.DynamicsBonus, Is.Zero);
            Assert.That(engine.EngineStats.TotalNotes, Is.EqualTo(totalNotes));
            Assert.That(engine.EngineStats.TotalAccents, Is.EqualTo(totalAccents));
            Assert.That(engine.EngineStats.TotalGhosts, Is.EqualTo(totalGhosts));
        }
    }

    [Test]
    public void Overhit_DoesNothingBeforeFirstNote()
    {
        var (engine, _) = CreateEngine(isBot: false);

        engine.Overhit();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(engine.EngineStats.Overhits, Is.Zero);
            Assert.That(engine.EngineStats.Combo, Is.Zero);
        }
    }

    [Test]
    public void Overhit_DoesNothingAfterLastNote()
    {
        var (engine, notes) = CreateEngine(isBot: true);

        RunEngineToEnd(engine, notes);
        engine.Overhit();

        Assert.That(engine.EngineStats.Overhits, Is.Zero);
    }

    [Test]
    public void MatchingInput_HitsIsolatedNeutralNote_AndRaisesSuccessfulPadHitEvent()
    {
        var (engine, notes) = CreateEngine(isBot: false);
        (double frontEnd, double backEnd) = engine.CalculateHitWindow();
        var minimumGap = backEnd - frontEnd + 0.05;
        (int targetIndex, var target) = FindIsolatedNote(notes, minimumGap, 1,
            note => note is { IsChord: false, Type: DrumNoteType.Neutral });

        AdvanceToBeforeNote(engine, notes, targetIndex, frontEnd);

        int padHitCalls = 0;
        DrumsAction? reportedAction = null;
        bool? noteWasHit = null;
        bool? bonusAwarded = null;
        bool? wasOverhitInLane = null;
        DrumNoteType? reportedType = null;
        float? reportedVelocity = null;
        engine.OnPadHit += (action, wasHit, wereBonusPointsAwarded, laneOverhit, type, velocity) =>
        {
            padHitCalls++;
            reportedAction = action;
            noteWasHit = wasHit;
            bonusAwarded = wereBonusPointsAwarded;
            wasOverhitInLane = laneOverhit;
            reportedType = type;
            reportedVelocity = velocity;
        };

        var action = GetActionForPad(target.Pad);
        var input = GameInput.Create(target.Time, action, 0.9f);
        engine.QueueInput(ref input);
        engine.Update(target.Time);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(target.WasHit, Is.True);
            Assert.That(engine.EngineStats.NotesHit, Is.EqualTo(1));
            Assert.That(engine.EngineStats.Overhits, Is.Zero);
            Assert.That(padHitCalls, Is.EqualTo(1));
            Assert.That(reportedAction, Is.EqualTo(action));
            Assert.That(noteWasHit, Is.True);
            Assert.That(bonusAwarded, Is.False);
            Assert.That(wasOverhitInLane, Is.False);
            Assert.That(reportedType, Is.EqualTo(DrumNoteType.Neutral));
            Assert.That(reportedVelocity, Is.EqualTo(0.9f));
        }
    }

    [Test]
    public void MismatchedInput_OnLaterIsolatedNote_RecordsOverhitAndRaisesEvents()
    {
        var (engine, notes) = CreateEngine(isBot: false);
        (double frontEnd, double backEnd) = engine.CalculateHitWindow();
        var minimumGap = backEnd - frontEnd + 0.05;
        (int targetIndex, var target) = FindIsolatedNote(notes, minimumGap, 1,
            note => note is { IsChord: false, Type: DrumNoteType.Neutral });

        AdvanceToBeforeNote(engine, notes, targetIndex, frontEnd);

        int overhitCalls = 0;
        int padHitCalls = 0;
        DrumsAction? reportedAction = null;
        bool? noteWasHit = null;
        bool? bonusAwarded = null;
        bool? wasOverhitInLane = null;
        DrumNoteType? reportedType = null;
        float? reportedVelocity = null;
        engine.OnOverhit += () => overhitCalls++;
        engine.OnPadHit += (action, wasHit, wereBonusPointsAwarded, laneOverhit, type, velocity) =>
        {
            padHitCalls++;
            reportedAction = action;
            noteWasHit = wasHit;
            bonusAwarded = wereBonusPointsAwarded;
            wasOverhitInLane = laneOverhit;
            reportedType = type;
            reportedVelocity = velocity;
        };

        var wrongAction = GetMismatchedAction(target);
        var input = GameInput.Create(target.Time, wrongAction, 0.8f);
        engine.QueueInput(ref input);
        engine.Update(target.Time);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(target.WasHit, Is.False);
            Assert.That(engine.EngineStats.Overhits, Is.EqualTo(1));
            Assert.That(engine.EngineStats.OverhitsByAction[(int) wrongAction], Is.EqualTo(1));
            Assert.That(overhitCalls, Is.EqualTo(1));
            Assert.That(padHitCalls, Is.EqualTo(1));
            Assert.That(reportedAction, Is.EqualTo(wrongAction));
            Assert.That(noteWasHit, Is.False);
            Assert.That(bonusAwarded, Is.False);
            Assert.That(wasOverhitInLane, Is.False);
            Assert.That(reportedType, Is.EqualTo(DrumNoteType.Neutral));
            Assert.That(reportedVelocity, Is.EqualTo(0.8f));
        }
    }

    private (YargDrumsEngine Engine, InstrumentDifficulty<DrumNote> Notes) CreateEngine(bool isBot)
    {
        var chartPath = Path.Combine(ChartDirectory!, "drawntotheflame.mid");
        var midi = MidiFile.Read(chartPath);
        var chart = SongChart.FromMidi(in ParseSettings.Default_Midi, midi);
        var notes = chart.ProDrums.GetDifficulty(Difficulty.Expert);
        var engine = new YargDrumsEngine(notes, chart.SyncTrack, _engineParams, isBot, false);
        return (engine, notes);
    }

    private static void RunEngineToEnd(YargDrumsEngine engine, InstrumentDifficulty<DrumNote> notes)
    {
        var endTime = notes.GetEndTime();
        const double TIME_STEP = 0.01;
        for (double i = 0; i < endTime; i += TIME_STEP)
        {
            engine.Update(i);
        }
    }

    private static void AdvanceToBeforeNote(YargDrumsEngine engine, InstrumentDifficulty<DrumNote> notes,
        int targetIndex, double frontEnd)
    {
        if (targetIndex <= 0)
        {
            return;
        }

        double updateTime = Math.Max(0, notes.Notes[targetIndex].Time + frontEnd - 0.01);
        engine.Update(updateTime);
    }

    private static (int Index, DrumNote Note) FindIsolatedNote(InstrumentDifficulty<DrumNote> notes,
        double minimumGap, int startIndex, Predicate<DrumNote> predicate)
    {
        for (int i = startIndex; i < notes.Notes.Count; i++)
        {
            var note = notes.Notes[i];
            if (!predicate(note))
            {
                continue;
            }

            double previousGap = note.Time - notes.Notes[i - 1].Time;
            double nextGap = i == notes.Notes.Count - 1 ? double.MaxValue : notes.Notes[i + 1].Time - note.Time;

            if (previousGap > minimumGap && nextGap > minimumGap)
            {
                return (i, note);
            }
        }

        Assert.Fail("Could not find an isolated drum note in the test chart.");
        return default;
    }

    private static DrumsAction GetActionForPad(int pad)
    {
        return (FourLaneDrumPad) pad switch
        {
            FourLaneDrumPad.Kick => DrumsAction.Kick,
            FourLaneDrumPad.RedDrum => DrumsAction.RedDrum,
            FourLaneDrumPad.YellowDrum => DrumsAction.YellowDrum,
            FourLaneDrumPad.BlueDrum => DrumsAction.BlueDrum,
            FourLaneDrumPad.GreenDrum => DrumsAction.GreenDrum,
            FourLaneDrumPad.YellowCymbal => DrumsAction.YellowCymbal,
            FourLaneDrumPad.BlueCymbal => DrumsAction.BlueCymbal,
            FourLaneDrumPad.GreenCymbal => DrumsAction.GreenCymbal,
            FourLaneDrumPad.Wildcard => DrumsAction.WildcardPad,
            _ => throw new ArgumentOutOfRangeException(nameof(pad), pad, null)
        };
    }

    private static DrumsAction GetMismatchedAction(DrumNote target)
    {
        var usedActions = new HashSet<DrumsAction>();
        foreach (var note in target.AllNotes)
        {
            usedActions.Add(GetActionForPad(note.Pad));
        }

        DrumsAction[] candidateActions =
        {
            DrumsAction.Kick,
            DrumsAction.RedDrum,
            DrumsAction.YellowDrum,
            DrumsAction.BlueDrum,
            DrumsAction.GreenDrum,
            DrumsAction.YellowCymbal,
            DrumsAction.BlueCymbal,
            DrumsAction.GreenCymbal,
        };

        foreach (var action in candidateActions)
        {
            if (!usedActions.Contains(action))
            {
                return action;
            }
        }

        Assert.Fail("Could not find a mismatched drum action for the target note.");
        return default;
    }
}
