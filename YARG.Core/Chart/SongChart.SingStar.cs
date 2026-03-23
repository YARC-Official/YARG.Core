using YARG.Core.IO;

namespace YARG.Core.Chart
{
    /// <summary>
    /// Extends SongChart with a SingStar .xml factory method.
    /// </summary>
    public partial class SongChart
    {
        internal static SongChart FromSingStar(in ParseSettings settings, FixedArray<byte> data)
        {
            var loader = MoonSongLoader.LoadSingStar(settings, data);
            return new SongChart(loader);
        }
    }
}
