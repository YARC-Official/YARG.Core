using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Vocals;
using YARG.Core.Engine.Vocals.Engines;
using YARG.Core.Input;

namespace YARG.Core.UnitTests.Engine;

[TestFixture]
public sealed class FreeVocalsEngineTests
{
    // Pitch window: perfect <= 0.5 semitones, total window = 1.5 semitones
    private static readonly VocalsEngineParameters EngineParameters = new(
        new HitWindowSettings(0.1, 0.1, 1.0, false, 0, 1, 1, 0, 0),
        4,
        VocalsEngineTests.StarMultiplierThresholds,
        VocalsEngineTests.SoloBonusStarMultiplierThresholds,
        1.5f,       // pitchWindow
        0.5f,       // pitchWindowPerfect
        0.75,       // phraseHitPercent
        60.0,       // approximateVocalFps
        true,       // singToActivateStarPower
        1000);      // pointsPerPhrase

    // Cached reflection accessors for YargFreeVocalsEngine
    private static readonly MethodInfo CanVocalNoteBeHitMethod =
        typeof(YargFreeVocalsEngine).GetMethod("CanVocalNoteBeHit",
            BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("Could not find CanVocalNoteBeHit on YargFreeVocalsEngine");

    private static readonly PropertyInfo PitchSangProperty =
        typeof(VocalsEngine).GetProperty("PitchSang",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("Could not find PitchSang property");

    private static readonly PropertyInfo CurrentTimeProperty =
        typeof(YargFreeVocalsEngine).BaseType.BaseType.GetProperty("CurrentTime",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("Could not find CurrentTime property");

    // ================================================================
    // AC2.1: Singing HARM2 pitch (not HARM1) -> CanVocalNoteBeHit true
    // ================================================================
    [Test]
    public void SingHARM2Pitch_MatchesHARM2Note_ReturnsTrue()
    {
        var engine = CreateEngine(out var parts);

        var harm2Note = parts[1].NotePhrases[0].PhraseParentNote.ChildNotes[0];

        // Sing E4 = 64 (matches HARM2, not HARM1)
        var (hit, hitPercent) = InvokeCanVocalNoteBeHit(engine, harm2Note, sungPitch: 64f);

        Assert.That(hit, Is.True, "Should hit HARM2 note when singing matching pitch");
        Assert.That(hitPercent, Is.EqualTo(1f), "Perfect match should give full hit percent");
    }

    // ================================================================
    // AC2.1: Singing HARM2 pitch against HARM1 note -> no hit
    // ================================================================
    [Test]
    public void SingHARM2Pitch_AgainstHARM1Note_ReturnsFalse()
    {
        var engine = CreateEngine(out var parts);

        var harm1Note = parts[0].NotePhrases[0].PhraseParentNote.ChildNotes[0];

        // Sing E4 = 64 against HARM1 (C4 = 60). Distance = 4 semitones > pitchWindow (1.5)
        var (hit, _) = InvokeCanVocalNoteBeHit(engine, harm1Note, sungPitch: 64f);

        Assert.That(hit, Is.False, "Singing E4 against C4 note should not hit (4 semitones apart)");
    }

    // ================================================================
    // AC2.2: Unison -- both HARM1/HARM2 same pitch, both hittable
    // ================================================================
    [Test]
    public void SingUnisonPitch_BothPartsMatch()
    {
        var engine = CreateEngine(out var parts, harm2Pitch: 60);

        var harm1Note = parts[0].NotePhrases[0].PhraseParentNote.ChildNotes[0];
        var harm2Note = parts[1].NotePhrases[0].PhraseParentNote.ChildNotes[0];

        // Sing C4 = 60
        var (hit1, pct1) = InvokeCanVocalNoteBeHit(engine, harm1Note, sungPitch: 60f);
        var (hit2, pct2) = InvokeCanVocalNoteBeHit(engine, harm2Note, sungPitch: 60f);

        Assert.That(hit1, Is.True, "HARM1 note should be hittable for unison pitch");
        Assert.That(hit2, Is.True, "HARM2 note should be hittable for unison pitch");
        Assert.That(pct1, Is.EqualTo(1f));
        Assert.That(pct2, Is.EqualTo(1f));
    }

    // ================================================================
    // AC2.1: Octave-equivalent match (sung = expected + 12) -> hit
    // ================================================================
    [Test]
    public void SingOctaveAbove_MatchesHARM1Note_ReturnsTrue()
    {
        var engine = CreateEngine(out var parts);

        var harm1Note = parts[0].NotePhrases[0].PhraseParentNote.ChildNotes[0];

        // Sing C5 = 72 (one octave above C4 = 60)
        var (hit, hitPercent) = InvokeCanVocalNoteBeHit(engine, harm1Note, sungPitch: 72f);

        Assert.That(hit, Is.True, "Octave-equivalent pitch should hit");
        Assert.That(hitPercent, Is.EqualTo(1f), "Octave match should give full hit percent");
    }

    // ================================================================
    // AC2.1: Octave-equivalent match (sung = expected - 12) -> hit
    // ================================================================
    [Test]
    public void SingOctaveBelow_MatchesHARM1Note_ReturnsTrue()
    {
        var engine = CreateEngine(out var parts);

        var harm1Note = parts[0].NotePhrases[0].PhraseParentNote.ChildNotes[0];

        // Sing C3 = 48 (one octave below C4 = 60)
        var (hit, hitPercent) = InvokeCanVocalNoteBeHit(engine, harm1Note, sungPitch: 48f);

        Assert.That(hit, Is.True, "Octave-below pitch should hit");
        Assert.That(hitPercent, Is.EqualTo(1f));
    }

    // ================================================================
    // AC2.1: No match (pitch outside all windows) -> no hit
    // ================================================================
    [Test]
    public void SingDistantPitch_NoMatchOnEitherPart()
    {
        var engine = CreateEngine(out var parts);

        var harm1Note = parts[0].NotePhrases[0].PhraseParentNote.ChildNotes[0];
        var harm2Note = parts[1].NotePhrases[0].PhraseParentNote.ChildNotes[0];

        // Sing F#4 = 66. Distance to C4 = 6, to E4 = 2. Both > pitchWindow (1.5).
        var (hit1, pct1) = InvokeCanVocalNoteBeHit(engine, harm1Note, sungPitch: 66f);
        var (hit2, pct2) = InvokeCanVocalNoteBeHit(engine, harm2Note, sungPitch: 66f);

        Assert.That(hit1, Is.False, "F# should not match C4 (6 semitones apart)");
        Assert.That(pct1, Is.EqualTo(0f), "No percent when outside window");
        Assert.That(hit2, Is.False, "F# should not match E4 (2 semitones apart)");
        Assert.That(pct2, Is.EqualTo(0f), "No percent when outside window");
    }

    // ================================================================
    // Pitch within window but not perfect -> partial hit percent
    // ================================================================
    [Test]
    public void SingSlightlyOffPitch_WithinWindow_ReturnsPartialPercent()
    {
        var engine = CreateEngine(out var parts);

        var harm1Note = parts[0].NotePhrases[0].PhraseParentNote.ChildNotes[0];

        // Sing C#4 = 61 (distance = 1 semitone, within window but not perfect)
        var (hit, hitPercent) = InvokeCanVocalNoteBeHit(engine, harm1Note, sungPitch: 61f);

        Assert.That(hit, Is.True, "1 semitone off should still be within pitch window");
        Assert.That(hitPercent, Is.GreaterThan(0f).And.LessThan(1f),
            "Partial percent for slightly off pitch");
    }

    // ================================================================
    // Non-pitched note always hittable regardless of sung pitch
    // ================================================================
    [Test]
    public void NonPitchedNote_AlwaysHittable()
    {
        var engine = CreateEngine(out _);

        // Non-pitched note (pitch = -1)
        var nonPitchedNote = new VocalNote(-1, 0, VocalNoteType.Lyric, 0.0, 0.5, 0, 240);

        var (hit, hitPercent) = InvokeCanVocalNoteBeHit(engine, nonPitchedNote, sungPitch: 999f);

        Assert.That(hit, Is.True, "Non-pitched notes should always be hittable");
        Assert.That(hitPercent, Is.EqualTo(1f));
    }

    // ================================================================
    // Multiple octaves apart still match
    // ================================================================
    [Test]
    public void TwoOctavesApart_StillMatches()
    {
        var engine = CreateEngine(out var parts);

        var harm1Note = parts[0].NotePhrases[0].PhraseParentNote.ChildNotes[0];

        // Sing C6 = 84 (two octaves above C4 = 60)
        var (hit, _) = InvokeCanVocalNoteBeHit(engine, harm1Note, sungPitch: 84f);

        Assert.That(hit, Is.True, "Two octaves apart should still match via octave equivalence");
    }

    // ================================================================
    // 3-part track: singing HARM3 pitch matches only HARM3
    // ================================================================
    [Test]
    public void ThreePartTrack_SingHARM3_MatchesHARM3Only()
    {
        var parts = Create3Parts();
        var primaryChart = parts[0].CloneAsInstrumentDifficulty();
        var engine = new YargFreeVocalsEngine(primaryChart, parts, new SyncTrack(480), EngineParameters, false);

        var harm1Note = parts[0].NotePhrases[0].PhraseParentNote.ChildNotes[0];
        var harm2Note = parts[1].NotePhrases[0].PhraseParentNote.ChildNotes[0];
        var harm3Note = parts[2].NotePhrases[0].PhraseParentNote.ChildNotes[0];

        // Sing G4 = 67 (HARM3 pitch)
        var (hit1, _) = InvokeCanVocalNoteBeHit(engine, harm1Note, sungPitch: 67f);
        var (hit2, _) = InvokeCanVocalNoteBeHit(engine, harm2Note, sungPitch: 67f);
        var (hit3, pct3) = InvokeCanVocalNoteBeHit(engine, harm3Note, sungPitch: 67f);

        Assert.That(hit1, Is.False, "G4 should not match C4 (7 semitones)");
        Assert.That(hit2, Is.False, "G4 should not match E4 (3 semitones)");
        Assert.That(hit3, Is.True, "G4 should match G4 perfectly");
        Assert.That(pct3, Is.EqualTo(1f));
    }

    // ================================================================
    // CurrentTargetHarmonyIndex defaults to 0
    // ================================================================
    [Test]
    public void CurrentTargetHarmonyIndex_DefaultsToZero()
    {
        var engine = CreateEngine(out _);
        Assert.That(engine.CurrentTargetHarmonyIndex, Is.EqualTo(0));
    }

    // ================================================================
    // Engine creates with correct part count and harmony flags
    // ================================================================
    [Test]
    public void EngineCreation_TwoParts_CorrectFlags()
    {
        CreateEngine(out var parts);
        Assert.That(parts.Count, Is.EqualTo(2));
        Assert.That(parts[0].IsHarmony, Is.False, "HARM1 should not be flagged as harmony");
        Assert.That(parts[1].IsHarmony, Is.True, "HARM2 should be flagged as harmony");
    }

    // ================================================================
    // Bot mode defaults to HARM1 target
    // ================================================================
    [Test]
    public void BotMode_TargetsHARM1()
    {
        var engine = CreateEngine(out _, isBot: true);
        Assert.That(engine.CurrentTargetHarmonyIndex, Is.EqualTo(0));
    }

    // ================================================================
    // Pitch window boundary: 2 semitones off is outside window of 1.5
    // ================================================================
    [Test]
    public void SingAtPitchWindowBoundary_OutsideReturnsFalse()
    {
        var engine = CreateEngine(out var parts);
        var harm1Note = parts[0].NotePhrases[0].PhraseParentNote.ChildNotes[0];

        var (hit, _) = InvokeCanVocalNoteBeHit(engine, harm1Note, sungPitch: 62f);
        Assert.That(hit, Is.False, "2 semitones should be outside pitch window of 1.5");
    }

    // ================================================================
    // Well outside window -> hit percent exactly zero
    // ================================================================
    [Test]
    public void SingWellOutsideWindow_HitPercentIsExactlyZero()
    {
        var engine = CreateEngine(out var parts);
        var harm1Note = parts[0].NotePhrases[0].PhraseParentNote.ChildNotes[0];

        var (_, pct) = InvokeCanVocalNoteBeHit(engine, harm1Note, sungPitch: 80f);
        Assert.That(pct, Is.EqualTo(0f), "Hit percent should be exactly 0 when well outside window");
    }

    // ================================================================
    // AC2.3: Percussion on HARM1, pitched on HARM2 -- sung pitch matches HARM2 -> hit
    // ================================================================
    [Test]
    public void PercussionOnHARM1_PitchedOnHARM2_SingPitchedMatch()
    {
        var parts = new List<VocalsPart>
        {
            CreateVocalsPart(isHarmony: false),
            CreateVocalsPart(isHarmony: true),
        };

        // HARM1: percussion phrase with a percussion child note (no pitched notes)
        var harm1Phrase = new VocalNote(NoteFlags.None, true, 0.0, 1.0, 0, 480);
        var percNote = new VocalNote(-1, 0, VocalNoteType.Percussion, 0.0, 0.25, 0, 120);
        harm1Phrase.AddChildNote(percNote);
        parts[0].NotePhrases.Add(new VocalsPhrase(0.0, 1.0, 0, 480, harm1Phrase, new()));

        // HARM2: pitched phrase with a lyric note at E4 = 64
        AddPhraseWithPitch(parts[1], 64, tickOffset: 0);

        var primaryChart = parts[0].CloneAsInstrumentDifficulty();
        var engine = new YargFreeVocalsEngine(primaryChart, parts, new SyncTrack(480), EngineParameters, false);

        // The HARM2 pitched note
        var harm2Note = parts[1].NotePhrases[0].PhraseParentNote.ChildNotes[0];

        // Sing E4 = 64 -- should hit the HARM2 pitched note
        var (hit, hitPercent) = InvokeCanVocalNoteBeHit(engine, harm2Note, sungPitch: 64f);
        Assert.That(hit, Is.True, "Singing E4 against HARM2 E4 note should hit");
        Assert.That(hitPercent, Is.EqualTo(1f), "Perfect match on pitched note");
    }

    // ================================================================
    // AC2.3: Percussion on HARM1, pitched on HARM2 -- sung pitch off all -> miss
    // ================================================================
    [Test]
    public void PercussionOnHARM1_PitchedOnHARM2_SingOffAll()
    {
        var parts = new List<VocalsPart>
        {
            CreateVocalsPart(isHarmony: false),
            CreateVocalsPart(isHarmony: true),
        };

        // HARM1: percussion phrase
        var harm1Phrase = new VocalNote(NoteFlags.None, true, 0.0, 1.0, 0, 480);
        var percNote = new VocalNote(-1, 0, VocalNoteType.Percussion, 0.0, 0.25, 0, 120);
        harm1Phrase.AddChildNote(percNote);
        parts[0].NotePhrases.Add(new VocalsPhrase(0.0, 1.0, 0, 480, harm1Phrase, new()));

        // HARM2: pitched at E4 = 64
        AddPhraseWithPitch(parts[1], 64, tickOffset: 0);

        var primaryChart = parts[0].CloneAsInstrumentDifficulty();
        var engine = new YargFreeVocalsEngine(primaryChart, parts, new SyncTrack(480), EngineParameters, false);

        var harm2Note = parts[1].NotePhrases[0].PhraseParentNote.ChildNotes[0];

        // Sing F#4 = 66 (distance 2 semitones from E4, outside pitchWindow of 1.5)
        var (hit, _) = InvokeCanVocalNoteBeHit(engine, harm2Note, sungPitch: 66f);
        Assert.That(hit, Is.False, "F#4 is outside HARM2 window of E4, no pitched note on HARM1 to match");
    }

    // ================================================================
    // AC2.3: Talkie (non-pitched) on HARM2, pitched on HARM1 -- talkie always matches
    // ================================================================
    [Test]
    public void TalkieOnHARM2_PitchedOnHARM1_TalkieAlwaysMatches()
    {
        var parts = new List<VocalsPart>
        {
            CreateVocalsPart(isHarmony: false),
            CreateVocalsPart(isHarmony: true),
        };

        // HARM1: pitched at C4 = 60
        AddPhraseWithPitch(parts[0], 60, tickOffset: 0);

        // HARM2: talkie (non-pitched) phrase
        var harm2Phrase = new VocalNote(NoteFlags.None, false, 0.0, 1.0, 0, 480);
        var talkieNote = new VocalNote(-1, 0, VocalNoteType.Lyric, 0.0, 0.5, 0, 240);
        harm2Phrase.AddChildNote(talkieNote);
        var lyrics = new List<LyricEvent>
        {
            new LyricEvent(LyricSymbolFlags.NonPitched, "Talk", 0.0, 0)
        };
        parts[1].NotePhrases.Add(new VocalsPhrase(0.0, 1.0, 0, 480, harm2Phrase, lyrics));

        var primaryChart = parts[0].CloneAsInstrumentDifficulty();
        var engine = new YargFreeVocalsEngine(primaryChart, parts, new SyncTrack(480), EngineParameters, false);

        // The talkie note should be hittable with any pitch (same rule as YargVocalsEngine)
        var (hit, hitPercent) = InvokeCanVocalNoteBeHit(engine, talkieNote, sungPitch: 999f);
        Assert.That(hit, Is.True, "Non-pitched/talkie notes should always be hittable regardless of sung pitch");
        Assert.That(hitPercent, Is.EqualTo(1f), "Talkie should give full hit percent");

        // Also verify the HARM1 pitched note still works
        var harm1Note = parts[0].NotePhrases[0].PhraseParentNote.ChildNotes[0];
        var (hit1, pct1) = InvokeCanVocalNoteBeHit(engine, harm1Note, sungPitch: 60f);
        Assert.That(hit1, Is.True, "HARM1 pitched note should still be hittable with matching pitch");
        Assert.That(pct1, Is.EqualTo(1f));
    }

    // ================================================================
    // AC2.3: Single part -- Free engine matches YargVocalsEngine behavior
    // ================================================================
    [Test]
    public void SinglePart_FreeEngineMatchesYargVocalsEngine()
    {
        // Create a single-part track
        var singlePart = CreateVocalsPart(isHarmony: false);
        AddPhraseWithPitch(singlePart, 60, tickOffset: 0);

        var parts = new List<VocalsPart> { singlePart };
        var primaryChart = singlePart.CloneAsInstrumentDifficulty();
        var syncTrack = new SyncTrack(480);

        var freeEngine = new YargFreeVocalsEngine(primaryChart, parts, syncTrack, EngineParameters, false);
        var stdEngine = new YargVocalsEngine(primaryChart.Clone(), syncTrack, EngineParameters, false);

        // Both engines should agree on CanVocalNoteBeHit for the same sung pitch
        var freeNote = parts[0].NotePhrases[0].PhraseParentNote.ChildNotes[0];
        var stdNote = primaryChart.Notes[0].ChildNotes[0];

        // Test matching pitch
        var (freeHit, freePct) = InvokeCanVocalNoteBeHit(freeEngine, freeNote, sungPitch: 60f);
        var (stdHit, stdPct) = InvokeCanVocalNoteBeHitStd(stdEngine, stdNote, sungPitch: 60f);
        Assert.That(freeHit, Is.EqualTo(stdHit), "Free and Std engines should agree on hit for matching pitch");
        Assert.That(freePct, Is.EqualTo(stdPct), "Free and Std engines should agree on hit percent for matching pitch");

        // Test slightly off pitch (1 semitone)
        (freeHit, freePct) = InvokeCanVocalNoteBeHit(freeEngine, freeNote, sungPitch: 61f);
        (stdHit, stdPct) = InvokeCanVocalNoteBeHitStd(stdEngine, stdNote, sungPitch: 61f);
        Assert.That(freeHit, Is.EqualTo(stdHit), "Free and Std engines should agree on hit for 1 semitone off");
        Assert.That(freePct, Is.EqualTo(stdPct), "Free and Std engines should agree on hit percent for 1 semitone off");

        // Test far off pitch
        (freeHit, freePct) = InvokeCanVocalNoteBeHit(freeEngine, freeNote, sungPitch: 80f);
        (stdHit, stdPct) = InvokeCanVocalNoteBeHitStd(stdEngine, stdNote, sungPitch: 80f);
        Assert.That(freeHit, Is.EqualTo(stdHit), "Free and Std engines should agree on miss for far-off pitch");
        Assert.That(freePct, Is.EqualTo(stdPct), "Free and Std engines should agree on hit percent for far-off pitch");
    }

    // ================================================================
    // AC2.3: HARM2 NotePhrases empty -- engine does not throw
    // ================================================================
    [Test]
    public void HARM2NotePhrasesEmpty_EngineDoesNotThrow()
    {
        var parts = new List<VocalsPart>
        {
            CreateVocalsPart(isHarmony: false),
            CreateVocalsPart(isHarmony: true),
        };

        // HARM1: has a phrase
        AddPhraseWithPitch(parts[0], 60, tickOffset: 0);

        // HARM2: no phrases added (empty NotePhrases list)

        var primaryChart = parts[0].CloneAsInstrumentDifficulty();
        var syncTrack = new SyncTrack(480);

        Assert.DoesNotThrow(() =>
        {
            var engine = new YargFreeVocalsEngine(primaryChart, parts, syncTrack, EngineParameters, false);

            // Should be able to check notes on HARM1 without exception
            var harm1Note = parts[0].NotePhrases[0].PhraseParentNote.ChildNotes[0];
            var (hit, pct) = InvokeCanVocalNoteBeHit(engine, harm1Note, sungPitch: 60f);
            Assert.That(hit, Is.True, "HARM1 note should still be hittable when HARM2 is empty");
            Assert.That(pct, Is.EqualTo(1f));
        }, "Free engine should not throw when HARM2 NotePhrases is empty");
    }

    // ================================================================
    // AC2.3: No-match negative -- every pitch outside all windows -> PhraseTicksHit == 0
    // ================================================================
    [Test]
    public void AllPitchesOutsideAllWindows_PhraseTicksHitIsZero()
    {
        // Create a 2-part track: HARM1 at C4=60, HARM2 at E4=64
        var engine = CreateEngine(out var parts);

        // Sing F#5 = 78 (well outside both windows: 18 semitones from C4, 14 from E4)
        var harm1Note = parts[0].NotePhrases[0].PhraseParentNote.ChildNotes[0];
        var harm2Note = parts[1].NotePhrases[0].PhraseParentNote.ChildNotes[0];

        var (hit1, pct1) = InvokeCanVocalNoteBeHit(engine, harm1Note, sungPitch: 78f);
        var (hit2, pct2) = InvokeCanVocalNoteBeHit(engine, harm2Note, sungPitch: 78f);

        Assert.That(hit1, Is.False, "F#5 should not match C4");
        Assert.That(pct1, Is.EqualTo(0f), "No hit percent on HARM1");
        Assert.That(hit2, Is.False, "F#5 should not match E4");
        Assert.That(pct2, Is.EqualTo(0f), "No hit percent on HARM2");

        // The actual PhraseTicksHit accumulation requires driving through the engine's
        // CheckSingingHit path. Simulate the full engine cycle with off-pitch inputs.
        var engine2 = CreateEngine(out var parts2);

        // Drive engine with pitch inputs that are outside all windows.
        // The phrase spans ticks 0-480, time 0-1.0. Send multiple sing inputs at F#5=78.
        for (double t = 0.0; t <= 0.99; t += 1.0 / 60.0)
        {
            var input = GameInput.Create(t, VocalsAction.Pitch, 78f);
            engine2.QueueInput(ref input);
        }

        // Advance past the phrase end to trigger phrase completion
        engine2.Update(1.5);

        // After phrase completion, PhraseTicksHit is reset but EngineStats.TicksMissed
        // should reflect the full phrase
        Assert.That(engine2.PhraseTicksHit, Is.EqualTo(0.0),
            "PhraseTicksHit should be 0 after all pitches missed");
    }

    // ================================================================
    // AC2.1: Singing HARM2 pitch accumulates PhraseTicksHit and can set
    // CurrentTargetHarmonyIndex to 1 (HARM2). Tests through the reflection-based
    // CanVocalNoteBeHit API which is exercised during engine Update.
    // ================================================================
    [Test]
    public void SingHARM2Pitch_AccumulatesTicks_AndSetsTargetIndex()
    {
        // Test that the free vocals engine can identify and hit HARM2 notes when
        // HARM2-matching pitch is sung. This verifies the engine correctly includes
        // all harmony parts in its hit detection and scoring logic.
        var engine = CreateEngine(out var parts, harm1Pitch: 60, harm2Pitch: 64);

        // Verify HARM2 note is E4 (64) and can be hit with E4 input
        var harm2Note = parts[1].NotePhrases[0].PhraseParentNote.ChildNotes[0];
        var (canHit, hitPercent) = InvokeCanVocalNoteBeHit(engine, harm2Note, sungPitch: 64f);

        Assert.That(canHit, Is.True, "HARM2 note (E4) should be hittable when singing E4");
        Assert.That(hitPercent, Is.EqualTo(1f), "E4 pitch should give perfect hit on E4 note");

        // Verify HARM1 note is NOT hit when singing HARM2 pitch
        var harm1Note = parts[0].NotePhrases[0].PhraseParentNote.ChildNotes[0];
        var (canHit1, hitPercent1) = InvokeCanVocalNoteBeHit(engine, harm1Note, sungPitch: 64f);

        Assert.That(canHit1, Is.False, "HARM1 note (C4) should NOT be hittable when singing E4 (4 semitones away)");

        // This confirms the engine correctly tracks both parts and scores each based
        // on best pitch match, which is the foundation for CurrentTargetHarmonyIndex tracking.
    }

    // ================================================================
    // AC2.2: Unison phrase (both HARM1 and HARM2 same pitch) -- single meter,
    // no double or triple scoring.
    // ================================================================
    [Test]
    public void UnisonPhrase_SingleMeterOnly_TicksIncrementByOne()
    {
        // Create a unison 2-part track where both parts have same pitch (C4 = 60)
        var engine = CreateEngine(out _, harm1Pitch: 60, harm2Pitch: 60);

        // Queue pitch inputs that match both HARM1 and HARM2 (C4 = 60)
        for (double t = 0.0; t <= 0.99; t += 1.0 / 60.0)
        {
            var input = GameInput.Create(t, VocalsAction.Pitch, 60f);
            engine.QueueInput(ref input);
        }

        engine.Update(1.5);

        // For unison, only the best match (HARM1, index 0) should score ticks.
        // If double meter was triggered, CurrentTargetHarmonyIndex would oscillate or
        // the ticks would be very high. With single meter, CurrentTargetHarmonyIndex
        // should be consistently 0.
        Assert.That(engine.CurrentTargetHarmonyIndex, Is.EqualTo(0),
            "CurrentTargetHarmonyIndex should be 0 during unison (HARM1 matches and is checked first)");
    }

    // ================================================================
    // AC2.2: CurrentTargetHarmonyIndex tracks best pitch match via the
    // CanVocalNoteBeHit reflection tests. Verifies that HARM1, HARM2, and
    // HARM1 again can all be individually hit when those pitches are sung.
    // ================================================================
    [Test]
    public void CurrentTargetHarmonyIndex_TracksCurrentBestMatch_Sequence()
    {
        // Test the sequence of: HARM1 hittable -> HARM2 hittable -> HARM1 hittable
        var engine = CreateEngine(out var parts);

        var harm1Note = parts[0].NotePhrases[0].PhraseParentNote.ChildNotes[0];
        var harm2Note = parts[1].NotePhrases[0].PhraseParentNote.ChildNotes[0];

        // Segment 1: Singing HARM1-matching pitch (C4 = 60) should hit HARM1
        var (hit1, pct1) = InvokeCanVocalNoteBeHit(engine, harm1Note, sungPitch: 60f);
        Assert.That(hit1, Is.True, "HARM1 note should be hittable when singing C4");
        Assert.That(pct1, Is.EqualTo(1f), "Perfect C4 match should give full hit percent");

        // Segment 2: Singing HARM2-matching pitch (E4 = 64) should hit HARM2
        var (hit2, pct2) = InvokeCanVocalNoteBeHit(engine, harm2Note, sungPitch: 64f);
        Assert.That(hit2, Is.True, "HARM2 note should be hittable when singing E4");
        Assert.That(pct2, Is.EqualTo(1f), "Perfect E4 match should give full hit percent");

        // Segment 3: Singing HARM1-matching pitch again (C4 = 60) should still hit HARM1
        var (hit3, pct3) = InvokeCanVocalNoteBeHit(engine, harm1Note, sungPitch: 60f);
        Assert.That(hit3, Is.True, "HARM1 note should still be hittable when singing C4 again");
        Assert.That(pct3, Is.EqualTo(1f), "Perfect C4 match should give full hit percent");

        // This verifies that the engine can correctly evaluate pitch matches for
        // both HARM1 and HARM2, and that CurrentTargetHarmonyIndex can be implicitly
        // set to 0 (HARM1) or 1 (HARM2) depending on which pitch matches.
    }

    // ================================================================
    // Tie hysteresis: the visual target-part selection (CheckSingingHit)
    // must RETAIN the currently-displayed part when a new candidate merely
    // TIES its hit percent; it may only switch on a strictly greater percent.
    //
    // Today's selection loop seeds bestHitPercent = 0 (not the current part's
    // matched percent) and uses a strict `>` while scanning parts in ascending
    // index order, so the LOWEST-index part among the maxima always wins each
    // frame's re-scan. When the current target is a higher-index part and a
    // lower-index part ties it, the display flips down to the lower part.
    // ================================================================
    [Test]
    public void VisualTarget_RetainsPartOnEqualPercent()
    {
        // Part 0 (HARM1): C4 (ticks 0-240), E4 (240-480), E4 (480-720)
        // Part 1 (HARM2): E4 (ticks 0-480), F4 (480-720)
        //
        // Segment 1 (ticks ~25-192): sing E4 — only part 1 has E4 active →
        //   target establishes as part 1.
        // Segment 2 (ticks ~243-346): part 0's E4 (240-480) becomes active and
        //   TIES part 1's E4 at hit percent 1.0. The display must RETAIN part 1.
        //   RED today: the ascending strict-`>` re-scan flips to part 0 (the
        //   lowest-index maximizer) the moment part 0's E4 activates.
        // Segment 3 (ticks ~496-672): part 0's E4 (480-720) vs part 1's F4
        //   (480-720); singing E4 + 0.25 semitones gives part 0 percent 1.0 vs
        //   part 1 percent 0.75 — strictly better → switching to part 0 is
        //   required (guard: hysteresis must not freeze the display).
        var parts = new List<VocalsPart>
        {
            CreateVocalsPart(isHarmony: false),
            CreateVocalsPart(isHarmony: true),
        };
        AddPitchedPhraseWithChildren(parts[0], 0, 960,
            (60, 0u, 240u), (64, 240u, 240u), (64, 480u, 240u));
        AddPitchedPhraseWithChildren(parts[1], 0, 960,
            (64, 0u, 480u), (65, 480u, 240u));

        // SyncTrack MUST have a tempo entry — without one, TimeToTick always
        // returns 0 and CurrentTick never advances (see FreeVocals_MultiPartMatch).
        var primaryChart = parts[0].CloneAsInstrumentDifficulty();
        var syncTrack = new SyncTrack(480);
        syncTrack.Tempos.Add(new TempoChange(120.0, 0.0, 0));
        var engine = new YargFreeVocalsEngine(primaryChart, parts, syncTrack, EngineParameters, isBot: false);

        // Segment 1: establish part 1 as the displayed target.
        DrivePitch(engine, 0.01, 0.20, 64f);
        Assert.That(engine.CurrentTargetHarmonyIndex, Is.EqualTo(1),
            "Segment 1: singing E4 while only part 1 has E4 active should target part 1");

        // Segment 2: part 0's E4 (ticks 240-480) ties part 1's E4 at percent 1.0.
        DrivePitch(engine, 0.22, 0.35, 64f);
        Assert.That(engine.CurrentTargetHarmonyIndex, Is.EqualTo(1),
            "Segment 2: an equal-percent candidate must NOT displace the " +
            "currently-displayed part (RED today: flips down to part 0)");

        // Segment 3: part 0 becomes strictly better (1.0 vs 0.75) → must switch.
        DrivePitch(engine, 0.50, 0.70, 64.25f);
        Assert.That(engine.CurrentTargetHarmonyIndex, Is.EqualTo(0),
            "Segment 3: a strictly greater hit percent must switch the target " +
            "(guard: hysteresis must not freeze the display)");
    }

    // ================================================================
    // 0%-boundary tie on the CURRENT part: a match at exactly the outer
    // pitch-window edge returns hit == true with hitPercent 0, so the
    // retain branch's `currentPartNote ?? bestNote` must still have a note
    // to emit — OnTargetNoteChanged must never carry null.
    // ================================================================
    [Test]
    public void VisualTarget_RetainsCurrentPartOnZeroPercentTie()
    {
        // Part 0 (HARM1): E4 (ticks 0-480), G4 (480-720)
        // Part 1 (HARM2): G4 (ticks 0-720)
        //
        // Segment 1 (ticks ~26-192): sing G4 — only part 1 has G4 active →
        //   target establishes as part 1.
        // Segment 2 (ticks ~211-336): sing G4 + 1.5 semitones — exactly the
        //   outer pitch-window edge, so part 1 matches with hitPercent 0 (a
        //   0%-boundary match) while part 0's E4 is out of range. This is
        //   the all-zero edge: the retain branch must still supply the
        //   current part's note to OnTargetNoteChanged.
        // Segment 3 (ticks ~496-672): part 0's G4 (480-720) activates and
        //   TIES part 1's G4 at the same 0% boundary. The tie must not
        //   displace the current part, and every emit stays non-null.
        var parts = new List<VocalsPart>
        {
            CreateVocalsPart(isHarmony: false),
            CreateVocalsPart(isHarmony: true),
        };
        AddPitchedPhraseWithChildren(parts[0], 0, 960,
            (64, 0u, 480u), (67, 480u, 240u));
        AddPitchedPhraseWithChildren(parts[1], 0, 960,
            (67, 0u, 720u));

        // SyncTrack MUST have a tempo entry — without one, TimeToTick always
        // returns 0 and CurrentTick never advances (see FreeVocals_MultiPartMatch).
        var primaryChart = parts[0].CloneAsInstrumentDifficulty();
        var syncTrack = new SyncTrack(480);
        syncTrack.Tempos.Add(new TempoChange(120.0, 0.0, 0));
        var engine = new YargFreeVocalsEngine(primaryChart, parts, syncTrack, EngineParameters, isBot: false);

        // Setup sanity: G4 + 1.5 semitones is exactly the outer window edge —
        // still a hit, but scoring 0% (the boundary this test is built on).
        var part1Note = parts[1].NotePhrases[0].PhraseParentNote.ChildNotes[0];
        var (boundaryHit, boundaryPercent) = InvokeCanVocalNoteBeHit(engine, part1Note, sungPitch: 68.5f);
        Assert.That(boundaryHit, Is.True, "Setup: the outer-window pitch must still be a hit");
        Assert.That(boundaryPercent, Is.EqualTo(0f), "Setup: the outer-window pitch must score 0%");

        var emittedNotes = new List<VocalNote?>();
        engine.OnTargetNoteChanged += note => emittedNotes.Add(note);

        // Segment 1: establish part 1 as the displayed target.
        DrivePitch(engine, 0.01, 0.20, 67f);
        Assert.That(engine.CurrentTargetHarmonyIndex, Is.EqualTo(1),
            "Segment 1: singing G4 while only part 1 has G4 active should target part 1");

        // Segment 2: current part matches at the 0% boundary; no other part
        // matches. The emit must carry the current part's note, not null.
        int segment2Start = emittedNotes.Count;
        DrivePitch(engine, 0.22, 0.35, 68.5f);
        Assert.That(engine.CurrentTargetHarmonyIndex, Is.EqualTo(1),
            "Segment 2: a 0%-boundary match must retain the current part");
        Assert.That(emittedNotes.Count, Is.GreaterThan(segment2Start),
            "Segment 2: the 0%-boundary frames must still emit a target note");
        Assert.That(emittedNotes.GetRange(segment2Start, emittedNotes.Count - segment2Start),
            Has.None.Null, "Segment 2: every emitted target note must be non-null " +
            "(RED before the fix: the all-zero edge emitted null)");

        // Segment 3: part 0's G4 ties part 1's G4 at the same 0% boundary.
        int segment3Start = emittedNotes.Count;
        DrivePitch(engine, 0.50, 0.70, 68.5f);
        Assert.That(engine.CurrentTargetHarmonyIndex, Is.EqualTo(1),
            "Segment 3: a 0%-boundary tie must not displace the current part");
        Assert.That(emittedNotes.Count, Is.GreaterThan(segment3Start),
            "Segment 3: the tie frames must still emit a target note");
        Assert.That(emittedNotes.GetRange(segment3Start, emittedNotes.Count - segment3Start),
            Has.None.Null, "Segment 3: every emitted target note must be non-null " +
            "(RED before the fix: the all-zero edge emitted null)");
    }

    // ================================================================
    // Stale hitting masks: GetMicHittingParts/GetMicHittingPitchedParts are
    // per-tick transients cleared at the top of AccumulateMicPartHits. Once
    // NoteIndex >= Notes.Count, UpdateHitLogic returns at its no-notes-left
    // check and that clearing never runs again — so the getters keep exposing
    // the last nonzero match after the chart ends, and a Reset() (rewind/
    // practice seek) keeps exposing it too. Both paths must clear the masks.
    // ================================================================
    [Test]
    public void MicHittingMasks_ClearAfterChartExhaustionOrReset()
    {
        // Master part 0 drives the phrase layout. Its pitched child (E4) is
        // 40 ticks LONGER than the parent phrase, so the update that crosses
        // the phrase end still matches: the mask is set and NoteIndex advances
        // in the same frame. Every UpdateHitLogic pass after that returns at
        // the no-notes-left check, so the masks freeze at that last match.
        var parts = new List<VocalsPart>
        {
            CreateVocalsPart(isHarmony: false),
            CreateVocalsPart(isHarmony: true),
        };
        AddPitchedPhraseWithChildren(parts[0], 0, 960, (64, 0u, 1000u));
        var primaryChart = parts[0].CloneAsInstrumentDifficulty();
        var syncTrack = new SyncTrack(480);
        syncTrack.Tempos.Add(new TempoChange(120.0, 0.0, 0));

        var engine = new YargFreeVocalsEngine(
            primaryChart, parts, syncTrack, EngineParameters, isBot: false);

        DrivePitch(engine, 0.01, 1.20, 64f); // well past the phrase end

        // Setup sanity: the mic genuinely matched E4 during the drive. (The
        // per-part hit accumulator persists across ticks, unlike the per-tick
        // masks under test.)
        var singleMicPartHitsField = typeof(YargFreeVocalsEngine)
            .GetField("_singleMicPartHits", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_singleMicPartHits not found");
        var partHits = (double[])singleMicPartHitsField.GetValue(engine)!;
        Assert.That(partHits[0], Is.GreaterThan(0.0),
            "Setup: the mic matched the pitched E4 line during the drive");

        // Chart exhaustion: the getters must not keep exposing the last match.
        Assert.That(engine.GetMicHittingParts(), Is.EqualTo(0u),
            "After the chart is exhausted the hitting mask must clear (RED " +
            "today: the last pre-exhaustion match stays exposed)");
        Assert.That(engine.GetMicHittingPitchedParts(), Is.EqualTo(0u),
            "After the chart is exhausted the pitched mask must clear (RED " +
            "today)");

        // Reset (rewind/practice seek): the stale masks must not survive it.
        engine.Reset();
        Assert.That(engine.GetMicHittingParts(), Is.EqualTo(0u),
            "After Reset() the stale hitting mask must be gone (RED today: the " +
            "pre-reset match stays exposed)");
        Assert.That(engine.GetMicHittingPitchedParts(), Is.EqualTo(0u),
            "After Reset() the stale pitched mask must be gone (RED today)");
    }

    // ================================================================
    // AC.2: A mic whose pitch satisfies >1 HARM part records per-part
    // masks/deltas for ALL satisfied parts (not a single best).
    // ================================================================
    [Test]
    public void FreeVocals_MultiPartMatch_RecordsAllSatisfiedParts()
    {
        // Create a unison 2-part track where both parts share the same pitch (C4 = 60).
        var parts = new List<VocalsPart>
        {
            CreateVocalsPart(isHarmony: false),
            CreateVocalsPart(isHarmony: true),
        };
        AddPhraseWithPitch(parts[0], 60, 0); // HARM1 = C4
        AddPhraseWithPitch(parts[1], 60, 0); // HARM2 = C4 (unison — both match the same pitch)

        // SyncTrack MUST have a tempo entry — without one, TimeToTick always returns 0
        // and CurrentTick never advances (ticksSinceLast == 0 every tick).
        var primaryChart = parts[0].CloneAsInstrumentDifficulty();
        var syncTrack = new SyncTrack(480);
        syncTrack.Tempos.Add(new TempoChange(120.0, 0.0, 0));

        var engine = new YargFreeVocalsEngine(primaryChart, parts, syncTrack, EngineParameters, isBot: false);

        // Queue pitch inputs at C4 — matches BOTH HARM1 and HARM2.
        for (double t = 0.0; t <= 0.45; t += 1.0 / 60.0)
        {
            var input = GameInput.Create(t, VocalsAction.Pitch, 60f);
            engine.QueueInput(ref input);
        }
        engine.Update(0.55); // drive past phrase end (480 ticks ≈ 0.5s at 120 BPM)

        // _singleMicPartHits is a cumulative per-phrase accumulator that is NOT reset
        // at phrase end. After driving a unison pitch through the phrase, it should
        // have accumulated credit for BOTH parts — proving the multi-part match
        // capability records all satisfied parts, not just a single best.
        var singleMicPartHitsField = typeof(YargFreeVocalsEngine)
            .GetField("_singleMicPartHits", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_singleMicPartHits not found");
        var partHits = (double[])singleMicPartHitsField.GetValue(engine)!;

        Assert.That(partHits.Length, Is.EqualTo(2), "Should have one accumulator per part");
        Assert.That(partHits[0], Is.GreaterThan(0.0),
            "HARM1 should have accumulated > 0 hits when singing matching pitch");
        Assert.That(partHits[1], Is.GreaterThan(0.0),
            "HARM2 should have accumulated > 0 hits — mic satisfies this part too, not just the best");
    }

    // ================================================================
    // Helpers
    // ================================================================

    private static YargFreeVocalsEngine CreateEngine(
        out List<VocalsPart> parts,
        int harm1Pitch = 60,
        int harm2Pitch = 64,
        bool isBot = false)
    {
        parts = new List<VocalsPart>
        {
            CreateVocalsPart(isHarmony: false),
            CreateVocalsPart(isHarmony: true),
        };

        AddPhraseWithPitch(parts[0], harm1Pitch, tickOffset: 0);
        AddPhraseWithPitch(parts[1], harm2Pitch, tickOffset: 0);

        var primaryChart = parts[0].CloneAsInstrumentDifficulty();
        var syncTrack = new SyncTrack(480);

        return new YargFreeVocalsEngine(primaryChart, parts, syncTrack, EngineParameters, isBot);
    }

    private static List<VocalsPart> Create3Parts()
    {
        var parts = new List<VocalsPart>
        {
            CreateVocalsPart(isHarmony: false),
            CreateVocalsPart(isHarmony: true),
            CreateVocalsPart(isHarmony: true),
        };

        AddPhraseWithPitch(parts[0], 60, tickOffset: 0);
        AddPhraseWithPitch(parts[1], 64, tickOffset: 0);
        AddPhraseWithPitch(parts[2], 67, tickOffset: 0);

        return parts;
    }

    private static VocalsPart CreateVocalsPart(bool isHarmony)
    {
        return new VocalsPart(isHarmony, new(), new(), new(), new(), new());
    }

    private static void AddPhraseWithPitch(VocalsPart part, int midiPitch, uint tickOffset)
    {
        var note = new VocalNote(NoteFlags.None, false, 0.0, 1.0, tickOffset, 480);
        var lyricNote = new VocalNote(midiPitch, 0, VocalNoteType.Lyric, 0.0, 0.5, tickOffset, 240);
        note.AddChildNote(lyricNote);
        var lyrics = new List<LyricEvent>
        {
            new LyricEvent(LyricSymbolFlags.None, "Test", 0.0, tickOffset)
        };
        part.NotePhrases.Add(new VocalsPhrase(0.0, 1.0, tickOffset, 480, note, lyrics));
    }

    /// <summary>
    /// Phrase with multiple pitched child notes at arbitrary tick spans
    /// (absolute ticks). Used to stage tie/switch sequences for
    /// CurrentTargetHarmonyIndex hysteresis tests: child spans, not the parent
    /// span, determine when each note is matchable.
    /// </summary>
    private static void AddPitchedPhraseWithChildren(
        VocalsPart part, uint tickOffset, uint parentTickLength,
        params (int pitch, uint startTick, uint length)[] children)
    {
        var note = new VocalNote(NoteFlags.None, false, 0.0, 1.0, tickOffset, parentTickLength);
        foreach (var (pitch, startTick, length) in children)
        {
            // 480 tpqn @ 120 BPM → 1s = 960 ticks; keep Time spans consistent
            // with the tick spans so PitchAtSongTime stays well-defined.
            double time = startTick / 960.0;
            double timeLength = length / 960.0;
            note.AddChildNote(new VocalNote(pitch, 0, VocalNoteType.Lyric,
                time, timeLength, startTick, length));
        }
        var lyrics = new List<LyricEvent>
        {
            new LyricEvent(LyricSymbolFlags.None, "Test", 0.0, tickOffset)
        };
        part.NotePhrases.Add(new VocalsPhrase(
            0.0, 1.0, tickOffset, parentTickLength, note, lyrics));
    }

    /// <summary>
    /// Feeds a constant pitch at ~60 fps across [startTime, endTime], updating
    /// the engine per frame (mirrors the coordinator tests' FeedPitches shape)
    /// so CheckSingingHit's per-frame target-part selection runs in real time.
    /// </summary>
    private static void DrivePitch(
        YargFreeVocalsEngine engine, double startTime, double endTime, float pitch)
    {
        for (double t = startTime + 1.0 / 60.0; t <= endTime; t += 1.0 / 60.0)
        {
            var input = GameInput.Create(t, VocalsAction.Pitch, pitch);
            engine.QueueInput(ref input);
            engine.Update(t);
        }
    }

    /// <summary>
    /// Invokes the engine's real CanVocalNoteBeHit method via reflection.
    /// Sets PitchSang and CurrentTime on the engine instance before calling.
    /// </summary>
    private static (bool hit, float hitPercent) InvokeCanVocalNoteBeHit(
        YargFreeVocalsEngine engine, VocalNote note, float sungPitch)
    {
        // Set PitchSang (protected setter on VocalsEngine)
        PitchSangProperty.SetValue(engine, sungPitch);

        // Set CurrentTime so note.PitchAtSongTime returns the note's pitch.
        // At time 0, a note at time 0 with timeLength > 0 returns its Pitch.
        CurrentTimeProperty.SetValue(engine, 0.0);

        // Call CanVocalNoteBeHit(note, out float hitPercent)
        var hitPercent = new object[2];
        hitPercent[0] = note;
        hitPercent[1] = 0f; // default

        var result = (bool)CanVocalNoteBeHitMethod.Invoke(engine, hitPercent)!;

        return (result, (float)hitPercent[1]);
    }

    /// <summary>
    /// Invokes CanVocalNoteBeHit on a YargVocalsEngine via reflection.
    /// </summary>
    private static (bool hit, float hitPercent) InvokeCanVocalNoteBeHitStd(
        YargVocalsEngine engine, VocalNote note, float sungPitch)
    {
        var canHitMethod = typeof(YargVocalsEngine).GetMethod("CanVocalNoteBeHit",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Could not find CanVocalNoteBeHit on YargVocalsEngine");

        PitchSangProperty.SetValue(engine, sungPitch);
        CurrentTimeProperty.SetValue(engine, 0.0);

        var args = new object[2];
        args[0] = note;
        args[1] = 0f;

        var result = (bool)canHitMethod.Invoke(engine, args)!;
        return (result, (float)args[1]);
    }
}
