using ReplayAnalyzer;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Engine.Logging;
using YARG.Core.Replays;

YargTrace.AddListener(new YargDebugTraceListener());

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

LOADING:

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
            var kp = analyzer.BandScores.First();
            var bandScore = kp.Value;
            if (bandScore != replay.BandScore)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("VERIFICATION FAILED!");
                Console.WriteLine($"Metadata score : {replay.BandScore}");
                Console.WriteLine($"Real score     : {bandScore}");
                Console.WriteLine($"Difference     : {Math.Abs(bandScore - replay.BandScore)}\n");

                var analyzerNoteEvents = analyzer.EventLog.Events.Where(e => e is NoteEngineEvent)
                    .Cast<NoteEngineEvent>().ToList();
                var metaDataNoteEvents = replay.Frames[0].EventLog.Events.Where(e => e is NoteEngineEvent)
                    .Cast<NoteEngineEvent>().ToList();

                analyzerNoteEvents.Sort((x, y) => x.NoteIndex.CompareTo(y.NoteIndex));
                metaDataNoteEvents.Sort((x, y) => x.NoteIndex.CompareTo(y.NoteIndex));

                Console.WriteLine($"Analyzer Count: {analyzerNoteEvents.Count}");
                Console.WriteLine($"Metadata Count: {analyzerNoteEvents.Count}");
                
                for (int i = 0; i < analyzerNoteEvents.Count; i++)
                {
                    if (analyzerNoteEvents[i].WasHit != metaDataNoteEvents[i].WasHit)
                    {
                        Console.WriteLine(
                            $"({analyzerNoteEvents[i].NoteIndex}, {analyzerNoteEvents[i].NoteMask}) " +
                            $"({metaDataNoteEvents[i].NoteIndex}, {metaDataNoteEvents[i].NoteMask}):\n" +
                            $"- (A) {analyzerNoteEvents[i].WasHit} != (M) {metaDataNoteEvents[i].WasHit}");
                    }
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("VERIFICATION SUCCESS!");
                Console.WriteLine($"Metadata score : {replay.BandScore}");
                Console.WriteLine($"Real score     : {bandScore}");
                Console.WriteLine($"Difference     : {Math.Abs(bandScore - replay.BandScore)}\n");
            }

            Console.WriteLine($"Metadata event count : {replay.Frames[0].EventLog.Events.Count}");
            Console.WriteLine($"Real event count     : {analyzer.EventLog.Events.Count}");
        }
        else
        {
            var distinctScores = new List<int>();
            var distincts = new List<(int fps, int score)>();

            foreach (var s in analyzer.BandScores)
            {
                if (distinctScores.Contains(s.Value)) continue;

                distinctScores.Add(s.Value);
                distincts.Add((s.Key, s.Value));
            }

            if (distinctScores.Count != 1)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("SCORES ARE NOT CONSISTENT!");
                Console.WriteLine($"Chart runs      : {Analyzer.ATTEMPTS}");
                Console.WriteLine($"Distinct scores : {distinctScores.Count}\n");
                Console.WriteLine("Scores:");
                foreach ((int fps, int score) in distincts)
                {
                    Console.WriteLine($" - {score} (FPS: {fps})");
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

    Console.WriteLine("Press R to re-run or any key to continue...");
    var key = Console.ReadKey(true);

    if (key.Key == ConsoleKey.R)
    {
        goto LOADING;
    }
}