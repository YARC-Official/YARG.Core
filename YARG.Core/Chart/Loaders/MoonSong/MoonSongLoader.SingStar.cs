using MoonscraperChartEditor.Song;
using YARG.Core.Chart.Loaders.SingStar;
using YARG.Core.IO;

namespace YARG.Core.Chart
{
    internal partial class MoonSongLoader : ISongLoader
    {
        #region Loading

        internal static MoonSongLoader LoadSingStar(ParseSettings settings, FixedArray<byte> file)
        {
            var singStarLoader = new SingStarLoader(file);
            var moonSong = ConvertSingStarToMoonSong(singStarLoader);

            return new MoonSongLoader(moonSong, settings);
        }

        #endregion

        #region Conversion

        private static MoonSong ConvertSingStarToMoonSong(SingStarLoader loader)
        {
            const uint RESOLUTION = 120;
            var moonSong = new MoonSong(RESOLUTION);

            // Sync track — SingStar has no GAP offset, tempo starts at t=0
            var syncTrack = loader.LoadSyncTrack();
            foreach (var t in syncTrack.Tempos)
            {
                moonSong.AddTempo(t.BeatsPerMinute, t.Tick);
            }

            foreach (var ts in syncTrack.TimeSignatures)
            {
                moonSong.AddTimeSignature(ts.Numerator, ts.Denominator, ts.Tick);
            }

            bool isDuet = loader.GetMetadata("PARTS") == "2";

            var vocalTrack = loader.LoadVocalsTrack(isDuet ? Instrument.Harmony : Instrument.Vocals);
            var soloChart = moonSong.GetChart(MoonSong.MoonInstrument.Vocals, MoonSong.Difficulty.Expert);

            if (vocalTrack.Parts.Count > 0)
            {
                AddPartToChart(new[]
                {
                    vocalTrack.Parts[0],
                }, soloChart, null);
                AddSongLyrics(moonSong, vocalTrack.Parts[0]);
            }

            if (isDuet && vocalTrack.Parts.Count >= 2)
            {
                var chartH1 = moonSong.GetChart(MoonSong.MoonInstrument.Harmony1, MoonSong.Difficulty.Expert);
                AddPartToChart(new[]
                {
                    vocalTrack.Parts[0],
                }, chartH1, null);

                var chartH2 = moonSong.GetChart(MoonSong.MoonInstrument.Harmony2, MoonSong.Difficulty.Expert);
                AddPartToChart(new[]
                {
                    vocalTrack.Parts[1],
                }, chartH2, null);
            }

            return moonSong;
        }

        #endregion
    }
}
