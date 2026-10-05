using MoonscraperChartEditor.Song;
using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.Parsing;

namespace YARG.Core.UnitTests.Parsing
{
    using static MoonSongLoaderTests;

    public class MoonSongLoaderTests_StaticLyrics
    {
        // "a" and "b" both come before the first note, which matches the closer "b" -- so "a"
        // has no note of its own. Static lyrics mode drops it; "b" and "c" must stay.
        private static VocalsPart LoadWithUnmatchedFirstLyric()
        {
            var song = CreateSong();
            var chart = song.GetChart(MoonSong.MoonInstrument.Vocals, MoonSong.Difficulty.Expert);

            chart.Add(new MoonPhrase(TICKS(0), TICKS(10), MoonPhrase.Type.Vocals_ScoringPhrase));

            chart.Add(new MoonText(TextEvents.LYRIC_PREFIX_WITH_SPACE + "a", TICKS(0)));
            chart.Add(new MoonText(TextEvents.LYRIC_PREFIX_WITH_SPACE + "b", TICKS(1)));
            chart.Add(new MoonText(TextEvents.LYRIC_PREFIX_WITH_SPACE + "c", TICKS(2)));

            chart.Add(new MoonNote(TICKS(1), 60, TICKS(0.5)));
            chart.Add(new MoonNote(TICKS(2), 62, TICKS(0.5)));

            var loader = new MoonSongLoader(song, ParseSettings.Default);
            return loader.LoadVocalsTrack(Instrument.Vocals).Parts[0];
        }

        [Test]
        public void StaticLyricsDropOnlyTheLyricWithoutANote()
        {
            var part = LoadWithUnmatchedFirstLyric();

            var staticLyrics = part.StaticLyricPhrases.SelectMany(p => p.Lyrics).Select(l => l.Text);
            Assert.That(staticLyrics, Is.EqualTo(new[] { "b", "c" }));
        }

        [Test]
        public void NoteLyricsKeepEveryLyric()
        {
            var part = LoadWithUnmatchedFirstLyric();

            var noteLyrics = part.NotePhrases.SelectMany(p => p.Lyrics).Select(l => l.Text);
            Assert.That(noteLyrics, Is.EqualTo(new[] { "a", "b", "c" }));
        }
    }
}
