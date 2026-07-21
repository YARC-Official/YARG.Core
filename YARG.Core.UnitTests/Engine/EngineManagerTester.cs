using Melanchall.DryWetMidi.Core;
using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Keys;
using YARG.Core.Game;

namespace YARG.Core.UnitTests.Engine;

public class EngineManagerTester : EngineTester
{

    // The default star thresholds are not the same for all engines as those used in gameplay,
    // but it doesn't matter here since we aren't testing scoring
    private readonly GuitarEngineParameters _guitarEngineParams =
        EnginePreset.Default.FiveFretGuitar.Create(StarMultiplierThresholds, SoloBonusStarMultiplierThresholds, false);
    private readonly GuitarEngineParameters _bassEngineParams =
        EnginePreset.Default.FiveFretGuitar.Create(StarMultiplierThresholds, SoloBonusStarMultiplierThresholds, true);
    private readonly DrumsEngineParameters _drumsEngineParams =
        EnginePreset.Default.Drums.Create(StarMultiplierThresholds, SoloBonusStarMultiplierThresholds, DrumsEngineParameters.DrumMode.ProFourLane);
    private readonly KeysEngineParameters _keysEngineParams =
        EnginePreset.Default.ProKeys.Create(StarMultiplierThresholds, SoloBonusStarMultiplierThresholds, false);

    [Test]
    public void EngineManagerHasFourEngines()
    {
        var (manager, engines) = CreateDefaultEngineManagerAndEngines();

        Assert.That(manager.Engines, Has.Count.EqualTo(4));
    }

    [Test]
    public void CanRemoveEngineFromManager()
    {
        var (manager, engines) = CreateDefaultEngineManagerAndEngines();

        manager.Unregister(engines[0]);

        Assert.That(manager.Engines, Has.Count.EqualTo(3));
    }

    [Test]
    public void EngineManagerGeneratesUnisonPhrases()
    {
        var guitarPhrases = EngineManager.GetUnisonPhrases(Instrument.FiveFretGuitar, Difficulty.Expert, GetChart(), false);
        var bassPhrases = EngineManager.GetUnisonPhrases(Instrument.FiveFretBass, Difficulty.Expert, GetChart(), true);
        var drumsPhrases = EngineManager.GetUnisonPhrases(Instrument.FourLaneDrums, Difficulty.Expert, GetChart(), false);
        var keysPhrases = EngineManager.GetUnisonPhrases(Instrument.ProKeys, Difficulty.Expert, GetChart(), false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(guitarPhrases, Has.Count.EqualTo(11));
            Assert.That(bassPhrases, Has.Count.EqualTo(11));
            Assert.That(drumsPhrases, Has.Count.EqualTo(11));
            Assert.That(keysPhrases, Has.Count.EqualTo(9));
        }
    }

    [Test]
    public void EngineManagerGeneratesEqualUnisonPhrases()
    {
        var tolerance = (GetChart().Resolution / 4) - 1; // 16th note tolerance
        Func<EngineManager.UnisonPhrase, EngineManager.UnisonPhrase, bool> comparer = (a, b) => EngineManager.TickWithinTolerance(a.Tick, b.Tick, tolerance) && EngineManager.TickWithinTolerance(a.TickEnd, b.TickEnd, tolerance);
        var guitarPhrases = EngineManager.GetUnisonPhrases(Instrument.FiveFretGuitar, Difficulty.Expert, GetChart(), false);
        var bassPhrases = EngineManager.GetUnisonPhrases(Instrument.FiveFretBass, Difficulty.Expert, GetChart(), false);
        var drumsPhrases = EngineManager.GetUnisonPhrases(Instrument.FourLaneDrums, Difficulty.Expert, GetChart(), true);
        var keysPhrases = EngineManager.GetUnisonPhrases(Instrument.ProKeys, Difficulty.Expert, GetChart(), false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(guitarPhrases, Is.EqualTo(bassPhrases).Using<EngineManager.UnisonPhrase, EngineManager.UnisonPhrase>(comparer));
            Assert.That(guitarPhrases, Is.EqualTo(drumsPhrases).Using<EngineManager.UnisonPhrase, EngineManager.UnisonPhrase>(comparer));
            // Keys does not participate in all unisons in the test chart
            Assert.That(guitarPhrases, Is.SupersetOf(keysPhrases).Using(comparer));
        }
    }

    [Test]
    public void EngineManagerUnisonPhraseNoteCountIsCorrect()
    {
        var chart = GetChart();
        var guitarPhrases = EngineManager.GetUnisonPhrases(Instrument.FiveFretGuitar, Difficulty.Expert, GetChart(), false);
        var bassPhrases = EngineManager.GetUnisonPhrases(Instrument.FiveFretBass, Difficulty.Expert, GetChart(), false);
        var drumsPhrases = EngineManager.GetUnisonPhrases(Instrument.FourLaneDrums, Difficulty.Expert, GetChart(), true);
        var keysPhrases = EngineManager.GetUnisonPhrases(Instrument.ProKeys, Difficulty.Expert, GetChart(), false);

        using (Assert.EnterMultipleScope())
        {
            foreach (var phrase in guitarPhrases)
            {
                var notes = chart.GetFiveFretTrack(Instrument.FiveFretGuitar).GetDifficulty(Difficulty.Expert).Notes;
                var noteCount = notes.Count(n => n.Tick >= phrase.Tick && n.Tick < phrase.TickEnd);
                Assert.That(phrase.NoteCount, Is.EqualTo(noteCount));
            }
            foreach (var phrase in bassPhrases)
            {
                var notes = chart.GetFiveFretTrack(Instrument.FiveFretBass).GetDifficulty(Difficulty.Expert).Notes;
                var noteCount = notes.Count(n => n.Tick >= phrase.Tick && n.Tick < phrase.TickEnd);
                Assert.That(phrase.NoteCount, Is.EqualTo(noteCount));
            }
            foreach (var phrase in drumsPhrases)
            {
                var notes = chart.GetDrumsTrack(Instrument.FourLaneDrums).GetDifficulty(Difficulty.Expert).Notes;
                var noteCount = notes.Sum(n => (n.Tick >= phrase.Tick && n.Tick < phrase.TickEnd) ? n.ChildNotes.Count + 1 : 0);
                Assert.That(phrase.NoteCount, Is.EqualTo(noteCount));
            }

            foreach (var phrase in keysPhrases)
            {
                // For some reason, chart.GetProKeysTrack doesn't exist?
                var notes = CreateEngine(_keysEngineParams, true).Notes.Notes;
                var noteCount = notes.Count(n => n.Tick >= phrase.Tick && n.Tick < phrase.TickEnd);
                Assert.That(phrase.NoteCount, Is.EqualTo(noteCount));
            }
        }
    }

    [Test]
    public void EngineManagerAddsUnisonPhrasesToGlobalListCorrectly()
    {
        var engineManager = new EngineManager();
        var chart = GetChart();

        var guitarEngineContainer = engineManager.Register(CreateEngine(_guitarEngineParams, true, false).Engine, Instrument.FiveFretGuitar, Difficulty.Expert, chart, RockMeterPreset.Normal);
        var bassEngineContainer = engineManager.Register(CreateEngine(_bassEngineParams, true, true).Engine, Instrument.FiveFretBass, Difficulty.Expert, chart, RockMeterPreset.Normal);
        var drumsEngineContainer = engineManager.Register(CreateEngine(_drumsEngineParams, true).Engine, Instrument.FourLaneDrums, Difficulty.Expert, chart, RockMeterPreset.Normal);
        var keysEngineContainer = engineManager.Register(CreateEngine(_keysEngineParams, true).Engine, Instrument.ProKeys, Difficulty.Expert, chart, RockMeterPreset.Normal);

        var globalUnisonEvents = engineManager.UnisonEvents;
        using (Assert.EnterMultipleScope())
        {
            // Check that every unison phrase is accounted for here
            foreach (var phrase in guitarEngineContainer.UnisonPhrases)
            {
                // Exactly one global unison event should have this phrase in its ParticipantsToPhrase list
                Assert.That(globalUnisonEvents.Count(e => e.ParticipantToPhrase.TryGetValue(guitarEngineContainer.EngineId, out var foundPhrase) && foundPhrase == phrase), Is.EqualTo(1));
            }
            foreach (var phrase in bassEngineContainer.UnisonPhrases)
            {
                Assert.That(globalUnisonEvents.Count(e => e.ParticipantToPhrase.TryGetValue(bassEngineContainer.EngineId, out var foundPhrase) && foundPhrase == phrase), Is.EqualTo(1));
            }

            foreach (var phrase in drumsEngineContainer.UnisonPhrases)
            {
                Assert.That(globalUnisonEvents.Count(e => e.ParticipantToPhrase.TryGetValue(drumsEngineContainer.EngineId, out var foundPhrase) && foundPhrase == phrase), Is.EqualTo(1));
            }

            foreach (var phrase in keysEngineContainer.UnisonPhrases)
            {
                Assert.That(globalUnisonEvents.Count(e => e.ParticipantToPhrase.TryGetValue(keysEngineContainer.EngineId, out var foundPhrase) && foundPhrase == phrase), Is.EqualTo(1));
            }
        }
    }

    [Test]
    public void EqualStarpowerSectionsAreEqual()
    {
        var phrase1 = new Phrase(PhraseType.StarPower, 8.4000003337860107, 1.2750000506639481, 13440, 2040);
        var phrase2 = new Phrase(PhraseType.StarPower, 8.4000003337860107, 1.2750000506639481, 13440, 2040);

        var starpowerSection1 = new EngineManager.StarPowerSection(8.4000003337860107, 9.675000384449958, phrase1);
        var starpowerSection2 = new EngineManager.StarPowerSection(8.4000003337860107, 9.675000384449958, phrase2);

        Assert.That(starpowerSection1, Is.EqualTo(starpowerSection2).UsingPropertiesComparer());
    }

    [Test]
    public void UnequalStarpowerSectionsAreNotEqual()
    {
        var phrase1 = new Phrase(PhraseType.StarPower, 8.4000003337860107, 1.2750000506639481, 13440, 2040);
        var phrase2 = new Phrase(PhraseType.StarPower, 8.4000003337860107, 1.275625050688783, 13440, 2041);

        var starpowerSection1 = new EngineManager.StarPowerSection(8.4000003337860107, 9.675000384449958, phrase1);
        var starpowerSection2 = new EngineManager.StarPowerSection(8.4000003337860107, 9.67562539067384, phrase2);

        Assert.That(starpowerSection1, Is.Not.EqualTo(starpowerSection2).UsingPropertiesComparer());
    }

    [Test]
    public void StarPowerSectionsCanBeAlmostEqual()
    {
        var phrase1 = new Phrase(PhraseType.StarPower, 8.4000003337860107, 1.2750000506639481, 13440, 2040);
        var phrase2 = new Phrase(PhraseType.StarPower, 8.4000003337860107, 1.275625050688783, 13440, 2041);

        var starpowerSection1 = new EngineManager.StarPowerSection(8.4000003337860107, 9.675000384449958, phrase1);
        var starpowerSection2 = new EngineManager.StarPowerSection(8.4000003337860107, 9.67562539067384, phrase2);

        Assert.That(starpowerSection1.TickAlmostEquals(starpowerSection2, 16), Is.True);
    }

    [Test]
    public void StarPowerSectionsAreNotAlmostEqualIfEndTicksAreOutsideOfTolerance()
    {
        var phrase1 = new Phrase(PhraseType.StarPower, 8.4000003337860107, 1.2750000506639481, 13440, 2040);
        var phrase2 = new Phrase(PhraseType.StarPower, 8.4000003337860107, 1.337500053147475, 13440, 2140);

        var starpowerSection1 = new EngineManager.StarPowerSection(8.4000003337860107, 9.675000384449958, phrase1);
        var starpowerSection2 = new EngineManager.StarPowerSection(8.4000003337860107, 9.737500386933485, phrase2);

        Assert.That(starpowerSection1.TickAlmostEquals(starpowerSection2, 16), Is.False);
    }

    private (EngineManager manager, EngineManager.EngineContainer[] engines) CreateDefaultEngineManagerAndEngines()
    {
        var manager = new EngineManager();

        var chart = GetChart();

        var guitarEngine = CreateEngine(_guitarEngineParams, true, false);
        var bassEngine = CreateEngine(_bassEngineParams, true, true);
        var drumsEngine = CreateEngine(_drumsEngineParams, true);
        var keysEngine = CreateEngine(_keysEngineParams, true);

        var guitarContainer = manager.Register(guitarEngine.Engine, Instrument.FiveFretGuitar, Difficulty.Expert, chart, RockMeterPreset.Normal);
        var bassContainer = manager.Register(bassEngine.Engine, Instrument.FiveFretBass, Difficulty.Expert, chart, RockMeterPreset.Normal);
        var drumsContainer = manager.Register(drumsEngine.Engine, Instrument.FourLaneDrums, Difficulty.Expert, chart, RockMeterPreset.Normal);
        var keysContainer = manager.Register(keysEngine.Engine, Instrument.ProKeys, Difficulty.Expert, chart, RockMeterPreset.Normal);

        return (manager, [guitarContainer, bassContainer, drumsContainer, keysContainer]);
    }


}