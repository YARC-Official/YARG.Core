using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using MoonscraperChartEditor.Song;
using MoonscraperChartEditor.Song.IO;
using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.Parsing;

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
    public void GuitarForcingMarkers_ResolveOverlappingHopoAndStrumByPriority()
    {
        var green = MidIOHelper.GUITAR_DIFF_START_LOOKUP[Difficulty.Expert];
        var forcedHopo = green + 5;
        var forcedStrum = green + 6;
        var midi = MakeMidi(MakeTrack(MidIOHelper.GUITAR_TRACK,
            Note(10, 100, forcedHopo),
            Note(10, 100, forcedStrum),
            Note(20, 30, green)));

        var song = MidReader.ReadMidi(midi);
        var notes = song.GetChart(MoonInstrument.Guitar, Difficulty.Expert).notes;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(notes, Has.Count.EqualTo(1));
            AssertHasFlag(notes[0], Flags.Forced_Hopo);
            AssertDoesNotHaveFlag(notes[0], Flags.Forced_Strum);
        }
    }

    [Test]
    public void SysExTapPhrase_DoesNotIncludeNoteAtExactEndTick()
    {
        var green = MidIOHelper.GUITAR_DIFF_START_LOOKUP[Difficulty.Expert];
        var midi = MakeMidi(MakeTrack(MidIOHelper.GUITAR_TRACK,
            PhaseShiftTap(10, start: true),
            PhaseShiftTap(20, start: false),
            Note(20, 30, green)));

        var song = MidReader.ReadMidi(midi);
        var notes = song.GetChart(MoonInstrument.Guitar, Difficulty.Expert).notes;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(notes, Has.Count.EqualTo(1));
            AssertDoesNotHaveFlag(notes[0], Flags.Tap);
        }
    }

    [Test]
    public void ReadMidi_UsesFirstRecognizedTrackNameWhenMultipleTrackNamesArePresent()
    {
        var green = MidIOHelper.GUITAR_DIFF_START_LOOKUP[Difficulty.Expert];
        var midi = MakeMidi(MakeTrackWithTrackNames(
            $"[{MidIOHelper.ENHANCED_OPENS_TEXT}]",
            MidIOHelper.GUITAR_TRACK,
            Note(10, 100, green)));

        var song = MidReader.ReadMidi(midi);
        var notes = song.GetChart(MoonInstrument.Guitar, Difficulty.Expert).notes;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(notes, Has.Count.EqualTo(1));
            Assert.That(notes[0].guitarFret, Is.EqualTo(GuitarFret.Green));
        }
    }

    [Test]
    public void SongChart_MidiChordToSameFretSingle_IsStrumWithoutLoaderCancellation()
    {
        var red = MidIOHelper.GUITAR_DIFF_START_LOOKUP[Difficulty.Expert] + 1;
        var yellow = MidIOHelper.GUITAR_DIFF_START_LOOKUP[Difficulty.Expert] + 2;
        var settings = ParseSettings.Default_Midi;
        settings.ChordHopoCancellation = false;
        var midi = MakeMidi(MakeTrack(MidIOHelper.GUITAR_TRACK,
            Note(0, 10, red),
            Note(0, 10, yellow),
            Note(64, 100, red)));

        var songChart = SongChart.FromMidi(in settings, midi);
        var notes = songChart.GetFiveFretTrack(Instrument.FiveFretGuitar)
            .GetDifficulty(YARG.Core.Difficulty.Expert).Notes;
        var redAfterChord = notes.Single(note => note.Tick == 64);

        Assert.That(redAfterChord.Type, Is.EqualTo(GuitarNoteType.Strum));
    }

    [Test]
    public void CodaPostProcessing_SetsCodaEndAndConvertsDrumFillToBigRockEnding()
    {
        var red = MidIOHelper.DRUMS_DIFF_START_LOOKUP[Difficulty.Expert] + 1;
        var midi = MakeMidi(
            MakeEventsTrack(
                Text(50, $"[{MidIOHelper.MIDCODA_START}]"),
                Text(250, $"[{MidIOHelper.MIDCODA_END}]")),
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

    [TestCase(MidIOHelper.GUITAR_TRACK, MidIOHelper.GUITAR_TRACK, 10u)]
    [TestCase(MidIOHelper.GH1_GUITAR_TRACK, MidIOHelper.GUITAR_TRACK, 10u)]
    [TestCase(MidIOHelper.DRUMS_TRACK, MidIOHelper.DRUMS_TRACK, 10u)]
    // A track under the instrument's own name replaces a fallback track, and is then the first
    [TestCase(MidIOHelper.DRUMS_REAL_TRACK, MidIOHelper.DRUMS_TRACK, 20u)]
    public void ReadMidi_ReadsOnlyTheFirstTrackOfAnInstrument(string firstName, string laterName, uint expectedTick)
    {
        var instrument = MidIOHelper.TrackNameToInstrumentMap[laterName];
        var red = instrument == MoonInstrument.Drums
            ? MidIOHelper.DRUMS_DIFF_START_LOOKUP[Difficulty.Expert] + 1
            : MidIOHelper.GUITAR_DIFF_START_LOOKUP[Difficulty.Expert] + 1;
        // An Expert red in each track, each on its own tick
        var midi = MakeMidi(
            MakeTrack(firstName, Note(10, 100, red)),
            MakeTrack(laterName, Note(20, 100, red)),
            MakeTrack(laterName, Note(30, 100, red)));

        var song = MidReader.ReadMidi(midi);
        var notes = song.GetChart(instrument, Difficulty.Expert).notes;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(notes, Has.Count.EqualTo(1));
            Assert.That(notes[0].tick, Is.EqualTo(expectedTick));
        }
    }

    [TestCase(MidIOHelper.GUITAR_TRACK)]
    [TestCase(MidIOHelper.BASS_TRACK)]
    public void ReadMidi_ReadsOnlyTheFirstTrackOfAnInstrumentWhenItHasNoNotes(string trackName)
    {
        var green = MidIOHelper.GUITAR_DIFF_START_LOOKUP[Difficulty.Expert];
        var midi = MakeMidi(
            MakeTrack(trackName),
            MakeTrack(trackName, Note(10, 100, green)));

        var song = MidReader.ReadMidi(midi);
        var instrument = MidIOHelper.TrackNameToInstrumentMap[trackName];

        Assert.That(song.GetChart(instrument, Difficulty.Expert).notes, Is.Empty);
    }

    [Test]
    public void ReadMidi_ReadsOnlyTheFirstTrackOfEachVocalPart()
    {
        var pitch = MidIOHelper.VOCALS_RANGE_START;
        var midi = MakeMidi(
            MakeVocalTrack(MidIOHelper.VOCALS_TRACK, pitch, Lyric(10, "first")),
            MakeVocalTrack(MidIOHelper.VOCALS_TRACK, pitch + 1, Lyric(10, "second")),
            // A HARM1 track replaces a PART HARM1 track, and is then the first
            MakeVocalTrack(MidIOHelper.HARMONY_1_TRACK_2, pitch + 2),
            MakeVocalTrack(MidIOHelper.HARMONY_1_TRACK, pitch),
            MakeVocalTrack(MidIOHelper.HARMONY_1_TRACK, pitch + 1));

        var song = MidReader.ReadMidi(midi);
        var vocals = song.GetChart(MoonInstrument.Vocals, Difficulty.Expert).notes;
        var harmony = song.GetChart(MoonInstrument.Harmony1, Difficulty.Expert).notes;
        var lyrics = song.events.Select(ev => ev.text).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(vocals, Has.Count.EqualTo(1));
            Assert.That(vocals[0].vocalsPitch, Is.EqualTo(pitch));
            Assert.That(harmony, Has.Count.EqualTo(1));
            Assert.That(harmony[0].vocalsPitch, Is.EqualTo(pitch));
            Assert.That(lyrics, Has.Member(TextEvents.LYRIC_PREFIX_WITH_SPACE + "first"));
            Assert.That(lyrics, Has.No.Member(TextEvents.LYRIC_PREFIX_WITH_SPACE + "second"));
        }
    }

    [TestCase("part guitar", MidIOHelper.GUITAR_TRACK)]
    [TestCase("Part Guitar GHL", MidIOHelper.GHL_GUITAR_TRACK)]
    [TestCase("Part Drums", MidIOHelper.DRUMS_TRACK)]
    [TestCase("harm1", MidIOHelper.HARMONY_1_TRACK)]
    public void ReadMidi_ReadsTrackNamesInAnyLetterCase(string trackName, string standardName)
    {
        var instrument = MidIOHelper.TrackNameToInstrumentMap[standardName];
        var track = instrument switch
        {
            MoonInstrument.GHLiveGuitar => MakeTrack(trackName, Note(10, 100, MidIOHelper.GHL_GUITAR_DIFF_START_LOOKUP[Difficulty.Expert])),
            MoonInstrument.Drums => MakeTrack(trackName, Note(10, 100, MidIOHelper.DRUMS_DIFF_START_LOOKUP[Difficulty.Expert])),
            MoonInstrument.Harmony1 => MakeVocalTrack(trackName, MidIOHelper.VOCALS_RANGE_START),
            _ => MakeTrack(trackName, Note(10, 100, MidIOHelper.GUITAR_DIFF_START_LOOKUP[Difficulty.Expert])),
        };

        var song = MidReader.ReadMidi(MakeMidi(track));

        Assert.That(song.GetChart(instrument, Difficulty.Expert).notes, Has.Count.EqualTo(1));
    }

    [Test]
    public void ReadMidi_ReadsTheEventsTrackNameInAnyLetterCase()
    {
        var song = MidReader.ReadMidi(MakeMidi(MakeTrack("Events", Text(10, "[section Intro]"))));

        Assert.That(song.sections, Has.Count.EqualTo(1));
    }

    [Test]
    public void GetCodaRanges_ReturnsBalancedRanges()
    {
        var song = new MoonSong((uint) Resolution);
        song.InsertText(new MoonText(MidIOHelper.MIDCODA_START, 50));
        song.InsertText(new MoonText(MidIOHelper.MIDCODA_END, 100));
        song.InsertText(new MoonText(MidIOHelper.MIDCODA_START, 150));
        song.InsertText(new MoonText(MidIOHelper.MIDCODA_END, 250));

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

    private static TimedMidiEvent Lyric(long tick, string text)
    {
        return new TimedMidiEvent(tick, new Melanchall.DryWetMidi.Core.LyricEvent(text));
    }

    private static TrackChunk MakeVocalTrack(string trackName, int pitch, params TimedMidiEvent[] lyrics)
    {
        return MakeTrack(trackName,
            Note(0, 100, MidIOHelper.LYRICS_PHRASE_1),
            Note(10, 90, pitch),
            lyrics);
    }

    private static TimedMidiEvent PhaseShiftTap(long tick, bool start)
    {
        return new TimedMidiEvent(tick, new NormalSysExEvent(new byte[]
        {
            0x50,
            0x53,
            0x00,
            0x00,
            0x03,
            0x04,
            (byte) (start ? 0x01 : 0x00),
            0xF7,
        }));
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

    private static TrackChunk MakeTrackWithTrackNames(string firstTrackName, string secondTrackName, params object[] eventItems)
    {
        var ordered = FlattenEvents(eventItems)
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

        var chunk = new TrackChunk(
            new SequenceTrackNameEvent(firstTrackName),
            new SequenceTrackNameEvent(secondTrackName));
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
