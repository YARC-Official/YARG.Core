using NUnit.Framework;
using YARG.Core.Chart;

namespace YARG.Core.UnitTests.Chart
{
    /// <summary>
    /// Verifies the phoneme-to-viseme mapping used by <see cref="LipsyncGenerator"/> against the
    /// values observed in handmade Milo lipsync data (12 Rock Band charts, per-phoneme lift analysis).
    /// </summary>
    /// <remarks>
    /// The generator is exercised through <see cref="LipsyncGenerator.GenerateFromLyrics"/> on
    /// single-word lyrics with a minimal embedded CMU dictionary, so the tests are deterministic
    /// and independent of the game's full dictionary asset.
    /// </remarks>
    public class LipsyncGeneratorTests
    {
        // Minimal CMU dictionary covering all test words (stress markers included to exercise parsing)
        private const string MiniDictionary =
            """
            THE  DH AH0
            THINK  TH IH1 NG2 K
            OFF  AO1 F
            HER  HH ER0
            DAY  D EY1
            GO  G OW1
            BOY  B OY1
            MY  M AY1
            CAT  K AE1 T
            SHE  SH IY1
            SING  S IH1 NG
            LOVE  L AH1 V
            BED  B EH1 D
            LA  L AH1
            GO1  G OW1
            """;

        [OneTimeSetUp]
        public void SetUp()
        {
            LipsyncGenerator.Initialize(MiniDictionary);
        }

        private static List<LipsyncEvent.LipsyncType> GetEmittedVisemes(string word)
        {
            var lyric = new LyricEvent(LyricSymbolFlags.None, word, 0.0, 0);
            var phrase = new LyricsPhrase(0.0, 1.0, 0, 480, new List<LyricEvent> { lyric });
            var track = new LyricsTrack(new List<LyricsPhrase> { phrase });

            var events = LipsyncGenerator.GenerateFromLyrics(track);
            // Zero-weight events are intentional release keyframes, not mouth shapes; skip them.
            // Blink/expression types may also appear (randomized) and are harmless here:
            // assertions only check for the presence/absence of specific viseme types.
            return events.Where(e => e.Value > 0.01f)
                .Select(e => e.Type)
                .Distinct()
                .ToList();
        }

        private static void AssertEmits(string word, params LipsyncEvent.LipsyncType[] expected)
        {
            var visemes = GetEmittedVisemes(word);
            foreach (var type in expected)
            {
                Assert.That(visemes, Does.Contain(type),
                    $"Word '{word}' should emit {type}. Emitted: {string.Join(", ", visemes)}");
            }
        }

        private static void AssertDoesNotEmit(string word, params LipsyncEvent.LipsyncType[] forbidden)
        {
            var visemes = GetEmittedVisemes(word);
            foreach (var type in forbidden)
            {
                Assert.That(visemes, Does.Not.Contain(type),
                    $"Word '{word}' should not emit {type}. Emitted: {string.Join(", ", visemes)}");
            }
        }

        [Test]
        public void TH_And_DH_MapToThough()
        {
            AssertEmits("think", LipsyncEvent.LipsyncType.Though_lo);
            AssertDoesNotEmit("think", LipsyncEvent.LipsyncType.Told_lo);
            AssertEmits("the", LipsyncEvent.LipsyncType.Though_lo);
            AssertDoesNotEmit("the", LipsyncEvent.LipsyncType.Told_lo);
        }

        [Test]
        public void AO_MapsToOx()
        {
            AssertEmits("off", LipsyncEvent.LipsyncType.Ox_lo);
            AssertDoesNotEmit("off", LipsyncEvent.LipsyncType.Earth_lo);
        }

        [Test]
        public void ER_MapsToEarth()
        {
            AssertEmits("her", LipsyncEvent.LipsyncType.Earth_lo);
            AssertDoesNotEmit("her", LipsyncEvent.LipsyncType.Church_lo);
        }

        [Test]
        public void EY_MapsToOxThenIf()
        {
            AssertEmits("day", LipsyncEvent.LipsyncType.Ox_lo, LipsyncEvent.LipsyncType.If_lo);
            AssertDoesNotEmit("day", LipsyncEvent.LipsyncType.Cage_lo);
        }

        [Test]
        public void OW_MapsToOxHeld()
        {
            AssertEmits("go", LipsyncEvent.LipsyncType.Ox_lo);
            AssertDoesNotEmit("go", LipsyncEvent.LipsyncType.Oat_lo, LipsyncEvent.LipsyncType.Wet_lo);
        }

        [Test]
        public void OY_MapsToOxThenEat()
        {
            AssertEmits("boy", LipsyncEvent.LipsyncType.Ox_lo, LipsyncEvent.LipsyncType.Eat_lo);
            AssertDoesNotEmit("boy", LipsyncEvent.LipsyncType.Oat_lo, LipsyncEvent.LipsyncType.If_lo);
        }

        [Test]
        public void AY_MapsToEatThenIf()
        {
            AssertEmits("my", LipsyncEvent.LipsyncType.Eat_lo, LipsyncEvent.LipsyncType.If_lo);
        }

        [Test]
        public void K_And_G_MapToCage()
        {
            AssertEmits("cat", LipsyncEvent.LipsyncType.Cage_lo);
            AssertEmits("go", LipsyncEvent.LipsyncType.Cage_lo);
        }

        [Test]
        public void SH_MapsToChurch()
        {
            AssertEmits("she", LipsyncEvent.LipsyncType.Church_lo);
            AssertDoesNotEmit("she", LipsyncEvent.LipsyncType.Told_lo);
        }

        [Test]
        public void NG_MapsToNew()
        {
            AssertEmits("sing", LipsyncEvent.LipsyncType.New_lo);
        }

        [Test]
        public void L_MapsToTold()
        {
            AssertEmits("love", LipsyncEvent.LipsyncType.Told_lo);
            AssertDoesNotEmit("love", LipsyncEvent.LipsyncType.New_lo);
        }

        [Test]
        public void EH_MapsToIf()
        {
            AssertEmits("bed", LipsyncEvent.LipsyncType.If_lo);
            AssertDoesNotEmit("bed", LipsyncEvent.LipsyncType.Cage_lo);
        }

        [Test]
        public void GapBetweenWordsClosesAllVisemeChannels()
        {
            // Viseme channels persist until explicitly rewritten; the generator must zero every
            // used channel during silence, or the mouth stays open between words/parts.
            // Two separate phrases with a long instrumental gap between them
            var phrase1 = new LyricsPhrase(0.0, 0.5, 0, 1000, new List<LyricEvent>
            {
                new(LyricSymbolFlags.None, "go", 0.0, 0),
            });
            var phrase2 = new LyricsPhrase(5.0, 0.5, 5000, 1000, new List<LyricEvent>
            {
                new(LyricSymbolFlags.None, "la", 5.0, 5000),
            });
            var track = new LyricsTrack(new List<LyricsPhrase> { phrase1, phrase2 });

            var events = LipsyncGenerator.GenerateFromLyrics(track);

            // Viseme channels persist until rewritten, so for EVERY viseme type the last event
            // before the gap must be zero-weight, or that channel holds the mouth open.
            var visemeEvents = events
                .Where(e => e.Time < 4.5)
                .Where(e => e.Type.ToString().EndsWith("_lo", StringComparison.Ordinal)
                    || e.Type.ToString().EndsWith("_hi", StringComparison.Ordinal))
                .GroupBy(e => e.Type);

            Assert.That(visemeEvents, Is.Not.Empty, "Expected viseme events before the gap");
            foreach (var group in visemeEvents)
            {
                var last = group.OrderBy(e => e.Time).Last();
                Assert.That(last.Value, Is.EqualTo(0).Within(0.0001),
                    $"Viseme channel {group.Key} must end at zero before silence");
            }
        }

        [Test]
        public void NoHiVisemesAreEverEmitted()
        {
            foreach (var word in new[] { "the", "think", "off", "her", "day", "go", "boy", "my", "cat", "she", "sing", "love", "bed" })
            {
                var visemes = GetEmittedVisemes(word);
                Assert.That(visemes.Where(t => t.ToString().EndsWith("_hi", StringComparison.Ordinal)),
                    Is.Empty, $"Word '{word}' emitted _hi visemes");
            }
        }
    }
}
