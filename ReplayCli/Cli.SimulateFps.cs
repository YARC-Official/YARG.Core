using YARG.Core.Replays.Analyzer;

namespace ReplayCli;

public partial class Cli
{
    private const int SIMULATED_FPS_ATTEMPTS = 100;

    private bool RunSimulateFps()
    {
        // The count of each score
        var scores = new Dictionary<long, int>();

        var chart = ReadChart();
        if (chart is null)
        {
            return false;
        }

        PrintReplayMetadata();

        // Run each a replay at each FPS
        for (int i = 0; i < SIMULATED_FPS_ATTEMPTS; i++)
        {
            double fps = i * 2 + 1;
            Console.WriteLine($"Analyzing replay at {fps} FPS...");

            var results = ReplayAnalyzer.AnalyzeReplay(chart, _replay, fps);
            long bandScore = results.Sum(x => (long) x.Stats.TotalScore);

            if (scores.TryGetValue(bandScore, out int value))
            {
                value++;
                scores[bandScore] = value;
            }
            else
            {
                scores[bandScore] = 1;
            }

            Console.WriteLine($"Done! Final score: {bandScore}");
            Console.WriteLine();
        }

        // Print result data
        if (scores.Count != 1)
        {
            Console.ForegroundColor = ConsoleColor.Red;

            Console.WriteLine("NOT CONSISTENT!");
            Console.WriteLine($"Distinct scores: {scores.Count}");

            foreach ((long score, int count) in scores.OrderBy(i => i.Key))
            {
                Console.WriteLine($" - {score} {new string('|', count)}");
            }

            return false;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;

            Console.WriteLine("CONSISTENT!");
            Console.WriteLine("Distinct scores: 1");
            Console.WriteLine($" - {scores.First().Key}");

            return true;
        }
    }
}