using YARG.Core.IO;

namespace YARG.Core.Chart
{
    /// <summary>
    /// Extends SongChart with an UltraStar .txt factory method.
    /// </summary>
    public partial class SongChart
    {
        /// <summary>Loads from bytes the caller already holds, without copying them.</summary>
        internal static SongChart FromUltraStar(in ParseSettings settings, FixedArray<byte> data)
        {
            var loader = MoonSongLoader.LoadUltraStar(settings, data);
            return new SongChart(loader);
        }
    }
}
