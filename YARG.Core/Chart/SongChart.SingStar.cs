using System;

namespace YARG.Core.Chart
{
    /// <summary>
    ///     Extends SongChart with SingStar .xml factory methods.
    ///     Drop this file next to SongChart.cs (same namespace, partial class).
    /// </summary>
    public partial class SongChart
    {
        /// <summary>Loads a SongChart from a SingStar melody .xml file path.</summary>
        public static SongChart FromSingStarFile(in ParseSettings settings, string filePath)
        {
            var loader = MoonSongLoader.LoadSingStar(settings, filePath);
            return new SongChart(loader);
        }

        /// <summary>Loads a SongChart from raw SingStar melody .xml bytes.</summary>
        public static SongChart FromSingStarBytes(in ParseSettings settings, ReadOnlySpan<byte> data)
        {
            var loader = MoonSongLoader.LoadSingStar(settings, data.ToArray());
            return new SongChart(loader);
        }
    }
}