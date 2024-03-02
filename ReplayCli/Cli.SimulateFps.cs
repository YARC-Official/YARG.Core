using YARG.Core.Engine.Logging;
using YARG.Core.Replays.Analyzer;

namespace ReplayCli;

public partial class Cli
{
    private struct InconsistentEvent
    {
        public double Time;
        public string Message;
        public int    Count;
    }

    private const int SIMULATED_FPS_ATTEMPTS = 25;

    private bool RunSimulateFps()
    {
        // The count of each score
        var scores = new Dictionary<long, int>();
        var loggers = new List<EngineEventLogger[]>();

        var chart = ReadChart();
        if (chart is null)
        {
            return false;
        }

        PrintReplayMetadata();

        // Run each a replay at each FPS
        for (int i = 0; i < SIMULATED_FPS_ATTEMPTS; i++)
        {
            double fps = i * 8 + 1;

            var analyzerResults = ReplayAnalyzer.AnalyzeReplay(chart, _replay, fps, _searchForProblems);
            long bandScore = analyzerResults.Sum(x => (long) x.Stats.TotalScore);

            if (_searchForProblems)
            {
                loggers.Add(analyzerResults.Select(x => x.EventLogger).ToArray());
            }

            if (scores.TryGetValue(bandScore, out int value))
            {
                value++;
                scores[bandScore] = value;
            }
            else
            {
                scores[bandScore] = 1;
            }

            Console.WriteLine($"Final score at {fps} FPS: {bandScore}");
        }

        // Print result data
        Console.WriteLine();
        if (scores.Count != 1)
        {
            Console.ForegroundColor = ConsoleColor.Red;

            Console.WriteLine("NOT CONSISTENT!");
            Console.WriteLine($"Distinct scores: {scores.Count}");

            foreach ((long score, int count) in scores.OrderBy(i => i.Key))
            {
                Console.WriteLine($" - {score} {new string('|', count)}");
            }
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;

            Console.WriteLine("CONSISTENT!");
            Console.WriteLine("Distinct scores: 1");
            Console.WriteLine($" - {scores.First().Key}");
        }

        // If we're not searching for problems, just return here
        if (!_searchForProblems)
        {
            return scores.Count == 1;
        }

        // Search for problems on each player
        bool noProblems = true;
        for (int i = 0; i < _replay.PlayerCount; i++)
        {
            Console.WriteLine($"\nSearching for problems for player {i}...\n");

            int indexCopy = i;
            if (SearchForProblems(loggers.Select(x => x[indexCopy])))
            {
                noProblems = false;
            }
        }

        return noProblems;
    }

    private bool SearchForProblems(IEnumerable<EngineEventLogger> enumerableLoggers)
    {
        var loggers = enumerableLoggers.ToList();
        var processedEvents = new HashSet<BaseEngineEvent>();
        var inconsistentEvents = new List<InconsistentEvent>();

        // TODO: This is severely unoptimized
        foreach (var logger in loggers)
        {
            foreach (var e in logger.Events)
            {
                if (e is not ConsistentEngineEvent consistentEvent) continue;

                if (processedEvents.Contains(e))
                {
                    continue;
                }

                // Check if all other loggers have the same event
                // We start off with one since this logger has it
                int found = 1;
                foreach (var otherLogger in loggers)
                {
                    if (otherLogger == logger)
                    {
                        continue;
                    }

                    foreach (var otherEvent in otherLogger.Events)
                    {
                        if (otherEvent.EventTime < e.EventTime)
                        {
                            continue;
                        }

                        if (otherEvent.EventTime > e.EventTime)
                        {
                            break;
                        }

                        if (otherEvent == e)
                        {
                            found++;
                            break;
                        }
                    }
                }

                if (found != loggers.Count)
                {
                    inconsistentEvents.Add(new InconsistentEvent
                    {
                        Time = e.EventTime,
                        Message = consistentEvent.Message,
                        Count = found
                    });
                }

                processedEvents.Add(e);
            }
        }

        foreach (var e in inconsistentEvents.OrderBy(i => i.Time))
        {
            Console.WriteLine($"Inconsistent event at {e.Time} found {e.Count} times: {e.Message}");
        }

        return false;
    }
}