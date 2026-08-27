using System.Collections.Generic;
using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Guitar.Engines;
using YARG.Core.Input;

namespace YARG.Core.UnitTests.Engine;

public class SixFretGuitarEngineTests : EngineTester
{
    // GuitarAction has no Open action; open is bit 6 (OPEN_MASK in the engine).
    private const int OpenMask = 1 << 6;

    #region IsAnchoringValid tests (direct)

    [Test]
    public void Anchoring_W2_Anchors_W3_SingleNote()
    {
        // Player holds W2 + W3, note is W3
        // W2 is a lower fret number → valid anchor
        var engine = CreateSixFretEngine();
        Assert.That(engine.IsAnchoringValidFor(Mask(GuitarAction.White2Fret), Mask(GuitarAction.White3Fret)), Is.True);
    }

    [Test]
    public void Anchoring_B2_Anchors_W3_SingleNote()
    {
        // Player holds B2 + W3, note is W3
        // B2 is a lower fret number → valid anchor
        var engine = CreateSixFretEngine();
        Assert.That(engine.IsAnchoringValidFor(Mask(GuitarAction.Black2Fret), Mask(GuitarAction.White3Fret)), Is.True);
    }

    [Test]
    public void Anchoring_B3_Cannot_Anchor_W3_SameFret()
    {
        // Player holds B3 as anchor for W3 — same fret number, not interchangeable
        var engine = CreateSixFretEngine();
        Assert.That(engine.IsAnchoringValidFor(Mask(GuitarAction.Black3Fret), Mask(GuitarAction.White3Fret)), Is.False);
    }

    [Test]
    public void Anchoring_B1_Anchors_W3()
    {
        // Player holds B1 + W3, note is W3
        // B1 is a lower fret number → valid anchor
        var engine = CreateSixFretEngine();
        Assert.That(engine.IsAnchoringValidFor(Mask(GuitarAction.Black1Fret), Mask(GuitarAction.White3Fret)), Is.True);
    }

    [Test]
    public void Anchoring_W3_Cannot_Anchor_W2()
    {
        // Player holds W3 + W2, note is W2
        // W3 is a higher fret number → invalid anchor
        var engine = CreateSixFretEngine();
        Assert.That(engine.IsAnchoringValidFor(Mask(GuitarAction.White3Fret), Mask(GuitarAction.White2Fret)), Is.False);
    }

    [Test]
    public void Anchoring_W1_NotHeld_For_B1()
    {
        // No interchangeability: holding W1 for a B1 note → B1 not held → invalid
        // anchorButtons = heldMask ^ noteMask, which contains W1 and B1
        var engine = CreateSixFretEngine();
        Assert.That(engine.IsAnchoringValidFor(Mask(GuitarAction.White1Fret) ^ Mask(GuitarAction.Black1Fret), Mask(GuitarAction.Black1Fret)), Is.False);
    }

    [Test]
    public void Anchoring_B1_NotHeld_For_W1()
    {
        // No interchangeability: holding B1 for a W1 note → W1 not held → invalid
        var engine = CreateSixFretEngine();
        Assert.That(engine.IsAnchoringValidFor(Mask(GuitarAction.Black1Fret) ^ Mask(GuitarAction.White1Fret), Mask(GuitarAction.White1Fret)), Is.False);
    }

    [Test]
    public void Anchoring_W2_NotHeld_For_B2()
    {
        // No interchangeability: holding W2 for a B2 note → B2 not held → invalid
        var engine = CreateSixFretEngine();
        Assert.That(engine.IsAnchoringValidFor(Mask(GuitarAction.White2Fret) ^ Mask(GuitarAction.Black2Fret), Mask(GuitarAction.Black2Fret)), Is.False);
    }

    [Test]
    public void Anchoring_W3_NotHeld_For_B3()
    {
        // No interchangeability: holding W3 for a B3 note → B3 not held → invalid
        var engine = CreateSixFretEngine();
        Assert.That(engine.IsAnchoringValidFor(Mask(GuitarAction.White3Fret) ^ Mask(GuitarAction.Black3Fret), Mask(GuitarAction.Black3Fret)), Is.False);
    }

    [Test]
    public void Anchoring_B3_NotHeld_For_W3()
    {
        // No interchangeability: holding B3 for a W3 note → W3 not held → invalid
        var engine = CreateSixFretEngine();
        Assert.That(engine.IsAnchoringValidFor(Mask(GuitarAction.Black3Fret) ^ Mask(GuitarAction.White3Fret), Mask(GuitarAction.White3Fret)), Is.False);
    }

    [Test]
    public void Anchoring_NoAnchors()
    {
        // Only the target fret held (exact match) — anchorButtons = 0 → valid
        var engine = CreateSixFretEngine();
        Assert.That(engine.IsAnchoringValidFor(0, Mask(GuitarAction.White3Fret)), Is.True);
    }

    [Test]
    public void Anchoring_NeitherTargetHeld()
    {
        // Note is B1, player holds B2 only — B1 not held, no equivalent
        var engine = CreateSixFretEngine();
        Assert.That(engine.IsAnchoringValidFor(Mask(GuitarAction.Black1Fret) ^ Mask(GuitarAction.Black2Fret), Mask(GuitarAction.Black1Fret)), Is.False);
    }

    #endregion

    #region CanNoteBeHit tests (end-to-end hit detection)

    [Test]
    public void CannotHit_B1_By_Holding_W1()
    {
        // No interchangeability: holding W1 for a B1 note → not hittable
        var engine = CreateSixFretEngine();
        var note = CreateSixFretNote(SixFretGuitarFret.Black1, GuitarNoteType.Strum);
        engine.SetButtonMask(Mask(GuitarAction.White1Fret));
        Assert.That(engine.CanBeHit(note), Is.False);
    }

    [Test]
    public void CannotHit_W1_By_Holding_B1()
    {
        var engine = CreateSixFretEngine();
        var note = CreateSixFretNote(SixFretGuitarFret.White1, GuitarNoteType.Strum);
        engine.SetButtonMask(Mask(GuitarAction.Black1Fret));
        Assert.That(engine.CanBeHit(note), Is.False);
    }

    [Test]
    public void CannotHit_B2_By_Holding_W2()
    {
        var engine = CreateSixFretEngine();
        var note = CreateSixFretNote(SixFretGuitarFret.Black2, GuitarNoteType.Strum);
        engine.SetButtonMask(Mask(GuitarAction.White2Fret));
        Assert.That(engine.CanBeHit(note), Is.False);
    }

    [Test]
    public void CannotHit_W2_By_Holding_B2()
    {
        var engine = CreateSixFretEngine();
        var note = CreateSixFretNote(SixFretGuitarFret.White2, GuitarNoteType.Strum);
        engine.SetButtonMask(Mask(GuitarAction.Black2Fret));
        Assert.That(engine.CanBeHit(note), Is.False);
    }

    [Test]
    public void CannotHit_B3_By_Holding_W3()
    {
        var engine = CreateSixFretEngine();
        var note = CreateSixFretNote(SixFretGuitarFret.Black3, GuitarNoteType.Strum);
        engine.SetButtonMask(Mask(GuitarAction.White3Fret));
        Assert.That(engine.CanBeHit(note), Is.False);
    }

    [Test]
    public void CannotHit_W3_By_Holding_B3()
    {
        var engine = CreateSixFretEngine();
        var note = CreateSixFretNote(SixFretGuitarFret.White3, GuitarNoteType.Strum);
        engine.SetButtonMask(Mask(GuitarAction.Black3Fret));
        Assert.That(engine.CanBeHit(note), Is.False);
    }

    [Test]
    public void CannotHit_OpenNote_With_FretHeld()
    {
        // Open note (Fret = 7, NoteMask = OPEN_MASK) should not be hittable
        // while holding an extra fret — matches base 5-fret strictness.
        var engine = CreateSixFretEngine();
        var note = CreateSixFretNote(SixFretGuitarFret.Open, GuitarNoteType.Strum);
        engine.SetButtonMask(OpenMask | Mask(GuitarAction.Black1Fret));
        Assert.That(engine.CanBeHit(note), Is.False);
    }

    [Test]
    public void CanHit_W3_With_W2_Anchor()
    {
        var engine = CreateSixFretEngine();
        var note = CreateSixFretNote(SixFretGuitarFret.White3, GuitarNoteType.Strum);
        engine.SetButtonMask(Mask(GuitarAction.White2Fret, GuitarAction.White3Fret)); // W2 is lower anchor
        Assert.That(engine.CanBeHit(note), Is.True);
    }

    [Test]
    public void CannotHit_W3_With_W3_And_B3_Extra()
    {
        // Same fret number (3), different row — B3 cannot anchor W3, not interchangeable
        var engine = CreateSixFretEngine();
        var note = CreateSixFretNote(SixFretGuitarFret.White3, GuitarNoteType.Strum);
        engine.SetButtonMask(Mask(GuitarAction.White3Fret, GuitarAction.Black3Fret));
        Assert.That(engine.CanBeHit(note), Is.False);
    }

    [Test]
    public void CanHit_W3_ExactMatch()
    {
        var engine = CreateSixFretEngine();
        var note = CreateSixFretNote(SixFretGuitarFret.White3, GuitarNoteType.Strum);
        engine.SetButtonMask(Mask(GuitarAction.White3Fret)); // exact match
        Assert.That(engine.CanBeHit(note), Is.True);
    }

    [Test]
    public void CannotHit_W2_With_Higher_W3_Anchor()
    {
        var engine = CreateSixFretEngine();
        var note = CreateSixFretNote(SixFretGuitarFret.White2, GuitarNoteType.Strum);
        engine.SetButtonMask(Mask(GuitarAction.White2Fret, GuitarAction.White3Fret)); // W3 is higher → invalid
        Assert.That(engine.CanBeHit(note), Is.False);
    }

    [Test]
    public void CanHit_B2_With_B1_Anchor()
    {
        // B1 (fret 1) can anchor B2 (fret 2)
        var engine = CreateSixFretEngine();
        var note = CreateSixFretNote(SixFretGuitarFret.Black2, GuitarNoteType.Strum);
        engine.SetButtonMask(Mask(GuitarAction.Black1Fret, GuitarAction.Black2Fret));
        Assert.That(engine.CanBeHit(note), Is.True);
    }

    [Test]
    public void CannotHit_B2_With_W2_Extra()
    {
        // Same fret number (2), different row — W2 cannot anchor B2, not interchangeable
        var engine = CreateSixFretEngine();
        var note = CreateSixFretNote(SixFretGuitarFret.Black2, GuitarNoteType.Strum);
        engine.SetButtonMask(Mask(GuitarAction.Black2Fret, GuitarAction.White2Fret));
        Assert.That(engine.CanBeHit(note), Is.False);
    }

    // --- Chord tests ---

    [Test]
    public void Chord_ExactMatch_W2_W3()
    {
        var engine = CreateSixFretEngine();
        var note = CreateSixFretChordNote(
            new[] { SixFretGuitarFret.White2, SixFretGuitarFret.White3 },
            GuitarNoteType.Strum);
        engine.SetButtonMask(Mask(GuitarAction.White2Fret, GuitarAction.White3Fret));
        Assert.That(engine.CanBeHit(note), Is.True);
    }

    [Test]
    public void Chord_InvalidAnchoring()
    {
        var engine = CreateSixFretEngine();
        var note = CreateSixFretChordNote(
            new[] { SixFretGuitarFret.White1, SixFretGuitarFret.Black3 },
            GuitarNoteType.Tap);
        engine.SetButtonMask(Mask(GuitarAction.White1Fret, GuitarAction.Black3Fret, GuitarAction.Black2Fret)); // B2 is an invalid anchor
        Assert.That(engine.CanBeHit(note), Is.False);
    }

    [Test]
    public void Chord_NoInterchangeability_B2_Held_For_W2()
    {
        var engine = CreateSixFretEngine();
        var note = CreateSixFretChordNote(
            new[] { SixFretGuitarFret.White2, SixFretGuitarFret.White3 },
            GuitarNoteType.Strum);
        // Hold B2 + W3 instead of W2 + W3 — chords require exact buttons
        engine.SetButtonMask(Mask(GuitarAction.Black2Fret, GuitarAction.White3Fret));
        Assert.That(engine.CanBeHit(note), Is.False);
    }

    [Test]
    public void Chord_NoInterchangeability_W1_Held_For_B1()
    {
        var engine = CreateSixFretEngine();
        var note = CreateSixFretChordNote(
            new[] { SixFretGuitarFret.Black1, SixFretGuitarFret.White2 },
            GuitarNoteType.Strum);
        // Hold W1 + W2 instead of B1 + W2 — chords require exact buttons
        engine.SetButtonMask(Mask(GuitarAction.White1Fret, GuitarAction.White2Fret));
        Assert.That(engine.CanBeHit(note), Is.False);
    }

    [Test]
    public void Chord_WithLowerAnchor_B1_Anchors_B2_W3()
    {
        // HOPO chords allow anchoring (B1 fret 1 anchors chord B2+W3)
        var engine = CreateSixFretEngine();
        var note = CreateSixFretChordNote(
            new[] { SixFretGuitarFret.Black2, SixFretGuitarFret.White3 },
            GuitarNoteType.Hopo);
        engine.SetButtonMask(Mask(GuitarAction.Black1Fret, GuitarAction.Black2Fret, GuitarAction.White3Fret));
        Assert.That(engine.CanBeHit(note), Is.True);
    }

    [Test]
    public void Chord_B3_Cannot_Anchor_W2_W3_Chord()
    {
        // B3 (fret 3) as anchor for chord W2+W3 (lowest fret 2) should fail
        var engine = CreateSixFretEngine();
        var note = CreateSixFretChordNote(
            new[] { SixFretGuitarFret.White2, SixFretGuitarFret.White3 },
            GuitarNoteType.Strum);
        engine.SetButtonMask(Mask(GuitarAction.Black3Fret, GuitarAction.White2Fret, GuitarAction.White3Fret));
        Assert.That(engine.CanBeHit(note), Is.False);
    }

    [Test]
    public void Chord_Hopo_WithLowerAnchor()
    {
        // HOPO chord with lower anchor should work
        var engine = CreateSixFretEngine();
        var note = CreateSixFretChordNote(
            new[] { SixFretGuitarFret.Black2, SixFretGuitarFret.White3 },
            GuitarNoteType.Hopo);
        engine.SetButtonMask(Mask(GuitarAction.Black1Fret, GuitarAction.Black2Fret, GuitarAction.White3Fret));
        Assert.That(engine.CanBeHit(note), Is.True);
    }

    #endregion

    #region CheckForGhostInput tests (HOPO rules)

    [Test]
    public void Ghost_HammerOn_B1_To_W2_CorrectFret()
    {
        var notes = new[]
        {
            CreateSixFretNote(SixFretGuitarFret.Black1, GuitarNoteType.Strum),
            CreateSixFretNote(SixFretGuitarFret.White2, GuitarNoteType.Hopo),
        };
        LinkNotes(notes);
        var engine = CreateSixFretEngine(notes);

        // Previous: held B1. Now: B1 released, W2 pressed
        engine.SetButtonState(effectiveMask: Mask(GuitarAction.White2Fret), lastMask: Mask(GuitarAction.Black1Fret), isFretPress: true);
        engine.ClockToNote(1);

        Assert.That(engine.IsGhostInput(notes[1]), Is.False);
    }

    [Test]
    public void Ghost_HammerOn_B1_To_W3_CorrectFret()
    {
        var notes = new[]
        {
            CreateSixFretNote(SixFretGuitarFret.Black1, GuitarNoteType.Strum),
            CreateSixFretNote(SixFretGuitarFret.White3, GuitarNoteType.Hopo),
        };
        LinkNotes(notes);
        var engine = CreateSixFretEngine(notes);

        // Previous: held B1. Now: B1 released, W3 pressed
        engine.SetButtonState(effectiveMask: Mask(GuitarAction.White3Fret), lastMask: Mask(GuitarAction.Black1Fret), isFretPress: true);
        engine.ClockToNote(1);

        Assert.That(engine.IsGhostInput(notes[1]), Is.False);
    }

    [Test]
    public void Ghost_HammerOn_B1_To_W2_WrongFret_B3()
    {
        var notes = new[]
        {
            CreateSixFretNote(SixFretGuitarFret.Black1, GuitarNoteType.Strum),
            CreateSixFretNote(SixFretGuitarFret.White2, GuitarNoteType.Hopo),
        };
        LinkNotes(notes);
        var engine = CreateSixFretEngine(notes);

        // Previous: held B1 (fret 1). Now: B3 pressed (fret 3) — wrong fret
        // currentFretNumber = 3, previousFretNumber = 1 → hammer-on
        // note is W2, and W2 is not held → ghost
        engine.SetButtonState(effectiveMask: Mask(GuitarAction.Black3Fret), lastMask: Mask(GuitarAction.Black1Fret), isFretPress: true);
        engine.ClockToNote(1);

        Assert.That(engine.IsGhostInput(notes[1]), Is.True);
    }

    [Test]
    public void Ghost_HammerOn_B1_To_B2_WrongFret_W2()
    {
        // No interchangeability: holding W2 instead of B2 → ghost
        // W2 has the right fret number (2) but wrong button
        var notes = new[]
        {
            CreateSixFretNote(SixFretGuitarFret.Black1, GuitarNoteType.Strum),
            CreateSixFretNote(SixFretGuitarFret.Black2, GuitarNoteType.Hopo),
        };
        LinkNotes(notes);
        var engine = CreateSixFretEngine(notes);

        // Previous: held B1 (fret 1). Now: W2 pressed (fret 2)
        // currentFretNumber = 2, previousFretNumber = 1 → hammer-on
        // note is B2, and B2 is not held → ghost
        engine.SetButtonState(effectiveMask: Mask(GuitarAction.White2Fret), lastMask: Mask(GuitarAction.Black1Fret), isFretPress: true);
        engine.ClockToNote(1);

        Assert.That(engine.IsGhostInput(notes[1]), Is.True);
    }

    [Test]
    public void Ghost_Vertical_B1_To_W1_WithoutRelease()
    {
        var notes = new[]
        {
            CreateSixFretNote(SixFretGuitarFret.Black1, GuitarNoteType.Strum),
            CreateSixFretNote(SixFretGuitarFret.White1, GuitarNoteType.Hopo),
        };
        LinkNotes(notes);
        var engine = CreateSixFretEngine(notes);

        // Previous: held B1. Now: held B1 + W1, B1 not released
        engine.SetButtonState(effectiveMask: Mask(GuitarAction.Black1Fret, GuitarAction.White1Fret), lastMask: Mask(GuitarAction.Black1Fret), isFretPress: true);
        engine.ClockToNote(1);

        Assert.That(engine.IsGhostInput(notes[1]), Is.True);
    }

    [Test]
    public void Ghost_Vertical_B1_To_W1_WithRelease()
    {
        var notes = new[]
        {
            CreateSixFretNote(SixFretGuitarFret.Black1, GuitarNoteType.Strum),
            CreateSixFretNote(SixFretGuitarFret.White1, GuitarNoteType.Hopo),
        };
        LinkNotes(notes);
        var engine = CreateSixFretEngine(notes);

        // Previous: held B1. Now: B1 released, only W1 held
        engine.SetButtonState(effectiveMask: Mask(GuitarAction.White1Fret), lastMask: Mask(GuitarAction.Black1Fret), isFretPress: true);
        engine.ClockToNote(1);

        Assert.That(engine.IsGhostInput(notes[1]), Is.False);
    }

    [Test]
    public void Ghost_Vertical_B2_To_W2_WithoutRelease()
    {
        var notes = new[]
        {
            CreateSixFretNote(SixFretGuitarFret.Black2, GuitarNoteType.Strum),
            CreateSixFretNote(SixFretGuitarFret.White2, GuitarNoteType.Hopo),
        };
        LinkNotes(notes);
        var engine = CreateSixFretEngine(notes);

        engine.SetButtonState(effectiveMask: Mask(GuitarAction.Black2Fret, GuitarAction.White2Fret), lastMask: Mask(GuitarAction.Black2Fret), isFretPress: true);
        engine.ClockToNote(1);

        Assert.That(engine.IsGhostInput(notes[1]), Is.True);
    }

    [Test]
    public void Ghost_Vertical_W2_To_B2_WithoutRelease()
    {
        var notes = new[]
        {
            CreateSixFretNote(SixFretGuitarFret.White2, GuitarNoteType.Strum),
            CreateSixFretNote(SixFretGuitarFret.Black2, GuitarNoteType.Hopo),
        };
        LinkNotes(notes);
        var engine = CreateSixFretEngine(notes);

        engine.SetButtonState(effectiveMask: Mask(GuitarAction.Black2Fret, GuitarAction.White2Fret), lastMask: Mask(GuitarAction.White2Fret), isFretPress: true);
        engine.ClockToNote(1);

        Assert.That(engine.IsGhostInput(notes[1]), Is.True);
    }

    [Test]
    public void Ghost_Vertical_W3_To_B3_WithoutRelease()
    {
        var notes = new[]
        {
            CreateSixFretNote(SixFretGuitarFret.White3, GuitarNoteType.Strum),
            CreateSixFretNote(SixFretGuitarFret.Black3, GuitarNoteType.Hopo),
        };
        LinkNotes(notes);
        var engine = CreateSixFretEngine(notes);

        engine.SetButtonState(effectiveMask: Mask(GuitarAction.Black3Fret, GuitarAction.White3Fret), lastMask: Mask(GuitarAction.White3Fret), isFretPress: true);
        engine.ClockToNote(1);

        Assert.That(engine.IsGhostInput(notes[1]), Is.True);
    }

    [Test]
    public void Ghost_Vertical_W3_To_B3_WithRelease()
    {
        var notes = new[]
        {
            CreateSixFretNote(SixFretGuitarFret.White3, GuitarNoteType.Strum),
            CreateSixFretNote(SixFretGuitarFret.Black3, GuitarNoteType.Hopo),
        };
        LinkNotes(notes);
        var engine = CreateSixFretEngine(notes);

        // W3 released, B3 pressed → not a ghost
        engine.SetButtonState(effectiveMask: Mask(GuitarAction.Black3Fret), lastMask: Mask(GuitarAction.White3Fret), isFretPress: true);
        engine.ClockToNote(1);

        Assert.That(engine.IsGhostInput(notes[1]), Is.False);
    }

    [Test]
    public void Ghost_HammerOn_B2_To_W3_CorrectFret()
    {
        var notes = new[]
        {
            CreateSixFretNote(SixFretGuitarFret.Black2, GuitarNoteType.Strum),
            CreateSixFretNote(SixFretGuitarFret.White3, GuitarNoteType.Hopo),
        };
        LinkNotes(notes);
        var engine = CreateSixFretEngine(notes);

        // Previous: held B2. Now: B2 released, W3 pressed
        // currentFretNumber = 3, previousFretNumber = 2 → hammer-on
        // note is W3, and W3 is held → not a ghost
        engine.SetButtonState(effectiveMask: Mask(GuitarAction.White3Fret), lastMask: Mask(GuitarAction.Black2Fret), isFretPress: true);
        engine.ClockToNote(1);

        Assert.That(engine.IsGhostInput(notes[1]), Is.False);
    }

    [Test]
    public void Ghost_HammerOn_W1_To_B3_WithAnchor()
    {
        // B3 (fret 3) has a higher fret number than W1 (fret 1),
        // but a LOWER bit position. GetMostSignificantBit would find W1 (fret 1)
        // and incorrectly think this is a vertical transition (fret 1 → fret 1).
        // The fix iterates all bits to find the highest FRET NUMBER (3, from B3),
        // correctly identifying this as a hammer-on (fret 1 → fret 3).
        var notes = new[]
        {
            CreateSixFretNote(SixFretGuitarFret.White1, GuitarNoteType.Strum),
            CreateSixFretNote(SixFretGuitarFret.Black3, GuitarNoteType.Hopo),
        };
        LinkNotes(notes);
        var engine = CreateSixFretEngine(notes);

        // Previous: held W1. Now: held W1 + B3 (W1 as anchor)
        engine.SetButtonState(effectiveMask: Mask(GuitarAction.Black3Fret, GuitarAction.White1Fret), lastMask: Mask(GuitarAction.White1Fret), isFretPress: true);
        engine.ClockToNote(1);

        Assert.That(engine.IsGhostInput(notes[1]), Is.False);
    }

    [Test]
    public void Ghost_PullOff_W3_To_W2_NotGhost()
    {
        var notes = new[]
        {
            CreateSixFretNote(SixFretGuitarFret.White3, GuitarNoteType.Strum),
            CreateSixFretNote(SixFretGuitarFret.White2, GuitarNoteType.Hopo),
        };
        LinkNotes(notes);
        var engine = CreateSixFretEngine(notes);

        // Previous: held W3. Now: W3 released, W2 pressed
        // currentFretNumber = 2, previousFretNumber = 3 → pull-off (not hammer-on)
        // Vertical check not triggered (different fret numbers)
        // No ghost
        engine.SetButtonState(effectiveMask: Mask(GuitarAction.White2Fret), lastMask: Mask(GuitarAction.White3Fret), isFretPress: true);
        engine.ClockToNote(1);

        Assert.That(engine.IsGhostInput(notes[1]), Is.False);
    }

    #endregion

    #region Test helpers

    /// <summary>
    /// Converts fret buttons into an int button mask.
    /// </summary>
    private static int Mask(params GuitarAction[] frets)
    {
        var mask = 0;
        foreach (var fret in frets)
        {
            mask |= 1 << (int) fret;
        }

        return mask;
    }

    private static TestSixFretGuitarEngine CreateSixFretEngine()
    {
        var firstNote = CreateSixFretNote(SixFretGuitarFret.Black1, GuitarNoteType.Strum);
        var secondNote = CreateSixFretNote(SixFretGuitarFret.White3, GuitarNoteType.Hopo);
        LinkNotes(firstNote, secondNote);

        return CreateSixFretEngine([firstNote, secondNote]);
    }

    private static TestSixFretGuitarEngine CreateSixFretEngine(GuitarNote[] notes)
    {
        var difficulty = new InstrumentDifficulty<GuitarNote>(Instrument.SixFretGuitar, Difficulty.Expert,
            new(notes), new(), new());

        var engineParams = new GuitarEngineParameters(
            CreateHitWindowSettings(),
            4,      // maxMultiplier
            0,      // spWhammyBuffer
            0,      // sustainDropLeniency
            StarMultiplierThresholds,
            SoloBonusStarMultiplierThresholds,
            0.1,    // hopoLeniency
            0.1,    // strumLeniency
            0.1,    // strumLeniencySmall
            false,  // infiniteFrontEnd
            true,   // antiGhosting
            false,  // soloTaps
            false,  // noStarPowerOverlap
            true    // enableLanes
        );

        return new TestSixFretGuitarEngine(difficulty, CreateSyncTrack(), engineParams);
    }

    private static GuitarNote CreateSixFretNote(SixFretGuitarFret fret, GuitarNoteType type)
    {
        return new GuitarNote((int) fret, type, GuitarNoteFlags.None, NoteFlags.None, 0.0, 0, 0, 0);
    }

    private static GuitarNote CreateSixFretChordNote(SixFretGuitarFret[] frets, GuitarNoteType type)
    {
        var parent = new GuitarNote((int) frets[0], type, GuitarNoteFlags.None, NoteFlags.None, 0.0, 0, 0, 0);
        for (int i = 1; i < frets.Length; i++)
        {
            var child = new GuitarNote((int) frets[i], type, GuitarNoteFlags.None, NoteFlags.None, 0.0, 0, 0, 0);
            parent.AddChildNote(child);
        }
        return parent;
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

    private sealed class TestSixFretGuitarEngine : YargSixFretGuitarEngine
    {
        public TestSixFretGuitarEngine(
            InstrumentDifficulty<GuitarNote> chart,
            SyncTrack syncTrack,
            GuitarEngineParameters engineParameters)
            : base(chart, syncTrack, engineParameters, false)
        {
        }

        public bool IsAnchoringValidFor(int anchorButtons, int targetFretValue)
        {
            return IsAnchoringValid(anchorButtons, targetFretValue);
        }

        public bool CanBeHit(GuitarNote note)
        {
            return CanNoteBeHit(note);
        }

        public bool IsGhostInput(GuitarNote note)
        {
            return CheckForGhostInput(note);
        }

        public void SetButtonMask(int mask)
        {
            EffectiveButtonMask = (byte) mask;
        }

        public void SetButtonState(int effectiveMask, int lastMask, bool isFretPress)
        {
            EffectiveButtonMask = (byte) effectiveMask;
            LastButtonMask = (byte) lastMask;
            IsFretPress = isFretPress;
        }

        public void ClockToNote(int noteIndex)
        {
            var note = Notes[noteIndex];
            CurrentTime = note.Time;
            NoteIndex = noteIndex;
        }

        public IList<GuitarNote> GetNotes() => Notes;
    }

    #endregion
}
