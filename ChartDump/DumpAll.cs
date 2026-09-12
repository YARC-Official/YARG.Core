using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Batch mode: walks a chart directory, parses each chart with YARG.Core,
/// and dumps JSON files to an output directory matching the folder structure.
///
/// Usage: ChartDump --batch <input-dir> <output-dir> [limit]
/// </summary>
static class DumpAll
{
    public static int Run(string inputDir, string outputDir, int limit)
    {
        if (!Directory.Exists(inputDir))
        {
            Console.Error.WriteLine($"Input directory not found: {inputDir}");
            return 1;
        }

        Directory.CreateDirectory(outputDir);

        var folders = FindChartFolders(inputDir);
        if (limit > 0 && limit < folders.Count)
            folders = folders.GetRange(0, limit);

        int success = 0, failed = 0, skipped = 0;
        var startTime = DateTime.UtcNow;

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        Parallel.ForEach(folders, new ParallelOptions { MaxDegreeOfParallelism = 16 }, folder =>
        {
            var relPath = Path.GetRelativePath(inputDir, folder);
            var outPath = Path.Combine(outputDir, relPath, "yarg-dump.json");

            try
            {
                var chartFile = Path.Combine(folder, "notes.chart");
                if (!File.Exists(chartFile))
                    chartFile = Path.Combine(folder, "notes.mid");
                if (!File.Exists(chartFile))
                {
                    Interlocked.Increment(ref skipped);
                    return;
                }

                var dump = Program.ParseAndDump(chartFile);

                Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
                File.WriteAllText(outPath, JsonSerializer.Serialize(dump, jsonOptions));
                int s = Interlocked.Increment(ref success);

                if ((s + Volatile.Read(ref failed)) % 100 == 0)
                {
                    int done = s + Volatile.Read(ref failed) + Volatile.Read(ref skipped);
                    int remaining = folders.Count - done;
                    var elapsed = DateTime.UtcNow - startTime;
                    var perItem = elapsed / done;
                    var eta = TimeSpan.FromTicks(perItem.Ticks * remaining);
                    Console.Error.WriteLine($"  Processed {done}/{folders.Count} ({s} ok, {Volatile.Read(ref failed)} err, {Volatile.Read(ref skipped)} skip) — ETA {eta:h\\:mm\\:ss}");
                }
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref failed);
                Console.Error.WriteLine($"FAIL: {relPath}: {ex.Message}");
            }
        });

        Console.Error.WriteLine($"\nDone: {success} success, {failed} failed, {skipped} skipped out of {folders.Count}");
        return 0;
    }

    static List<string> FindChartFolders(string dir)
    {
        var results = new List<string>();
        FindChartFoldersRecursive(dir, results);
        results.Sort(StringComparer.Ordinal);
        return results;
    }

    static void FindChartFoldersRecursive(string dir, List<string> results)
    {
        try
        {
            var files = Directory.GetFiles(dir);
            bool hasChart = false;
            foreach (var f in files)
            {
                var name = Path.GetFileName(f);
                if (name == "notes.chart" || name == "notes.mid")
                {
                    hasChart = true;
                    break;
                }
            }

            if (hasChart)
            {
                results.Add(dir);
                return;
            }

            foreach (var subdir in Directory.GetDirectories(dir))
            {
                FindChartFoldersRecursive(subdir, results);
            }
        }
        catch { /* skip inaccessible dirs */ }
    }
}
