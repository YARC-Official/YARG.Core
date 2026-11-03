using MoonscraperChartEditor.Song;
using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.Parsing;

namespace YARG.Core.UnitTests.Parsing
{
    using static MoonSongLoaderTests;

    public class MoonSongLoaderTests_VocalsPhrases
    {
        private static VocalsPart LoadVocals(params (double start, double end)[] phrases)
        {
            var song = CreateSong();
            var chart = song.GetChart(MoonSong.MoonInstrument.Vocals, MoonSong.Difficulty.Expert);

            foreach (var (start, end) in phrases)
            {
                chart.Add(new MoonPhrase(TICKS(start), TICKS(end - start), MoonPhrase.Type.Vocals_ScoringPhrase));
            }

            AddLyricNote(chart, 0, "Hel", 60);
            AddLyricNote(chart, 1, "lo", 62);
            AddLyricNote(chart, 4, "Next", 64);

            var loader = new MoonSongLoader(song, ParseSettings.Default);
            return loader.LoadVocalsTrack(Instrument.Vocals).Parts[0];
        }

        private static void AddLyricNote(MoonChart chart, double beat, string lyric, int pitch)
        {
            chart.Add(new MoonText(TextEvents.LYRIC_PREFIX_WITH_SPACE + lyric, TICKS(beat)));
            chart.Add(new MoonNote(TICKS(beat), pitch, TICKS(0.5)));
        }

        private static IEnumerable<uint> NoteTicks(VocalsPhrase phrase)
        {
            return phrase.PhraseParentNote.ChildNotes.Select(note => note.Tick);
        }

        private static IEnumerable<string> LyricTexts(VocalsPhrase phrase)
        {
            return phrase.Lyrics.Select(lyric => lyric.Text);
        }

        [Test]
        public void NoteOnPhraseEndTick_BelongsToThatPhrase()
        {
            var part = LoadVocals((0, 1), (4, 5));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(part.NotePhrases, Has.Count.EqualTo(2));
                Assert.That(NoteTicks(part.NotePhrases[0]), Is.EqualTo(new[] { TICKS(0), TICKS(1) }));
                Assert.That(LyricTexts(part.NotePhrases[0]), Is.EqualTo(new[] { "Hel", "lo" }));
                Assert.That(NoteTicks(part.NotePhrases[1]), Is.EqualTo(new[] { TICKS(4) }));
                Assert.That(LyricTexts(part.NotePhrases[1]), Is.EqualTo(new[] { "Next" }));
            }
        }

        [Test]
        public void NoteOnLastPhraseEndTick_BelongsToThatPhrase()
        {
            var part = LoadVocals((0, 1));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(part.NotePhrases, Has.Count.EqualTo(1));
                Assert.That(NoteTicks(part.NotePhrases[0]), Is.EqualTo(new[] { TICKS(0), TICKS(1) }));
                Assert.That(LyricTexts(part.NotePhrases[0]), Is.EqualTo(new[] { "Hel", "lo" }));
            }
        }

        [Test]
        public void NoteOnPhraseEndTick_BelongsToThePhraseStartingThere()
        {
            var part = LoadVocals((0, 1), (1, 5));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(part.NotePhrases, Has.Count.EqualTo(2));
                Assert.That(NoteTicks(part.NotePhrases[0]), Is.EqualTo(new[] { TICKS(0) }));
                Assert.That(LyricTexts(part.NotePhrases[0]), Is.EqualTo(new[] { "Hel" }));
                Assert.That(NoteTicks(part.NotePhrases[1]), Is.EqualTo(new[] { TICKS(1), TICKS(4) }));
                Assert.That(LyricTexts(part.NotePhrases[1]), Is.EqualTo(new[] { "lo", "Next" }));
            }
        }
    }
}
