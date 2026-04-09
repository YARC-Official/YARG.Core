using System;
using System.IO;
using YARG.Core.IO;

namespace YARG.Core.Chart
{
    /// <summary>
    /// Extends SongChart with UltraStar .txt factory methods.
    /// Drop this file next to SongChart.cs (same namespace, partial class).
    /// </summary>
    public partial class SongChart
    {
        /// <summary>Loads a SongChart from an UltraStar .txt file path.</summary>
        public static SongChart FromUltraStarFile(in ParseSettings settings, string filePath)
        {
            var loader = MoonSongLoader.LoadUltraStar(settings, filePath);
            return new SongChart(loader);
        }

        /// <summary>Loads a SongChart from raw UltraStar .txt bytes.</summary>
        public static SongChart FromUltraStarBytes(in ParseSettings settings, ReadOnlySpan<byte> data)
        {
            var loader = MoonSongLoader.LoadUltraStar(settings, data.ToArray());
            return new SongChart(loader);
        }
    }
}
