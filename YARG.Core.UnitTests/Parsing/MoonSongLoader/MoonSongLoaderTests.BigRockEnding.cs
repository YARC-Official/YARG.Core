using MoonscraperChartEditor.Song;
using NUnit.Framework;
using YARG.Core;
using YARG.Core.Chart;

namespace YARG.Core.UnitTests.Parsing
{
    using static MoonSongLoaderTests;

    public class MoonSongLoaderTests_BigRockEnding
    {
        // BRE phrase spanning ticks [4, 8] (start tick 4, length 4 beats).
        private const double BRE_START_BEAT = 4;
        private const double BRE_LENGTH_BEAT = 4;
        private const double BRE_END_BEAT = BRE_START_BEAT + BRE_LENGTH_BEAT; // 8

        private static GuitarNote LoadExpertGuitar(MoonSong song, uint tick)
        {
            var track = new MoonSongLoader(song, ParseSettings.Default)
                .LoadGuitarTrack(Instrument.FiveFretGuitar);

            var notes = track.GetDifficulty(Difficulty.Expert).Notes;
            return notes.Find(n => n.Tick == tick)
                ?? throw new AssertionException($"No note loaded at tick {tick}");
        }

        private static MoonSong CreateSongWithBre(params uint[] noteTicks)
        {
            var song = CreateSong();
            var chart = song.GetChart(MoonSong.MoonInstrument.Guitar, MoonSong.Difficulty.Expert);

            chart.Add(new MoonPhrase(TICKS(BRE_START_BEAT), TICKS(BRE_LENGTH_BEAT),
                MoonPhrase.Type.BigRockEnding));

            foreach (uint tick in noteTicks)
            {
                chart.Add(new MoonNote(tick, (int) MoonNote.GuitarFret.Green));
            }

            return song;
        }

        [Test]
        public void NoteOnBreEndTick_IsCoveredByBigRockEnding()
        {
            // Regression: BRE coverage must be end-inclusive. A note charted on the exact
            // tick where the BRE ends is still under the lanes and must be suppressed.
            uint endTick = TICKS(BRE_END_BEAT);
            var song = CreateSongWithBre(endTick);

            var note = LoadExpertGuitar(song, endTick);

            Assert.That(note.IsBigRockEnding, Is.True,
                "Note on the BRE end tick should be flagged as part of the Big Rock Ending.");
        }

        [Test]
        public void NoteInsideBre_IsCoveredByBigRockEnding()
        {
            uint insideTick = TICKS(BRE_START_BEAT + 1);
            var song = CreateSongWithBre(insideTick);

            var note = LoadExpertGuitar(song, insideTick);

            Assert.That(note.IsBigRockEnding, Is.True,
                "Note inside the BRE span should be flagged as part of the Big Rock Ending.");
        }

        [Test]
        public void NoteAfterBreEndTick_IsNotCovered()
        {
            uint afterTick = TICKS(BRE_END_BEAT + 2);
            var song = CreateSongWithBre(afterTick);

            var note = LoadExpertGuitar(song, afterTick);

            Assert.That(note.IsBigRockEnding, Is.False,
                "Note past the BRE end tick should not be flagged as part of the Big Rock Ending.");
        }
    }
}
