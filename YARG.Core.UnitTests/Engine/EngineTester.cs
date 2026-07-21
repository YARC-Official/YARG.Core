using Melanchall.DryWetMidi.Core;
using YARG.Core.Chart;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Drums.Engines;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Guitar.Engines;
using YARG.Core.Engine.Keys;
using YARG.Core.Engine.Keys.Engines;

namespace YARG.Core.UnitTests.Engine;

public class EngineTester
{
    public static float[] SoloBonusStarMultiplierThresholds = {
        0.05f, 0.1f, 0.2f, 0.35f, 0.65f, 0.95f
    };

    public static float[] StarMultiplierThresholds { get; } =
    {
        0.06f, 0.12f, 0.2f, 0.45f, 0.75f, 1.09f
    };

    protected string ChartDirectory;

    protected EngineTester()
    {
        string workingDirectory = Environment.CurrentDirectory;

        string projectDirectory = Directory.GetParent(workingDirectory)!.Parent!.Parent!.Parent!.FullName;

        ChartDirectory = Path.Combine(projectDirectory, "Engine", "Test Charts");
    }

    protected SongChart GetChart()
    {
        var chartPath = Path.Combine(ChartDirectory!, "drawntotheflame.mid");
        var midi = MidiFile.Read(chartPath);
        return SongChart.FromMidi(in ParseSettings.Default_Midi, midi);
    }

    protected (GuitarEngine Engine, InstrumentDifficulty<GuitarNote> Notes) CreateEngine(GuitarEngineParameters engineParams, bool isBot, bool isBass)
    {
        var chart = GetChart();
        var notes = isBass ?
            chart.FiveFretBass.GetDifficulty(Difficulty.Expert) :
            chart.FiveFretGuitar.GetDifficulty(Difficulty.Expert);
        var engine = new YargFiveFretGuitarEngine(notes, chart.SyncTrack, engineParams, isBot);
        return (engine, notes);
    }

    protected (YargDrumsEngine Engine, InstrumentDifficulty<DrumNote> Notes) CreateEngine(
        DrumsEngineParameters engineParams, bool isBot)
    {
        var chart = GetChart();
        var notes = chart.ProDrums.GetDifficulty(Difficulty.Expert);
        var engine = new YargDrumsEngine(notes, chart.SyncTrack, engineParams, isBot, false);
        return (engine, notes);
    }

    protected (YargFiveLaneKeysEngine Engine, InstrumentDifficulty<GuitarNote> Notes) CreateEngine(KeysEngineParameters engineParams, bool isBot)
    {
        var chart = GetChart();
        var notes = chart.Keys.GetDifficulty(Difficulty.Expert);
        var engine = new YargFiveLaneKeysEngine(notes, chart.SyncTrack, engineParams, isBot);
        return (engine, notes);
    }
}