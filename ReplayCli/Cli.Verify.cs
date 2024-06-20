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

        var results = ReplayAnalyzer.AnalyzeReplay(chart, _replay, keepEngineLoggers: true);

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
            Console.ResetColor();

            if (results.Length != _replay.Frames.Length)
            {
                Console.WriteLine("Analysis results and replay frames differ in size!");
            }
            else
            {
                for (int frameIndex = 0; frameIndex < _replay.Frames.Length; frameIndex++)
                {
                    var frame = _replay.Frames[frameIndex];
                    var result = results[frameIndex];

                    Console.WriteLine($"-------------");
                    Console.WriteLine($"Frame {frameIndex + 1}");
                    Console.WriteLine($"-------------");
                    PrintStatDifferences(frame.Stats, result.Stats);
                    PrintEventLogDifferences(frame.EventLog, result.EventLogger);
                    Console.WriteLine();
                }
            }

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