using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Vocals;
using YARG.Core.Engine.Vocals.Engines;
using YARG.Core.Input;

namespace YARG.Core.UnitTests.Engine;

[TestFixture]
public sealed class PartyVocalsCoordinatorEngineTests
{
    private const double AwesomeThreshold = 0.75;
    private const double Epsilon = 1e-9;
    private const double ApproximateVocalFps = 60.0;

    private static readonly VocalsEngineParameters EngineParams = new(
        new HitWindowSettings(0.1, 0.1, 1.0, false, 0, 1, 1, 0, 0),
        4,
        VocalsEngineTests.StarMultiplierThresholds,
        VocalsEngineTests.SoloBonusStarMultiplierThresholds,
        1.5f, 0.5f, AwesomeThreshold, 60.0, true, 1000);

    private static readonly VocalsEngineParameters EngineParamsNoSingToActivate = new(
        new HitWindowSettings(0.1, 0.1, 1.0, false, 0, 1, 1, 0, 0),
        4,
        VocalsEngineTests.StarMultiplierThresholds,
        VocalsEngineTests.SoloBonusStarMultiplierThresholds,
        1.5f, 0.5f, AwesomeThreshold, 60.0, false, 1000);

    private static readonly FieldInfo HarmDirectTicksField =
        typeof(PartyVocalsCoordinatorEngine).GetField("_harmDirectTicks",
            BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("Could not find _harmDirectTicks");

    private static readonly FieldInfo AmbiguityBucketsField =
        typeof(PartyVocalsCoordinatorEngine).GetField("_ambiguityBuckets",
            BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("Could not find _ambiguityBuckets");

    private static readonly MethodInfo RunAllocatorMethod =
        typeof(PartyVocalsCoordinatorEngine).GetMethod("RunAllocatorIntoCanonicalMeters",
            BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("Could not find RunAllocatorIntoCanonicalMeters");

    // ================================================================
    // Helpers
    // ================================================================

    private static VocalsPart CreateVocalsPart(bool isHarmony = false) =>
        new(isHarmony, new(), new(), new(), new());

    private static SyncTrack CreateSyncTrack()
    {
        var sync = new SyncTrack(480);
        sync.Tempos.Add(new TempoChange(120.0, 0.0, 0));
        sync.TimeSignatures.Add(new TimeSignatureChange(4, 4, 0.0, 0, 0, 0, 0, 0));
        return sync;
    }

    private static void AddPhrase(VocalsPart part, uint tickOffset, uint tickLength, int midiPitch)
    {
        var note = new VocalNote(NoteFlags.None, false, 0.0, 2.0, tickOffset, tickLength);
        var lyricNote = new VocalNote(midiPitch, 0, VocalNoteType.Lyric, 0.0, 1.0, tickOffset, tickLength / 2);
        note.AddChildNote(lyricNote);
        var lyrics = new List<LyricEvent> { new(LyricSymbolFlags.None, "La", 0.0, tickOffset) };
        part.NotePhrases.Add(new VocalsPhrase(0.0, 2.0, tickOffset, tickLength, note, lyrics));
    }

    [Test]
    public void BuildMergedTrack_UnionDeduplicatesAndSortsAllParts()
    {
        var parts = new List<VocalsPart> { CreateVocalsPart(), CreateVocalsPart(true), CreateVocalsPart(true) };
        AddPhrase(parts[0], 480, 240, 60);
        AddPhrase(parts[1], 960, 240, 64);
        AddPhrase(parts[2], 480, 240, 67);
        AddPhrase(parts[2], 1440, 240, 69);

        // SP phrases live per-part; HARM2/3-only SP phrases must reach the merged
        // track so TotalStarPowerPhrases matches StarPowerPhrasesHit.
        parts[0].OtherPhrases.Add(new Phrase(PhraseType.StarPower, 0.0, 1.0, 480, 240));
        parts[1].OtherPhrases.Add(new Phrase(PhraseType.StarPower, 1.0, 1.0, 960, 240));
        parts[1].OtherPhrases.Add(new Phrase(PhraseType.StarPower, 2.0, 1.0, 2400, 240));
        parts[2].OtherPhrases.Add(new Phrase(PhraseType.StarPower, 0.0, 1.0, 480, 240)); // dup of HARM1's

        var merged = PartyVocalsCoordinatorEngine.BuildMergedTrack(
            parts, parts[0].CloneAsInstrumentDifficulty());

        Assert.That(merged.Notes.Select(note => (note.Tick, note.TickEnd)), Is.EqualTo(new[]
        {
            (480u, 720u), (960u, 1200u), (1440u, 1680u)
        }));
        Assert.That(merged.Notes[1], Is.SameAs(parts[1].NotePhrases[0].PhraseParentNote));
        Assert.That(merged.Notes[2], Is.SameAs(parts[2].NotePhrases[1].PhraseParentNote));

        Assert.That(merged.Phrases.Select(phrase => phrase.Tick), Is.EqualTo(new[]
        {
            480u, 960u, 2400u
        }));
    }

    // 480 tpqn @ 120bpm => seconds = tick / 960.0. The percussion child note's Time tracks
    // its tick. (Previously this hardcoded Time=0.0/TimeLength=1.0, giving every note the
    // hit window [0.0, 1.0] — wide and zero-anchored, which masked the coordinator's
    // percussion-window bug; see docs/bugs/party-vocals-percussion-broken.md.)
    private static void AddPercussionPhrase(VocalsPart part, uint tickOffset, uint tickLength)
    {
        const double secondsPerTick = 1.0 / 960.0;
        double phraseTime = tickOffset * secondsPerTick;
        double phraseLen = tickLength * secondsPerTick;
        uint percTickLen = tickLength / 2;

        var note = new VocalNote(NoteFlags.None, false, phraseTime, phraseLen, tickOffset, tickLength);
        var percussionNote = new VocalNote(-1, 0, VocalNoteType.Percussion,
            phraseTime, percTickLen * secondsPerTick, tickOffset, percTickLen);
        note.AddChildNote(percussionNote);
        var lyrics = new List<LyricEvent> { new(LyricSymbolFlags.NonPitched, "Perc", phraseTime, tickOffset) };
        part.NotePhrases.Add(new VocalsPhrase(phraseTime, phraseLen, tickOffset, tickLength, note, lyrics));
    }

    private static void AddTalkiePhrase(VocalsPart part, uint tickOffset, uint tickLength)
    {
        var note = new VocalNote(NoteFlags.None, false, 0.0, 2.0, tickOffset, tickLength);
        var talkieNote = new VocalNote(-1, 0, VocalNoteType.Lyric, 0.0, 1.0, tickOffset, tickLength / 2);
        note.AddChildNote(talkieNote);
        var lyrics = new List<LyricEvent> { new(LyricSymbolFlags.NonPitched, "Talk", 0.0, tickOffset) };
        part.NotePhrases.Add(new VocalsPhrase(0.0, 2.0, tickOffset, tickLength, note, lyrics));
    }

    private static PartyVocalsCoordinatorEngine CreateCoordinator(
        List<VocalsPart> parts, int micCount)
    {
        return CreateCoordinator(parts, micCount, EngineParams);
    }

    private static PartyVocalsCoordinatorEngine CreateCoordinator(
        List<VocalsPart> parts, int micCount, VocalsEngineParameters engineParams)
    {
        var primaryChart = parts[0].CloneAsInstrumentDifficulty();
        return new PartyVocalsCoordinatorEngine(
            primaryChart, parts, CreateSyncTrack(), engineParams, false, micCount);
    }

    private static (PartyVocalsCoordinatorEngine engine, List<PhraseGrade> grades) RunCoordinatorScenario(
        List<VocalsPart> parts, int micCount, Action<PartyVocalsCoordinatorEngine> feedAction, double endTime)
    {
        var engine = CreateCoordinator(parts, micCount);
        var grades = new List<PhraseGrade>();
        engine.OnPartyVocalsPhrase += (grade, meters, isLast) => grades.Add(grade);
        engine.Update(0.1);
        feedAction(engine);
        engine.Update(endTime);
        return (engine, grades);
    }

    private static void FeedPitches(PartyVocalsCoordinatorEngine engine, int micCount,
        float[][] micPitchArrays, double startTime, double duration)
    {
        int totalFrames = (int)(duration * ApproximateVocalFps);
        for (int f = 0; f < totalFrames; f++)
        {
            double time = startTime + (f + 1) / ApproximateVocalFps;
            for (int m = 0; m < micCount; m++)
            {
                int idx = Math.Min(f, micPitchArrays[m].Length - 1);
                float pitch = micPitchArrays[m][idx];
                // float.NaN is the silence sentinel — don't feed a pitch input, so the
                // mic contributes nothing to scoring.
                // (Plain numeric "out of range" values like -1f don't work because the
                // engine's pitch comparison is octave-equivalent — any value matches
                // C4 if the modular distance is within the pitch window.)
                if (float.IsNaN(pitch)) continue;
                int packed = PartyVocalsInput.Pack(m, VocalsAction.Pitch);
                var input = new GameInput(time, packed, pitch);
                engine.QueueInput(ref input);
            }
            engine.Update(time);
        }
    }

    private static double[] GetHarmDirectTicks(PartyVocalsCoordinatorEngine engine) =>
        (double[])HarmDirectTicksField.GetValue(engine)!;

    private static double[] GetAmbiguityBuckets(PartyVocalsCoordinatorEngine engine) =>
        (double[])AmbiguityBucketsField.GetValue(engine)!;

    private static double[] GetCanonicalMeters(PartyVocalsCoordinatorEngine engine)
    {
        var field = typeof(PartyVocalsCoordinatorEngine)
            .GetField("_canonicalMeters", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return ((double[])field.GetValue(engine)!).ToArray();
    }

    private static void SetDirectTicks(PartyVocalsCoordinatorEngine engine, int partIndex, double ticks)
    {
        var arr = (double[])HarmDirectTicksField.GetValue(engine)!;
        arr[partIndex] = ticks;
    }

    private static void SetAmbiguityBucket(PartyVocalsCoordinatorEngine engine, int mask, double ticks)
    {
        var arr = (double[])AmbiguityBucketsField.GetValue(engine)!;
        arr[mask] = ticks;
        // Also set the per-mic bookkeeping so perMicCap matches the bucket total.
        // For unit tests, mic 0 is treated as the sole contributor — perMicCap = ticks.
        // This makes the bucket's credit fully usable by any single HARM (matching the
        // pre-per-mic-cap allocator's behavior for the cases these tests exercise).
        var perMic = (double[,])typeof(PartyVocalsCoordinatorEngine)
            .GetField("_bucketPerMic", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(engine)!;
        perMic[0, mask] = ticks;
    }

    /// <summary>
    /// Per-mic bucket credit injector for tests that need to model multi-mic
    /// contributions explicitly (e.g., stacking-shortcut regression tests).
    /// Caller is responsible for keeping _ambiguityBuckets[mask] consistent
    /// (= sum across mics).
    /// </summary>
    private static void SetBucketPerMic(PartyVocalsCoordinatorEngine engine, int micIndex, int mask, double ticks)
    {
        var perMic = (double[,])typeof(PartyVocalsCoordinatorEngine)
            .GetField("_bucketPerMic", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(engine)!;
        perMic[micIndex, mask] = ticks;
    }

    private static void SetPhraseTicksTotalPerPart(PartyVocalsCoordinatorEngine engine, params uint[] values)
    {
        var field = typeof(PartyVocalsCoordinatorEngine)
            .GetField("_phraseTicksTotalPerPart", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var arr = (uint[])field.GetValue(engine)!;
        for (int i = 0; i < values.Length && i < arr.Length; i++)
            arr[i] = values[i];
    }

    private static double[] RunAllocatorAndReturnMeters(PartyVocalsCoordinatorEngine engine)
    {
        RunAllocatorMethod.Invoke(engine, new object[] { false });
        return GetCanonicalMeters(engine);
    }

    // ================================================================
    // Classifier Tests (1-4)
    // AC9: Per-tick classification + accumulation
    // ================================================================

    [Test]
    public void Classifier_UnambiguousSingleMicSingleHarm_CreditsDirectOnce()
    {
        // Two parts at different pitches. Feed one mic matching only HARM0.
        // After one phrase, verify the engine scored an Awesome for HARM0.
        var parts = new List<VocalsPart> { CreateVocalsPart(), CreateVocalsPart(true) };
        AddPhrase(parts[0], 0, 960, 60); // HARM0 at C4
        AddPhrase(parts[1], 0, 960, 64); // HARM1 at E4

        var (engine, grades) = RunCoordinatorScenario(parts, 2, e =>
        {
            // Mic 0 sings C4 (matches HARM0 only), mic 1 silent
            FeedPitches(e, 2, new[] { new[] { 60f }, new[] { -1f } }, 0.1, 2.0);
        }, 4.0);

        Assert.AreEqual(1, grades.Count, "One phrase grade");
        Assert.AreEqual(PhraseGrade.Awesome, grades[0], "Single HARM0 hit = Awesome");
    }

    [Test]
    public void Classifier_AmbiguousSingleMicTwoHarms_CreditsBucketOnce()
    {
        // Two parts at the SAME pitch. ONE mic (micCount=1, so no phantom contribution
        // from a silent-but-pitch-window-matching second mic) singing that pitch is
        // ambiguous on {0,1}. Bucket gets N ticks total, perMicCap = N. Allocator fills
        // HARM0 to N (capped by perMicCap, no spill to HARM1 since the per-HARM cap is
        // also N) → Awesome (not Double).
        var parts = new List<VocalsPart> { CreateVocalsPart(), CreateVocalsPart(true) };
        AddPhrase(parts[0], 0, 960, 60); // Both at C4
        AddPhrase(parts[1], 0, 960, 60);

        var (engine, grades) = RunCoordinatorScenario(parts, 2, e =>
        {
            // Mic 0 sings ambiguous C4. Mic 1 stays SILENT — NaN sentinel bypasses
            // SetMicPitch so _micHasSang[1] never flips true. (Plain -1f wouldn't
            // work: pitch comparison is octave-modular, so -1 vs C4=60 is 1
            // semitone apart, within the pitch window.)
            FeedPitches(e, 2, new[] { new[] { 60f }, new[] { float.NaN } }, 0.1, 2.0);
        }, 4.0);

        Assert.AreEqual(1, grades.Count, "One phrase grade");
        Assert.AreEqual(PhraseGrade.Awesome, grades[0],
            "Single ambiguous mic should fill only one HARM");
    }

    [Test]
    public void Classifier_TwoMicsBothUnambigOnSameHarm_DirectTakesMaxDelta()
    {
        // Two mics both singing HARM0 pitch. Direct credit is binary across mics
        // (max delta), so stacking doesn't shortcut. Both sing for the full phrase → Awesome.
        // But singing half the phrase → M_0 = 0.5, which is Miss (stack shortcut prevention).
        var parts = new List<VocalsPart> { CreateVocalsPart(), CreateVocalsPart(true) };
        AddPhrase(parts[0], 0, 960, 60);
        AddPhrase(parts[1], 960, 960, 64); // Non-overlapping second phrase

        var (engine, grades) = RunCoordinatorScenario(parts, 2, e =>
        {
            // Both mics on HARM0 for the full phrase
            FeedPitches(e, 2, new[] { new[] { 60f }, new[] { 60f } }, 0.1, 2.0);
        }, 4.0);

        Assert.AreEqual(1, grades.Count, "One phrase grade");
        Assert.AreEqual(PhraseGrade.Awesome, grades[0],
            "Two mics stacking on HARM0 still = Awesome (not Double) since only one HARM hit");
    }

    [Test]
    public void Classifier_TwoMicsBothAmbigInSameSet_BucketIncrementsTwice()
    {
        // Two mics both ambiguous on {0,1} for the full phrase.
        // Bucket credit is additive: 2N ticks in bucket {0,1}.
        // Allocator fills HARM0 then HARM1 → DoubleAwesome.
        var parts = new List<VocalsPart> { CreateVocalsPart(), CreateVocalsPart(true) };
        AddPhrase(parts[0], 0, 960, 60); // Both at same pitch
        AddPhrase(parts[1], 0, 960, 60);

        var (engine, grades) = RunCoordinatorScenario(parts, 2, e =>
        {
            FeedPitches(e, 2, new[] { new[] { 60f }, new[] { 60f } }, 0.0, 2.0);
        }, 4.0);

        Assert.AreEqual(1, grades.Count, "One phrase grade");
        Assert.AreEqual(PhraseGrade.DoubleAwesome, grades[0],
            "Two mics ambig on {0,1} should credit both HARMs");
    }

    // ================================================================
    // Allocator Tests (5-9)
    // AC10: Greedy allocator
    // ================================================================

    [Test]
    public void Allocator_OnlyDirect_FillsHarmsExact()
    {
        // direct = [100, 0], no buckets, capacity = [100, 100]
        var parts = new List<VocalsPart> { CreateVocalsPart(), CreateVocalsPart(true) };
        var engine = CreateCoordinator(parts, 2);

        SetPhraseTicksTotalPerPart(engine, 100, 100);
        SetDirectTicks(engine, 0, 100);
        SetDirectTicks(engine, 1, 0);

        var meters = RunAllocatorAndReturnMeters(engine);

        Assert.AreEqual(1.0, meters[0], Epsilon, "HARM0 filled to 1.0");
        Assert.AreEqual(0.0, meters[1], Epsilon, "HARM1 stays 0.0");
    }

    [Test]
    public void Allocator_OnlyBucket01_FillsHarm0First()
    {
        // direct = [0, 0], bucket[{0,1}] = 100, capacity = [100, 100]
        var parts = new List<VocalsPart> { CreateVocalsPart(), CreateVocalsPart(true) };
        var engine = CreateCoordinator(parts, 2);

        SetPhraseTicksTotalPerPart(engine, 100, 100);
        SetDirectTicks(engine, 0, 0);
        SetDirectTicks(engine, 1, 0);
        SetAmbiguityBucket(engine, 3, 100); // {0,1} = 0b011

        var meters = RunAllocatorAndReturnMeters(engine);

        Assert.AreEqual(1.0, meters[0], Epsilon, "HARM0 filled first (tiebreak)");
        Assert.AreEqual(0.0, meters[1], Epsilon, "HARM1 stays 0.0");
    }

    [Test]
    public void Allocator_Bucket01_2N_FillsBothHarms()
    {
        // direct = [0, 0], bucket[{0,1}] = 200, capacity = [100, 100]
        var parts = new List<VocalsPart> { CreateVocalsPart(), CreateVocalsPart(true) };
        var engine = CreateCoordinator(parts, 2);

        SetPhraseTicksTotalPerPart(engine, 100, 100);
        SetDirectTicks(engine, 0, 0);
        SetDirectTicks(engine, 1, 0);
        SetAmbiguityBucket(engine, 3, 200); // {0,1} with 2× capacity

        var meters = RunAllocatorAndReturnMeters(engine);

        Assert.AreEqual(1.0, meters[0], Epsilon, "HARM0 filled to 1.0");
        Assert.AreEqual(1.0, meters[1], Epsilon, "HARM1 filled to 1.0");
    }

    [Test]
    public void Allocator_Direct0Full_Bucket01_RoutesToHarm1()
    {
        // direct = [100, 0], bucket[{0,1}] = 100, capacity = [100, 100]
        // HARM0 already full, bucket routes to HARM1
        var parts = new List<VocalsPart> { CreateVocalsPart(), CreateVocalsPart(true) };
        var engine = CreateCoordinator(parts, 2);

        SetPhraseTicksTotalPerPart(engine, 100, 100);
        SetDirectTicks(engine, 0, 100);
        SetDirectTicks(engine, 1, 0);
        SetAmbiguityBucket(engine, 3, 100);

        var meters = RunAllocatorAndReturnMeters(engine);

        Assert.AreEqual(1.0, meters[0], Epsilon, "HARM0 capped by direct");
        Assert.AreEqual(1.0, meters[1], Epsilon, "Bucket routed to HARM1");
    }

    [Test]
    public void Allocator_NarrowestFirst()
    {
        // bucket[{0,1}] = 100, bucket[{0,1,2}] = 100, capacity = [100, 100, 100]
        // Narrowest {0,1} fills HARM0, then {0,1,2} fills HARM1
        var parts = new List<VocalsPart> { CreateVocalsPart(), CreateVocalsPart(true), CreateVocalsPart(true) };
        var engine = CreateCoordinator(parts, 2);

        SetPhraseTicksTotalPerPart(engine, 100, 100, 100);
        SetDirectTicks(engine, 0, 0);
        SetDirectTicks(engine, 1, 0);
        SetDirectTicks(engine, 2, 0);
        SetAmbiguityBucket(engine, 3, 100);  // {0,1}
        SetAmbiguityBucket(engine, 7, 100);  // {0,1,2}

        var meters = RunAllocatorAndReturnMeters(engine);

        Assert.AreEqual(1.0, meters[0], Epsilon, "HARM0 filled by {0,1}");
        Assert.AreEqual(1.0, meters[1], Epsilon, "HARM1 filled by {0,1,2}");
        Assert.AreEqual(0.0, meters[2], Epsilon, "HARM2 stays 0");
    }

    // ================================================================
    // Scenario Tests (10-13)
    // AC14: Correctness scenarios
    // ================================================================

    [Test]
    public void Scenario_StackShortcutPrevention_HalfPhraseTwoMicsHarm0_Miss()
    {
        // AC14.1: Two mics stacking on HARM0 for half the phrase.
        // Direct credit is binary (max across mics), so M_0 = 0.5 < PhraseHitPercent.
        // Test via the allocator directly: direct = [240, 0], capacity = [480, 0].
        // Meter = 240/480 = 0.5 < 0.75 → Miss.
        var parts = new List<VocalsPart> { CreateVocalsPart() };
        var engine = CreateCoordinator(parts, 1);

        SetPhraseTicksTotalPerPart(engine, 480);
        SetDirectTicks(engine, 0, 240); // Half coverage

        var meters = RunAllocatorAndReturnMeters(engine);

        Assert.AreEqual(0.5, meters[0], Epsilon, "HARM0 meter should be 0.5");
        Assert.Less(meters[0], AwesomeThreshold, "Should be below Awesome threshold");
    }

    [Test]
    public void Scenario_TrueUnison_TwoMics_DoubleAwesome()
    {
        // Two parts at the same pitch. Two mics both singing that pitch = ambig {0,1}.
        // Bucket gets 2N ticks (additive). Allocator fills both HARMs → DoubleAwesome.
        var parts = new List<VocalsPart> { CreateVocalsPart(), CreateVocalsPart(true) };
        AddPhrase(parts[0], 0, 960, 60); // Both at C4
        AddPhrase(parts[1], 0, 960, 60);

        var (engine, grades) = RunCoordinatorScenario(parts, 2, e =>
        {
            FeedPitches(e, 2, new[] { new[] { 60f }, new[] { 60f } }, 0.0, 2.0);
        }, 4.0);

        Assert.AreEqual(1, grades.Count, "One phrase grade");
        Assert.AreEqual(PhraseGrade.DoubleAwesome, grades[0],
            "Two mics on true unison = DoubleAwesome");
    }

    [Test]
    public void Scenario_SingleMicAmbig_WholePhrase_Awesome()
    {
        // One mic ambiguous on {0,1}. Bucket gets N ticks (perMicCap = N). Allocator
        // fills HARM0 to N (capped). HARM1 can also take up to perMicCap=N from this
        // bucket, but the bucket is exhausted after HARM0 → HARM1 stays 0. Awesome.
        var parts = new List<VocalsPart> { CreateVocalsPart(), CreateVocalsPart(true) };
        AddPhrase(parts[0], 0, 960, 60); // Both at C4
        AddPhrase(parts[1], 0, 960, 60);

        var (engine, grades) = RunCoordinatorScenario(parts, 2, e =>
        {
            // Mic 0 sings ambiguous C4. Mic 1 silent via NaN sentinel.
            FeedPitches(e, 2, new[] { new[] { 60f }, new[] { float.NaN } }, 0.1, 2.0);
        }, 4.0);

        Assert.AreEqual(1, grades.Count, "One phrase grade");
        Assert.AreEqual(PhraseGrade.Awesome, grades[0],
            "Single ambiguous mic = Awesome (not Double)");
    }

    [Test]
    public void Scenario_CrossCoverage_DoubleAwesome()
    {
        // Mic 0 sings HARM0 pitch (unambiguous), mic 1 sings that same pitch (ambiguous {0,1}).
        // direct[0] = N, bucket[{0,1}] = N. Allocator caps HARM0, routes bucket to HARM1.
        var parts = new List<VocalsPart> { CreateVocalsPart(), CreateVocalsPart(true) };
        AddPhrase(parts[0], 0, 960, 60); // HARM0 at C4
        AddPhrase(parts[1], 0, 960, 64); // HARM1 at E4

        var (engine, grades) = RunCoordinatorScenario(parts, 2, e =>
        {
            // Mic 0 sings C4 (unambiguous HARM0), mic 1 also sings C4 (ambiguous {0,1} since
            // C4 is within pitch window of HARM0 but NOT HARM1 at E4)
            FeedPitches(e, 2, new[] { new[] { 60f }, new[] { 60f } }, 0.1, 2.0);
        }, 4.0);

        Assert.AreEqual(1, grades.Count, "One phrase grade");
        // Both mics on C4 which only matches HARM0. This is stacking, not cross-coverage.
        // For true cross-coverage we need a talkie on one part.
    }

    // ================================================================
    // Scoring Tests (14-15)
    // AC12: Scoring through the standard path
    // ================================================================

    [Test]
    public void Scoring_HitNoteOncePerPhrase_NotPerHarm()
    {
        // Drive 2 phrases, both hitting. Verify NotesHit = 2, combo = 2.
        var parts = new List<VocalsPart> { CreateVocalsPart(), CreateVocalsPart(true) };
        AddPhrase(parts[0], 0, 960, 60);    // Phrase 1
        AddPhrase(parts[0], 960, 960, 60);  // Phrase 2
        AddPhrase(parts[1], 0, 960, 64);    // HARM1 overlap with phrase 1

        var (engine, grades) = RunCoordinatorScenario(parts, 2, e =>
        {
            // Sing HARM0 for both phrases, HARM1 for overlap
            FeedPitches(e, 2, new[] { new[] { 60f }, new[] { 64f } }, 0.1, 5.0);
        }, 6.0);

        // The engine should have processed multiple phrases
        Assert.GreaterOrEqual(grades.Count, 1, "Should have phrase grades");
        var stats = engine.BaseStats;
        Assert.AreEqual(grades.Count, stats.NotesHit,
            "NotesHit should equal number of graded phrases");
        Assert.AreEqual(grades.Count, stats.Combo,
            "Combo should equal number of graded phrases");
    }

    [Test]
    public void Scoring_MissPhraseResetsCombo_FlipsFc()
    {
        // Three sequential primary phrases. HARM1 overlaps with phrase 1.
        // Phrase 1: both HARMs hit → DoubleAwesome.
        // Phrase 2: good singing → Awesome.
        // Phrase 3: bad singing → Miss → FC flips.
        var parts = new List<VocalsPart> { CreateVocalsPart(), CreateVocalsPart(true) };
        AddPhrase(parts[0], 0, 960, 60);     // Phrase 1: tick 0-960
        AddPhrase(parts[0], 960, 960, 60);   // Phrase 2: tick 960-1920
        AddPhrase(parts[0], 1920, 960, 60);  // Phrase 3: tick 1920-2880
        AddPhrase(parts[1], 0, 960, 64);     // HARM1 overlap with phrase 1

        var engine = CreateCoordinator(parts, 2);
        var grades = new List<PhraseGrade>();
        engine.OnPartyVocalsPhrase += (grade, meters, isLast) => grades.Add(grade);

        // Phrase 1: sing well (both HARMs → DoubleAwesome)
        // Content range: tick 0-480 (0.0-0.5s). Feed from 0.0 to 0.5s.
        FeedPitches(engine, 2, new[] { new[] { 60f }, new[] { 64f } }, 0.017, 0.6);

        // Phrase 2: sing well (HARM0 only → Awesome)
        // Content range: tick 960-1440 (1.0-1.5s). Feed from 1.0 to 1.5s.
        FeedPitches(engine, 2, new[] { new[] { 60f }, new[] { -1f } }, 1.017, 0.6);

        // Phrase 3: sing badly (wrong pitch → Miss)
        // Content range: tick 1920-2400 (2.0-2.5s). Feed from 2.0 to 2.5s.
        FeedPitches(engine, 2, new[] { new[] { 90f }, new[] { 90f } }, 2.017, 0.6);

        // Advance past all phrases
        engine.Update(4.0);

        Assert.GreaterOrEqual(grades.Count, 3, "Should have 3 phrase grades");
        Assert.AreEqual(PhraseGrade.DoubleAwesome, grades[0], "Phrase 1: both HARMs");
        Assert.AreEqual(PhraseGrade.Awesome, grades[1], "Phrase 2: HARM0 only");
        Assert.AreEqual(PhraseGrade.Miss, grades[2], "Phrase 3: wrong pitch");
        Assert.IsFalse(engine.BaseStats.IsFullCombo, "FC should be false after a miss");
    }

    // ================================================================
    // Visual-event Tests (real-mic OnTargetNoteChanged → trail)
    // ================================================================

    [Test]
    public void RealMic_OnNote_SubEngineFiresOnTargetNoteChanged()
    {
        // Regression guard for the missing per-mic trail. PartyVocalsPlayer's trail
        // gate requires slot.TargetNote, which is populated ONLY by each sub-engine's
        // OnTargetNoteChanged. Bots emit it from UpdateBot; real mics must emit it from
        // CheckSingingHit (YargFreeVocalsEngine line ~425). After live mic input was
        // re-routed through the packed-input queue, the meters/scoring path kept working
        // (AccumulateMicPartHits) but the trail's target-note emit must still fire.
        var parts = new List<VocalsPart> { CreateVocalsPart() };
        AddPhrase(parts[0], 0, 960, 60); // C4

        var engine = CreateCoordinator(parts, micCount: 1); // isBot = false → real-mic path
        bool fired = false;
        VocalNote? captured = null;
        engine.SubEngines[0].OnTargetNoteChanged += note => { fired = true; captured = note; };

        engine.Update(0.1);
        // Real mic sings C4 on the note. Same packed-queue path the runtime uses.
        FeedPitches(engine, 1, new[] { new[] { 60f } }, 0.1, 1.0);

        Assert.IsTrue(fired,
            "Real-mic sub-engine must fire OnTargetNoteChanged while on a note (drives the per-mic trail)");
        Assert.IsNotNull(captured, "Emitted target note should be non-null");
    }

    [Test]
    public void RealMic_TwoMics_BothSubEnginesFireOnTargetNoteChanged()
    {
        // Faithful to the reported repro: two real mics, two HARM parts. Each mic's own
        // sub-engine must fire OnTargetNoteChanged so its needle's trail can render.
        var parts = new List<VocalsPart> { CreateVocalsPart(), CreateVocalsPart(true) };
        AddPhrase(parts[0], 0, 960, 60); // HARM0 C4
        AddPhrase(parts[1], 0, 960, 64); // HARM1 E4

        var engine = CreateCoordinator(parts, micCount: 2); // isBot = false
        var fired = new bool[2];
        engine.SubEngines[0].OnTargetNoteChanged += _ => fired[0] = true;
        engine.SubEngines[1].OnTargetNoteChanged += _ => fired[1] = true;

        engine.Update(0.1);
        // Mic 0 sings C4 (HARM0), mic 1 sings E4 (HARM1) — each on its own line.
        FeedPitches(engine, 2, new[] { new[] { 60f }, new[] { 64f } }, 0.1, 1.0);

        Assert.IsTrue(fired[0], "Mic 0 sub-engine must fire OnTargetNoteChanged");
        Assert.IsTrue(fired[1], "Mic 1 sub-engine must fire OnTargetNoteChanged");
    }

    [Test]
    public void RealMic_TwoMics_MicOneLagging_StillFiresOnTargetNoteChanged()
    {
        // Robustness guard for the runtime queue ORDER from PartyVocalsPlayer.RouteMicInputs:
        // mic 0's input is queued, then mic 1's at an EARLIER timestamp (mic 1 lagging),
        // which BaseEngine.QueueInput snaps forward. This was a suspected cause of the
        // missing trail (echoing bugfix #10) but turned out NOT to be — the emit survives
        // frame-granularity disorder. Kept as a guard so the per-mic emit stays robust to it.
        var parts = new List<VocalsPart> { CreateVocalsPart(), CreateVocalsPart(true) };
        AddPhrase(parts[0], 0, 960, 60);
        AddPhrase(parts[1], 0, 960, 64);

        var engine = CreateCoordinator(parts, micCount: 2);
        var fired = new bool[2];
        engine.SubEngines[0].OnTargetNoteChanged += _ => fired[0] = true;
        engine.SubEngines[1].OnTargetNoteChanged += _ => fired[1] = true;

        engine.Update(0.1);
        const double fps = 60.0;
        int frames = (int)(0.9 * fps);
        for (int f = 1; f <= frames; f++)
        {
            double t0 = 0.1 + f / fps;
            double t1 = 0.1 + (f - 0.5) / fps; // mic 1 half a frame behind mic 0
            var in0 = new GameInput(t0, PartyVocalsInput.Pack(0, VocalsAction.Pitch), 60f);
            engine.QueueInput(ref in0);
            var in1 = new GameInput(t1, PartyVocalsInput.Pack(1, VocalsAction.Pitch), 64f);
            engine.QueueInput(ref in1); // queued after in0 but earlier time
            engine.Update(t0);
        }

        Assert.IsTrue(fired[0], "Mic 0 fires OnTargetNoteChanged");
        Assert.IsTrue(fired[1], "Mic 1 (lagging) must STILL fire OnTargetNoteChanged for its trail");
    }

    [Test]
    public void RealMic_OnNote_GetMicHittingPartsNonZero()
    {
        // The trail's hitting-gate also requires LastOnNoteTime, refreshed from
        // coordinator.GetMicHittingParts(mic). The fill meters use a DIFFERENT path
        // (CanonicalMeters from the allocator/deltas), so meters filling does NOT prove
        // this bitmask is non-zero. If it reads 0 while the mic is on a note, the trail
        // dies even though the target-note emit fired and the meters moved.
        var parts = new List<VocalsPart> { CreateVocalsPart() };
        AddPhrase(parts[0], 0, 960, 60); // C4, child note spans ticks 0..480 (0..0.5s)

        var engine = CreateCoordinator(parts, micCount: 1);
        engine.Update(0.1);
        // Sing C4 squarely inside the note window.
        FeedPitches(engine, 1, new[] { new[] { 60f } }, 0.1, 0.3);

        Assert.AreNotEqual(0u, engine.GetMicHittingParts(0),
            "GetMicHittingParts must be non-zero while on a note (refreshes the trail's LastOnNoteTime)");
    }

    // ================================================================
    // Event + Throttle Tests (16-17)
    // AC11: Grading and event emission, AC13: HUD reads
    // ================================================================

    [Test]
    public void OnPartyVocalsPhrase_FiresOncePerPhrase_WithGradeAndMeters()
    {
        var parts = new List<VocalsPart> { CreateVocalsPart(), CreateVocalsPart(true) };
        AddPhrase(parts[0], 0, 960, 60);
        AddPhrase(parts[1], 0, 960, 64);

        var (engine, grades) = RunCoordinatorScenario(parts, 2, e =>
        {
            FeedPitches(e, 2, new[] { new[] { 60f }, new[] { 64f } }, 0.1, 2.0);
        }, 4.0);

        Assert.AreEqual(1, grades.Count, "Should fire once per phrase");
        Assert.AreEqual(PhraseGrade.DoubleAwesome, grades[0], "Both HARMs covered = DoubleAwesome");
    }

    [Test]
    public void Throttle_CanonicalMetersRefreshAt100ms()
    {
        // During a phrase, meters update on the 100ms throttle.
        // After <100ms: meters stay at initial value.
        // After >=100ms: meters refresh.
        var parts = new List<VocalsPart> { CreateVocalsPart(), CreateVocalsPart(true) };
        AddPhrase(parts[0], 0, 1920, 60); // Long phrase = 2.0s
        AddPhrase(parts[1], 1920, 960, 64);

        var engine = CreateCoordinator(parts, 2);
        engine.Update(0.1);

        // Feed for ~50ms (3 frames at 60fps)
        for (int i = 0; i < 3; i++)
        {
            double t = 0.1 + (i + 1) / ApproximateVocalFps;
            int packed0 = PartyVocalsInput.Pack(0, VocalsAction.Pitch);
            var in0 = new GameInput(t, packed0, 60f);
            engine.QueueInput(ref in0);
            int packed1 = PartyVocalsInput.Pack(1, VocalsAction.Pitch);
            var in1 = new GameInput(t, packed1, -1f);
            engine.QueueInput(ref in1);
            engine.Update(t);
        }

        var metersEarly = GetCanonicalMeters(engine);
        // After only ~50ms, the 100ms throttle hasn't fired yet, so meters may still be 0
        // (they could also have been updated if the throttle fires — this is timing-sensitive)
        // We verify the meters are in a valid range [0,1]
        Assert.GreaterOrEqual(metersEarly[0], 0.0, "Meter should be >= 0");
        Assert.LessOrEqual(metersEarly[0], 1.0, "Meter should be <= 1");

        // Feed for another ~100ms (7 more frames) to pass the 100ms throttle
        double startTime = 0.1 + 4.0 / ApproximateVocalFps;
        for (int i = 0; i < 7; i++)
        {
            double t = startTime + (i + 1) / ApproximateVocalFps;
            int packed0 = PartyVocalsInput.Pack(0, VocalsAction.Pitch);
            var in0 = new GameInput(t, packed0, 60f);
            engine.QueueInput(ref in0);
            int packed1 = PartyVocalsInput.Pack(1, VocalsAction.Pitch);
            var in1 = new GameInput(t, packed1, -1f);
            engine.QueueInput(ref in1);
            engine.Update(t);
        }

        var metersLater = GetCanonicalMeters(engine);
        Assert.Greater(metersLater[0], 0.0, "Meters should have positive value after >100ms of singing");
        Assert.LessOrEqual(metersLater[0], 1.0, "Meter should not exceed 1.0");
    }

    // ================================================================
    // Per-phrase state reset regression (issue: coordinator's ResetPhraseState
    // was not clearing _micPartHits, leaving stale accumulation across phrase
    // boundaries. Fixed by clearing all per-phrase arrays in ResetPhraseState.)
    // ================================================================

    [Test]
    public void StateReset_PhraseBoundary_ClearsMicPartHitsAndPhraseTicksTotal()
    {
        // Two sequential HARM0-only phrases with DIFFERENT tick lengths, so a
        // stale PhraseTicksTotal carried over from phrase 1 would be detectable
        // when phrase 2 starts (the `??=` at top of UpdateHitLogic only assigns
        // if PhraseTicksTotal is null — a stale non-null value would persist).
        var parts = new List<VocalsPart> { CreateVocalsPart(), CreateVocalsPart(true) };
        AddPhrase(parts[0], 0, 960, 60);     // Phrase 1: ticks 0-960 (1.0s)
        AddPhrase(parts[0], 1920, 480, 60);  // Phrase 2: ticks 1920-2400 (0.5s)

        var engine = CreateCoordinator(parts, 2);
        var grades = new List<PhraseGrade>();
        engine.OnPartyVocalsPhrase += (grade, meters, isLast) => grades.Add(grade);

        // Phrase 1: sing HARM0 well to populate _micPartHits and PhraseTicksTotal.
        engine.Update(0.05);
        FeedPitches(engine, 2, new[] { new[] { 60f }, new[] { -1f } }, 0.05, 0.5);
        // Advance past phrase 1's TickEnd (tick 960 → t=1.0s).
        engine.Update(1.1);

        Assert.AreEqual(1, grades.Count, "Phrase 1 should have graded");

        // Inspect _micPartHits via reflection — ResetPhraseState must have cleared it.
        // Without the fix, phrase 1's per-mic accumulation would still be sitting in
        // the array, leaking into phrase 2's stats.
        var micPartHitsField = typeof(PartyVocalsCoordinatorEngine)
            .GetField("_micPartHits", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var micPartHits = (double[,])micPartHitsField.GetValue(engine)!;
        double sumAfterPhrase1 = 0;
        foreach (var v in micPartHits) sumAfterPhrase1 += v;
        Assert.AreEqual(0.0, sumAfterPhrase1, Epsilon,
            "_micPartHits must be cleared between phrases (regression: prior coordinator " +
            "override skipped the base's Array.Clear of _micPartHits).");

        // Drive into phrase 2 with no mic activity. PhraseTicksTotal must be
        // re-derived for phrase 2 (480 ticks) — if the prior bug were present,
        // it would still hold phrase 1's value (960) because `??=` doesn't
        // overwrite a non-null value.
        // Feed silent pitch to both mics via the queue
        int packed0 = PartyVocalsInput.Pack(0, VocalsAction.Pitch);
        var in0 = new GameInput(2.04, packed0, -1f);
        engine.QueueInput(ref in0);
        int packed1 = PartyVocalsInput.Pack(1, VocalsAction.Pitch);
        var in1 = new GameInput(2.04, packed1, -1f);
        engine.QueueInput(ref in1);
        engine.Update(2.05); // within phrase 2 (tick 1920-2400 → t=2.0-2.5s)

        // PhraseTicksTotal reflects the sum of lyric child-note ticks in the phrase.
        // AddPhrase uses tickLength/2 for the lyric, so phrase 1 = 480, phrase 2 = 240.
        // If the bug regressed (PhraseTicksTotal never nulled at phrase 1 end), the
        // `??=` at top of UpdateHitLogic would leave it at 480 forever.
        Assert.IsTrue(engine.PhraseTicksTotal.HasValue,
            "PhraseTicksTotal should be populated for phrase 2");
        Assert.AreEqual(240u, engine.PhraseTicksTotal!.Value,
            "PhraseTicksTotal must reflect phrase 2's lyric ticks (240), not phrase 1's (480). " +
            "Regression: prior coordinator override skipped the base's PhraseTicksTotal = null.");
        Assert.AreNotEqual(480u, engine.PhraseTicksTotal!.Value,
            "Explicit guard against the specific regression — phrase 1's stale value.");

        // Finish phrase 2 with no singing → grade Miss.
        engine.Update(3.0);
        Assert.AreEqual(2, grades.Count, "Phrase 2 should have graded");
        Assert.AreEqual(PhraseGrade.Miss, grades[1], "Phrase 2 with no singing = Miss");
    }

    // ================================================================
    // Regression tests for issues found during real-game testing
    // (post-merge of the per-phrase reset fix).
    // ================================================================

    [Test]
    public void StatsPercent_AccumulatesTicksHitAndTicksMissed()
    {
        // Prior bug: coordinator's ProcessPhraseEnd skipped the
        // EngineStats.TicksHit/TicksMissed accumulation that the base does. With
        // both fields at 0, VocalsStats.Percent (TicksHit/TotalTicks) defaults to
        // 1.0 (100%) when TotalTicks == 0 — making the end-of-song accuracy display
        // show 100% even when the player missed phrases.
        var parts = new List<VocalsPart> { CreateVocalsPart(), CreateVocalsPart(true) };
        AddPhrase(parts[0], 0, 960, 60);    // Phrase 1: ticks 0-960
        AddPhrase(parts[0], 960, 960, 60);  // Phrase 2: ticks 960-1920

        var engine = CreateCoordinator(parts, 2);

        // Phrase 1: sing well → Hit.
        FeedPitches(engine, 2, new[] { new[] { 60f }, new[] { float.NaN } }, 0.05, 0.5);
        engine.Update(1.1);

        // Phrase 2: sing wrong pitch → Miss.
        FeedPitches(engine, 2, new[] { new[] { 90f }, new[] { float.NaN } }, 1.05, 0.5);
        engine.Update(2.1);

        var stats = (VocalsStats) engine.BaseStats;
        Assert.Greater(stats.TicksHit, 0u,
            "TicksHit must accumulate on Hit phrases (regression: was 0).");
        Assert.Greater(stats.TicksMissed, 0u,
            "TicksMissed must accumulate on Miss phrases (regression: was 0).");
        Assert.Less(stats.Percent, 1.0f,
            "Percent must reflect real accuracy < 100% when there are misses " +
            "(regression: VocalsStats.Percent defaults to 1.0 when TotalTicks == 0).");
    }

    [Test]
    public void OnPhraseHit_FiresOnMissForIsFcFlip()
    {
        // Prior bug: coordinator never fired OnPhraseHit. VocalsPlayer.cs:486-489
        // subscribes to OnPhraseHit to flip IsFc = false on !fullPoints — without
        // this firing, the FC tile stays lit through misses.
        var parts = new List<VocalsPart> { CreateVocalsPart(), CreateVocalsPart(true) };
        AddPhrase(parts[0], 0, 960, 60);

        var engine = CreateCoordinator(parts, 2);
        bool hitEventFired = false;
        bool hitEventFullPoints = true;
        engine.OnPhraseHit += (percent, fullPoints, isLast) =>
        {
            hitEventFired = true;
            hitEventFullPoints = fullPoints;
        };

        // Sing wrong pitch → Miss.
        FeedPitches(engine, 2, new[] { new[] { 90f }, new[] { float.NaN } }, 0.05, 0.5);
        engine.Update(1.1);

        Assert.IsTrue(hitEventFired, "OnPhraseHit must fire on the coordinator path.");
        Assert.IsFalse(hitEventFullPoints, "fullPoints must be false on Miss (drives IsFc flip).");
    }

    [Test]
    public void StackingShortcut_TwoMicsTalkieHalfPhrase_GradeMiss()
    {
        // The bug the per-mic-span cap was added to fix.
        // 2 mics both ambiguous on {0,1} for HALF a phrase (talkies + harmonized
        // talkies are the typical real-game case). Under the prior additive-bucket
        // model: bucket = 2 × N/2 = N, allocator filled HARM0 to 100% → Awesome.
        // Equivalent unambiguous singing (2 mics on HARM1 half phrase) graded Miss.
        // Inconsistency was the shortcut.
        // Under per-mic-span cap: bucket = N, perMicCap = N/2. Each HARM can receive
        // at most N/2 → both HARMs at 50% → below threshold → Miss. Consistent.
        var parts = new List<VocalsPart> { CreateVocalsPart(), CreateVocalsPart(true) };
        AddTalkiePhrase(parts[0], 0, 960);
        AddTalkiePhrase(parts[1], 0, 960);

        var engine = CreateCoordinator(parts, 2);
        var grades = new List<PhraseGrade>();
        engine.OnPartyVocalsPhrase += (grade, meters, isLast) => grades.Add(grade);

        // Both mics making noise for ONLY HALF the phrase (0.0-0.25s of a 0.0-0.5s
        // content window) — they then go silent for the second half. Under the new
        // model this is a Miss.
        FeedPitches(engine, 2, new[] { new[] { 60f }, new[] { 60f } }, 0.0, 0.25);
        engine.Update(1.1); // past phrase end

        Assert.AreEqual(1, grades.Count, "One phrase grade");
        Assert.AreEqual(PhraseGrade.Miss, grades[0],
            "Two mics on harmonized talkies for HALF the phrase must grade Miss " +
            "(stacking shortcut prevention via per-mic-span cap).");
    }

    [Test]
    public void TrueUnison_TwoMicsTalkieFullPhrase_DoubleAwesome()
    {
        // Companion to the stacking-shortcut test: 2 mics ambiguous on a harmonized
        // talkie for the FULL phrase should still grade DoubleAwesome. Bucket = 2N,
        // perMicCap = N. Each HARM receives up to N (= capacity). Both filled.
        // Verifies the per-mic-span cap doesn't break Goal G1 (true unison → Double).
        var parts = new List<VocalsPart> { CreateVocalsPart(), CreateVocalsPart(true) };
        AddTalkiePhrase(parts[0], 0, 960);
        AddTalkiePhrase(parts[1], 0, 960);

        var engine = CreateCoordinator(parts, 2);
        var grades = new List<PhraseGrade>();
        engine.OnPartyVocalsPhrase += (grade, meters, isLast) => grades.Add(grade);

        // Both mics making noise for the WHOLE phrase content window.
        FeedPitches(engine, 2, new[] { new[] { 60f }, new[] { 60f } }, 0.0, 0.55);
        engine.Update(1.1);

        Assert.AreEqual(1, grades.Count, "One phrase grade");
        Assert.AreEqual(PhraseGrade.DoubleAwesome, grades[0],
            "Two mics on harmonized talkies for the FULL phrase = DoubleAwesome " +
            "(per-mic-span cap permits both HARMs when each mic vouches for a full span).");
    }

    [Test]
    public void EmptyPhrase_TreatedAsHit_NoSpuriousMiss()
    {
        // Prior bug: phraseTicksTotal == 0 (lyric-less phrase) went through the
        // allocator → all-zero meters → grade Miss → MissNote. The base treats
        // empty phrases as a free Hit. Coordinator now short-circuits to match.
        var parts = new List<VocalsPart> { CreateVocalsPart(), CreateVocalsPart(true) };
        // Phrase with non-zero tick length but no child lyric notes (phraseTicksTotal == 0).
        var emptyNote = new VocalNote(NoteFlags.None, false, 0.0, 2.0, 0, 960);
        parts[0].NotePhrases.Add(new VocalsPhrase(
            0.0, 2.0, 0, 960, emptyNote, new List<LyricEvent>()));

        var engine = CreateCoordinator(parts, 2);
        var grades = new List<PhraseGrade>();
        engine.OnPartyVocalsPhrase += (grade, meters, isLast) => grades.Add(grade);

        engine.Update(1.5); // past the empty phrase's TickEnd

        Assert.AreEqual(1, grades.Count, "Empty phrase should still emit a grade event");
        Assert.AreNotEqual(PhraseGrade.Miss, grades[0],
            "Empty phrase should NOT grade as Miss (base treats it as Hit).");
        Assert.AreEqual(1, engine.BaseStats.NotesHit,
            "Empty phrase should count as a NotesHit (HitNote was called).");
    }

    [Test]
    public void PartInCurrentPhrase_ReflectsPerPartPresence()
    {
        // Three parts: HARM0 at tick 0-960, HARM2 at tick 0-960, HARM1 has no phrase.
        // After running through the phrase, PartInCurrentPhrase(0) and (2) should be true,
        // PartInCurrentPhrase(1) should be false.
        var parts = new List<VocalsPart>
        {
            CreateVocalsPart(), CreateVocalsPart(true), CreateVocalsPart(true)
        };
        AddPhrase(parts[0], 0, 960, 60); // HARM0
        AddPhrase(parts[2], 0, 960, 67); // HARM2

        var engine = CreateCoordinator(parts, 2);
        engine.Update(0.1);

        // Drive into the phrase so _phraseTicksTotalPerPart gets populated.
        FeedPitches(engine, 2, new[] { new[] { 60f }, new[] { float.NaN } }, 0.1, 0.3);

        Assert.IsTrue(engine.PartInCurrentPhrase(0), "HARM0 has notes in this phrase");
        Assert.IsFalse(engine.PartInCurrentPhrase(1), "HARM1 has no notes in this phrase");
        Assert.IsTrue(engine.PartInCurrentPhrase(2), "HARM2 has notes in this phrase");
        Assert.IsFalse(engine.PartInCurrentPhrase(-1), "Negative index → false");
        Assert.IsFalse(engine.PartInCurrentPhrase(99), "Out-of-range index → false");
    }

    [Test]
    public void Bot_HitsPercussionNote()
    {
        // A Party Vocals bot should hit percussion notes. The coordinator scores percussion
        // via HasHit, which is only set by a queued Hit input (MutateStateWithInput). Bots
        // queue no inputs and the coordinator's UpdateBot is a no-op, so a bot currently
        // never scores percussion (live mics do). Repro for the reported bug.
        var parts = new List<VocalsPart> { CreateVocalsPart() };
        AddPercussionPhrase(parts[0], 480, 960); // percussion at tick 480 (t=0.5s)

        var primaryChart = parts[0].CloneAsInstrumentDifficulty();
        var engine = new PartyVocalsCoordinatorEngine(
            primaryChart, parts, CreateSyncTrack(), EngineParams, isBot: true, micCount: 1);

        int percussionHits = 0;
        engine.OnNoteHit += (_, note) => { if (note.IsPercussion) percussionHits++; };

        int totalFrames = (int) (2.0 * ApproximateVocalFps);
        for (int f = 0; f < totalFrames; f++)
            engine.Update((f + 1) / ApproximateVocalFps);

        Assert.That(percussionHits, Is.EqualTo(1),
            "A Party Vocals bot should hit the percussion note.");
    }

    [Test]
    public void PartInNextPhrase_ReflectsNextPhrasePerPartPresence()
    {
        // Master (parts[0]) has TWO phrases: tick 0-960 and tick 1920-2880.
        // While in the first phrase, "next phrase" is the tick 1920-2880 window.
        //   HARM0: present in next (its own second phrase).
        //   HARM1: present in next (charted at 1920-2880).
        //   HARM2: present only in the FIRST phrase (0-960) → absent in next.
        var parts = new List<VocalsPart>
        {
            CreateVocalsPart(), CreateVocalsPart(true), CreateVocalsPart(true)
        };
        AddPhrase(parts[0], 0, 960, 60);     // HARM0 phrase 1
        AddPhrase(parts[0], 1920, 960, 60);  // HARM0 phrase 2 (the "next" master phrase)
        AddPhrase(parts[1], 1920, 960, 64);  // HARM1 only in the next phrase
        AddPhrase(parts[2], 0, 960, 67);     // HARM2 only in the first phrase

        var engine = CreateCoordinator(parts, 2);
        engine.Update(0.1); // NoteIndex = 0 (first phrase)

        Assert.IsTrue(engine.PartInNextPhrase(0), "HARM0 has a note in the next phrase");
        Assert.IsTrue(engine.PartInNextPhrase(1), "HARM1 has a note in the next phrase");
        Assert.IsFalse(engine.PartInNextPhrase(2), "HARM2 is only in the current phrase");
        Assert.IsFalse(engine.PartInNextPhrase(-1), "Negative index → false");
        Assert.IsFalse(engine.PartInNextPhrase(99), "Out-of-range index → false");
    }

    [Test]
    public void CurrentPhraseProgress_TracksTickSpanOfCurrentPhrase()
    {
        // Phrase spans tick 0-960 = t 0.0..1.0 (480 tpqn @120bpm). At t=0.5 the engine is
        // halfway through the phrase span, so progress ≈ 0.5 — driven by the SPAN, not by
        // how much was sung.
        var parts = new List<VocalsPart> { CreateVocalsPart() };
        AddPhrase(parts[0], 0, 960, 60);

        var engine = CreateCoordinator(parts, 2);
        engine.Update(0.5);

        Assert.That(engine.CurrentPhraseProgress, Is.EqualTo(0.5).Within(0.05),
            "Progress should reflect position within the phrase's tick span");
    }

    [Test]
    public void CurrentPhraseDurationSeconds_ReflectsPhraseSpan()
    {
        // AddPhrase hardcodes the parent phrase note's Time=0.0 / TimeLength=2.0 (the same
        // tick-vs-time helper artifact the percussion tests note), so TimeEnd-Time = 2.0s
        // here. In real charts the parent note's time span is accurate; this just verifies
        // the accessor returns TimeEnd - Time of the current phrase.
        var parts = new List<VocalsPart> { CreateVocalsPart() };
        AddPhrase(parts[0], 0, 960, 60);

        var engine = CreateCoordinator(parts, 2);
        engine.Update(0.1);

        Assert.That(engine.CurrentPhraseDurationSeconds, Is.EqualTo(2.0).Within(0.001),
            "Duration should be the current phrase's time span (TimeEnd - Time)");
    }

    [Test]
    public void Bot_ThreeMicsThreeHarms_EachBotHitsItsPart()
    {
        // Regression guard: bot Party Vocals builds one sub-engine per HARM part
        // (micCount == part count). Each bot sub-engine must self-drive via UpdateBot
        // and hit its assigned part, so the coordinator credits every part and the
        // phrase grades as a hit. This path was never exercised by existing tests
        // (CreateCoordinator passes isBot=false and feeds pitches manually); here we
        // drive a real bot coordinator with no SetMicPitch calls.
        var parts = new List<VocalsPart>
        {
            CreateVocalsPart(isHarmony: true),
            CreateVocalsPart(isHarmony: true),
            CreateVocalsPart(isHarmony: true),
        };
        AddPhrase(parts[0], 0, 960, 60); // HARM1 C4
        AddPhrase(parts[1], 0, 960, 64); // HARM2 E4
        AddPhrase(parts[2], 0, 960, 67); // HARM3 G4

        var primaryChart = parts[0].CloneAsInstrumentDifficulty();
        var engine = new PartyVocalsCoordinatorEngine(
            primaryChart, parts, CreateSyncTrack(), EngineParams, isBot: true, micCount: 3);

        var grades = new List<PhraseGrade>();
        engine.OnPartyVocalsPhrase += (grade, meters, isLast) => grades.Add(grade);

        // Drive the bots through the phrase at ~60fps; bots self-generate pitch, so
        // we deliberately do NOT call SetMicPitch.
        int totalFrames = (int) (2.0 * ApproximateVocalFps);
        for (int f = 0; f < totalFrames; f++)
            engine.Update((f + 1) / ApproximateVocalFps);

        Assert.AreEqual(1, grades.Count, "Bot phrase should be graded once");
        Assert.AreNotEqual(PhraseGrade.Miss, grades[0],
            "Bots should hit their assigned parts, not miss");
        Assert.Greater(((VocalsStats)engine.BaseStats).TicksHit, 0u,
            "Bot hits should accumulate TicksHit");
    }

    // ================================================================
    // Coordinator Percussion Scoring Tests (18-22)
    // percussion.AC1: Any mic tap scores
    // percussion.AC2: No double-count
    // percussion.AC5: No false hit
    // ================================================================

    [Test]
    public void Percussion_TapWithDueNote_ScoresPercussionNote_AC1_1()
    {
        // AC1.1: A tap with a due percussion note scores it.
        var parts = new List<VocalsPart> { CreateVocalsPart() };
        AddPercussionPhrase(parts[0], 480, 960); // Percussion note at tick 480 (t=0.5s)

        var engine = CreateCoordinator(parts, 2);
        var hitFired = false;
        engine.OnNoteHit += (noteIndex, note) => hitFired = true;

        // Drive to the note's time
        engine.Update(0.5);

        // Queue a single Hit input within the note's hit window
        var hitInput = GameInput.Create(0.5, VocalsAction.Hit, true);
        engine.QueueInput(ref hitInput);

        // Advance to process the hit (phrase ends at tick 1440 = t=1.5s)
        engine.Update(1.6);

        // The percussion note should have been scored
        Assert.IsTrue(hitFired, "OnNoteHit should have fired for the percussion note");
        Assert.AreEqual(1, engine.BaseStats.NotesHit, "NotesHit should increase by 1");
    }

    [Test]
    public void Percussion_EarlyTapWithinHitWindow_ScoresPercussionNote_WindowRepro()
    {
        // RED repro for docs/bugs/party-vocals-percussion-broken.md (Problem 2).
        // A tap landing 0.03s EARLY on a short, realistically-timed percussion note is
        // well inside the engine's ±0.05s hit window. Solo (YargVocalsEngine) scores it
        // via IsNoteInWindow (front/back tolerance). The coordinator gates on the raw
        // span CurrentTime >= percussion.Time, which gives ZERO early tolerance, so the
        // tap is dropped. This is why runtime taps reach the engine (logs A+B fire) but
        // never score (log C never fires).
        var parts = new List<VocalsPart> { CreateVocalsPart() };
        // Percussion at tick 4800 = t=5.0s (480 tpqn @120bpm). The raw span is [5.0, 5.5];
        // the engine hit window is [4.95, 5.05].
        AddPercussionPhrase(parts[0], 4800, 960);

        var engine = CreateCoordinator(parts, 2);
        int percussionHits = 0;
        engine.OnNoteHit += (_, note) => { if (note.IsPercussion) percussionHits++; };

        engine.Update(4.9);
        // Tap at t=4.97: inside the hit window [4.95, 5.05], but BEFORE the raw span
        // [5.0, 5.5] the old code required (CurrentTime >= percussion.Time).
        var hit = GameInput.Create(4.97, VocalsAction.Hit, true);
        engine.QueueInput(ref hit);
        engine.Update(6.5); // process the tap, then advance past phrase end

        Assert.AreEqual(1, percussionHits,
            "An early tap within the hit window should score the percussion note, " +
            "matching solo YargVocalsEngine. The coordinator's raw [Time, TimeEnd] gate drops it.");
    }

    [Test]
    public void Percussion_TapScoresNextDueNote_AC1_2()
    {
        // AC1.2: The note scored is the next due percussion note per GetNextPercussionNote windowing.
        var parts = new List<VocalsPart> { CreateVocalsPart() };
        AddPercussionPhrase(parts[0], 480, 960);  // First percussion note at tick 480 (t=0.5s)
        AddPercussionPhrase(parts[0], 1920, 960); // Second percussion note at tick 1920 (t=2.0s)

        var engine = CreateCoordinator(parts, 2);
        var hitNoteIndex = -1;
        engine.OnNoteHit += (noteIndex, note) => hitNoteIndex = noteIndex;

        // Drive to the first note's time
        engine.Update(0.5);

        // Queue a single Hit input within the first note's hit window
        var hitInput = GameInput.Create(0.5, VocalsAction.Hit, true);
        engine.QueueInput(ref hitInput);

        // Advance to process the hit (first phrase ends at tick 1440 = t=1.5s)
        engine.Update(1.6);

        // The first note (index 0) should have been scored, not the second
        Assert.AreEqual(0, hitNoteIndex, "First note (index 0) should have been scored");
        Assert.AreEqual(1, engine.BaseStats.NotesHit, "Only one note should have been scored");
    }

    [Test]
    public void Percussion_TwoMicsSameNote_ScoreOnce_AC2_1()
    {
        // AC2.1: Multiple mics tapping the same due percussion note score it exactly once.
        var parts = new List<VocalsPart> { CreateVocalsPart() };
        AddPercussionPhrase(parts[0], 480, 960); // Percussion note at tick 480 (t=0.5s)

        var engine = CreateCoordinator(parts, 2);
        var hitFired = false;
        engine.OnNoteHit += (noteIndex, note) => hitFired = true;

        // Drive to the note's time
        engine.Update(0.5);

        // Queue Hit inputs from both mics (coordinator aggregates into single HasHit)
        var hitInput1 = GameInput.Create(0.5, VocalsAction.Hit, true);
        var hitInput2 = GameInput.Create(0.5, VocalsAction.Hit, true);
        engine.QueueInput(ref hitInput1);
        engine.QueueInput(ref hitInput2);

        // Advance to process the hits (phrase ends at tick 1440 = t=1.5s)
        engine.Update(1.6);

        // The note should have been scored exactly once (HasHit is shared across mics)
        Assert.IsTrue(hitFired, "OnNoteHit should have fired");
        Assert.AreEqual(1, engine.BaseStats.NotesHit, "Note should be scored exactly once, not twice");
    }

    [Test]
    public void Percussion_TapWithNoDueNote_NoScore_AC5_1()
    {
        // AC5.1: A tap with no due percussion note does not score; HasHit is consumed.
        var parts = new List<VocalsPart> { CreateVocalsPart() };
        AddPhrase(parts[0], 960, 960, 60); // Regular note (not percussion) at tick 960-1920

        var engine = CreateCoordinator(parts, 2);
        var hitFired = false;
        engine.OnNoteHit += (noteIndex, note) => hitFired = true;

        // Drive to time but no percussion note is due yet
        engine.Update(0.5);

        // Queue a Hit input
        var hitInput = GameInput.Create(0.5, VocalsAction.Hit, true);
        engine.QueueInput(ref hitInput);

        // Advance to process the hit (need to advance past phrase end)
        engine.Update(1.5); // Phrase ends at tick 480 (t=1.0s)

        // No note should have been scored
        Assert.IsFalse(hitFired, "OnNoteHit should not fire when no percussion note is due");
        Assert.AreEqual(0, engine.BaseStats.NotesHit, "NotesHit should not increase");
    }

    // ================================================================
    // Tests for mic packing and input demux (Subcomponent A)
    // ================================================================

    private static void QueueAndUpdate(PartyVocalsCoordinatorEngine engine, GameInput input)
    {
        engine.QueueInput(ref input);
        engine.Update(0.5);
    }

    [Test]
    public void PitchRouting_PackedInput_RoutesToCorrectMic_AC32()
    {
        // AC32: Pitch input packed for mic k routes to sub-engine k
        var parts = new List<VocalsPart> { CreateVocalsPart() };
        AddPhrase(parts[0], 0, 960, 60); // Note at tick 0-960

        var engine = CreateCoordinator(parts, 2);

        // Queue pitch for mic 0
        var packedPitch0 = new GameInput(0.5, PartyVocalsInput.Pack(0, VocalsAction.Pitch), 100f);
        QueueAndUpdate(engine, packedPitch0);

        // Queue pitch for mic 1
        var packedPitch1 = new GameInput(0.5, PartyVocalsInput.Pack(1, VocalsAction.Pitch), 200f);
        QueueAndUpdate(engine, packedPitch1);

        // Verify pitch values reached correct sub-engines
        Assert.AreEqual(100f, engine.GetMicPitch(0), Epsilon);
        Assert.AreEqual(200f, engine.GetMicPitch(1), Epsilon);
    }

    [Test]
    public void PitchRouting_UnpackedInput_DegradesToMic0_AC32()
    {
        // AC32: Unpacked input (no mic bits) routes to mic 0
        var parts = new List<VocalsPart> { CreateVocalsPart() };
        AddPhrase(parts[0], 0, 960, 60);

        var engine = CreateCoordinator(parts, 2);

        // Queue unpacked pitch (mic 0 equivalent)
        var unpackedPitch = GameInput.Create(0.5, VocalsAction.Pitch, 300f);
        QueueAndUpdate(engine, unpackedPitch);

        // Verify it routes to mic 0
        Assert.AreEqual(300f, engine.GetMicPitch(0), Epsilon);
        Assert.AreEqual(0f, engine.GetMicPitch(1), Epsilon);
    }

    [Test]
    public void PitchRouting_OutOfRangeMic_DroppedWithoutException_AC32()
    {
        // AC32: Pitch packed for mic >= _micCount is dropped
        var parts = new List<VocalsPart> { CreateVocalsPart() };
        AddPhrase(parts[0], 0, 960, 60);

        var engine = CreateCoordinator(parts, 2);

        // Queue pitch for out-of-range mic (2, but we only have 2 mics: 0,1)
        var packedPitchOutOfRange = new GameInput(0.5, PartyVocalsInput.Pack(2, VocalsAction.Pitch), 400f);
        QueueAndUpdate(engine, packedPitchOutOfRange);

        // Verify no state changed
        Assert.AreEqual(0f, engine.GetMicPitch(0), Epsilon);
        Assert.AreEqual(0f, engine.GetMicPitch(1), Epsilon);
    }

    [Test]
    public void Hit_StarPower_PackedInput_SetsCoordinatorFlags_AC32()
    {
        // AC32: Hit/StarPower packed inputs set coordinator-level flags
        var parts = new List<VocalsPart> { CreateVocalsPart() };
        AddPhrase(parts[0], 0, 960, 60);

        var engine = CreateCoordinator(parts, 2);

        // Queue packed StarPower
        var packedSP = new GameInput(1.0, PartyVocalsInput.Pack(1, VocalsAction.StarPower), true);
        engine.QueueInput(ref packedSP);
        engine.Update(1.0);

        Assert.IsTrue(engine.IsStarPowerInputActive);
    }

    [Test]
    public void ActionPack_RoundTrip_RecoversOriginalValues_AC32()
    {
        // AC32: Pack -> UnpackMic/UnpackAction round-trip preserves original values
        for (int mic = 0; mic <= 6; mic++)
        {
            foreach (VocalsAction action in Enum.GetValues<VocalsAction>())
            {
                int packed = PartyVocalsInput.Pack(mic, action);
                int unpackedMic = PartyVocalsInput.UnpackMic(packed);
                VocalsAction unpackedAction = PartyVocalsInput.UnpackAction(packed);

                Assert.AreEqual(mic, unpackedMic);
                Assert.AreEqual(action, unpackedAction);
            }
        }
    }

    [Test]
    public void SangFlag_SetByPitch_DidMicSingThisTick_AC32()
    {
        // AC32: _micSangThisTick set by Pitch, exposed via accessors
        var parts = new List<VocalsPart> { CreateVocalsPart() };
        AddPhrase(parts[0], 0, 960, 60);

        var engine = CreateCoordinator(parts, 2);

        // Initially all flags should be false
        Assert.IsFalse(engine.DidMicSingThisTick(0));
        Assert.IsFalse(engine.DidMicSingThisTick(1));

        // Queue pitch for mic 1
        var packedPitch1 = new GameInput(0.5, PartyVocalsInput.Pack(1, VocalsAction.Pitch), 200f);
        QueueAndUpdate(engine, packedPitch1);

        // Only mic 1 should have sang
        Assert.IsFalse(engine.DidMicSingThisTick(0));
        Assert.IsTrue(engine.DidMicSingThisTick(1));

        // Reset flags
        engine.ResetMicSangFlags();
        Assert.IsFalse(engine.DidMicSingThisTick(0));
        Assert.IsFalse(engine.DidMicSingThisTick(1));
    }

    [Test]
    public void SangFlag_HitOrStarPower_DoesNotSetSangFlag_AC32()
    {
        // AC32: Hit/StarPower inputs do not set sang flag
        var parts = new List<VocalsPart> { CreateVocalsPart() };
        AddPhrase(parts[0], 0, 960, 60);

        var engine = CreateCoordinator(parts, 2);

        // Queue Hit
        var packedHit = new GameInput(0.5, PartyVocalsInput.Pack(0, VocalsAction.Hit), true);
        QueueAndUpdate(engine, packedHit);

        Assert.IsFalse(engine.DidMicSingThisTick(0));

        // Queue StarPower
        var packedSP = new GameInput(1.0, PartyVocalsInput.Pack(1, VocalsAction.StarPower), true);
        QueueAndUpdate(engine, packedSP);

        Assert.IsFalse(engine.DidMicSingThisTick(1));
    }

    // ================================================================
    // Hit routing invariant (AC34 structural)
    // Hit reaches coordinator only — sub-engines receive no queued Hit,
    // so sub-engine CheckPercussionHit cannot fire from input.
    // ================================================================

    [Test]
    public void Hit_RoutesToCoordinatorOnly_SubEnginesNotFedInput_AC34()
    {
        // Verify that Hit inputs queued to the coordinator set only the coordinator's
        // HasHit — sub-engines do not have their MutateStateWithInput called (they
        // receive no queued input; they're driven via Update + SetMicPitch).
        // This is the structural invariant that prevents double percussion scoring.
        var parts = new List<VocalsPart> { CreateVocalsPart(), CreateVocalsPart(true) };
        AddPhrase(parts[0], 0, 960, 60);
        AddPhrase(parts[1], 0, 960, 64);

        var engine = CreateCoordinator(parts, 2);
        engine.Update(0.1);

        // Queue Hit inputs from two different mics
        for (int m = 0; m < 2; m++)
        {
            var hit = new GameInput(0.15, PartyVocalsInput.Pack(m, VocalsAction.Hit), true);
            engine.QueueInput(ref hit);
        }
        engine.Update(0.2);

        // The coordinator consumed HasHit during CheckPercussionHit (no percussion
        // in this chart, so it was a no-op). The key invariant is that the coordinator
        // demux routed Hit to coordinator.HasHit, not to any sub-engine's input queue.
        // Sub-engines have no input queue — they are driven solely via Update(time).
        // Verify by checking the sub-engines' MutateStateWithInput was never called:
        // their input queue count must be zero.
        var subEnginesField = typeof(PartyVocalsCoordinatorEngine)
            .GetField("_subEngines", BindingFlags.NonPublic | BindingFlags.Instance);
        var subEngines = (YargFreeVocalsEngine[])subEnginesField.GetValue(engine)!;

        foreach (var sub in subEngines)
        {
            var inputQueueField = typeof(BaseEngine<VocalNote, VocalsEngineParameters, VocalsStats>)
                .GetField("InputQueue", BindingFlags.NonPublic | BindingFlags.Instance);
            var queue = (System.Collections.Generic.Queue<GameInput>)inputQueueField.GetValue(sub)!;
            Assert.AreEqual(0, queue.Count,
                "Sub-engine input queue must be empty — sub-engines receive no queued input");
        }
    }

    // ================================================================
    // Sing-to-activate star power tests (overdrive.AC1, AC2)
    // ================================================================

    private static void BankStarPower(PartyVocalsCoordinatorEngine engine, uint ticks)
    {
        engine.EngineStats.StarPowerTickAmount += ticks;
    }

    /// overdrive.AC1.1: A hit (sing-to-activate noise) deploys SP when flag is on and SP is banked.
    [Test]
    public void SingToActivate_DeploysStarPower_WhenSingingAndSpBanked()
    {
        var parts = new List<VocalsPart> { CreateVocalsPart() };
        AddTalkiePhrase(parts[0], 0, 960);
        var engine = CreateCoordinator(parts, 2);

        BankStarPower(engine, engine.TicksPerHalfSpBar + 100);
        engine.Update(0.1);

        // Queue a Hit action (mirrors how solo engine triggers sing-to-activate
        // via HasHit in CheckPercussionHit's else branch)
        var hitInput = GameInput.Create(0.15, VocalsAction.Hit, true);
        engine.QueueInput(ref hitInput);
        engine.Update(0.2);

        Assert.That(engine.EngineStats.IsStarPowerActive, Is.True,
            "Sing-to-activate should deploy SP on hit when SP is banked");
    }

    /// overdrive.AC1.2: No deploy when CanStarPowerActivate is false.
    [Test]
    public void SingToActivate_DoesNotDeploy_WhenSpNotBanked()
    {
        var parts = new List<VocalsPart> { CreateVocalsPart() };
        AddTalkiePhrase(parts[0], 0, 960);
        var engine = CreateCoordinator(parts, 2);

        engine.Update(0.1);
        Assert.That(engine.CanStarPowerActivate, Is.False);

        var hitInput = GameInput.Create(0.15, VocalsAction.Hit, true);
        engine.QueueInput(ref hitInput);
        engine.Update(0.2);

        Assert.That(engine.EngineStats.IsStarPowerActive, Is.False,
            "SP must not activate when CanStarPowerActivate is false");
    }

    /// overdrive.AC1.3: No deploy when sing-to-activate is off.
    [Test]
    public void SingToActivate_DoesNotDeploy_WhenFlagOff()
    {
        var parts = new List<VocalsPart> { CreateVocalsPart() };
        AddTalkiePhrase(parts[0], 0, 960);
        var engine = CreateCoordinator(parts, 2, EngineParamsNoSingToActivate);

        BankStarPower(engine, engine.TicksPerHalfSpBar + 100);
        engine.Update(0.1);

        var hitInput = GameInput.Create(0.15, VocalsAction.Hit, true);
        engine.QueueInput(ref hitInput);
        engine.Update(0.2);

        Assert.That(engine.EngineStats.IsStarPowerActive, Is.False,
            "SP must not activate via hit when SingToActivateStarPower is off");
    }

    /// SP earning: singing a star power phrase should award SP ticks to the coordinator.
    [Test]
    public void SingingSpPhrase_AwardsStarPower_ToCoordinator()
    {
        // Create a part with a single phrase that has StarPower flags.
        var part = CreateVocalsPart();
        uint phraseTick = 0;
        uint phraseTickLength = 960; // 2 beats at 480 ticks/beat
        int midiPitch = 60; // C4

        var note = new VocalNote(NoteFlags.StarPower, false, 0.0, 2.0, phraseTick, phraseTickLength);
        var lyricNote = new VocalNote(midiPitch, 0, VocalNoteType.Lyric, 0.0, 1.0, phraseTick, phraseTickLength / 2);
        note.AddChildNote(lyricNote);
        var lyrics = new List<LyricEvent> { new(LyricSymbolFlags.None, "La", 0.0, phraseTick) };
        part.NotePhrases.Add(new VocalsPhrase(0.0, 2.0, phraseTick, phraseTickLength, note, lyrics));

        var parts = new List<VocalsPart> { part };
        var engine = CreateCoordinator(parts, 1);

        // Verify the note track has SP flag via reflection (Notes is protected)
        var notesField = typeof(BaseEngine<VocalNote, VocalsEngineParameters, VocalsStats>)
            .GetField("Notes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(notesField, "Should find Notes field via reflection");
        var notes = (System.Collections.Generic.List<VocalNote>)notesField!.GetValue(engine)!;
        Assert.That(notes[0].IsStarPower, Is.True, "Phrase should have StarPower flag");

        engine.Update(0.1);

        // Feed pitch inputs matching the note for the entire phrase duration
        float[] micPitches = { midiPitch };
        FeedPitches(engine, 1, new[] { micPitches }, 0.1, 2.5);

        // Process past phrase end
        engine.Update(3.0);

        // The coordinator should have earned star power
        Assert.That(engine.EngineStats.StarPowerTickAmount, Is.GreaterThan(0),
            "Coordinator should have earned star power from singing a SP phrase");
        Assert.That(engine.EngineStats.StarPowerPhrasesHit, Is.EqualTo(1),
            "Should have recorded exactly one SP phrase hit");
    }

    /// SP earning regression: a per-mic sub-engine that MISSES a star power phrase
    /// must not strip the StarPower flag off the shared VocalNote before the
    /// coordinator scores that phrase.
    ///
    /// The coordinator and every sub-engine are built from the same note track, so
    /// they share the same VocalNote objects. The coordinator drives all sub-engines
    /// (including their phrase-end logic) *before* running its own phrase-end. A
    /// sub-engine that misses calls MissNote -> StripStarPower, clearing the flag on
    /// the shared note; the coordinator then reads IsStarPower == false and awards
    /// nothing — even though it graded the phrase a hit. This is why a real singer
    /// never earned SP while a bot (whose sub-engines always hit, never strip) did.
    ///
    /// Repro: two mics on one SP phrase. Mic 0 sings it (coordinator aggregate hits);
    /// mic 1 is silent (its sub-engine misses and strips). Expect SP still awarded.
    [Test]
    public void SubEngineMiss_DoesNotStripStarPower_FromCoordinatorScoring()
    {
        var part = CreateVocalsPart();
        uint phraseTick = 0;
        uint phraseTickLength = 960;
        int midiPitch = 60; // C4

        var note = new VocalNote(NoteFlags.StarPower, false, 0.0, 2.0, phraseTick, phraseTickLength);
        var lyricNote = new VocalNote(midiPitch, 0, VocalNoteType.Lyric, 0.0, 1.0, phraseTick, phraseTickLength / 2);
        note.AddChildNote(lyricNote);
        var lyrics = new List<LyricEvent> { new(LyricSymbolFlags.None, "La", 0.0, phraseTick) };
        part.NotePhrases.Add(new VocalsPhrase(0.0, 2.0, phraseTick, phraseTickLength, note, lyrics));

        var parts = new List<VocalsPart> { part };
        var engine = CreateCoordinator(parts, 2); // two mics

        var grades = new List<PhraseGrade>();
        engine.OnPartyVocalsPhrase += (grade, meters, isLast) => grades.Add(grade);

        engine.Update(0.1);

        // Mic 0 sings the SP phrase; mic 1 stays silent (NaN sentinel = no input),
        // so mic 1's sub-engine misses the phrase and runs StripStarPower.
        FeedPitches(engine, 2,
            new[] { new[] { (float) midiPitch }, new[] { float.NaN } },
            0.1, 2.5);

        engine.Update(3.0);

        // Guard: the coordinator must actually grade the phrase a hit, so a failure
        // below isolates the SP-award bug rather than a scoring miss.
        Assert.That(grades, Has.Some.Not.EqualTo(PhraseGrade.Miss),
            "Mic 0 sang the phrase, so the coordinator should grade it a hit");

        Assert.That(engine.EngineStats.StarPowerTickAmount, Is.GreaterThan(0u),
            "Coordinator must earn SP on a phrase it grades a hit, even though a silent " +
            "mic's sub-engine missed it (the sub-engine's MissNote stripped the shared " +
            "StarPower flag before the coordinator scored the phrase).");
        Assert.That(engine.EngineStats.StarPowerPhrasesHit, Is.EqualTo(1),
            "Should record exactly one SP phrase hit");
    }

    /// overdrive.AC2.1: Manual button deploy still works.
    [Test]
    public void ManualDeploy_ActivatesStarPower_WhenSpBanked()
    {
        var parts = new List<VocalsPart> { CreateVocalsPart() };
        AddTalkiePhrase(parts[0], 0, 960);
        var engine = CreateCoordinator(parts, 2);

        BankStarPower(engine, engine.TicksPerHalfSpBar + 100);
        engine.Update(0.1);
        Assert.That(engine.CanStarPowerActivate, Is.True);

        int packed = PartyVocalsInput.Pack(0, VocalsAction.StarPower);
        var input = new GameInput(0.15, packed, true);
        engine.QueueInput(ref input);
        engine.Update(0.2);

        Assert.That(engine.EngineStats.IsStarPowerActive, Is.True,
            "Manual StarPower button should deploy SP when banked");
    }

    /// Bot deploy: a Party Vocals *bot* must self-activate star power once it has
    /// enough banked, exactly like the solo bot (YargVocalsEngine.UpdateBot toggles
    /// IsStarPowerInputActive). The coordinator's UpdateBot is a no-op and the per-mic
    /// sub-engines only toggle their own (dead-data) IsStarPowerInputActive, so the
    /// authoritative coordinator never set it for a bot — the bot never deployed.
    [Test]
    public void BotCoordinator_DeploysStarPower_WhenBanked()
    {
        var parts = new List<VocalsPart> { CreateVocalsPart() };
        // A phrase so the coordinator's UpdateHitLogic doesn't early-return on "no notes".
        AddPhrase(parts[0], 0, 960, 60);

        var primaryChart = parts[0].CloneAsInstrumentDifficulty();
        var engine = new PartyVocalsCoordinatorEngine(
            primaryChart, parts, CreateSyncTrack(), EngineParams, isBot: true, micCount: 1);

        BankStarPower(engine, engine.TicksPerHalfSpBar + 100);
        engine.Update(0.1);
        Assert.That(engine.CanStarPowerActivate, Is.True, "precondition: a half bar is banked");

        // No input is fed — a bot must self-activate.
        engine.Update(0.5);

        Assert.That(engine.EngineStats.IsStarPowerActive, Is.True,
            "A Party Vocals bot with banked SP should deploy it (like the solo bot). The " +
            "coordinator never toggled IsStarPowerInputActive for bots, so it never fired.");
    }

    /// <summary>
    /// AC.6: Star-power deploys exactly once per qualifying input. Asserting the activation
    /// COUNT (not just IsStarPowerActive) is strictly stronger — it proves a fresh deploy
    /// occurred for this input, not just that SP was already active from a prior tick.
    /// Approach (A) owns the singing/percussion/SP flow in the subclass (the base path
    /// never runs), so the inherited-base double-fire hazard is avoided by design.
    /// </summary>
    [Test]
    public void Coordinator_DeploysStarPower_OncePerBank()
    {
        var parts = new List<VocalsPart> { CreateVocalsPart() };
        AddTalkiePhrase(parts[0], 0, 960);
        var engine = CreateCoordinator(parts, 2);

        // Precondition: no activations yet.
        Assert.That(engine.BaseStats.StarPowerActivationCount, Is.EqualTo(0),
            "precondition: no SP activations yet");

        BankStarPower(engine, engine.TicksPerHalfSpBar + 100);
        engine.Update(0.1);

        // Trigger sing-to-activate via a Hit input (mirrors solo engine's path).
        var hitInput = GameInput.Create(0.15, VocalsAction.Hit, true);
        engine.QueueInput(ref hitInput);
        engine.Update(0.2);

        Assert.That(engine.BaseStats.StarPowerActivationCount, Is.EqualTo(1),
            "Exactly one fresh SP deploy should occur for this qualifying input — " +
            "not zero (missed deploy) and not >1 (double-activation hazard).");
        Assert.That(engine.EngineStats.IsStarPowerActive, Is.True,
            "SP should be active after deploy");
    }

}
