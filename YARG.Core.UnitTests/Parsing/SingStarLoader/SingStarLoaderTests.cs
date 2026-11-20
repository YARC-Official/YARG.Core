using System.Text;
using YARG.Core.Chart;
using YARG.Core.IO;

namespace YARG.Core.UnitTests.Parsing;

internal class SingStarLoaderTests
{
    protected static readonly ParseSettings DefaultSettings = ParseSettings.Default;

    protected static FixedArray<byte> CreateSingStarFile(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);

        using var ms = new MemoryStream(bytes);
        return FixedArray.Read(ms, bytes.Length);
    }

    protected static Core.Chart.Loaders.SingStar.SingStarLoader LoadSingStar(string content)
    {
        using var file = CreateSingStarFile(content);
        return new Core.Chart.Loaders.SingStar.SingStarLoader(file);
    }

    /// <summary>
    /// Loads all the way through the final SongChart (via MoonSongLoader), where lyric
    /// markers are interpreted and pitch slides merge their notes.
    /// </summary>
    protected static SongChart LoadSingStarChart(string content)
    {
        using var file = CreateSingStarFile(content);
        return SongChart.FromSingStar(DefaultSettings, file);
    }

    protected static string Ss(params string[] lines) => string.Join("\n", lines);
}