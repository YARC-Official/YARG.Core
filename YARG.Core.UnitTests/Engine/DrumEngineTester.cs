using Melanchall.DryWetMidi.Core;
using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Drums.Engines;
using YARG.Core.Game;

namespace YARG.Core.UnitTests.Engine;

public class DrumEngineTester
{
    public static float[] StarMultiplierThresholds { get; } =
    {
        0.06f, 0.12f, 0.2f, 0.45f, 0.75f, 1.09f
    };
    // This should probably be in some parent class of the tester, but right now there's only drums tests so it's fine
    public static float[] SoloBonusStarMultiplierThresholds = {
        0.05f, 0.1f, 0.2f, 0.35f, 0.65f, 0.95f
    };

    private readonly DrumsEngineParameters _engineParams =
        EnginePreset.Default.Drums.Create(StarMultiplierThresholds, SoloBonusStarMultiplierThresholds, DrumsEngineParameters.DrumMode.ProFourLane);

    private string? _chartsDirectory;

    [SetUp]
    public void Setup()
    {
        string workingDirectory = Environment.CurrentDirectory;

        string projectDirectory = Directory.GetParent(workingDirectory)!.Parent!.Parent!.Parent!.FullName;

        _chartsDirectory = Path.Combine(projectDirectory, "Engine", "Test Charts");
    }

    [Test]
    public void DrumSoloThatEndsInChord_ShouldWorkCorrectly()
    {
        var chartPath = Path.Combine(_chartsDirectory!, "drawntotheflame.mid");
        var midi = MidiFile.Read(chartPath);
        var chart = SongChart.FromMidi(in ParseSettings.Default_Midi, midi);
        var notes = chart.ProDrums.GetDifficulty(Difficulty.Expert);

        var engine = new YargDrumsEngine(notes, chart.SyncTrack, _engineParams, true, false);
        var endTime = notes.GetEndTime();
        var timeStep = 0.01;
        for (double i = 0; i < endTime; i += timeStep)
        {
            engine.Update(i);
        }

        Assert.That(engine.EngineStats.SoloBonuses, Is.EqualTo(3900));
    }

    [Test]
    public void DrumTrackWithKickDrumRemoved_ShouldWorkCorrectly()
    {
        var chartPath = Path.Combine(_chartsDirectory!, "drawntotheflame.mid");
        var midi = MidiFile.Read(chartPath);
        var chart = SongChart.FromMidi(in ParseSettings.Default_Midi, midi);
        var notes = chart.ProDrums.GetDifficulty(Difficulty.Expert);

        notes.RemoveKickDrumNotes();

        var engine = new YargDrumsEngine(notes, chart.SyncTrack, _engineParams, true, false);
        var endTime = notes.GetEndTime();
        var timeStep = 0.01;
        for (double i = 0; i < endTime; i += timeStep)
        {
            engine.Update(i);
        }

        Assert.That(engine.EngineStats.NotesHit, Is.EqualTo(notes.GetTotalNoteCount()));
    }
}