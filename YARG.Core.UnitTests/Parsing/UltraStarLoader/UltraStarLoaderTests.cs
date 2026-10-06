using System.Text;
using YARG.Core.Chart;
using YARG.Core.Chart.Loaders.UltraStar;
using YARG.Core.IO;

namespace YARG.Core.UnitTests.Parsing
{
    internal class UltraStarLoaderTests
    {
        protected static readonly ParseSettings DefaultSettings = ParseSettings.Default;

        protected static FixedArray<byte> CreateUltraStarFile(string content)
        {
            var bytes = Encoding.UTF8.GetBytes(content);

            using var ms = new MemoryStream(bytes);
            return FixedArray.Read(ms, bytes.Length);
        }

        protected static UltraStarLoader LoadUltraStar(string content)
        {
            using var file = CreateUltraStarFile(content);
            return new UltraStarLoader(file);
        }

        /// <summary>
        /// Loads all the way through to the final SongChart (via MoonSongLoader), not just
        /// the raw UltraStarLoader -- some values (GAP in particular) only take effect once
        /// baked into MoonSong's tick-based tempo map, so isolated loader-level assertions
        /// can pass while the actual produced chart timing is wrong.
        /// </summary>
        protected static SongChart LoadUltraStarChart(string content)
        {
            return SongChart.FromUltraStarBytes(DefaultSettings, Encoding.UTF8.GetBytes(content));
        }

        /// <summary>
        /// A part's lyrics as one comparable string: lyrics joined by " | ", phrases by " / ",
        /// and a trailing '*' on each lyric with JoinWithNext.
        /// </summary>
        protected static string DescribeLyrics(IEnumerable<IEnumerable<LyricEvent>> phrases)
            => string.Join(" / ", phrases.Select(phrase =>
                string.Join(" | ", phrase.Select(lyric => lyric.Text + (lyric.JoinWithNext ? "*" : "")))));

        protected static string DescribeLyrics(VocalsPart part)
            => DescribeLyrics(part.NotePhrases.Select(phrase => phrase.Lyrics));

        /// <summary>
        /// Each phrase's top-level notes, as how many notes a pitch slide merged into each:
        /// "2,0" is a note with two merged in, then a plain note. Phrases are joined by " / ".
        /// </summary>
        protected static string DescribeNoteGroups(VocalsPart part)
            => string.Join(" / ", part.NotePhrases.Select(phrase =>
                string.Join(",", phrase.PhraseParentNote.ChildNotes.Select(note => note.ChildNotes.Count))));

        /// <summary>
        /// Helper to create UltraStar content in a cleaner way.
        /// Example: Us("BPM:120", ": 0 4 0 Ok")
        /// </summary>
        protected static string Us(params string[] lines)
        {
            return string.Join("\n", lines);
        }
    }
}
