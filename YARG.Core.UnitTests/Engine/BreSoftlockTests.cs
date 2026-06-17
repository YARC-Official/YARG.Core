using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Drums.Engines;
using YARG.Core.Game;
using YARG.Core.Input;

namespace YARG.Core.UnitTests.Engine;

// Regression tests for the Big Rock Ending soft-lock: when a BRE fill contains
// real gem notes and the player mashes out of order, the engine must not strand
// NoteIndex on an already-resolved BRE note (which previously froze the engine so
// that no further notes registered for the rest of the song).
public class BreSoftlockTests : EngineTester
{
    private const int RES = 480;

    private static DrumsEngineParameters Params() =>
        EnginePreset.Default.Drums.Create(StarMultiplierThresholds, SoloBonusStarMultiplierThresholds,
            DrumsEngineParameters.DrumMode.NonProFourLane);

    // Builds: lead-in note, a BRE phrase densely packed with gems on rotating pads,
    // a couple of finale notes after the fill, the last of which is the CodaEnd note.
    private static (YargDrumsEngine engine, InstrumentDifficulty<DrumNote> notes, List<DrumNote> finale)
        BuildGemFilledBre(int gemCount)
    {
        double TimeOf(uint tick) => tick / (double) RES * 0.5; // 120 bpm

        var notes = new List<DrumNote>();

        // lead-in
        notes.Add(new DrumNote(FourLaneDrumPad.RedDrum, DrumNoteType.Neutral, DrumNoteFlags.None,
            NoteFlags.None, TimeOf(0), 0));

        // BRE fill gems, every 120 ticks (0.125s), rotating Red/Yellow/Blue/Green
        var pads = new[] { FourLaneDrumPad.RedDrum, FourLaneDrumPad.YellowDrum, FourLaneDrumPad.BlueDrum, FourLaneDrumPad.GreenDrum };
        uint breStart = 480;
        uint t = breStart;
        const uint gemSpacing = 40; // dense fill so several gems share a hit window
        for (int i = 0; i < gemCount; i++)
        {
            notes.Add(new DrumNote(pads[i % pads.Length], DrumNoteType.Neutral, DrumNoteFlags.None,
                NoteFlags.BigRockEnding, TimeOf(t), t));
            t += gemSpacing;
        }
        uint breEnd = t + gemSpacing;

        // finale: two normal notes after the fill, last one is CodaEnd
        uint finaleA = breEnd + 240;
        uint finaleB = finaleA + 240;
        var fA = new DrumNote(FourLaneDrumPad.YellowDrum, DrumNoteType.Neutral, DrumNoteFlags.None,
            NoteFlags.None, TimeOf(finaleA), finaleA);
        var fB = new DrumNote(FourLaneDrumPad.BlueDrum, DrumNoteType.Neutral, DrumNoteFlags.None,
            NoteFlags.CodaEnd, TimeOf(finaleB), finaleB);
        notes.Add(fA);
        notes.Add(fB);

        // wire neighbour links
        for (int i = 1; i < notes.Count; i++)
        {
            notes[i].PreviousNote = notes[i - 1];
            notes[i - 1].NextNote = notes[i];
        }

        var phrases = new List<Phrase>
        {
            new(PhraseType.BigRockEnding, TimeOf(breStart), TimeOf(breEnd) - TimeOf(breStart), breStart, breEnd - breStart),
        };

        var diff = new InstrumentDifficulty<DrumNote>(Instrument.FourLaneDrums, Difficulty.Expert,
            notes, phrases, new());

        var syncTrack = new SyncTrack(RES);
        syncTrack.Tempos.Add(new TempoChange(120, 0, 0));

        var engine = new YargDrumsEngine(diff, syncTrack, Params(), isBot: false, isMidiDrumsInput: false);
        return (engine, diff, new List<DrumNote> { fA, fB });
    }

    private static DrumsAction ActionFor(int pad) => (FourLaneDrumPad) pad switch
    {
        FourLaneDrumPad.Kick => DrumsAction.Kick,
        FourLaneDrumPad.RedDrum => DrumsAction.RedDrum,
        FourLaneDrumPad.YellowDrum => DrumsAction.YellowDrum,
        FourLaneDrumPad.BlueDrum => DrumsAction.BlueDrum,
        FourLaneDrumPad.GreenDrum => DrumsAction.GreenDrum,
        _ => DrumsAction.RedDrum,
    };

    // Builds an empty-fill BRE followed by a finale that is a chord. The CodaEnd flag
    // sits on a single sub-note of that chord (the kick), mirroring real charts.
    private static (YargDrumsEngine engine, InstrumentDifficulty<DrumNote> notes, DrumNote finale)
        BuildBreWithFinalChord()
    {
        double TimeOf(uint tick) => tick / (double) RES * 0.5; // 120 bpm

        var leadIn = new DrumNote(FourLaneDrumPad.RedDrum, DrumNoteType.Neutral, DrumNoteFlags.None,
            NoteFlags.None, TimeOf(0), 0);

        // Final chord after the (empty) fill: kick carries CodaEnd, green does not.
        uint finaleTick = 1680;
        var finale = new DrumNote(FourLaneDrumPad.Kick, DrumNoteType.Neutral, DrumNoteFlags.None,
            NoteFlags.CodaEnd, TimeOf(finaleTick), finaleTick);
        finale.AddChildNote(new DrumNote(FourLaneDrumPad.GreenDrum, DrumNoteType.Neutral, DrumNoteFlags.None,
            NoteFlags.None, TimeOf(finaleTick), finaleTick));

        leadIn.NextNote = finale;
        finale.PreviousNote = leadIn;

        uint breStart = 480, breEnd = 1440;
        var phrases = new List<Phrase>
        {
            new(PhraseType.BigRockEnding, TimeOf(breStart), TimeOf(breEnd) - TimeOf(breStart), breStart, breEnd - breStart),
        };

        var diff = new InstrumentDifficulty<DrumNote>(Instrument.FourLaneDrums, Difficulty.Expert,
            new List<DrumNote> { leadIn, finale }, phrases, new());

        var syncTrack = new SyncTrack(RES);
        syncTrack.Tempos.Add(new TempoChange(120, 0, 0));

        var engine = new YargDrumsEngine(diff, syncTrack, Params(), isBot: false, isMidiDrumsInput: false);
        return (engine, diff, finale);
    }

    // Runs the engine to the end, hitting only the listed pads on the finale chord.
    private static bool? RunFinale(YargDrumsEngine engine, InstrumentDifficulty<DrumNote> notes,
        DrumNote finale, params int[] padsToHit)
    {
        bool? successAtCodaEnd = null;
        engine.OnCodaEnd += _ => successAtCodaEnd = engine.CodaSuccess; // mirrors the band handler

        var fired = false;
        double endTime = finale.Time + 2.0;
        for (double time = 0; time <= endTime; time += 0.02)
        {
            if (!fired && time >= finale.Time)
            {
                fired = true;
                foreach (var pad in padsToHit)
                {
                    var gi = GameInput.Create(time, ActionFor(pad), 1.0f);
                    engine.QueueInput(ref gi);
                }
            }

            engine.Update(time);
        }

        return successAtCodaEnd;
    }

    [Test]
    public void HittingOnlyCodaEndSubNote_OfFinalChord_DoesNotBankBonus()
    {
        var (engine, notes, finale) = BuildBreWithFinalChord();

        // Hit only the kick (the CodaEnd-flagged sub-note); leave the green unhit.
        bool? success = RunFinale(engine, notes, finale, (int) FourLaneDrumPad.Kick);

        Assert.That(success, Is.False,
            "hitting only the CodaEnd sub-note must not count as completing the final chord");
    }

    [Test]
    public void HittingFullFinalChord_BanksBonus()
    {
        var (engine, notes, finale) = BuildBreWithFinalChord();

        bool? success = RunFinale(engine, notes, finale,
            (int) FourLaneDrumPad.Kick, (int) FourLaneDrumPad.GreenDrum);

        Assert.That(success, Is.True, "hitting the entire final chord should award the coda bonus");
    }

    [Test]
    public void MashingGemFilledBre_OutOfOrder_DoesNotSoftlock()
    {
        var (engine, notes, finale) = BuildGemFilledBre(gemCount: 64);

        double endTime = notes.Notes[^1].Time + 1.0;
        var mash = new[] { DrumsAction.GreenDrum, DrumsAction.RedDrum, DrumsAction.BlueDrum, DrumsAction.YellowDrum };
        int mp = 0;
        var firedFinale = new HashSet<uint>();

        for (double time = 0; time <= endTime; time += 0.02)
        {
            // Mash continuously across the fill (and a bit past it), out of order vs the gem layout.
            if (time >= 0.5 && time <= notes.Notes[^1].Time)
            {
                var gi = GameInput.Create(time, mash[mp++ % mash.Length], 1.0f);
                engine.QueueInput(ref gi);
            }

            // Player also tries to hit the finale notes at their times.
            foreach (var f in finale)
            {
                if (!firedFinale.Contains(f.Tick) && System.Math.Abs(time - f.Time) < 0.02)
                {
                    firedFinale.Add(f.Tick);
                    var gi = GameInput.Create(time, ActionFor(f.Pad), 1.0f);
                    engine.QueueInput(ref gi);
                }
            }

            engine.Update(time);
        }

        // The defining symptom of the soft-lock: post-fill notes left in limbo
        // (neither hit nor missed) because NoteIndex never reached them.
        int limbo = finale.Count(f => !f.WasHit && !f.WasMissed);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(limbo, Is.Zero, "finale notes were stranded in limbo (engine soft-locked during the BRE)");
            Assert.That(engine.NoteIndex, Is.EqualTo(notes.Notes.Count),
                "NoteIndex did not advance through the whole chart");
        }
    }
}
