using YARG.Core.IO;

namespace YARG.Core.Chart
{
    /// <summary>
    /// Extends SongChart with an UltraStar .txt factory method.
    /// </summary>
    public partial class SongChart
    {
        /// <summary>
        /// Loads from bytes the caller already holds, without copying them.
        /// </summary>
        /// <param name="harmonyData">A duet file merged with the solo <paramref name="data"/>
        /// (see CacheHandler.ScanUltraStarCharts). When given, Harmony1-3 come from this
        /// file instead of the solo one.</param>
        internal static SongChart FromUltraStar(in ParseSettings settings, FixedArray<byte> data, FixedArray<byte>? harmonyData = null)
        {
            var loader = MoonSongLoader.LoadUltraStar(settings, data, harmonyData);
            return new SongChart(loader);
        }
    }
}
