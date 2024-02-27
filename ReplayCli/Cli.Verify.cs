using YARG.Core.Replays.Analyzer;

namespace ReplayCli;

public partial class Cli
{
    private bool RunVerify()
    {
        var chart = ReadChart();
        if (chart is null)
        {
            return false;
        }

        PrintReplayMetadata();

        // Analyze replay

        Console.WriteLine("Analyzing replay...");

        var results = ReplayAnalyzer.AnalyzeReplay(chart, _replay);

        Console.WriteLine("Done!\n");

        // Print result data

        var bandScore = results.Sum(x => x.Stats.TotalScore);
        if (bandScore != _replay.BandScore)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("VERIFICATION FAILED!");
            Console.WriteLine($"Metadata score : {_replay.BandScore}");
            Console.WriteLine($"Real score     : {bandScore}");
            Console.WriteLine($"Difference     : {Math.Abs(bandScore - _replay.BandScore)}\n");
            return false;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("VERIFICATION SUCCESS!");
            Console.WriteLine($"Metadata score : {_replay.BandScore}");
            Console.WriteLine($"Real score     : {bandScore}");
            Console.WriteLine($"Difference     : {Math.Abs(bandScore - _replay.BandScore)}\n");
            return true;
        }
    }
}