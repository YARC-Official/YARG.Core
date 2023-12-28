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
                new(LyricFlags.None,         "A",      SECONDS(0), TICKS(0)),

                new(LyricFlags.JoinWithNext, "state-", SECONDS(1), TICKS(1)),
                new(LyricFlags.JoinWithNext, "of-",    SECONDS(2), TICKS(2)),
                new(LyricFlags.JoinWithNext, "the-",   SECONDS(3), TICKS(3)),
                new(LyricFlags.None,         "art",    SECONDS(4), TICKS(4)),

                new(LyricFlags.JoinWithNext, "wel",    SECONDS(5), TICKS(5)),
                new(LyricFlags.None,         "ding",   SECONDS(6), TICKS(6)),

                new(LyricFlags.JoinWithNext, "ma",     SECONDS(7), TICKS(7)),
                new(LyricFlags.None,         "chine",  SECONDS(8), TICKS(8)),
            ]),
            // Built to construct many different parts
            new(SECONDS(9), SECONDS(11), TICKS(9), TICKS(11),
            [
                new(LyricFlags.None,         "Built",  SECONDS(9 + 0), TICKS(9 + 0)),
                new(LyricFlags.None,         "to",     SECONDS(9 + 1), TICKS(9 + 1)),

                new(LyricFlags.JoinWithNext, "con",    SECONDS(9 + 2), TICKS(9 + 2)),
                new(LyricFlags.None,         "struct", SECONDS(9 + 3), TICKS(9 + 3)),

                new(LyricFlags.JoinWithNext, "ma",     SECONDS(9 + 4), TICKS(9 + 4)),
                new(LyricFlags.None,         "ny",     SECONDS(9 + 5), TICKS(9 + 5)),

                // Ensure empty lyrics are handled correctly
                new(LyricFlags.JoinWithNext, "",       SECONDS(9 + 6),   TICKS(9 + 6)),
                new(LyricFlags.JoinWithNext, "",       SECONDS(9 + 6.5), TICKS(9 + 6.5)),
                new(LyricFlags.JoinWithNext, "",       SECONDS(9 + 6.75), TICKS(9 + 6.75)),

                new(LyricFlags.JoinWithNext, "dif",    SECONDS(9 + 7), TICKS(9 + 7)),
                new(LyricFlags.JoinWithNext, "fer",    SECONDS(9 + 8), TICKS(9 + 8)),
                new(LyricFlags.None,         "ent",    SECONDS(9 + 9), TICKS(9 + 9)),

                new(LyricFlags.None,         "parts",  SECONDS(9 + 10), TICKS(9 + 10)),
            ]),
            // For a high-speed assembly line
            new(SECONDS(20), SECONDS(8), TICKS(20), TICKS(8),
            [
                new(LyricFlags.None,         "For",   SECONDS(20 + 0), TICKS(20 + 0)),
                new(LyricFlags.None,         "a",     SECONDS(20 + 1), TICKS(20 + 1)),

                new(LyricFlags.JoinWithNext, "high-", SECONDS(20 + 2), TICKS(20 + 2)),
                new(LyricFlags.None,         "speed", SECONDS(20 + 3), TICKS(20 + 3)),

                new(LyricFlags.JoinWithNext, "as",    SECONDS(20 + 4), TICKS(20 + 4)),
                new(LyricFlags.JoinWithNext, "sem",   SECONDS(20 + 5), TICKS(20 + 5)),
                new(LyricFlags.None,         "bly",   SECONDS(20 + 6), TICKS(20 + 6)),

                new(LyricFlags.None,         "line",  SECONDS(20 + 7), TICKS(20 + 7)),
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
                    if (lyric.JoinWithNext)
                    {
                        if (text.EndsWith('-'))
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
    }
}