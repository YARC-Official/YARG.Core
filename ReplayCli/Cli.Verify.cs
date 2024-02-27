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

        // Print metadata

        Console.WriteLine($"Players ({_replay.PlayerCount}):");
        for (int i = 0; i < _replay.Frames.Length; i++)
        {
            var frame = _replay.Frames[i];
            var profile = frame.PlayerInfo.Profile;

            Console.WriteLine($"{i}. {profile.Name}, {profile.CurrentInstrument} ({profile.CurrentDifficulty})");
        }

        Console.WriteLine($"Band score: {_replay.BandScore} (as per metadata)\n");

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