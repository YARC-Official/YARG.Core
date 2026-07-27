using MoonscraperChartEditor.Song;
using NUnit.Framework;
using YARG.Core.Chart;

namespace YARG.Core.UnitTests.Parsing
{
    using static MoonSongLoaderTests;

    public class MoonSongLoaderTests_Lyrics
    {
        private static readonly List<LyricsPhrase> LyricPhrases =
        [
            // A state-of-the-art welding machine
            new(SECONDS(0), SECONDS(9), TICKS(0), TICKS(9),
            [
                new(LyricSymbolFlags.None,         "A",      SECONDS(0), TICKS(0)),

                new(LyricSymbolFlags.HyphenateWithNext, "state-", SECONDS(1), TICKS(1)),
                new(LyricSymbolFlags.HyphenateWithNext, "of-",    SECONDS(2), TICKS(2)),
                new(LyricSymbolFlags.HyphenateWithNext, "the-",   SECONDS(3), TICKS(3)),
                new(LyricSymbolFlags.None,         "art",    SECONDS(4), TICKS(4)),

                new(LyricSymbolFlags.JoinWithNext, "wel",    SECONDS(5), TICKS(5)),
                new(LyricSymbolFlags.None,         "ding",   SECONDS(6), TICKS(6)),

                new(LyricSymbolFlags.JoinWithNext, "ma",     SECONDS(7), TICKS(7)),
                new(LyricSymbolFlags.None,         "chine",  SECONDS(8), TICKS(8)),
            ]),
            // Built to construct many different parts
            new(SECONDS(9), SECONDS(11), TICKS(9), TICKS(11),
            [
                new(LyricSymbolFlags.None,         "Built",  SECONDS(9 + 0), TICKS(9 + 0)),
                new(LyricSymbolFlags.None,         "to",     SECONDS(9 + 1), TICKS(9 + 1)),

                new(LyricSymbolFlags.JoinWithNext, "con",    SECONDS(9 + 2), TICKS(9 + 2)),
                new(LyricSymbolFlags.None,         "struct", SECONDS(9 + 3), TICKS(9 + 3)),

                new(LyricSymbolFlags.JoinWithNext, "ma",     SECONDS(9 + 4), TICKS(9 + 4)),
                new(LyricSymbolFlags.None,         "ny",     SECONDS(9 + 5), TICKS(9 + 5)),

                // Ensure empty lyrics are handled correctly
                new(LyricSymbolFlags.JoinWithNext, "",       SECONDS(9 + 6),   TICKS(9 + 6)),
                new(LyricSymbolFlags.JoinWithNext, "",       SECONDS(9 + 6.5), TICKS(9 + 6.5)),
                new(LyricSymbolFlags.JoinWithNext, "",       SECONDS(9 + 6.75), TICKS(9 + 6.75)),

                new(LyricSymbolFlags.JoinWithNext, "dif",    SECONDS(9 + 7), TICKS(9 + 7)),
                new(LyricSymbolFlags.JoinWithNext, "fer",    SECONDS(9 + 8), TICKS(9 + 8)),
                new(LyricSymbolFlags.None,         "ent",    SECONDS(9 + 9), TICKS(9 + 9)),

                new(LyricSymbolFlags.None,         "parts",  SECONDS(9 + 10), TICKS(9 + 10)),
            ]),
            // For a high-speed assembly line
            new(SECONDS(20), SECONDS(8), TICKS(20), TICKS(8),
            [
                new(LyricSymbolFlags.None,         "For",   SECONDS(20 + 0), TICKS(20 + 0)),
                new(LyricSymbolFlags.None,         "a",     SECONDS(20 + 1), TICKS(20 + 1)),

                new(LyricSymbolFlags.HyphenateWithNext, "high-", SECONDS(20 + 2), TICKS(20 + 2)),
                new(LyricSymbolFlags.None,         "speed", SECONDS(20 + 3), TICKS(20 + 3)),

                new(LyricSymbolFlags.JoinWithNext, "as",    SECONDS(20 + 4), TICKS(20 + 4)),
                new(LyricSymbolFlags.JoinWithNext, "sem",   SECONDS(20 + 5), TICKS(20 + 5)),
                new(LyricSymbolFlags.None,         "bly",   SECONDS(20 + 6), TICKS(20 + 6)),

                new(LyricSymbolFlags.None,         "line",  SECONDS(20 + 7), TICKS(20 + 7)),
            ]),
        ];

        [TestCase]
        public void ParseLyrics()
        {
            var song = CreateSong();

            for (int phraseIndex = 0; phraseIndex < LyricPhrases.Count; phraseIndex++)
            {
                var phrase = LyricPhrases[phraseIndex];

                // Variants for ensuring proper handling:
                // - Start event after first lyric event
                // - No end event starting the next phrase
                int variant = phraseIndex % 3;
                bool startAfterFirst = variant == 0;
                bool noEndEvent = variant == 1;
                // bool noVariant = variant == 2;

                if (!startAfterFirst)
                    song.events.Add(new("phrase_start", phrase.Tick));

                int emptyCount = 0;
                for (int lyricIndex = 0; lyricIndex < phrase.Lyrics.Count; lyricIndex++)
                {
                    var lyric = phrase.Lyrics[lyricIndex];

                    string text = lyric.Text;
                    if (lyric.JoinOrHyphenateWithNext)
                    {
                        if (lyric.HyphenateWithNext && text.EndsWith('-'))
                            text = text[..^1] + LyricSymbols.LYRIC_JOIN_HYPHEN_SYMBOL;
                        else
                            text += LyricSymbols.LYRIC_JOIN_SYMBOL;
                    }

                    // Test both empty lyrics and stripped-symbol-only lyrics
                    if (string.IsNullOrEmpty(text))
                    {
                        text = (emptyCount++ % 3) switch
                        {
                            0 => "+",  // Pitch slide
                            1 => "  ", // Extra whitespace
                            _ => "",   // Empty lyric
                        };
                    }

                    song.events.Add(new($"lyric {text}", lyric.Tick));

                    if (lyricIndex == 0 && startAfterFirst)
                        song.events.Add(new("phrase_start", phrase.Tick));
                }

                if (phraseIndex >= LyricPhrases.Count - 1 || !noEndEvent)
                    song.events.Add(new("phrase_end", phrase.TickEnd));
            }

            var loader = new MoonSongLoader(song, ParseSettings.Default);
            var lyrics = loader.LoadLyrics();

            Assert.That(lyrics.Phrases, Has.Count.EqualTo(LyricPhrases.Count), "Lyric phrase count does not match!");

            var comparer = new LyricEventComparer();
            for (int i = 0; i < lyrics.Phrases.Count; i++)
            {
                // Can't use CollectionAssert here, as it only accepts IComparer,
                // so we settle for its implementation instead
                Assert.That(lyrics.Phrases[i].Lyrics, Is.EqualTo(LyricPhrases[i].Lyrics).Using(comparer),
                    $"Lyric phrase {i} does not match!");
            }
        }

        [Test]
        public void LyricPhrases_ParseIntoSectionsCorrectlyOnSameTickAsPhraseStart()
        {
            var song = CreateSong();
            /*
                  1354 = E "phrase_start"
                  1392 = E "lyric shouldBeInPhrase1"
                  2035 = E "lyric shouldBeInPhrase2"
                  2035 = E "phrase_start"
                  2035 = E "lyric shouldAlsoBeInPhrase2
                  2688 = E "phrase_end"
             */
            // Phrase 1
            song.events.Add(new("phrase_start", 1));
            song.events.Add(new("lyric shouldBeInPhrase1", 2));
            // Before the phrase start in the list, but on the same tick, so should still be in the next phrase.
            song.events.Add(new("lyric shouldBeInPhrase2", 3));
            // Phrase 2
            song.events.Add(new("phrase_start", 3));
            song.events.Add(new("lyric shouldAlsoBeInPhrase2", 3));
            song.events.Add(new("phrase_end", 4));

            var lyrics = new MoonSongLoader(song, ParseSettings.Default).LoadLyrics();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(lyrics.Phrases, Has.Count.EqualTo(2));

                Assert.That(lyrics.Phrases[0].Lyrics, Has.Count.EqualTo(1));
                Assert.That(lyrics.Phrases[0].Lyrics[0].Text, Is.EqualTo("shouldBeInPhrase1"));

                Assert.That(lyrics.Phrases[1].Lyrics, Has.Count.EqualTo(2));
                Assert.That(lyrics.Phrases[1].Lyrics[0].Text, Is.EqualTo("shouldBeInPhrase2"));
                Assert.That(lyrics.Phrases[1].Lyrics[1].Text, Is.EqualTo("shouldAlsoBeInPhrase2"));
            }
        }

        [Test]
        public void LyricPhrases_ParseIntoSectionsCorrectlyOnSameTickAsPhraseEnd()
        {
            var song = CreateSong();
            /*
              1354 = E "phrase_start"
              1392 = E "lyric phrase1"
              2035 = E "lyric phrase1Again"
              2035 = E "phrase_end"
              2039 = E "lyric ignored"
              2097 = E "phrase_start"
              2097 = E "lyric phrase2"
              2592 = E "lyric phrase2again"
              2688 = E "phrase_end"
             */

            // Phrase 1
            song.events.Add(new("phrase_start", 1));
            song.events.Add(new("lyric phrase1", 2));
            song.events.Add(new("lyric phrase1Again", 3));
            song.events.Add(new("phrase_end", 3));
            // Event in between phrases, should be ignored
            song.events.Add(new("lyric ignored", 4));
            // Phrase 2
            song.events.Add(new("phrase_start", 5));
            song.events.Add(new("lyric phrase2", 5));
            song.events.Add(new("lyric phrase2again", 6));
            song.events.Add(new("phrase_end", 7));

            var lyrics = new MoonSongLoader(song, ParseSettings.Default).LoadLyrics();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(lyrics.Phrases, Has.Count.EqualTo(2));

                Assert.That(lyrics.Phrases[0].Lyrics, Has.Count.EqualTo(2));
                Assert.That(lyrics.Phrases[0].Lyrics[0].Text, Is.EqualTo("phrase1"));
                Assert.That(lyrics.Phrases[0].Lyrics[1].Text, Is.EqualTo("phrase1Again"));

                Assert.That(lyrics.Phrases[1].Lyrics, Has.Count.EqualTo(2));
                Assert.That(lyrics.Phrases[1].Lyrics[0].Text, Is.EqualTo("phrase2"));
                Assert.That(lyrics.Phrases[1].Lyrics[1].Text, Is.EqualTo("phrase2again"));
            }
        }
    }
}
