using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Drums.Engines;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Guitar.Engines;
using YARG.Core.Engine.Logging;
using YARG.Core.Engine.Vocals;
using YARG.Core.Engine.Vocals.Engines;
using YARG.Core.Input;
using YARG.Core.Replays;

namespace ReplayAnalyzer;

public class Analyzer
{
    public const int ATTEMPTS = 100;

    private readonly SongChart _chart;
    private readonly Replay    _replay;

    private          int           _currentBandScore;
    private readonly Dictionary<int, int> _bandScores = new();

    public IReadOnlyDictionary<int, int> BandScores => _bandScores;

    public EngineEventLogger EventLog;

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
            int fps = i * 2 + 1;
            randomValues.AddRange(GenerateFrameTimes(fps));

            Console.WriteLine($"> Running at {fps} FPS");
            RunAnalyzer(fps, randomValues);
        }
    }

    private List<double> GenerateFrameTimes(int fps)
    {
        var random = new Random();
        var frameTimes = new List<double>();
        
        double secondsPerFrame = 1.0 / fps;
        double endTime = _chart.GetEndTime();
        for (double time = -2; time < endTime; time += secondsPerFrame)
        {
            // Add a little bit of inconsistency
            frameTimes.Add(time + (random.NextDouble() - 0.5) * secondsPerFrame);
        }

        // Sort
        frameTimes.Sort();

        return frameTimes;
    }

    private void RunAnalyzer(int fps, IReadOnlyList<double> frameUpdates)
    {
        _currentBandScore = 0;

        // Run it one player at a time
        foreach (var frame in _replay.Frames)
        {
            RunFrame(frame, frameUpdates);
        }

        _bandScores.Add(fps, _currentBandScore);
    }

    private void RunFrame(ReplayFrame replayFrame, IReadOnlyList<double> frameUpdates)
    {
        var engine = CreateEngine(replayFrame);
        engine.Reset();

        Console.WriteLine($"> Running for {replayFrame.PlayerInfo.Profile.Name}...");

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
                while (inputQueue.TryPeek(out var input) && frameTime >= input.Time)
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
        Console.WriteLine($"> Done running for {replayFrame.PlayerInfo.Profile.Name}, final score: {score}");
        _currentBandScore += score;
    }

    private BaseEngine CreateEngine(ReplayFrame replayFrame)
    {
        var profile = replayFrame.PlayerInfo.Profile;
        Console.WriteLine($"> Creating engine for {profile.Name}...");

        var gameMode = profile.GameMode;

        switch (gameMode)
        {
            case GameMode.FiveFretGuitar:
            {
                // Reset the notes
                var notes = _chart.GetFiveFretTrack(profile.CurrentInstrument).Difficulties[profile.CurrentDifficulty];
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
                    (GuitarEngineParameters) replayFrame.EngineParameters);
            }
            case GameMode.FourLaneDrums:
            case GameMode.FiveLaneDrums:
            {
                // Reset the notes
                var notes = _chart.GetDrumsTrack(profile.CurrentInstrument).Difficulties[profile.CurrentDifficulty];
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
                    (DrumsEngineParameters) replayFrame.EngineParameters);
            }
            case GameMode.Vocals:
            {
                // Get the notes
                var notes = _chart.GetVocalsTrack(profile.CurrentInstrument).Parts[0].CloneAsInstrumentDifficulty();

                // Create engine
                return new YargVocalsEngine(
                    notes,
                    _chart.SyncTrack,
                    (VocalsEngineParameters) replayFrame.EngineParameters);
            }
            default:
                throw new InvalidOperationException("Game mode not configured!");
        }
    }

    private int GetScore(BaseEngine engine, ReplayFrame replayFrame)
    {
        var gameMode = replayFrame.PlayerInfo.Profile.GameMode;

        return gameMode switch
        {
            GameMode.FiveFretGuitar => ((GuitarEngine) engine).EngineStats.Score,
            GameMode.FourLaneDrums or
            GameMode.FiveLaneDrums  => ((DrumsEngine) engine).EngineStats.Score,
            GameMode.Vocals         => ((VocalsEngine) engine).EngineStats.Score,
            _                       => throw new InvalidOperationException("Game mode not configured!")
        };
    }
}