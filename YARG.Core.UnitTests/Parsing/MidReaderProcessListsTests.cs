using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using MoonscraperChartEditor.Song;
using MoonscraperChartEditor.Song.IO;
using NUnit.Framework;
using YARG.Core.Chart;

namespace YARG.Core.UnitTests.Parsing;

using static MoonSong;
using static MoonNote;
using static YARG.Core.UnitTests.Parsing.MoonNoteAssertions;
using MidiTextEvent = Melanchall.DryWetMidi.Core.TextEvent;

public class MidReaderProcessListsTests
{
    private const short Resolution = 192;

    private readonly record struct TimedMidiEvent(long Tick, MidiEvent Event);

    private static SevenBitNumber S(int number) => (SevenBitNumber) (byte) number;
    private static FourBitNumber F(int number) => (FourBitNumber) (byte) number;

    [Test]
    public void ReadMidi_ThrowsForMidiWithNoTracks()
    {
        var midi = new MidiFile
        {
            TimeDivision = new TicksPerQuarterNoteTimeDivision(Resolution),
        };

        Assert.Throws<InvalidDataException>(() => MidReader.ReadMidi(midi));
    }

    [Test]
    public void ReadMidi_ThrowsForNonTicksPerQuarterNoteTimeDivision()
    {
        var midi = new MidiFile(MakeSyncTrack())
        {
            TimeDivision = new SmpteTimeDivision(SmpteFormat.Thirty, 80),
        };

        Assert.Throws<InvalidDataException>(() => MidReader.ReadMidi(midi));
    }

    [Test]
    public void ReadMidi_ParsesSyncTrackFromFirstTrack()
    {
        var midi = MakeMidi();

        var song = MidReader.ReadMidi(midi);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(song.resolution, Is.EqualTo(Resolution));
            Assert.That(song.syncTrack.Tempos, Has.Count.EqualTo(1));
            Assert.That(song.syncTrack.Tempos[0].BeatsPerMinute, Is.EqualTo(150).Within(0.001));
            Assert.That(song.syncTrack.TimeSignatures, Has.Count.EqualTo(1));
            Assert.That(song.syncTrack.TimeSignatures[0].Numerator, Is.EqualTo(7));
            Assert.That(song.syncTrack.TimeSignatures[0].Denominator, Is.EqualTo(8));
        }
    }

    [Test]
    public void GuitarOpenNotes_AreIgnoredUntilEnhancedOpensTextEvent()
    {
        var openNote = MidIOHelper.GUITAR_DIFF_START_LOOKUP[Difficulty.Expert] - 1;
        var withoutEnhancedOpens = MidReader.ReadMidi(MakeMidi(
            MakeTrack(MidIOHelper.GUITAR_TRACK, Note(10, 100, openNote))));
        var withEnhancedOpens = MidReader.ReadMidi(MakeMidi(
            MakeTrack(MidIOHelper.GUITAR_TRACK,
                Text(0, $"[{MidIOHelper.ENHANCED_OPENS_TEXT}]"),
                Note(10, 100, openNote))));

        var withoutNotes = withoutEnhancedOpens.GetChart(MoonInstrument.Guitar, Difficulty.Expert).notes;
        var withNotes = withEnhancedOpens.GetChart(MoonInstrument.Guitar, Difficulty.Expert).notes;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(withoutNotes, Is.Empty);
            Assert.That(withNotes, Has.Count.EqualTo(1));
            Assert.That(withNotes[0].guitarFret, Is.EqualTo(GuitarFret.Open));
        }
    }

    [Test]
    public void DrumVelocities_SetAccentAndGhostFlagsAfterChartDynamicsTextEvent()
    {
        var red = MidIOHelper.DRUMS_DIFF_START_LOOKUP[Difficulty.Expert] + 1;
        var blue = MidIOHelper.DRUMS_DIFF_START_LOOKUP[Difficulty.Expert] + 3;
        var midi = MakeMidi(MakeTrack(MidIOHelper.DRUMS_TRACK,
            Text(0, $"[{MidIOHelper.CHART_DYNAMICS_TEXT}]"),
            Note(10, 100, red, velocity: MidIOHelper.VELOCITY_ACCENT),
            Note(110, 200, blue, velocity: MidIOHelper.VELOCITY_GHOST)));

        var song = MidReader.ReadMidi(midi);
        var notes = song.GetChart(MoonInstrument.Drums, Difficulty.Expert).notes;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(notes, Has.Count.EqualTo(2));
            Assert.That(notes[0].drumPad, Is.EqualTo(DrumPad.Red));
            AssertHasFlag(notes[0], Flags.ProDrums_Accent);
            Assert.That(notes[1].drumPad, Is.EqualTo(DrumPad.Blue));
            AssertHasFlag(notes[1], Flags.ProDrums_Cymbal);
            AssertHasFlag(notes[1], Flags.ProDrums_Ghost);
        }
    }

    [Test]
    public void EliteDrumsStrictHatPedalStateTextEvent_SetsStrictHatPedalFlag()
    {
        var hatPedal = MidIOHelper.ELITE_DRUMS_DIFF_START_LOOKUP[Difficulty.Expert] - 2;
        var midi = MakeMidi(MakeTrack(MidIOHelper.ELITE_DRUMS_TRACK,
            Text(0, $"[{MidIOHelper.STRICT_HAT_PEDAL_STATE}]"),
            Note(10, 100, hatPedal)));

        var song = MidReader.ReadMidi(midi);
        var notes = song.GetChart(MoonInstrument.EliteDrums, Difficulty.Expert).notes;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(notes, Has.Count.EqualTo(1));
            Assert.That(notes[0].eliteDrumPad, Is.EqualTo(EliteDrumPad.HatPedal));
            AssertHasFlag(notes[0], Flags.EliteDrums_StrictHatState);
        }
    }

    [Test]
    public void GuitarForcingMarkers_ApplyAfterNotesAreParsed()
    {
        var green = MidIOHelper.GUITAR_DIFF_START_LOOKUP[Difficulty.Expert];
        var forcedStrum = green + 6;
        var tap = MidIOHelper.TAP_NOTE_CH;
        var midi = MakeMidi(MakeTrack(MidIOHelper.GUITAR_TRACK,
            Note(10, 100, forcedStrum),
            Note(20, 30, tap),
            Note(20, 100, green)));

        var song = MidReader.ReadMidi(midi);
        var notes = song.GetChart(MoonInstrument.Guitar, Difficulty.Expert).notes;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(notes, Has.Count.EqualTo(1));
            AssertHasFlag(notes[0], Flags.Tap);
            AssertDoesNotHaveFlag(notes[0], Flags.Forced);
            AssertDoesNotHaveFlag(notes[0], Flags.Forced_Strum);
        }
    }

    [Test]
    public void CodaPostProcessing_SetsCodaEndAndConvertsDrumFillToBigRockEnding()
    {
        var red = MidIOHelper.DRUMS_DIFF_START_LOOKUP[Difficulty.Expert] + 1;
        var midi = MakeMidi(
            MakeEventsTrack(
                Text(50, $"[{MidIOHelper.CODA_START}]"),
                Text(250, $"[{MidIOHelper.CODA_END}]")),
            MakeTrack(MidIOHelper.DRUMS_TRACK,
                Note(40, 45, red),
                Note(100, 105, red),
                Note(200, 205, red),
                Note(300, 305, red),
                Note(100, 220, MidIOHelper.DRUM_FILL_NOTE_0)));

        var song = MidReader.ReadMidi(midi);
        var chart = song.GetChart(MoonInstrument.Drums, Difficulty.Expert);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(chart.notes, Has.Count.EqualTo(4));
            AssertHasFlag(chart.notes.Single(note => note.tick == 200), Flags.CodaEnd);
            Assert.That(chart.specialPhrases, Has.One.Matches<MoonPhrase>(phrase =>
                phrase.type == MoonPhrase.Type.BigRockEnding && phrase.tick == 100 && phrase.length == 120));
            Assert.That(chart.specialPhrases, Has.None.Matches<MoonPhrase>(phrase =>
                phrase.type == MoonPhrase.Type.ProDrums_Activation));
        }
    }

    [Test]
    public void LegacyStarPowerFixup_ConvertsSoloToStarPowerWhenStarPowerOverrideIsUnset()
    {
        var settings = ParseSettings.Default_Midi;
        settings.StarPowerNote = ParseSettings.SETTING_DEFAULT;
        var midi = MakeMidi(MakeTrack(MidIOHelper.GUITAR_TRACK,
            Note(10, 100, MidIOHelper.SOLO_NOTE)));

        var song = MidReader.ReadMidi(ref settings, midi);
        var phrases = song.GetChart(MoonInstrument.Guitar, Difficulty.Expert).specialPhrases;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(phrases, Has.One.Matches<MoonPhrase>(phrase => phrase.type == MoonPhrase.Type.Starpower));
            Assert.That(phrases, Has.None.Matches<MoonPhrase>(phrase => phrase.type == MoonPhrase.Type.Solo));
        }
    }

    [Test]
    public void ProKeysSplitDifficultyTrack_OnlyPopulatesMatchingDifficulty()
    {
        var key = MidIOHelper.PRO_KEYS_RANGE_START;
        var midi = MakeMidi(MakeTrack(MidIOHelper.PRO_KEYS_HARD,
            Note(10, 100, key),
            Note(110, 200, MidIOHelper.PRO_KEYS_GLISSANDO)));

        var song = MidReader.ReadMidi(midi);
        var expert = song.GetChart(MoonInstrument.ProKeys, Difficulty.Expert);
        var hard = song.GetChart(MoonInstrument.ProKeys, Difficulty.Hard);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(expert.notes, Is.Empty);
            Assert.That(expert.specialPhrases, Is.Empty);
            Assert.That(hard.notes, Has.Count.EqualTo(1));
            Assert.That(hard.notes[0].proKeysKey, Is.EqualTo(0));
            Assert.That(hard.specialPhrases, Has.One.Matches<MoonPhrase>(phrase =>
                phrase.type == MoonPhrase.Type.ProKeys_Glissando && phrase.tick == 110 && phrase.length == 90));
        }
    }

    [Test]
    public void GetCodaRanges_ReturnsBalancedRanges()
    {
        var song = new MoonSong((uint) Resolution);
        song.InsertText(new MoonText(MidIOHelper.CODA_START, 50));
        song.InsertText(new MoonText(MidIOHelper.CODA_END, 100));
        song.InsertText(new MoonText(MidIOHelper.CODA_START, 150));
        song.InsertText(new MoonText(MidIOHelper.CODA_END, 250));

        var ranges = MidReader.GetCodaRanges(song);

        Assert.That(ranges, Is.EqualTo(new List<(uint start, uint end)>
        {
            (50, 100),
            (150, 250),
        }));
    }

    [Test]
    public void GetCodaRanges_ReturnsOpenRangeWhenFinalCodaEndIsMissing()
    {
        var song = new MoonSong((uint) Resolution);
        song.InsertText(new MoonText(MidIOHelper.CODA_START, 50));

        var ranges = MidReader.GetCodaRanges(song);

        Assert.That(ranges, Is.EqualTo(new List<(uint start, uint end)>
        {
            (50, uint.MaxValue),
        }));
    }

    private static MidiFile MakeMidi(params TrackChunk[] tracks)
    {
        var midi = new MidiFile(MakeSyncTrack())
        {
            TimeDivision = new TicksPerQuarterNoteTimeDivision(Resolution),
        };

        foreach (var track in tracks)
        {
            midi.Chunks.Add(track);
        }

        return midi;
    }

    private static TrackChunk MakeSyncTrack()
    {
        return MakeTrack("TEMPO_TRACK",
            new TimedMidiEvent(0, new SetTempoEvent(TempoChange.BpmToMicroSeconds(150))),
            new TimedMidiEvent(0, new TimeSignatureEvent(7, 8)));
    }

    private static TrackChunk MakeEventsTrack(params TimedMidiEvent[] events)
    {
        return MakeTrack(MidIOHelper.EVENTS_TRACK, events);
    }

    private static TimedMidiEvent Text(long tick, string text)
    {
        return new TimedMidiEvent(tick, new MidiTextEvent(text));
    }

    private static TimedMidiEvent[] Note(long startTick, long endTick, int noteNumber,
        int velocity = MidIOHelper.VELOCITY, int channel = 0)
    {
        return new[]
        {
            new TimedMidiEvent(startTick, new NoteOnEvent
            {
                NoteNumber = S(noteNumber),
                Velocity = S(velocity),
                Channel = F(channel),
            }),
            new TimedMidiEvent(endTick, new NoteOffEvent
            {
                NoteNumber = S(noteNumber),
                Velocity = S(0),
                Channel = F(channel),
            }),
        };
    }

    private static TrackChunk MakeTrack(string trackName, params object[] eventItems)
    {
        return MakeTrack(trackName, FlattenEvents(eventItems));
    }

    private static TrackChunk MakeTrack(string trackName, IEnumerable<TimedMidiEvent> events)
    {
        var ordered = events
            .OrderBy(item => item.Tick)
            .ThenBy(item => EventPriority(item.Event))
            .ThenBy(item => item.Event is NoteEvent note ? (int) note.NoteNumber : 0)
            .ToArray();

        long previousTick = 0;
        foreach (var (tick, midiEvent) in ordered)
        {
            midiEvent.DeltaTime = tick - previousTick;
            previousTick = tick;
        }

        var chunk = new TrackChunk(new SequenceTrackNameEvent(trackName));
        chunk.Events.AddRange(ordered.Select(item => item.Event));
        return chunk;
    }

    private static int EventPriority(MidiEvent midiEvent)
    {
        return midiEvent switch
        {
            NoteOffEvent => 0,
            BaseTextEvent => 1,
            NoteOnEvent => 2,
            _ => 1,
        };
    }

    private static IEnumerable<TimedMidiEvent> FlattenEvents(IEnumerable<object> eventItems)
    {
        foreach (var item in eventItems)
        {
            switch (item)
            {
                case TimedMidiEvent timedEvent:
                    yield return timedEvent;
                    break;
                case IEnumerable<TimedMidiEvent> eventGroup:
                    foreach (var timedEvent in eventGroup)
                    {
                        yield return timedEvent;
                    }
                    break;
                default:
                    throw new ArgumentException($"Unsupported MIDI test event item: {item.GetType()}");
            }
        }
    }
}
