using System;
using System.IO;
using Newtonsoft.Json;
using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Vocals;
using YARG.Core.Game;
using YARG.Core.IO;
using YARG.Core.Input;
using YARG.Core.Replays;
using YARG.Core.Replays.Analyzer;
using YARG.Core.Engine.Vocals.Engines;

namespace YARG.Core.UnitTests.Game;

public class PartyVocalsSessionTests
{
    // Round-trips a three-mic replay where only mic 2 sings, then checks live/replay score and combo agreement for each chart source.
    [TestCase(Instrument.Vocals)]
    [TestCase(Instrument.Harmony)]
    public void ReplayUsesRecordedSourceAndSilentSlots(Instrument source)
    {
        var chart = new SongChart(480);
        chart.SyncTrack.Tempos.Add(new TempoChange(120, 0, 0));
        chart.SyncTrack.TimeSignatures.Add(new TimeSignatureChange(4, 4, 0, 0, 0, 0, 0, 0));
        chart.Vocals.Parts.Add(Part(true));
        chart.Harmony.Parts.Add(Part(true));
        chart.Harmony.Parts.Add(Part(true));
        var profile = new YargProfile { GameMode = GameMode.PartyVocals, CurrentInstrument = Instrument.PartyVocals };
        var parameters = Parameters(3, source);
        var inputs = new System.Collections.Generic.List<GameInput>();
        for (int tick = 1; tick < 60; tick++)
            inputs.Add(new GameInput(tick / 60d, PartyVocalsInput.Pack(2, VocalsAction.Pitch), 60f));
        var parts = PartyVocalsChartSelection.CreateModifiedParts(chart, source, profile);
        var merged = PartyVocalsCoordinatorEngine.BuildMergedTrack(parts, parts[0].CloneAsInstrumentDifficulty());
        var live = new PartyVocalsCoordinatorEngine(merged, parts, chart.SyncTrack, parameters, false, 3);
        var manager = new EngineManager();
        manager.Register(live, merged, chart, RockMeterPreset.Normal);
        live.Reset();
        foreach (var input in inputs) { var copy = input; live.QueueInput(ref copy); }
        for (int tick = -120; tick <= 180; tick++) manager.UpdateEngines(tick / 60d);
        var frame = new ReplayFrame(profile, parameters, live.EngineStats, inputs.ToArray());
        // Round-trip the actual frame, including mic 0 and 1 which never emit input.
        using var bytes = new MemoryStream();
        using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true)) frame.Serialize(writer);
        using var buffer = FixedArray<byte>.Alloc((int) bytes.Length);
        bytes.ToArray().CopyTo(buffer.Span);
        var stream = new FixedArrayStream(buffer);
        frame = new ReplayFrame(ref stream, 20);
        var data = new ReplayData(new(), new(), new(), true, new[] { frame }, Array.Empty<double>());
        var info = new ReplayInfo("", "", 20, 0, default, "", "", "", default, default, 1, 3, 0, default, Array.Empty<PauseInfo>(), false, Array.Empty<ReplayStats>());
        var result = ReplayAnalyzer.AnalyzeReplay(chart, info, data, 60)[0];
        Assert.That(result.ResultStats.CommittedScore, Is.GreaterThan(0));
        Assert.That(result.ResultStats.CommittedScore, Is.EqualTo(live.EngineStats.CommittedScore));
        Assert.That(result.ResultStats.Combo, Is.EqualTo(live.EngineStats.Combo));
    }

    // A legacy Free Harmony replay on a solo-only song retains its one-mic fallback
    // and reproduces the recorded score, despite the unused Harmony source default.
    [Test]
    public void LegacyFreeHarmonyReplayFallsBackToSoloChart()
    {
        var chart = new SongChart(480);
        chart.SyncTrack.Tempos.Add(new TempoChange(120, 0, 0));
        chart.SyncTrack.TimeSignatures.Add(new TimeSignatureChange(4, 4, 0, 0, 0, 0, 0, 0));
        chart.Vocals.Parts.Add(Part(true));
        var emptyHarmony = Part(false);
        emptyHarmony.NotePhrases.Clear();
        chart.Harmony.Parts.Add(emptyHarmony);
        var profile = new YargProfile { GameMode = GameMode.Vocals, CurrentInstrument = Instrument.PartyVocals };
        var parameters = Parameters(1, Instrument.Harmony);
        var inputs = new System.Collections.Generic.List<GameInput>();
        for (int tick = 1; tick < 60; tick++)
            inputs.Add(new GameInput(tick / 60d, (int) VocalsAction.Pitch, 60f));

        // Establish the expected score directly from the solo chart, without fallback selection.
        var parts = PartyVocalsChartSelection.CreateModifiedParts(chart, Instrument.Vocals, profile);
        var primary = parts[0].CloneAsInstrumentDifficulty();
        var live = new PartyVocalsCoordinatorEngine(
            PartyVocalsCoordinatorEngine.BuildMergedTrack(parts, primary), parts, chart.SyncTrack, parameters, false, 1);
        var manager = new EngineManager();
        manager.Register(live, primary, chart, RockMeterPreset.Normal);
        live.Reset();
        foreach (var input in inputs) { var copy = input; live.QueueInput(ref copy); }
        for (int tick = -120; tick <= 180; tick++) manager.UpdateEngines(tick / 60d);

        // Write the v19 frame layout: v9 profile without chart preference, and
        // vocal parameters without the two new session fields.
        using var profileBytes = new MemoryStream();
        using (var writer = new BinaryWriter(profileBytes, System.Text.Encoding.UTF8, true)) profile.Serialize(writer);
        var legacyProfile = profileBytes.ToArray()[..^1];
        BitConverter.GetBytes(9).CopyTo(legacyProfile, 0);
        using var parameterBytes = new MemoryStream();
        using (var writer = new BinaryWriter(parameterBytes, System.Text.Encoding.UTF8, true)) parameters.Serialize(writer);
        using var bytes = new MemoryStream();
        using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RPFM"));
            writer.Write(legacyProfile);
            writer.Write(parameterBytes.ToArray()[..^2]);
            live.EngineStats.Serialize(writer);
            writer.Write(inputs.Count);
            foreach (var input in inputs)
            {
                writer.Write(input.Time);
                writer.Write(input.Action);
                writer.Write(input.Integer);
            }
        }
        using var buffer = FixedArray<byte>.Alloc((int) bytes.Length);
        bytes.ToArray().CopyTo(buffer.Span);
        var stream = new FixedArrayStream(buffer);
        var frame = new ReplayFrame(ref stream, 19);
        Assert.That(frame.Profile.GameMode, Is.EqualTo(GameMode.Vocals));
        Assert.That(frame.Profile.CurrentInstrument, Is.EqualTo(Instrument.PartyVocals));
        var restored = (VocalsEngineParameters) frame.EngineParameters;
        Assert.That(restored.PartyVocalsMicCount, Is.EqualTo(1));
        Assert.That(restored.PartyVocalsChartSource, Is.EqualTo(Instrument.Harmony));

        var data = new ReplayData(new(), new(), new(), true, new[] { frame }, Array.Empty<double>());
        var info = new ReplayInfo("", "", 19, 0, default, "", "", "", default, default, 1, 3, 0, default, Array.Empty<PauseInfo>(), false, Array.Empty<ReplayStats>());
        var result = ReplayAnalyzer.AnalyzeReplay(chart, info, data, 60)[0];
        Assert.That(result.ResultStats.CommittedScore, Is.GreaterThan(0));
        Assert.That(result.ResultStats.CommittedScore, Is.EqualTo(frame.Stats.CommittedScore));
        Assert.That(result.ResultStats.Combo, Is.EqualTo(frame.Stats.Combo));
    }

    // Replay construction rejects inputs outside the declared roster, rather than silently dropping them like the live engine.
    [TestCase(-1)]
    [TestCase(2)]
    public void ReplayRejectsMicOutsideRecordedSlots(int mic)
    {
        var profile = new YargProfile { GameMode = GameMode.PartyVocals, CurrentInstrument = Instrument.PartyVocals };
        Assert.Throws<InvalidDataException>(() => new ReplayFrame(profile, Parameters(2, Instrument.Vocals),
            new VocalsStats(), new[] { new GameInput(0, PartyVocalsInput.Pack(mic, VocalsAction.Pitch), 60f) }));
    }

    // Saving and loading parameters preserves the explicit mic count and chart source, independently of emitted inputs.
    [TestCase(1, Instrument.Vocals)]
    [TestCase(7, Instrument.Harmony)]
    public void ParametersRoundTripSilentSlotsAndSource(int count, Instrument source)
    {
        var parameters = Parameters(count, source);
        using var bytes = new MemoryStream();
        using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true)) parameters.Serialize(writer);
        using var buffer = FixedArray<byte>.Alloc((int) bytes.Length);
        bytes.ToArray().CopyTo(buffer.Span);
        var stream = new FixedArrayStream(buffer);
        var restored = new VocalsEngineParameters(ref stream, 20);
        Assert.That(restored.PartyVocalsMicCount, Is.EqualTo(count));
        Assert.That(restored.PartyVocalsChartSource, Is.EqualTo(source));
    }

    // Rejects session counts outside the supported range of one through seven microphones.
    [TestCase(0)]
    [TestCase(8)]
    public void RejectInvalidMicCount(int count)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Parameters(count, Instrument.Harmony));
    }

    // Older replay parameters lack the new fields and retain the historical one-mic/Harmony defaults.
    [Test]
    public void LegacyParametersDefaultToOneMic()
    {
        using var bytes = new MemoryStream();
        using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true)) Parameters(7, Instrument.Vocals).Serialize(writer);
        using var buffer = FixedArray<byte>.Alloc((int) bytes.Length - 2);
        bytes.ToArray().AsSpan(0, (int) bytes.Length - 2).CopyTo(buffer.Span);
        var stream = new FixedArrayStream(buffer);
        var restored = new VocalsEngineParameters(ref stream, 19);
        Assert.That(restored.PartyVocalsMicCount, Is.EqualTo(1));
        Assert.That(restored.PartyVocalsChartSource, Is.EqualTo(Instrument.Harmony));
    }

    // Empty harmony placeholders allow Solo fallback; actual singing content enables Harmony selection.
    // Explicit Solo remains Solo, and modifiers preserve part positions without changing the original chart.
    [Test]
    public void ResolutionUsesSingingContentAndPreservesPartIndexes()
    {
        var chart = new SongChart(480);
        chart.Vocals.Parts.Add(Part(true));
        chart.Harmony.Parts.Add(Part(false));
        chart.Harmony.Parts.Add(Part(false));
        Assert.That(PartyVocalsChartSelection.Resolve(chart, PartyVocalsChartPreference.Harmony), Is.EqualTo(Instrument.Vocals));
        chart.Harmony.Parts.Add(Part(true));
        Assert.That(PartyVocalsChartSelection.Resolve(chart, PartyVocalsChartPreference.Harmony), Is.EqualTo(Instrument.Harmony));
        Assert.That(PartyVocalsChartSelection.Resolve(chart, PartyVocalsChartPreference.Solo), Is.EqualTo(Instrument.Vocals));
        var profile = new YargProfile();
        profile.AddSingleModifier(Modifier.UnpitchedOnly);
        var parts = PartyVocalsChartSelection.CreateModifiedParts(chart, Instrument.Harmony, profile);
        Assert.That(parts.Count, Is.EqualTo(3));
        Assert.That(parts[2].NotePhrases[0].PhraseParentNote.ChildNotes[0].IsNonPitched, Is.True);
        Assert.That(chart.Harmony.Parts[2].NotePhrases[0].PhraseParentNote.ChildNotes[0].IsNonPitched, Is.False);
    }

    private static VocalsPart Part(bool content)
    {
        var part = new VocalsPart(true, new(), new(), new(), new(), new());
        var parent = new VocalNote(NoteFlags.None, false, 0, 2, 0, 960);
        if (content) parent.AddChildNote(new VocalNote(60, 0, VocalNoteType.Lyric, 0, 1, 0, 480));
        part.NotePhrases.Add(new VocalsPhrase(0, 2, 0, 960, parent, new()));
        return part;
    }

    private static VocalsEngineParameters Parameters(int count, Instrument source) => new(
        new HitWindowSettings(0.1, 0.1, 1, false, 0, 1, 1, 0, 0), 4,
        new float[] { 1, 2, 3, 4, 5 }, new float[] { 1, 2, 3, 4, 5 },
        1.5f, 0.5f, 0.75, 60, true, 1000, count, source);

    // Saved Free Harmony profiles migrate to Party Vocals while retaining their name; traditional Solo profiles stay traditional.
    [Test]
    public void JsonMigratesOnlyFreeHarmonies()
    {
        var party = JsonConvert.DeserializeObject<YargProfile>("{\"GameMode\":15,\"CurrentInstrument\":42,\"Name\":\"Singers\"}")!;
        var solo = JsonConvert.DeserializeObject<YargProfile>("{\"GameMode\":15,\"CurrentInstrument\":40}")!;
        Assert.That((byte) party.GameMode, Is.EqualTo(16));
        Assert.That(party.Name, Is.EqualTo("Singers"));
        Assert.That(solo.GameMode, Is.EqualTo(GameMode.Vocals));
    }

    // Profiles stored in replays retain their recorded mode instead of undergoing live-profile migration.
    [TestCase(9)]
    [TestCase(10)]
    public void BinaryLegacyProfileDoesNotMigrate(int version)
    {
        var profile = new YargProfile { GameMode = GameMode.Vocals, CurrentInstrument = Instrument.PartyVocals };
        using var bytes = new MemoryStream();
        using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true)) profile.Serialize(writer);
        var serialized = bytes.ToArray();
        if (version == 9)
        {
            serialized = serialized[..^1];
            BitConverter.GetBytes(9).CopyTo(serialized, 0);
        }
        using var buffer = FixedArray<byte>.Alloc(serialized.Length);
        serialized.CopyTo(buffer.Span);
        var stream = new FixedArrayStream(buffer);
        Assert.That(new YargProfile(ref stream).GameMode, Is.EqualTo(GameMode.Vocals));
    }
}