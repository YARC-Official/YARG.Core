using YARG.Core.Chart;
using YARG.Core.Replays;

namespace ReplayCli;

public partial class Cli
{
    private string _songPath;
    private string _replayPath;
    private AnalyzerMode _runMode;

    private Replay _replay;

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

                    _songPath = args[i].Trim();
                    if (!Directory.Exists(_songPath))
                    {
                        Console.WriteLine("ERROR: Song folder does not exist!");
                    }

                    break;
                }
                case "--help":
                case "-h":
                {
                    PrintHelpMessage();
                    return false;
                }
            }
        }

        return true;
    }

    private static void PrintHelpMessage()
    {
        Console.WriteLine(
            """
            Usage: ReplayCli [mode] [replay-path] [options...]

            Mode: the run mode of the analyzer
              verify         Verifies the replay's metadata.
              simulate_fps   Simulates FPS updates to verify the engines consistency.
              dump_inputs    Dumps the replay's inputs.

            Replay Path: the path to the replay

            Options:
              --song     | -s    Path to `song.ini` folder (required in `verify` and `simulate_fps` modes).
              --help     | -h    Show this help message.
            """);
    }

    /// <summary>
    /// Runs the analyzer using the arguments parsed in <see cref="ParseArguments"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the run succeeded, <c>false</c> if it didn't.
    /// </returns>
    public bool Run()
    {
        _replay = ReadReplay();
        if (_replay is null)
        {
            return false;
        }

        return _runMode switch
        {
            AnalyzerMode.Verify => RunVerify(),
            _                   => false
        };
    }

    private Replay ReadReplay()
    {
        var result = ReplayIO.ReadReplay(_replayPath, out var replayFile);
        var replay = replayFile?.Replay;

        if (result != ReplayReadResult.Valid || replay is null)
        {
            Console.WriteLine($"ERROR: Failed to load replay. Read Result: {result}.");
            return null;
        }

        return replay;
    }

    private SongChart ReadChart()
    {
        string songIni = Path.Combine(_songPath, "song.ini");
        string notesMid = Path.Combine(_songPath, "notes.mid");
        string notesChart = Path.Combine(_songPath, "notes.chart");
        if (!File.Exists(songIni) || (!File.Exists(notesMid) && !File.Exists(notesChart)))
        {
            Console.WriteLine(
                "ERROR: Song directory does not contain necessary song files (song.ini, notes.mid/chart).");
            return null;
        }

        SongChart chart;
        try
        {
            // TODO: Prevent this workaround from being needed
            var parseSettings = ParseSettings.Default;
            parseSettings.DrumsType = DrumsType.FourLane;

            if (File.Exists(notesMid))
            {
                chart = SongChart.FromFile(parseSettings, notesMid);
            }
            else
            {
                chart = SongChart.FromFile(parseSettings, notesChart);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"ERROR: Failed to load notes file. \n{e}");
            return null;
        }

        return chart;
    }
}