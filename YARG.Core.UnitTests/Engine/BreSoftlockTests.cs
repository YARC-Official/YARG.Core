using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Drums.Engines;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Guitar.Engines;
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

    // ----- Guitar / bass BRE soft-lock -----
    //
    // GuitarEngine.HitNote has the same SkipPreviousNotes bypass in its BRE branch as
    // the drums engine did. Reproducing the strand headlessly needs the exact live
    // conditions, which a naive synthetic mash misses:
    //
    //   * Combo == 0. The guitar skip-ahead path (CheckForNoteHit strum branch) only
    //     fires for a note *ahead* of NoteIndex when Combo == 0. BRE gems don't build
    //     combo, so combo has to already be 0 entering the fill — here we leave the
    //     lead-in note unhit so it is missed and combo stays 0.
    //   * The gem at NoteIndex must still be in its hit window (not yet auto-resolved)
    //     when the out-of-order strum lands, otherwise it resolves in order and there
    //     is no skip. A dense fill plus a fixed window guarantees the overlap.
    //   * The held fret must match a gem *ahead* of NoteIndex but not the gem at it,
    //     so the loop skips the current gem (CanNoteBeHit == false) and strum-hits the
    //     next one. Alternating Green/Red gems give exactly that.
    //
    // Without the fix the out-of-order hit leaves the skipped gem unresolved while
    // NoteIndex advances onto the just-hit gem; the next CheckForNoteHit sees an
    // already-resolved note at NoteIndex and breaks forever — the soft-lock.

    private const int GUITAR_RES = 480;

    private static GuitarEngineParameters GuitarParams()
    {
        // Fixed (non-dynamic), generous window so several densely-packed gems share a
        // hit window and timing is deterministic. The bug is about note-resolution
        // order, not window sizing, so a controlled window only removes a confound.
        var hitWindow = new HitWindowSettings(0.2, 0.2, 1.0, false, 0, 1.0, 1.0, 0.15, 0.25);
        return new GuitarEngineParameters(hitWindow, 4, 0, 0,
            StarMultiplierThresholds, SoloBonusStarMultiplierThresholds,
            hopoLeniency: 0.08, strumLeniency: 0.05, strumLeniencySmall: 0.025,
            infiniteFrontEnd: false, antiGhosting: true, soloTaps: false,
            noStarPowerOverlap: false, enableLanes: true);
    }

    private static GuitarNote GuitarGem(FiveFretGuitarFret fret, NoteFlags flags, double time, uint tick) =>
        new(fret, GuitarNoteType.Strum, GuitarNoteFlags.None, flags, time, 0, tick, 0);

    // Builds: an unhit lead-in note, a BRE fill of gems alternating Green/Red, then a
    // short finale ending on a CodaEnd note. Returns the time of the out-of-order
    // strum (the second gem's time) the test should fire.
    private static (YargFiveFretGuitarEngine engine, InstrumentDifficulty<GuitarNote> notes,
        List<GuitarNote> finale, double strumTime) BuildGuitarGemFilledBre()
    {
        double TimeOf(uint tick) => tick / (double) GUITAR_RES * 0.5; // 120 bpm

        var notes = new List<GuitarNote>();

        // Lead-in note (NOT part of the BRE). It is never hit, so it is missed and
        // combo stays 0 — the precondition for the guitar skip-ahead path.
        notes.Add(GuitarGem(FiveFretGuitarFret.Green, NoteFlags.None, TimeOf(0), 0));

        // BRE fill: gems alternating Green/Red, densely packed.
        var frets = new[] { FiveFretGuitarFret.Green, FiveFretGuitarFret.Red };
        uint breStart = 480;
        uint t = breStart;
        const uint gemSpacing = 40;  // ~0.042s apart, well inside the 0.1s back end
        const int gemCount = 12;
        for (int i = 0; i < gemCount; i++)
        {
            notes.Add(GuitarGem(frets[i % frets.Length], NoteFlags.BigRockEnding, TimeOf(t), t));
            t += gemSpacing;
        }
        uint breEnd = t + gemSpacing;

        // Finale after the fill: two normal notes, the last carrying CodaEnd.
        uint finaleA = breEnd + 240;
        uint finaleB = finaleA + 240;
        var fA = GuitarGem(FiveFretGuitarFret.Yellow, NoteFlags.None, TimeOf(finaleA), finaleA);
        var fB = GuitarGem(FiveFretGuitarFret.Blue, NoteFlags.CodaEnd, TimeOf(finaleB), finaleB);
        notes.Add(fA);
        notes.Add(fB);

        for (int i = 1; i < notes.Count; i++)
        {
            notes[i].PreviousNote = notes[i - 1];
            notes[i - 1].NextNote = notes[i];
        }

        var phrases = new List<Phrase>
        {
            new(PhraseType.BigRockEnding, TimeOf(breStart), TimeOf(breEnd) - TimeOf(breStart), breStart, breEnd - breStart),
        };

        var diff = new InstrumentDifficulty<GuitarNote>(Instrument.FiveFretGuitar, Difficulty.Expert,
            notes, phrases, new());

        var syncTrack = new SyncTrack(GUITAR_RES);
        syncTrack.Tempos.Add(new TempoChange(120, 0, 0));

        var engine = new YargFiveFretGuitarEngine(diff, syncTrack, GuitarParams(), isBot: false);

        // The first BRE gem (chart index 1) is Green; the second (index 2) is Red.
        // Holding Red and strumming at the second gem's time skips the first, unresolved
        // gem and hits the one ahead of NoteIndex.
        double strumTime = TimeOf(breStart + gemSpacing);
        return (engine, diff, new List<GuitarNote> { fA, fB }, strumTime);
    }

    [Test]
    public void GuitarBre_OutOfOrderStrumAheadOfIndex_DoesNotSoftlock()
    {
        var (engine, notes, finale, strumTime) = BuildGuitarGemFilledBre();

        bool strummed = false;
        double endTime = notes.Notes[^1].Time + 1.0;
        for (double time = 0; time <= endTime; time += 0.01)
        {
            if (!strummed && time >= strumTime)
            {
                strummed = true;

                // Hold Red (matches the 2nd gem, not the 1st) then strum: an
                // out-of-order hit on a BRE gem ahead of NoteIndex while combo == 0.
                // Note: guitar fret/strum inputs are *button* inputs — they must be
                // created with a bool, not a float (a float sets Axis and leaves
                // Button false, so the input would be a no-op).
                var fret = GameInput.Create(strumTime, GuitarAction.RedFret, true);
                engine.QueueInput(ref fret);
                var strum = GameInput.Create(strumTime, GuitarAction.StrumDown, true);
                engine.QueueInput(ref strum);
            }

            engine.Update(time);
        }

        // The defining symptom of the soft-lock: post-fill notes left in limbo
        // (neither hit nor missed) because NoteIndex stranded on an already-hit gem.
        int limbo = finale.Count(f => !f.WasHit && !f.WasMissed);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(limbo, Is.Zero,
                "finale notes were stranded in limbo (guitar engine soft-locked during the BRE)");
            Assert.That(engine.NoteIndex, Is.EqualTo(notes.Notes.Count),
                "NoteIndex did not advance through the whole chart");
        }
    }
}
