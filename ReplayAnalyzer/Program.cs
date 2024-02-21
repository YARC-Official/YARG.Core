using ReplayAnalyzer;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Replays;

void ClearAndPrintHeader()
{
    const string HEADER = "Welcome to the YARG.Core Replay Analyzer";

    Console.Clear();
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.SetCursorPosition((Console.WindowWidth - HEADER.Length) / 2, Console.CursorTop + 1);
    Console.WriteLine(HEADER);

    Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop + 1);
    Console.ForegroundColor = ConsoleColor.White;
}

string songPath = string.Empty;
string replayPath = string.Empty;
int runMode = 0;

var defaultColor = Console.ForegroundColor;

for (int i = 0; i < args.Length; ++i)
{
    var arg = args[i];
    switch (arg)
    {
        case "--song":
        case "-s":
        {
            i++;
            songPath = args[i].Trim();
            if (!Directory.Exists(songPath))
            {
                Console.WriteLine("ERROR: Song directory does not exist.");
                return;
            }

            break;
        }
        case "--replay":
        case "-r":
        {
            i++;
            replayPath = args[i].Trim();
            if (!File.Exists(replayPath))
            {
                Console.WriteLine("ERROR: Replay file does not exist.");
                return;
            }

            break;
        }
        case "--mode":
        case "-m":
        {
            i++;
            if (!int.TryParse(args[i], out runMode))
            {
                Console.WriteLine("ERROR: Invalid run mode.");
                return;
            }

            break;
        }
        case "--help":
        case "-h":
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  --song     | -s   Path to song folder.");
            Console.WriteLine("  --replay   | -r   Path to the replay file.");
            Console.WriteLine("  --mode     | -m   Run mode (0 = normal, 1 = simulated fps, 2 = dump inputs).");
            Console.WriteLine("  --help     | -h   Show this help message.");
            return;
        }
    }
}

if (string.IsNullOrEmpty(songPath))
{
    Console.WriteLine("ERROR: A song directory must be specified.");
    return;
}

if (string.IsNullOrEmpty(replayPath))
{
    Console.WriteLine("ERROR: A replay file path must be specified.");
    return;
}

if (runMode is < 0 or > 2)
{
    Console.WriteLine("ERROR: Invalid run mode.");
    return;
}

string songIni = Path.Combine(songPath, "song.ini");
string notesMid = Path.Combine(songPath, "notes.mid");
string notesChart = Path.Combine(songPath, "notes.chart");
if (!File.Exists(songIni) || (!File.Exists(notesMid) && !File.Exists(notesChart)))
{
    Console.WriteLine("ERROR: Song directory does not contain necessary song files (song.ini, notes.mid/chart)");
    return;
}

SongChart chart;
try
{
    if (File.Exists(notesMid))
    {
        chart = SongChart.FromFile(ParseSettings.Default, notesMid);
    }
    else
    {
        chart = SongChart.FromFile(ParseSettings.Default, notesChart);
    }
}
catch (Exception e)
{
    Console.WriteLine($"ERROR: Failed to load notes file. \n{e}");
    return;
}

var result = ReplayIO.ReadReplay(replayPath, out var replayFile);
var replay = replayFile?.Replay;

if (result != ReplayReadResult.Valid || replay is null)
{
    Console.WriteLine($"ERROR: Failed to load replay. Read Result: {result}.");
    return;
}

Console.WriteLine($"Players ({replay.PlayerCount}):");
for (int i = 0; i < replay.Frames.Length; i++)
{
    var frame = replay.Frames[i];
    var profile = frame.PlayerInfo.Profile;

    Console.WriteLine($"{i}. {profile.Name}, {profile.CurrentInstrument} ({profile.CurrentDifficulty})");
}

Console.WriteLine($"Band score: {replay.BandScore} (as per metadata)\n");

if (runMode is 0 or 1)
{
    // Analyze replay

    Console.WriteLine("Analyzing replay...");
    var analyzer = new Analyzer(chart, replay);
    if (runMode == 0)
    {
        analyzer.Run();
    }
    else
    {
        analyzer.RunWithSimulatedUpdates();
    }

    Console.WriteLine("Done!\n");

    // Results

    if (runMode == 0)
    {
        var bandScore = analyzer.BandScores[0];
        if (bandScore != replay.BandScore)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("VERIFICATION FAILED!");
            Console.WriteLine($"Metadata score : {replay.BandScore}");
            Console.WriteLine($"Real score     : {bandScore}");
            Console.WriteLine($"Difference     : {Math.Abs(bandScore - replay.BandScore)}\n");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("VERIFICATION SUCCESS!");
            Console.WriteLine($"Metadata score : {replay.BandScore}");
            Console.WriteLine($"Real score     : {bandScore}");
            Console.WriteLine($"Difference     : {Math.Abs(bandScore - replay.BandScore)}\n");
        }
    }
    else
    {
        var distinctScores = analyzer.BandScores.Values.Distinct().ToList();

        if (distinctScores.Count != 1)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("SCORES ARE NOT CONSISTENT!");
            Console.WriteLine($"Chart runs      : {Analyzer.ATTEMPTS}");
            Console.WriteLine($"Distinct scores : {distinctScores.Count}\n");
            Console.WriteLine("Scores:");
            foreach (var score in distinctScores)
            {
                Console.WriteLine($" - {score}");
            }
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("SCORES WERE CONSISTENT!");
            Console.WriteLine($"Chart runs      : {Analyzer.ATTEMPTS}");
            Console.WriteLine($"Distinct scores : {distinctScores.Count}");
        }
    }
}
// else
// {
//     Console.Write("Enter player number to analyze inputs: ");
//     string playerId = Console.ReadLine();
//
//     if (!int.TryParse(playerId, out int selectedPlayer))
//     {
//         continue;
//     }
//
//     Console.WriteLine("|       Time | Action |       Axis |    Integer | Button | Difference |");
//     double lastTime = double.NegativeInfinity;
//     foreach (var replayInput in replay.Frames[selectedPlayer].Inputs)
//     {
//         Console.WriteLine(
//             $"| {replayInput.Time,10:0.0000} | {replayInput.Action,6} | {replayInput.Axis,10:0.00} | " +
//             $"{replayInput.Integer,10} | {(replayInput.Button ? "Y" : "N"),6} | {replayInput.Time - lastTime,10:0.0000} |");
//         lastTime = replayInput.Time;
//     }
//
//     Console.WriteLine(
//         $"{replay.Frames[selectedPlayer].Inputs.Length} input(s) were read from player {selectedPlayer}.");
// }

Console.ForegroundColor = defaultColor;

/*
while (true)
{
    ClearAndPrintHeader();

    // Show the options

    Console.WriteLine("Choose on of the options below:");
    Console.WriteLine("1. Verify replay");
    Console.WriteLine("2. Engine consistency checker");
    Console.WriteLine("3. Input dumper");

    // Prompt the user to select an option

    Console.Write("Enter option: ");
    string input = Console.ReadLine();

    if (!int.TryParse(input, out int selectedOption))
    {
        continue;
    }

    if (selectedOption is < 1 or > 3)
    {
        continue;
    }

    // Prompt for a replay path (regardless of option)

    ClearAndPrintHeader();
    Console.Write("Enter a valid replay path: ");

    string replayPath = Console.ReadLine();
    if (!File.Exists(replayPath))
    {
        Console.WriteLine("ERROR: Replay does not exist. Press any key to continue.");
        Console.ReadKey(true);
        continue;
    }

    // Prompt for a song folder (regardless of option)

    Console.Write("Enter a valid song folder: ");

    string songFolder = Console.ReadLine();
    if (!Directory.Exists(songFolder))
    {
        Console.WriteLine("ERROR: Song folder does not exist. Press any key to continue.");
        Console.ReadKey(true);
        continue;
    }

    // Look for song.ini and notes file

    string songIni = Path.Combine(songFolder, "song.ini");
    string notesMid = Path.Combine(songFolder, "notes.mid");
    string notesChart = Path.Combine(songFolder, "notes.chart");
    if (!File.Exists(songIni) || (!File.Exists(notesMid) && !File.Exists(notesChart)))
    {
        Console.WriteLine("ERROR: Song folder does not have to proper files inside! Press any key to continue.");
        Console.ReadKey(true);
        continue;
    }

    // Load song

    ClearAndPrintHeader();
    Console.WriteLine("Loading song...");

    SongChart chart;
    try
    {
        if (File.Exists(notesMid))
        {
            chart = SongChart.FromFile(ParseSettings.Default, notesMid);
        }
        else
        {
            chart = SongChart.FromFile(ParseSettings.Default, notesChart);
        }
    }
    catch (Exception e)
    {
        Console.WriteLine($"ERROR: {e}. Press any key to continue.");
        Console.ReadKey(true);
        continue;
    }

    Console.WriteLine("Done!");

    // Load replay

    Console.WriteLine("Loading replay...");
    var result = ReplayIO.ReadReplay(replayPath, out var replayFile);
    var replay = replayFile?.Replay;

    if (result != ReplayReadResult.Valid || replay is null)
    {
        Console.WriteLine($"ERROR: Replay result is {result}! Press any key to continue.");
        Console.ReadKey(true);
        continue;
    }

    Console.WriteLine("Done!\n");

    // Load players

    Console.WriteLine($"Players ({replay.PlayerCount}):");
    for (int i = 0; i < replay.Frames.Length; i++)
    {
        var frame = replay.Frames[i];
        var profile = frame.PlayerInfo.Profile;

        Console.WriteLine($"{i}. {profile.Name}, {profile.CurrentInstrument} ({profile.CurrentDifficulty})");
    }

    Console.WriteLine($"Band score: {replay.BandScore} (as per metadata)\n");

    if (selectedOption is 1 or 2)
    {
        // Analyze replay

        Console.WriteLine("Analyzing replay...");
        var analyzer = new Analyzer(chart, replay);
        if (selectedOption == 1)
        {
            analyzer.Run();
        }
        else
        {
            analyzer.RunWithSimulatedUpdates();
        }

        Console.WriteLine("Done!\n");

        // Results

        if (selectedOption == 1)
        {
            var bandScore = analyzer.BandScores[0];
            if (bandScore != replay.BandScore)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("VERIFICATION FAILED!");
                Console.WriteLine($"Metadata score : {replay.BandScore}");
                Console.WriteLine($"Real score     : {bandScore}");
                Console.WriteLine($"Difference     : {Math.Abs(bandScore - replay.BandScore)}\n");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("VERIFICATION SUCCESS!");
                Console.WriteLine($"Metadata score : {replay.BandScore}");
                Console.WriteLine($"Real score     : {bandScore}");
                Console.WriteLine($"Difference     : {Math.Abs(bandScore - replay.BandScore)}\n");
            }
        }
        else
        {
            var distinctScores = analyzer.BandScores.Distinct().ToList();

            if (distinctScores.Count != 1)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("SCORES ARE NOT CONSISTENT!");
                Console.WriteLine($"Chart runs      : {Analyzer.ATTEMPTS}");
                Console.WriteLine($"Distinct scores : {distinctScores.Count}\n");
                Console.WriteLine("Scores:");
                foreach (var score in distinctScores)
                {
                    Console.WriteLine($" - {score}");
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("SCORES WERE CONSISTENT!");
                Console.WriteLine($"Chart runs      : {Analyzer.ATTEMPTS}");
                Console.WriteLine($"Distinct scores : {distinctScores.Count}");
            }
        }
    }
    else
    {
        Console.Write("Enter player number to analyze inputs: ");
        string playerId = Console.ReadLine();

        if (!int.TryParse(playerId, out int selectedPlayer))
        {
            continue;
        }

        Console.WriteLine("|       Time | Action |       Axis |    Integer | Button | Difference |");
        double lastTime = double.NegativeInfinity;
        foreach (var replayInput in replay.Frames[selectedPlayer].Inputs)
        {
            Console.WriteLine(
                $"| {replayInput.Time,10:0.0000} | {replayInput.Action,6} | {replayInput.Axis,10:0.00} | " +
                $"{replayInput.Integer,10} | {(replayInput.Button ? "Y" : "N"),6} | {replayInput.Time - lastTime,10:0.0000} |");
            lastTime = replayInput.Time;
        }

        Console.WriteLine(
            $"{replay.Frames[selectedPlayer].Inputs.Length} input(s) were read from player {selectedPlayer}.");
    }

    Console.WriteLine("Press any key to continue...");
    Console.ReadKey(true);
}/*/