using System.Diagnostics;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.ProKeys;
using YARG.Core.Engine.Vocals;
using YARG.Core.Logging;
using YARG.Core.Replays;
using YARG.Core.Song.Cache;

namespace ReplayCli;

public partial class Cli
{
    private string _songPath;
    private string _replayPath;
    private AnalyzerMode _runMode;

    private int _framesPerSecond = 0;
    private int _frameIndex = -1;

    private string _logPath;
    private LogLevel _logLevel = LogLevel.Info;

    private ReplayInfo _replayInfo;
    private ReplayData _replayData;

    /// <summary>
    /// Parses the specified arguments.
    /// </summary>
    /// <returns>
    /// Returns <c>false</c> if there was an error, or the help menu was printed.
    /// </returns>
    public bool ParseArguments(string[] args)
    {
        if (args.Length < 2)
        {
            PrintHelpMessage();
            return false;
        }

        // Argument 1 is the mode

        _runMode = args[0] switch
        {
            "verify"       => AnalyzerMode.Verify,
            "simulate_fps" => AnalyzerMode.SimulateFps,
            "dump_inputs"  => AnalyzerMode.DumpInputs,
            "read"         => AnalyzerMode.Read,
            _              => AnalyzerMode.None
        };

        if (_runMode == AnalyzerMode.None)
        {
            PrintHelpMessage();
            return false;
        }

        // Argument 2 is the path to the replay
        _replayPath = args[1].Trim();
        if (!File.Exists(_replayPath))
        {
            Console.WriteLine("ERROR: Replay file does not exist!");
            return false;
        }

        // Rest of the arguments are options
        for (int i = 2; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--song":
                case "-s":
                {
                    i++;
                    if (i >= args.Length)
                    {
                        Console.WriteLine("ERROR: Missing song folder path.");
                        PrintHelpMessage();
                        return false;
                    }

                    _songPath = args[i].Trim();

                    break;
                }
                case "--fps":
                case "-f":
                {
                    i++;
                    if (i >= args.Length)
                    {
                        Console.WriteLine("ERROR: Missing FPS value.");
                        PrintHelpMessage();
                        return false;
                    }

                    if (!int.TryParse(args[i], out _framesPerSecond) || _framesPerSecond <= 0)
                    {
                        Console.WriteLine("ERROR: Invalid FPS value!");
                        return false;
                    }

                    break;
                }
                case "--frame-index":
                case "-fi":
                {
                    i++;
                    if (i >= args.Length)
                    {
                        Console.WriteLine("ERROR: Missing frame index value.");
                        PrintHelpMessage();
                        return false;
                    }

                    if (!int.TryParse(args[i], out _frameIndex) || _frameIndex < 0)
                    {
                        Console.WriteLine("ERROR: Invalid frame index!");
                        return false;
                    }

                    break;
                }
                case "--log-file":
                case "-l":
                {
                    i++;
                    if (i >= args.Length)
                    {
                        Console.WriteLine("ERROR: Missing log file path.");
                        PrintHelpMessage();
                        return false;
                    }

                    _logPath = args[i];

                    break;
                }
                case "--log-level":
                case "-lv":
                {
                    i++;
                    if (i >= args.Length)
                    {
                        Console.WriteLine("ERROR: Missing log file path.");
                        PrintHelpMessage();
                        return false;
                    }

                    string level = args[i];
                    switch (level)
                    {
                        case "error":   _logLevel = LogLevel.Error;   break;
                        case "warning": _logLevel = LogLevel.Warning; break;
                        case "info":    _logLevel = LogLevel.Info;    break;
                        case "debug":   _logLevel = LogLevel.Debug;   break;
                        case "trace":   _logLevel = LogLevel.Trace;   break;
                        default:
                        {
                            Console.WriteLine($"ERROR: Invalid log level '{level}'.");
                            PrintHelpMessage();
                            return false;
                        }
                    }

                    break;
                }
                case "--help":
                case "-h":
                {
                    PrintHelpMessage();
                    return false;
                }
                default:
                {
                    Console.WriteLine($"WARNING: Unrecognized argument '{args[i]}'");
                    break;
                }
            }
        }

        // Argument validation/warnings

        if (_frameIndex >= 0)
        {
            if (_framesPerSecond > 0 || _runMode == AnalyzerMode.SimulateFps)
            {
                _frameIndex = -1;
                Console.WriteLine("WARNING: Frame index is ignored when simulating FPS, as frame times from the replay are not used.");
            }
        }

        return true;
    }

    private static void PrintHelpMessage()
    {
        Console.WriteLine(
            """
            Usage: ReplayCli <mode> <replay-path> [options...]

              mode: the mode to run the analyzer in
                verify              Verifies the replay's metadata.
                simulate_fps        Simulates FPS updates to verify the engines consistency.
                dump_inputs         Dumps the replay's inputs.

              replay-path: the path to the replay file

            Options:
              -h  | --help                      Show this help message.

              -s  | --song <path>               Path to the song. (required in `verify` and `simulate_fps` modes)
              -f  | --fps <value>               The framerate value to simulate with. (optional)
              -fi | --frame-index <value>       Place a debug break at a specific frame index. (optional)

              -l  | --log-file <path>           Output log messages to the specified path.
                                                By default, log messages are output to the console.
              -lv | --log-level <level>         The logging level to use.
                error                             Only errors.
                warning                           Warnings and errors.
                info                              General information.
                debug                             Verbose information for troubleshooting. (forces log file)
                trace                             Log spam for thorough analysis. (forces log file)
            """
        );
    }

    private void PrintReplayMetadata()
    {
        Console.WriteLine($"Players ({_replayData.PlayerCount}):");
        for (int i = 0; i < _replayData.Frames.Length; i++)
        {
            var frame = _replayData.Frames[i];
            var profile = frame.Profile;

            Console.WriteLine($"{i}. {profile.Name}, {profile.CurrentInstrument} ({profile.CurrentDifficulty})");

            // Indent the engine parameters
            Console.WriteLine($"   {frame.EngineParameters.ToString()?.ReplaceLineEndings("\n   ")}");
        }

        Console.WriteLine($"Band score: {_replayInfo.BandScore} (as per metadata)\n");
    }

    /// <summary>
    /// Runs the analyzer using the arguments parsed in <see cref="ParseArguments"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the run succeeded, <c>false</c> if it didn't.
    /// </returns>
    public bool Run()
    {
        (var result, _replayInfo, _replayData) = ReplayIO.TryDeserialize(_replayPath);
        if (result != ReplayReadResult.Valid)
        {
            Console.WriteLine($"ERROR: Failed to load replay. Read Result: {result}.");
            return false;
        }

        return _runMode switch
        {
            AnalyzerMode.Verify      => RunVerify(),
            AnalyzerMode.SimulateFps => RunSimulateFps(),
            AnalyzerMode.DumpInputs  => RunDumpInputs(),
            AnalyzerMode.Read        => RunRead(),
            _                        => false
        };
    }

    private void InitializeLogging()
    {
        YargLogger.MinimumLogLevel = _logLevel;
        if (_logLevel < LogLevel.Info && string.IsNullOrEmpty(_logPath))
        {
            _logPath = $"ReplayCLI-{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.log";
        }

        if (!string.IsNullOrEmpty(_logPath))
        {
            YargLogger.AddLogListener(new FileYargLogListener(_logPath, new BasicYargLogFormatter()));
        }
        else
        {
            YargLogger.AddLogListener(new ConsoleYargLogListener());
        }
    }

    private SongChart ReadChart()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"ReplayCLI-{Guid.NewGuid()}");

        try
        {
            string tempCache = Path.Combine(tempDir, "songcache.bin");
            string tempBadSongs = Path.Combine(tempDir, "badsongs.txt");

            if (File.Exists(_songPath))
            {
                // CON files can't be scanned in via their direct file path
                // This also allows a file within song.ini charts to be passed in instead
                _songPath = Path.GetDirectoryName(_songPath);
            }
            else if (!Directory.Exists(_songPath))
            {
                Console.WriteLine("ERROR: Song path does not exist!");
                return null;
            }

            Console.Write("Running song scan... ");
            var directories = new List<string>() { _songPath };
            var cache = CacheHandler.RunScan(
                tryQuickScan: false,
                tempCache,
                tempBadSongs,
                fullDirectoryPlaylists: false,
                directories
            );

            Console.WriteLine($"Found {cache.Entries.Count} entries.");
            Console.WriteLine();

            if (!cache.Entries.TryGetValue(_replayInfo.SongChecksum, out var entries))
            {
                Console.WriteLine("ERROR: Could not load song from given song path!");
                return null;
            }

            return entries[0].LoadChart();
        }
        catch (Exception e)
        {
            Console.WriteLine($"ERROR: Failed to load notes file. \n{e}");
            return null;
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    private static void PrintStatDifferences(BaseStats originalStats, BaseStats resultStats)
    {
        static void PrintStatDifference<T>(string name, T frameStat, T resultStat)
        where T : IEquatable<T>
        {
            if (frameStat.Equals(resultStat))
                Console.WriteLine($"- {name + ":",-31} {frameStat,-12} (identical)");
            else
                Console.WriteLine($"- {name + ":",-31} {frameStat,-10} -> {resultStat}");
        }

        Console.WriteLine($"Base stats:");
        PrintStatDifference("CommittedScore",         originalStats.CommittedScore,         resultStats.CommittedScore);
        PrintStatDifference("PendingScore",           originalStats.PendingScore,           resultStats.PendingScore);
        PrintStatDifference("NoteScore",              originalStats.NoteScore,              resultStats.NoteScore);
        PrintStatDifference("SustainScore",           originalStats.SustainScore,           resultStats.SustainScore);
        PrintStatDifference("MultiplierScore",        originalStats.MultiplierScore,        resultStats.MultiplierScore);
        PrintStatDifference("TotalScore",             originalStats.TotalScore,             resultStats.TotalScore);
        PrintStatDifference("StarScore",              originalStats.StarScore,              resultStats.StarScore);
        PrintStatDifference("Combo",                  originalStats.Combo,                  resultStats.Combo);
        PrintStatDifference("MaxCombo",               originalStats.MaxCombo,               resultStats.MaxCombo);
        PrintStatDifference("ScoreMultiplier",        originalStats.ScoreMultiplier,        resultStats.ScoreMultiplier);
        PrintStatDifference("NotesHit",               originalStats.NotesHit,               resultStats.NotesHit);
        PrintStatDifference("TotalNotes",             originalStats.TotalNotes,             resultStats.TotalNotes);
        PrintStatDifference("NotesMissed",            originalStats.NotesMissed,            resultStats.NotesMissed);
        PrintStatDifference("Percent",                originalStats.Percent,                resultStats.Percent);
        PrintStatDifference("StarPowerTickAmount",    originalStats.StarPowerTickAmount,    resultStats.StarPowerTickAmount);
        PrintStatDifference("StarPowerWhammyTicks",   originalStats.StarPowerWhammyTicks,   resultStats.StarPowerWhammyTicks);
        PrintStatDifference("TotalStarPowerTicks",    originalStats.TotalStarPowerTicks,    resultStats.TotalStarPowerTicks);
        PrintStatDifference("TimeInStarPower",        originalStats.TimeInStarPower,        resultStats.TimeInStarPower);
        PrintStatDifference("IsStarPowerActive",      originalStats.IsStarPowerActive,      resultStats.IsStarPowerActive);
        PrintStatDifference("StarPowerPhrasesHit",    originalStats.StarPowerPhrasesHit,    resultStats.StarPowerPhrasesHit);
        PrintStatDifference("TotalStarPowerPhrases",  originalStats.TotalStarPowerPhrases,  resultStats.TotalStarPowerPhrases);
        PrintStatDifference("StarPowerPhrasesMissed", originalStats.StarPowerPhrasesMissed, resultStats.StarPowerPhrasesMissed);
        PrintStatDifference("SoloBonuses",            originalStats.SoloBonuses,            resultStats.SoloBonuses);
        PrintStatDifference("StarPowerScore",         originalStats.StarPowerScore,         resultStats.StarPowerScore);
        // PrintStatDifference("Stars",                  originalStats.Stars,                  resultStats.Stars);

        Console.WriteLine();
        switch (originalStats, resultStats)
        {
            case (GuitarStats originalGuitar, GuitarStats resultGuitar):
            {
                Console.WriteLine("Guitar stats:");
                PrintStatDifference("Overstrums",             originalGuitar.Overstrums,             resultGuitar.Overstrums);
                PrintStatDifference("HoposStrummed",          originalGuitar.HoposStrummed,          resultGuitar.HoposStrummed);
                PrintStatDifference("GhostInputs",            originalGuitar.GhostInputs,            resultGuitar.GhostInputs);
                break;
            }
            case (DrumsStats originalDrums, DrumsStats resultDrums):
            {
                Console.WriteLine("Drums stats:");
                PrintStatDifference("Overhits",      originalDrums.Overhits,      resultDrums.Overhits);
                PrintStatDifference("GhostsHit",     originalDrums.GhostsHit,     resultDrums.GhostsHit);
                PrintStatDifference("TotalGhosts",   originalDrums.TotalGhosts,   resultDrums.TotalGhosts);
                PrintStatDifference("AccentsHit",    originalDrums.AccentsHit,    resultDrums.AccentsHit);
                PrintStatDifference("TotalAccents",  originalDrums.TotalAccents,  resultDrums.TotalAccents);
                PrintStatDifference("DynamicsBonus", originalDrums.DynamicsBonus, resultDrums.DynamicsBonus);
                break;
            }
            case (VocalsStats originalVocals, VocalsStats resultVocals):
            {
                Console.WriteLine("Vocals stats:");
                PrintStatDifference("TicksHit",      originalVocals.TicksHit,      resultVocals.TicksHit);
                PrintStatDifference("TicksMissed",   originalVocals.TicksMissed,   resultVocals.TicksMissed);
                PrintStatDifference("TotalTicks",    originalVocals.TotalTicks,    resultVocals.TotalTicks);
                break;
            }
            case (ProKeysStats originalKeys, ProKeysStats resultKeys):
            {
                Console.WriteLine("Pro Keys stats:");
                PrintStatDifference("Overhits",      originalKeys.Overhits,      resultKeys.Overhits);
                PrintStatDifference("FatFingersIgnored", originalKeys.FatFingersIgnored, resultKeys.FatFingersIgnored);
                break;
            }
            default:
            {
                if (originalStats.GetType() != resultStats.GetType())
                    Console.WriteLine($"Stats types do not match! Original: {originalStats.GetType()}, result: {resultStats.GetType()}");
                else
                    Console.WriteLine($"Unhandled stats type {originalStats.GetType()}!");
                break;
            }
        }
    }
}