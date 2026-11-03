using System.Collections.Generic;
using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Vocals;
using YARG.Core.Engine.Vocals.Engines;
using YARG.Core.Input;

namespace YARG.Core.UnitTests.Engine;

[TestFixture]
public sealed class PartyVocalsDisplayPartIntegrationTests
{
    // Each displayed note must follow its assigned part, including octave differences.
    // Retain that result within a visual frame, then clear it on reset.
    [Test]
    public void JointDisplayNoteFollowsSelectedOctaveAndSurvivesLaterTicksInFrame()
    {
        var engine = CreateEngine(micCount: 2, partCount: 2, octaves: true);
        Assert.That(engine.GetMicDisplayNote(0), Is.Null);
        Sing(engine, 0.1, 0.3, micCount: 2);
        Assert.That(engine.GetMicDisplayPart(0), Is.EqualTo(0));
        Assert.That(engine.GetMicDisplayPart(1), Is.EqualTo(1));
        Assert.That(engine.GetMicDisplayNote(0), Is.Not.Null);
        Assert.That(engine.GetMicDisplayNote(1), Is.Not.Null);
        Assert.That(engine.GetMicDisplayNote(0)!.Pitch, Is.EqualTo(60));
        Assert.That(engine.GetMicDisplayNote(1)!.Pitch, Is.EqualTo(72));
        var note = engine.GetMicDisplayNote(1);
        engine.Update(1.1); // past the child note; no new sample and no visual-frame reset
        Assert.That(engine.GetMicDisplayNote(1), Is.SameAs(note));
        engine.ResetMicSangFlags();
        Assert.That(engine.GetMicDisplayNote(1), Is.Null);
        engine.Reset();
        Sing(engine, 0.1, 0.3, micCount: 2);
        Assert.That(engine.GetMicDisplayNote(1)!.Pitch, Is.EqualTo(72));
        engine.Reset();
        Assert.That(engine.GetMicDisplayNote(0), Is.Null);
        Assert.That(engine.GetMicDisplayNote(1), Is.Null);
        Assert.That(engine.GetMicDisplayNote(-1), Is.Null);
        Assert.That(engine.GetMicDisplayNote(2), Is.Null);
    }

    // Seven matching mics must represent all three parts even when their display results are queried in reverse order.
    [Test]
    public void SevenMicDisplayUsesJointFrameMatchesRegardlessOfQueryOrder()
    {
        var engine = CreateEngine();
        Assert.That(engine.GetMicDisplayPart(0), Is.EqualTo(-1));
        Sing(engine, 0.1, 0.3);
        var expected = new[] { 0, 1, 2, 0, 0, 0, 0 };
        for (int mic = 6; mic >= 0; mic--)
        {
            Assert.That(engine.GetMicHittingParts(mic), Is.EqualTo(7u));
            Assert.That(engine.GetMicDisplayPart(mic), Is.EqualTo(expected[mic]));
        }
        Assert.That(engine.GetMicDisplayPart(-1), Is.EqualTo(-1));
        Assert.That(engine.GetMicDisplayPart(7), Is.EqualTo(-1));
    }

    // Core frame resets and full engine resets discard previous matching results; fresh singing
    // establishes assignments again. Unity separately retains needle feedback between mic samples.
    [Test]
    public void FrameResetAndEngineResetDiscardCachedAssignments()
    {
        var engine = CreateEngine();
        Sing(engine, 0.1, 0.3);
        Assert.That(engine.GetMicDisplayPart(1), Is.EqualTo(1));
        engine.ResetMicSangFlags();
        for (int mic = 0; mic < 7; mic++) Assert.That(engine.GetMicDisplayPart(mic), Is.EqualTo(-1));
        Sing(engine, 0.31, 0.4);
        Assert.That(engine.GetMicDisplayPart(1), Is.EqualTo(1));
        engine.Reset();
        for (int mic = 0; mic < 7; mic++) Assert.That(engine.GetMicDisplayPart(mic), Is.EqualTo(-1));
        Sing(engine, 0.1, 0.3);
        Assert.That(engine.GetMicDisplayPart(1), Is.EqualTo(1));
    }

    private static PartyVocalsCoordinatorEngine CreateEngine(int micCount = 7, int partCount = 3, bool octaves = false)
    {
        var parts = new List<VocalsPart>();
        for (int partIndex = 0; partIndex < partCount; partIndex++)
        {
            var part = new VocalsPart(partIndex != 0, new(), new(), new(), new(), new());
            var parent = new VocalNote(NoteFlags.None, false, 0, 2, 0, 1920);
            parent.AddChildNote(new VocalNote(60 + (octaves ? 12 * partIndex : 0), 0, VocalNoteType.Lyric, 0, 1, 0, 960));
            part.NotePhrases.Add(new VocalsPhrase(0, 2, 0, 1920, parent,
                new List<LyricEvent> { new(LyricSymbolFlags.None, "la", 0, 0) }));
            parts.Add(part);
        }
        var sync = new SyncTrack(480);
        sync.Tempos.Add(new TempoChange(120, 0, 0));
        sync.TimeSignatures.Add(new TimeSignatureChange(4, 4, 0, 0, 0, 0, 0, 0));
        var parameters = new VocalsEngineParameters(
            new HitWindowSettings(0.1, 0.1, 1, false, 0, 1, 1, 0, 0), 4,
            VocalsEngineTests.StarMultiplierThresholds, VocalsEngineTests.SoloBonusStarMultiplierThresholds,
            1.5f, 0.5f, 0.75, 60, false, 1000);
        return new PartyVocalsCoordinatorEngine(parts[0].CloneAsInstrumentDifficulty(), parts, sync,
            parameters, false, micCount);
    }

    private static void Sing(PartyVocalsCoordinatorEngine engine, double start, double end, int micCount = 7)
    {
        for (double time = start; time < end; time += 1.0 / 60)
        {
            for (int mic = 0; mic < micCount; mic++)
            {
                var input = new GameInput(time, PartyVocalsInput.Pack(mic, VocalsAction.Pitch), 60f);
                engine.QueueInput(ref input);
            }
            engine.Update(time);
        }
    }
}
