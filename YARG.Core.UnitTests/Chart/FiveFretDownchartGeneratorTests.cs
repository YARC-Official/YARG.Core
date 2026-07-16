using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.Chart.AutoGeneration;

namespace YARG.Core.UnitTests.Chart;

public class FiveFretDownchartGeneratorTests
{
    private const uint RESOLUTION = 480;

    [Test]
    public void Generate_CreatesIndependentGeneratedDifficulty()
    {
        var sync = MakeSyncTrack();
        var source = MakeExpert(
            MakeNote(sync, 0, FiveFretGuitarFret.Green, FiveFretGuitarFret.Red, FiveFretGuitarFret.Yellow),
            MakeNote(sync, 120, FiveFretGuitarFret.Blue),
            MakeNote(sync, 240, FiveFretGuitarFret.Orange));

        var generated = FiveFretDownchartGenerator.Generate(source, Difficulty.Hard, sync);
        var clone = generated.Clone();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(generated.Difficulty, Is.EqualTo(Difficulty.Hard));
            Assert.That(generated.IsGenerated, Is.True);
            Assert.That(clone.IsGenerated, Is.True);
            Assert.That(generated.Notes[0].ChildNotes, Has.Count.EqualTo(1));
            Assert.That(source.Notes[0].ChildNotes, Has.Count.EqualTo(2));
            Assert.That(generated.Notes[0], Is.Not.SameAs(source.Notes[0]));
            Assert.That(generated.Notes[0].NextNote, Is.SameAs(generated.Notes[1]));
            Assert.That(generated.Notes[1].PreviousNote, Is.SameAs(generated.Notes[0]));
        }
    }

    [Test]
    public void SongChartGenerateFiveFretDownchart_UsesExpertDifficultyAndSyncTrack()
    {
        var sync = MakeSyncTrack();
        var expert = MakeExpert(
            MakeNote(sync, 0, FiveFretGuitarFret.Green),
            MakeNote(sync, 120, FiveFretGuitarFret.Red),
            MakeNote(sync, 240, FiveFretGuitarFret.Yellow));
        var chart = new SongChart(RESOLUTION)
        {
            SyncTrack = sync,
            FiveFretGuitar = new InstrumentTrack<GuitarNote>(Instrument.FiveFretGuitar,
                new Dictionary<Difficulty, InstrumentDifficulty<GuitarNote>>
                {
                    { Difficulty.Expert, expert },
                }),
        };

        var generated = chart.GenerateFiveFretDownchart(
            Instrument.FiveFretGuitar,
            Difficulty.Hard);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(generated.Instrument, Is.EqualTo(Instrument.FiveFretGuitar));
            Assert.That(generated.Difficulty, Is.EqualTo(Difficulty.Hard));
            Assert.That(generated.IsGenerated, Is.True);
        }
    }

    [Test]
    public void GenerateMissing_PreservesAuthoredDifficulty()
    {
        var sync = MakeSyncTrack();
        var expert = MakeExpert(
            MakeNote(sync, 0, FiveFretGuitarFret.Green),
            MakeNote(sync, 120, FiveFretGuitarFret.Red),
            MakeNote(sync, 240, FiveFretGuitarFret.Yellow));
        var authoredMedium = new InstrumentDifficulty<GuitarNote>(
            Instrument.FiveFretGuitar,
            Difficulty.Medium,
            new List<GuitarNote> { MakeNote(sync, 0, FiveFretGuitarFret.Orange) },
            new List<Phrase>(),
            new List<YARG.Core.Chart.TextEvent>());
        var track = new InstrumentTrack<GuitarNote>(Instrument.FiveFretGuitar,
            new Dictionary<Difficulty, InstrumentDifficulty<GuitarNote>>
            {
                { Difficulty.Expert, expert },
                { Difficulty.Medium, authoredMedium },
            });

        int generated = FiveFretDownchartGenerator.GenerateMissing(track, sync);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(generated, Is.EqualTo(2));
            Assert.That(track.GetDifficulty(Difficulty.Medium), Is.SameAs(authoredMedium));
            Assert.That(track.GetDifficulty(Difficulty.Medium).IsGenerated, Is.False);
            Assert.That(track.GetDifficulty(Difficulty.Hard).IsGenerated, Is.True);
            Assert.That(track.GetDifficulty(Difficulty.Easy).IsGenerated, Is.True);
            Assert.That(track.TryGetDifficulty(Difficulty.Beginner, out _), Is.False);
        }
    }

    [Test]
    public void Generate_RejectsInvalidArguments()
    {
        var sync = MakeSyncTrack();
        var source = MakeExpert(MakeNote(sync, 0, FiveFretGuitarFret.Green));

        Assert.That(
            () => FiveFretDownchartGenerator.Generate(source, Difficulty.Beginner, sync),
            Throws.TypeOf<ArgumentOutOfRangeException>());
        Assert.That(
            () => FiveFretDownchartGenerator.Generate(source, Difficulty.Easy, sync, 2.1),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void SongChartFromMidi_GeneratesMissingDifficulties()
    {
        var settings = ParseSettings.Default_Midi;
        settings.DownchartGeneration = DownchartGenerationMode.MissingDifficulties;
        var chart = SongChart.FromMidi(settings, MakeExpertMidi());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(chart.FiveFretGuitar.GetDifficulty(Difficulty.Expert).IsGenerated, Is.False);
            Assert.That(chart.FiveFretGuitar.GetDifficulty(Difficulty.Hard).IsGenerated, Is.True);
            Assert.That(chart.FiveFretGuitar.GetDifficulty(Difficulty.Medium).IsGenerated, Is.True);
            Assert.That(chart.FiveFretGuitar.GetDifficulty(Difficulty.Easy).IsGenerated, Is.True);
            Assert.That(chart.FiveFretGuitar.GetDifficulty(Difficulty.Beginner).Notes, Is.Empty);
        }
    }

    [Test]
    public void SongChartFromMidi_DoesNotGenerateDifficultiesByDefault()
    {
        var chart = SongChart.FromMidi(ParseSettings.Default_Midi, MakeExpertMidi());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(chart.FiveFretGuitar.GetDifficulty(Difficulty.Expert).Notes, Is.Not.Empty);
            Assert.That(chart.FiveFretGuitar.GetDifficulty(Difficulty.Hard).Notes, Is.Empty);
            Assert.That(chart.FiveFretGuitar.GetDifficulty(Difficulty.Medium).Notes, Is.Empty);
            Assert.That(chart.FiveFretGuitar.GetDifficulty(Difficulty.Easy).Notes, Is.Empty);
        }
    }

    [Test]
    public void MidiExporter_PreservesExpertAndWritesGeneratedDifficulties()
    {
        var source = MakeExpertMidi();
        var result = MidiDownchartExporter.Generate(source);
        var chart = SongChart.FromMidi(ParseSettings.Default_Midi, result.Midi);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.GeneratedDifficultyCount, Is.EqualTo(3));
            Assert.That(chart.FiveFretGuitar.GetDifficulty(Difficulty.Expert).Notes, Has.Count.EqualTo(4));
            Assert.That(chart.FiveFretGuitar.GetDifficulty(Difficulty.Hard).Notes, Is.Not.Empty);
            Assert.That(chart.FiveFretGuitar.GetDifficulty(Difficulty.Medium).Notes, Is.Not.Empty);
            Assert.That(chart.FiveFretGuitar.GetDifficulty(Difficulty.Easy).Notes, Is.Not.Empty);
            Assert.That(source.Chunks.OfType<TrackChunk>().Single(track => GetTrackName(track) == "PART GUITAR")
                .Events.OfType<NoteOnEvent>().Count(), Is.EqualTo(4));
        }
    }

    [Test]
    public void MidiExporter_DrawntotheflameProducesStableGuitarDowncharts()
    {
        string projectDirectory = Directory.GetParent(Environment.CurrentDirectory)!
            .Parent!.Parent!.Parent!.FullName;
        string midiPath = Path.Combine(projectDirectory, "Engine", "Test Charts", "drawntotheflame.mid");
        var source = MidiFile.Read(midiPath);

        var result = MidiDownchartExporter.Generate(source, new MidiDownchartExportOptions
        {
            ReplaceExisting = true,
            Instruments = [Instrument.FiveFretGuitar],
        });
        var chart = SongChart.FromMidi(ParseSettings.Default_Midi, result.Midi);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.GeneratedDifficultyCount, Is.EqualTo(3));
            Assert.That(result.SkippedDifficultyCount, Is.Zero);
            Assert.That(chart.FiveFretGuitar.GetDifficulty(Difficulty.Hard).Notes, Has.Count.EqualTo(1167));
            Assert.That(chart.FiveFretGuitar.GetDifficulty(Difficulty.Medium).Notes, Has.Count.EqualTo(976));
            Assert.That(chart.FiveFretGuitar.GetDifficulty(Difficulty.Easy).Notes, Has.Count.EqualTo(519));
        }
    }

    private static InstrumentDifficulty<GuitarNote> MakeExpert(params GuitarNote[] notes)
    {
        for (int i = 0; i < notes.Length; i++)
        {
            notes[i].PreviousNote = i > 0 ? notes[i - 1] : null;
            notes[i].NextNote = i + 1 < notes.Length ? notes[i + 1] : null;
        }

        return new InstrumentDifficulty<GuitarNote>(
            Instrument.FiveFretGuitar,
            Difficulty.Expert,
            notes.ToList(),
            new List<Phrase>(),
            new List<YARG.Core.Chart.TextEvent>());
    }

    private static GuitarNote MakeNote(
        SyncTrack sync,
        uint tick,
        FiveFretGuitarFret fret,
        params FiveFretGuitarFret[] children)
    {
        double time = sync.TickToTime(tick);
        var note = new GuitarNote(fret, GuitarNoteType.Strum, GuitarNoteFlags.None, NoteFlags.None,
            time, 0, tick, 0);
        foreach (var childFret in children)
        {
            note.AddChildNote(new GuitarNote(childFret, GuitarNoteType.Strum, GuitarNoteFlags.None,
                NoteFlags.None, time, 0, tick, 0));
        }

        return note;
    }

    private static SyncTrack MakeSyncTrack()
    {
        return new SyncTrack(
            RESOLUTION,
            new List<TempoChange> { new(120, 0, 0) },
            new List<TimeSignatureChange>(),
            new List<Beatline>());
    }

    private static MidiFile MakeExpertMidi()
    {
        var syncEvents = new List<(long Tick, MidiEvent Event)>
        {
            (0, new SequenceTrackNameEvent("TEMPO")),
            (0, new SetTempoEvent(500000)),
        };
        var guitarEvents = new List<(long Tick, MidiEvent Event)>
        {
            (0, new SequenceTrackNameEvent("PART GUITAR")),
        };

        int[] frets = { 96, 97, 98, 99 };
        for (int i = 0; i < frets.Length; i++)
        {
            long tick = i * 120;
            guitarEvents.Add((tick, new NoteOnEvent
            {
                NoteNumber = (SevenBitNumber) frets[i],
                Velocity = (SevenBitNumber) 100,
            }));
            guitarEvents.Add((tick + 30, new NoteOffEvent
            {
                NoteNumber = (SevenBitNumber) frets[i],
                Velocity = (SevenBitNumber) 0,
            }));
        }

        return new MidiFile(MakeTrack(syncEvents), MakeTrack(guitarEvents))
        {
            TimeDivision = new TicksPerQuarterNoteTimeDivision((short) RESOLUTION),
        };
    }

    private static TrackChunk MakeTrack(List<(long Tick, MidiEvent Event)> events)
    {
        var ordered = events
            .OrderBy(item => item.Tick)
            .ThenBy(item => item.Event is NoteOffEvent ? 0 : item.Event is NoteOnEvent ? 2 : 1)
            .ToList();
        long previousTick = 0;
        foreach (var item in ordered)
        {
            item.Event.DeltaTime = item.Tick - previousTick;
            previousTick = item.Tick;
        }

        return new TrackChunk(ordered.Select(item => item.Event));
    }

    private static string GetTrackName(TrackChunk track)
    {
        return track.Events.OfType<SequenceTrackNameEvent>().FirstOrDefault()?.Text ?? "";
    }
}
