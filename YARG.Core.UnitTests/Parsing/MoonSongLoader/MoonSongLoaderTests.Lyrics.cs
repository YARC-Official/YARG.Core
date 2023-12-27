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
            new(SECONDS(10), SECONDS(10), TICKS(10), TICKS(10),
            [
                new(LyricFlags.None,         "Built",  SECONDS(0), TICKS(0)),
                new(LyricFlags.None,         "to",     SECONDS(1), TICKS(1)),

                new(LyricFlags.JoinWithNext, "con",    SECONDS(2), TICKS(2)),
                new(LyricFlags.None,         "struct", SECONDS(3), TICKS(3)),

                new(LyricFlags.JoinWithNext, "ma",     SECONDS(4), TICKS(4)),
                new(LyricFlags.None,         "ny",     SECONDS(5), TICKS(5)),

                new(LyricFlags.JoinWithNext, "dif",    SECONDS(6), TICKS(6)),
                new(LyricFlags.JoinWithNext, "fer",    SECONDS(7), TICKS(7)),
                new(LyricFlags.None,         "ent",    SECONDS(8), TICKS(8)),

                new(LyricFlags.None,         "parts",  SECONDS(9), TICKS(9)),
            ]),
            // For a high-speed assembly line
            new(SECONDS(20), SECONDS(8), TICKS(20), TICKS(8),
            [
                new(LyricFlags.None,         "For",   SECONDS(0), TICKS(0)),
                new(LyricFlags.None,         "a",     SECONDS(1), TICKS(1)),

                new(LyricFlags.JoinWithNext, "high-", SECONDS(2), TICKS(2)),
                new(LyricFlags.None,         "speed", SECONDS(3), TICKS(3)),

                new(LyricFlags.JoinWithNext, "as",    SECONDS(4), TICKS(4)),
                new(LyricFlags.JoinWithNext, "sem",   SECONDS(5), TICKS(5)),
                new(LyricFlags.None,         "bly",   SECONDS(6), TICKS(6)),

                new(LyricFlags.None,         "line",  SECONDS(7), TICKS(7)),
            ]),
        ];

        [TestCase]
        public void ParseLyrics()
        {
            var song = CreateSong();

            foreach (var phrase in LyricPhrases)
            {
                song.events.Add(new("phrase_start", phrase.Tick));

                foreach (var lyric in phrase.Lyrics)
                {
                    string text = lyric.Text;
                    if (lyric.JoinWithNext)
                    {
                        if (text.EndsWith('-'))
                            text = text[..^1] + LyricSymbols.LYRIC_JOIN_HYPHEN_SYMBOL;
                        else
                            text += LyricSymbols.LYRIC_JOIN_SYMBOL;
                    }

                    song.events.Add(new($"lyric {text}", lyric.Tick));
                }

                song.events.Add(new("phrase_end", phrase.TickEnd));
            }

            var loader = new MoonSongLoader(song, ParseSettings.Default);
            var lyrics = loader.LoadLyrics();

            Assert.That(LyricPhrases, Has.Count.EqualTo(lyrics.Phrases.Count), "Lyric phrase count does not match!");

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