using MoonscraperChartEditor.Song;
using NUnit.Framework;
using YARG.Core;
using YARG.Core.Chart;

namespace YARG.Core.UnitTests.Parsing
{
    using static MoonSongLoaderTests;

    public class MoonSongLoaderTests_Drums
    {
        private static List<DrumNote> LoadProDrums(MoonSong song, Difficulty difficulty)
        {
            var settings = ParseSettings.Default;
            settings.DrumsType = DrumsType.FourLane;
            var track = new MoonSongLoader(song, settings).LoadDrumsTrack(Instrument.ProDrums, null);
            return track.GetDifficulty(difficulty).Notes;
        }

        [TestCase("easy")]
        [TestCase("easynokick")]
        [TestCase("x")]
        public void DiscoFlip_LastsThroughMixEventWithAnotherFlag(string flag)
        {
            var song = CreateSong();
            var chart = song.GetChart(MoonSong.MoonInstrument.Drums, MoonSong.Difficulty.Easy);
            chart.Add(new MoonText("mix 0 drums0d", 0));
            chart.Add(new MoonNote(TICKS(1), (int) MoonNote.DrumPad.Red));
            chart.Add(new MoonText($"mix 0 drums0{flag}", TICKS(2)));
            chart.Add(new MoonNote(TICKS(3), (int) MoonNote.DrumPad.Red));
            chart.Add(new MoonText("mix 0 drums0", TICKS(4)));
            chart.Add(new MoonNote(TICKS(5), (int) MoonNote.DrumPad.Red));

            var pads = LoadProDrums(song, Difficulty.Easy).Select(note => (FourLaneDrumPad) note.Pad);

            Assert.That(pads, Is.EqualTo(new[]
            {
                FourLaneDrumPad.YellowCymbal,
                FourLaneDrumPad.YellowCymbal,
                FourLaneDrumPad.RedDrum,
            }));
        }

        [Test]
        public void DiscoFlip_StartsAtFlagThatStartsWithD()
        {
            var song = CreateSong();
            var chart = song.GetChart(MoonSong.MoonInstrument.Drums, MoonSong.Difficulty.Easy);
            chart.Add(new MoonText("mix 0 drums0dx", 0));
            chart.Add(new MoonNote(TICKS(1), (int) MoonNote.DrumPad.Red));

            var pads = LoadProDrums(song, Difficulty.Easy).Select(note => (FourLaneDrumPad) note.Pad);

            Assert.That(pads, Is.EqualTo(new[] { FourLaneDrumPad.YellowCymbal }));
        }
    }
}
