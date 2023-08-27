using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Drums.Engines;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Guitar.Engines;
using YARG.Core.Input;
using YARG.Core.Replays;

namespace ReplayAnalyzer;

public class Analyzer
{
    public const int ATTEMPTS = 100;

    private readonly SongChart _chart;
    private readonly Replay    _replay;

    private int                _currentBandScore;
    private readonly List<int> _bandScores = new();

    public IReadOnlyList<int> BandScores => _bandScores;

    public Analyzer(SongChart chart, Replay replay)
    {
        _chart = chart;
        _replay = replay;
    }

    public void Run()
    {
        RunAnalyzer(null);
    }

    public void RunWithSimulatedUpdates()
    {
        var random = new Random();
        for (int i = 0; i < ATTEMPTS; i++)
        {
            var randomValues = new List<double>();

            // Populate with a fps
            int fps = random.Next(30, 240);
            double secondsPerFrame = 1.0 / fps;
            double endTime = _chart.GetEndTime();
            for (double time = 0; time < endTime; time += secondsPerFrame)
            {
                // Add a little bit of inconsistency
                randomValues.Add(time + (random.NextDouble() - 0.5) * secondsPerFrame);
            }

            // Sort
            randomValues.Sort();

            Console.WriteLine($"> Running at {fps} FPS");
            RunAnalyzer(randomValues);
        }
    }

    private void RunAnalyzer(IReadOnlyList<double> frameUpdates)
    {
        _currentBandScore = 0;

        // Run it one player at a time
        foreach (var frame in _replay.Frames)
        {
            RunFrame(frame, frameUpdates);
        }

        _bandScores.Add(_currentBandScore);
    }

    private void RunFrame(ReplayFrame replayFrame, IReadOnlyList<double> frameUpdates)
    {
        var engine = CreateEngine(replayFrame);

        Console.WriteLine($"> Running for {replayFrame.PlayerName}...");

        if (frameUpdates is null)
        {
            // Run each input through the engine
            foreach (var input in replayFrame.Inputs)
            {
                engine.QueueInput(input);
            }
            engine.UpdateEngine();
        }
        else
        {
            // Create queues for convenience
            var inputQueue = new Queue<GameInput>(replayFrame.Inputs);
            var frameUpdateQueue = new Queue<double>(frameUpdates);

            while (frameUpdateQueue.Count > 0)
            {
                double frameTime = frameUpdateQueue.Dequeue();

                // Queue all of the inputs for that frame
                while (inputQueue.TryPeek(out var input) && input.Time < frameTime)
                {
                    engine.QueueInput(inputQueue.Dequeue());
                }

                // Run!
                if (engine.IsInputQueued)
                {
                    engine.UpdateEngine();
                }
                else
                {
                    engine.UpdateEngine(frameTime);
                }
            }
        }

        // Done!
        int score = GetScore(engine, replayFrame);
        Console.WriteLine($"> Done running for {replayFrame.PlayerName}, final score: {score}");
        _currentBandScore += score;
    }

    private BaseEngine CreateEngine(ReplayFrame replayFrame)
    {
        Console.WriteLine($"> Creating engine for {replayFrame.PlayerName}...");

        var gameMode = replayFrame.Instrument.ToGameMode();

        switch (gameMode)
        {
            case GameMode.FiveFretGuitar:
            {
                // Reset the notes
                var notes = _chart.GetFiveFretTrack(replayFrame.Instrument).Difficulties[replayFrame.Difficulty];
                foreach (var note in notes.Notes)
                {
                    foreach (var subNote in note.ChordEnumerator())
                    {
                        subNote.ResetNoteState();
                    }
                }

                // Create engine
                return new YargFiveFretEngine(
                    notes,
                    _chart.SyncTrack,
                    replayFrame.EngineParameters as GuitarEngineParameters);
            }
            case GameMode.FourLaneDrums:
            {
                // Reset the notes
                var notes = _chart.GetDrumsTrack(replayFrame.Instrument).Difficulties[replayFrame.Difficulty];
                foreach (var note in notes.Notes)
                {
                    foreach (var subNote in note.ChordEnumerator())
                    {
                        subNote.ResetNoteState();
                    }
                }

                // Create engine
                return new YargDrumsEngine(
                    notes,
                    _chart.SyncTrack,
                    replayFrame.EngineParameters as DrumsEngineParameters);
            }
            default:
                throw new InvalidOperationException("Game mode not configured!");
        }
    }

    private int GetScore(BaseEngine engine, ReplayFrame replayFrame)
    {
        var gameMode = replayFrame.Instrument.ToGameMode();

        return gameMode switch
        {
            GameMode.FiveFretGuitar => ((GuitarEngine) engine).EngineStats.Score,
            GameMode.FourLaneDrums  => ((DrumsEngine) engine).EngineStats.Score,
            _                       => throw new InvalidOperationException("Game mode not configured!")
        };
    }
}