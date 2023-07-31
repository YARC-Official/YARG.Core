using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Guitar.Engines;
using YARG.Core.Replays;

namespace ReplayAnalyzer;

public class Analyzer
{
    private readonly SongChart _chart;
    private readonly Replay    _replay;

    public int BandScore { get; private set; }

    // TODO: This should be consistent in YARG and here
    private readonly GuitarEngineParameters _engineParams = new(0.16, 1, 0.08, 0.07, 0.035, false, true);

    public Analyzer(SongChart chart, Replay replay)
    {
        _chart = chart;
        _replay = replay;
    }

    public void Run()
    {
        // Run it one player at a time
        foreach (var frame in _replay.Frames)
        {
            RunFrame(frame);
        }
    }

    private void RunFrame(ReplayFrame frame)
    {
        var gameMode = frame.Instrument.ToGameMode();

        // TODO: Make this work for other instruments
        if (gameMode != GameMode.FiveFretGuitar)
        {
            return;
        }

        Console.WriteLine($"> Running for {frame.PlayerName}...");

        // Create engine
        var notes = _chart.GetFiveFretTrack(frame.Instrument).Difficulties[frame.Difficulty];
        var engine = new YargFiveFretEngine(notes, _chart.SyncTrack, _engineParams);

        // Run each input through the engine
        foreach (var input in frame.Inputs)
        {
            engine.QueueInput(input);
        }
        engine.UpdateEngine();

        // Done!
        Console.WriteLine($"> Done running for {frame.PlayerName}, final score: {engine.EngineStats.Score}");
        BandScore += engine.EngineStats.Score;
    }
}