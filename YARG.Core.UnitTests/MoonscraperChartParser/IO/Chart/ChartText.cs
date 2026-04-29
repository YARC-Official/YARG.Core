using System.Text;
using MoonscraperChartEditor.Song.IO;

namespace YARG.Core.UnitTests.MoonscraperChartParser.IO.Chart;

internal static class ChartText
{
    public const uint Resolution = 192;

    public static string Chart(params string[] sections)
    {
        return string.Join('\n', sections);
    }

    public static string SongSection(uint resolution = Resolution)
    {
        return Section(ChartIOHelper.SECTION_SONG, $"Resolution = {resolution}");
    }

    public static string SyncSection()
    {
        return Section(ChartIOHelper.SECTION_SYNC_TRACK, "0 = B 120000", "0 = TS 4 2");
    }

    public static string Section(string name, params string[] lines)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"[{name}]");
        builder.AppendLine("{");
        foreach (var line in lines)
        {
            builder.AppendLine($"  {line}");
        }
        builder.AppendLine("}");
        return builder.ToString();
    }
}
