using Melanchall.DryWetMidi.Core;
using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Drums.Engines;

namespace YARG.Core.UnitTests.Engine;

public class DrumEngineTester
{
    public static float[] StarMultiplierThresholds { get; } =
    {
        0.21f, 0.46f, 0.77f, 1.85f, 3.08f, 4.29f
    };

    private readonly DrumsEngineParameters _engineParams = new(new HitWindowSettings(0.15, 0.03, 1, false), StarMultiplierThresholds,
        DrumsEngineParameters.DrumMode.ProFourLane);

    private string? _chartsDirectory;

    private readonly ParseSettings _settings = ParseSettings.Default;

    [SetUp]
    public void Setup()
    {
        string workingDirectory = Environment.CurrentDirectory;

        string projectDirectory = Directory.GetParent(workingDirectory)!.Parent!.Parent!.FullName;

        _chartsDirectory = Path.Combine(projectDirectory, "Engine", "Test Charts");
    }

    [TestCase]
    public void DrumSoloThatEndsInChord_ShouldWorkCorrectly()
    {
        var chartPath = Path.Combine(_chartsDirectory!, "drawntotheflame.mid");
        var midi = MidiFile.Read(chartPath);
        var chart = SongChart.FromMidi(_settings, midi);
        var notes = chart.ProDrums.Difficulties[Difficulty.Expert];

        var engine = new YargDrumsEngine(notes, chart.SyncTrack, _engineParams);
        var endTime = notes.GetEndTime();
        var timeStep = 0.01;
        for (double i = 0; i < endTime; i += timeStep)
        {
            engine.UpdateBot(i);
        }

        Assert.That(engine.EngineStats.SoloBonuses, Is.EqualTo(3900));
    }
}