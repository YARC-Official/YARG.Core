using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Vocals;
using YARG.Core.Engine.Vocals.Engines;
using YARG.Core.Input;

namespace YARG.Core.UnitTests.Engine;

public sealed class VocalsCarriedNoteTests
{
    // Carried singing fills and scores each phrase it crosses, but not the silent phrase afterward.
    [TestCase(false, 1, 60f)]
    [TestCase(true, 1, 60f)]
    [TestCase(false, 2, 60f)]
    [TestCase(true, 2, 60f)]
    [TestCase(true, 1, -1f)]
    public void CarriedNoteOnlyPhraseAccumulatesMeterProgress(bool freeHarmonies, int boundaries, float pitch)
    {
        var first = new VocalNote(NoteFlags.None, false, 0.0, 1.0, 0, 960);
        first.AddChildNote(new VocalNote(pitch, 1, VocalNoteType.Lyric, 0.5, boundaries, 480, (uint) boundaries * 960));
        var phrases = new List<VocalsPhrase> { new(0.0, 1.0, 0, 960, first, []) };
        for (int phrase = 1; phrase <= boundaries + 1; phrase++)
        {
            uint tick = (uint) phrase * 960;
            var parent = new VocalNote(NoteFlags.None, false, phrase, 1.0, tick, 960);
            phrases.Add(new VocalsPhrase(phrase, 1.0, tick, 960, parent, []));
        }
        var part = new VocalsPart(true, phrases, [], [], [], []);
        var sync = new SyncTrack(480);
        sync.Tempos.Add(new TempoChange(120.0, 0.0, 0));
        sync.TimeSignatures.Add(new TimeSignatureChange(4, 4, 0.0, 0, 0, 0, 0, 0));
        var parameters = new VocalsEngineParameters(
            new HitWindowSettings(0.1, 0.1, 1.0, false, 0, 1, 1, 0, 0), 4,
            VocalsEngineTests.StarMultiplierThresholds, VocalsEngineTests.SoloBonusStarMultiplierThresholds,
            1.5f, 0.5f, 0.75, 60.0, true, 1000);
        var chart = part.CloneAsInstrumentDifficulty();
        var parts = new List<VocalsPart> { new(true, [], [], [], [], []), part };
        var merged = PartyVocalsCoordinatorEngine.BuildMergedTrack(parts, chart);
        VocalsEngine engine = freeHarmonies
            ? new PartyVocalsCoordinatorEngine(merged, parts, sync, parameters, false, 1)
            : new YargVocalsEngine(chart, sync, parameters, false);

        var results = new List<bool>();
        engine.OnPhraseHit += (_, fullPoints, _) => results.Add(fullPoints);
        int frame = 0;
        for (int phrase = 1; phrase <= boundaries; phrase++)
        {
            SingUntil(phrase + 0.25);
            Assert.That(engine.NoteIndex, Is.EqualTo(phrase));
            Assert.That(engine.PhraseTicksTotal, Is.EqualTo(phrase == boundaries ? 480 : 960),
                "Only the portion of the carried note inside this phrase counts toward its meter.");
            if (engine is PartyVocalsCoordinatorEngine coordinator)
                Assert.That(coordinator.CanonicalMeters[1], Is.GreaterThan(0.0));
            else
                Assert.That(engine.PhraseTicksHit, Is.GreaterThan(0.0));
        }

        SingUntil(boundaries + 1.25);
        Assert.That(engine.PhraseTicksTotal, Is.Zero, "The note has ended before this phrase.");
        Assert.That(results, Has.Count.EqualTo(boundaries + 1));
        Assert.That(results, Is.All.True, "Sustaining the matching pitch must score each crossed phrase.");
        if (engine is PartyVocalsCoordinatorEngine completed)
            Assert.That(completed.CanonicalMeters[1], Is.Zero);

        void SingUntil(double endTime)
        {
            for (; frame <= (int) (endTime * 120); frame++)
            {
                double time = frame / 120.0;
                var input = GameInput.Create(time, VocalsAction.Pitch, 60f);
                engine.QueueInput(ref input);
                engine.Update(time);
            }
        }
    }
}
