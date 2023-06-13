using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using MoonscraperChartEditor.Song;
using MoonscraperChartEditor.Song.IO;
using MoonscraperEngine;
using NUnit.Framework;

namespace YARG.Core.UnitTests.Parsing
{
    using static MoonSong;
    using static MoonChart;
    using static MoonNote;
    using static MidIOHelper;
    using static ParseBehaviorTests;

    using MidiEventList = List<(long absoluteTick, MidiEvent midiEvent)>;

    public class MidiParseBehaviorTests
    {
        private const uint SUSTAIN_CUTOFF_THRESHOLD = RESOLUTION / 3;

        private static readonly Dictionary<MoonInstrument, string> InstrumentToNameLookup = new()
        {
            { MoonInstrument.Guitar,       GUITAR_TRACK },
            { MoonInstrument.GuitarCoop,   GUITAR_COOP_TRACK },
            { MoonInstrument.Bass,         BASS_TRACK },
            { MoonInstrument.Rhythm,       RHYTHM_TRACK },
            { MoonInstrument.Keys,         KEYS_TRACK },
            { MoonInstrument.Drums,        DRUMS_TRACK },

            { MoonInstrument.GHLiveGuitar, GHL_GUITAR_TRACK },
            { MoonInstrument.GHLiveBass,   GHL_BASS_TRACK },
            { MoonInstrument.GHLiveRhythm, GHL_RHYTHM_TRACK },
            { MoonInstrument.GHLiveCoop,   GHL_GUITAR_COOP_TRACK },

            { MoonInstrument.ProGuitar_17Fret, PRO_GUITAR_17_FRET_TRACK },
            { MoonInstrument.ProGuitar_22Fret, PRO_GUITAR_22_FRET_TRACK },
            { MoonInstrument.ProBass_17Fret,   PRO_BASS_17_FRET_TRACK },
            { MoonInstrument.ProBass_22Fret,   PRO_BASS_22_FRET_TRACK },
        };

        private static readonly Dictionary<int, int> GuitarNoteOffsetLookup = new()
        {
            { (int)GuitarFret.Open,   -1 },
            { (int)GuitarFret.Green,  0 },
            { (int)GuitarFret.Red,    1 },
            { (int)GuitarFret.Yellow, 2 },
            { (int)GuitarFret.Blue,   3 },
            { (int)GuitarFret.Orange, 4 },
        };

        private static readonly Dictionary<MoonNoteType, int> GuitarForceOffsetLookup = new()
        {
            { MoonNoteType.Hopo,  5 },
            { MoonNoteType.Strum, 6 },
        };

        private static readonly Dictionary<int, int> GhlGuitarNoteOffsetLookup = new()
        {
            { (int)GHLiveGuitarFret.Open,   0 },
            { (int)GHLiveGuitarFret.Black1, 4 },
            { (int)GHLiveGuitarFret.Black2, 5 },
            { (int)GHLiveGuitarFret.Black3, 6 },
            { (int)GHLiveGuitarFret.White1, 1 },
            { (int)GHLiveGuitarFret.White2, 2 },
            { (int)GHLiveGuitarFret.White3, 3 },
        };

        private static readonly Dictionary<MoonNoteType, int> GhlGuitarForceOffsetLookup = new()
        {
            { MoonNoteType.Hopo,  7 },
            { MoonNoteType.Strum, 8 },
        };

        private static readonly Dictionary<int, int> ProGuitarNoteOffsetLookup = new()
        {
            { (int)ProGuitarString.Red,    0 },
            { (int)ProGuitarString.Green,  1 },
            { (int)ProGuitarString.Orange, 2 },
            { (int)ProGuitarString.Blue,   3 },
            { (int)ProGuitarString.Yellow, 4 },
            { (int)ProGuitarString.Purple, 5 },
        };

        private static readonly Dictionary<MoonNoteType, int> ProGuitarForceOffsetLookup = new()
        {
            { MoonNoteType.Hopo,  6 },
        };

        private static readonly Dictionary<Flags, byte> ProGuitarChannelFlagLookup =
            PRO_GUITAR_CHANNEL_FLAG_LOOKUP.ToDictionary((pair) => pair.Value, (pair) => pair.Key);

        private static readonly Dictionary<int, int> DrumsNoteOffsetLookup = new()
        {
            { (int)DrumPad.Kick,   0 },
            { (int)DrumPad.Red,    1 },
            { (int)DrumPad.Yellow, 2 },
            { (int)DrumPad.Blue,   3 },
            { (int)DrumPad.Orange, 4 },
            { (int)DrumPad.Green,  5 },
        };

        private static readonly Dictionary<GameMode, Dictionary<int, int>> InstrumentNoteOffsetLookup = new()
        {
            { GameMode.Guitar,    GuitarNoteOffsetLookup },
            { GameMode.Drums,     DrumsNoteOffsetLookup },
            { GameMode.GHLGuitar, GhlGuitarNoteOffsetLookup },
            { GameMode.ProGuitar, ProGuitarNoteOffsetLookup },
        };

        private static readonly Dictionary<GameMode, Dictionary<MoonNoteType, int>> InstrumentForceOffsetLookup = new()
        {
            { GameMode.Guitar,    GuitarForceOffsetLookup },
            { GameMode.Drums,     new() },
            { GameMode.GHLGuitar, GhlGuitarForceOffsetLookup },
            { GameMode.ProGuitar, ProGuitarForceOffsetLookup },
        };

        private static readonly Dictionary<GameMode, Dictionary<Flags, byte>> InstrumentChannelFlagLookup = new()
        {
            { GameMode.Guitar,    new() },
            { GameMode.Drums,     new() },
            { GameMode.GHLGuitar, new() },
            { GameMode.ProGuitar, ProGuitarChannelFlagLookup },
        };

        private static readonly Dictionary<GameMode, Dictionary<Difficulty, int>> InstrumentDifficultyStartLookup = new()
        {
            { GameMode.Guitar,    GUITAR_DIFF_START_LOOKUP },
            { GameMode.Drums,     DRUMS_DIFF_START_LOOKUP },
            { GameMode.GHLGuitar, GHL_GUITAR_DIFF_START_LOOKUP },
            { GameMode.ProGuitar, PRO_GUITAR_DIFF_START_LOOKUP },
        };

        // Because SevenBitNumber andFourBitNumber have no implicit operators for taking in bytes
        private static SevenBitNumber S(byte number) => (SevenBitNumber)number;
        private static FourBitNumber F(byte number) => (FourBitNumber)number;

        private static TrackChunk GenerateSyncChunk(MoonSong sourceSong)
        {
            var timedEvents = new MidiEventList();
            foreach (var sync in sourceSong.syncTrack)
            {
                switch (sync)
                {
                    case BPM bpm:
                        // MIDI stores tempo as microseconds per quarter note, so we need to convert
                        // Moonscraper already ties BPM to quarter notes, so no additional conversion is needed
                        double secondsPerBeat = 60 / bpm.displayValue;
                        double microseconds = secondsPerBeat * 1000 * 1000;
                        timedEvents.Add((sync.tick, new SetTempoEvent((long)microseconds)));
                        break;
                    case TimeSignature ts:
                        timedEvents.Add((sync.tick, new TimeSignatureEvent((byte)ts.numerator, (byte)ts.denominator)));
                        break;
                }
            }

            return FinalizeTrackChunk("TEMPO_TRACK", timedEvents);
        }

        private static TrackChunk GenerateEventsChunk(MoonSong sourceSong)
        {
            var timedEvents = new MidiEventList();
            foreach (var text in sourceSong.eventsAndSections)
            {
                timedEvents.Add((text.tick, new TextEvent(text.title)));
            }

            return FinalizeTrackChunk(EVENTS_TRACK, timedEvents);
        }

        private static TrackChunk GenerateTrackChunk(MoonSong sourceSong, MoonInstrument instrument)
        {
            var gameMode = MoonSong.InstumentToChartGameMode(instrument);
            var timedEvents = new MidiEventList();

            // Text event flags to enable extended features
            if (gameMode == GameMode.Drums)
                timedEvents.Add((0, new TextEvent(CHART_DYNAMICS_TEXT_BRACKET)));
            else if (gameMode == GameMode.Guitar)
                timedEvents.Add((0, new TextEvent(ENHANCED_OPENS_TEXT_BRACKET)));

            long lastNoteTick = 0;
            foreach (var difficulty in EnumX<Difficulty>.Values)
            {
                var chart = sourceSong.GetChart(instrument, difficulty);
                foreach (var chartObj in chart.chartObjects)
                {
                    switch (chartObj)
                    {
                        case MoonNote note:
                            GenerateNote(timedEvents, note, gameMode, difficulty, ref lastNoteTick);
                            break;
                    }
                }
            }

            // Write events to new track
            string instrumentName = InstrumentToNameLookup[instrument];
            return FinalizeTrackChunk(instrumentName, timedEvents);
        }

        private static void GenerateNote(MidiEventList events, MoonNote note, GameMode gameMode, Difficulty difficulty,
            ref long lastNoteTick)
        {
            // Apply sustain cutoffs
            if (note.length < (SUSTAIN_CUTOFF_THRESHOLD))
                note.length = 0;

            // Write notes
            long startTick = note.tick;
            long endTick = startTick + Math.Max(note.length, 1);
            long lastNoteDelta = startTick - lastNoteTick;
            GenerateNotesForDifficulty<NoteOnEvent>(events, gameMode, difficulty, note, startTick, VELOCITY, lastNoteDelta);
            GenerateNotesForDifficulty<NoteOffEvent>(events, gameMode, difficulty, note, endTick, 0, lastNoteDelta);

            // Keep track of last note tick for HOPO marking
            lastNoteTick = startTick;
        }

        private static void GenerateNotesForDifficulty<TNoteEvent>(MidiEventList events, GameMode gameMode, Difficulty difficulty,
            MoonNote note, long noteTick, byte velocity, long lastStartDelta)
            where TNoteEvent : NoteEvent, new()
        {
            // This code is somewhat hacky and makes a lot of assumptions, but it does the job

            // Whether or not certain note flags can be placed
            // 5/6-fret guitar
            bool canForceStrum = gameMode is not GameMode.Drums or GameMode.ProGuitar;
            bool canForceHopo = gameMode is not GameMode.Drums;
            bool canTap = gameMode is GameMode.Guitar or GameMode.GHLGuitar && difficulty == Difficulty.Expert; // Tap marker is all-difficulty
            // Drums
            bool canTom = gameMode is GameMode.Drums && difficulty == Difficulty.Expert; // Tom markers are all-difficulty
            bool canDoubleKick = gameMode is GameMode.Drums;
            bool canDynamics = gameMode is GameMode.Drums;

            // Note start + offsets
            int difficultyStart = InstrumentDifficultyStartLookup[gameMode][difficulty];
            var noteOffsetLookup = InstrumentNoteOffsetLookup[gameMode];
            var forceOffsetLookup = InstrumentForceOffsetLookup[gameMode];
            var channelFlagLookup = InstrumentChannelFlagLookup[gameMode];

            // Note properties
            var flags = note.flags;
            int rawNote = gameMode switch {
                GameMode.Guitar => (int)note.guitarFret,
                GameMode.GHLGuitar => (int)note.ghliveGuitarFret,
                GameMode.ProGuitar => (int)note.proGuitarString,
                GameMode.Drums => (int)note.drumPad,
                _ => note.rawNote
            };

            // Note number
            byte noteNumber = (byte)(difficultyStart + noteOffsetLookup[rawNote]);
            if (canDoubleKick && rawNote == (int)DrumPad.Kick && (flags & Flags.DoubleKick) != 0)
                noteNumber--;

            // Drum dynamics
            if (canDynamics && velocity > 0)
            {
                if ((flags & Flags.ProDrums_Accent) != 0)
                    velocity = VELOCITY_ACCENT;
                else if ((flags & Flags.ProDrums_Ghost) != 0)
                    velocity = VELOCITY_GHOST;
            }

            // Pro Guitar fret number
            if (gameMode is GameMode.ProGuitar && velocity > 0)
                velocity = (byte)(100 + note.proGuitarFret);

            // Pro Guitar channel flags
            if (!channelFlagLookup.TryGetValue(flags, out byte channel))
                channel = 0;

            // Main note
            var midiNote = new TNoteEvent()
            {
                NoteNumber = S(noteNumber),
                Velocity = S(velocity),
                DeltaTime = noteTick,
                Channel = F(channel)
            };
            events.Add((noteTick, midiNote));

            // Note flags
            if ((canForceStrum || canForceHopo) && (flags & Flags.Forced) != 0)
            {
                MoonNoteType type;
                if (canForceHopo && lastStartDelta is >= HOPO_THRESHOLD and > 0) 
                    type = MoonNoteType.Hopo;
                else
                    type = MoonNoteType.Strum;

                byte forceNote = (byte)(difficultyStart + forceOffsetLookup[type]);
                midiNote = new TNoteEvent() { NoteNumber = S(forceNote), Velocity = S(velocity) };
                events.Add((noteTick, midiNote));
            }
            if (canTap && (flags & Flags.Tap) != 0)
            {
                midiNote = new TNoteEvent() { NoteNumber = S(TAP_NOTE_CH), Velocity = S(velocity) };
                events.Add((noteTick, midiNote));
            }
            if (canTom && PAD_TO_CYMBAL_LOOKUP.TryGetValue((DrumPad)rawNote, out int padNote) &&
                (flags & Flags.ProDrums_Cymbal) == 0)
            {
                midiNote = new TNoteEvent() { NoteNumber = S((byte)padNote), Velocity = S(velocity) };
                events.Add((noteTick, midiNote));
            }
        }

        private static TrackChunk FinalizeTrackChunk(string trackName, MidiEventList events)
        {
            // Sort events by time
            events.Sort((ev1, ev2) => {
                if (ev1.absoluteTick > ev2.absoluteTick)
                    return 1;
                else if (ev1.absoluteTick < ev2.absoluteTick)
                    return -1;

                return 0;
            });

            // Calculate delta time
            long previousTick = 0;
            foreach (var (tick, midi) in events)
            {
                long delta = tick - previousTick;
                midi.DeltaTime = delta;
                previousTick = tick;
            }

            // Write events to new track
            // Track name is written here to ensure it is the first event
            var chunk = new TrackChunk(new SequenceTrackNameEvent(trackName));
            chunk.Events.AddRange(events.Select((ev) => ev.midiEvent));
            return chunk;
        }

        private static MidiFile GenerateMidi(MoonSong sourceSong)
        {
            var midi = new MidiFile(
                GenerateSyncChunk(sourceSong),
                GenerateEventsChunk(sourceSong),
                GenerateTrackChunk(sourceSong, MoonInstrument.Guitar),
                GenerateTrackChunk(sourceSong, MoonInstrument.GHLiveGuitar),
                GenerateTrackChunk(sourceSong, MoonInstrument.ProGuitar_22Fret),
                GenerateTrackChunk(sourceSong, MoonInstrument.Drums))
            {
                TimeDivision = new TicksPerQuarterNoteTimeDivision((short)sourceSong.resolution)
            };

            return midi;
        }

        [TestCase]
        public void GenerateAndParseMidiFile()
        {
            var sourceSong = GenerateSong();
            var midi = GenerateMidi(sourceSong);
            MoonSong parsedSong;
            try
            {
                parsedSong = MidReader.ReadMidi(midi);
            }
            catch (Exception ex)
            {
                Assert.Fail($"Chart parsing threw an exception!\n{ex}");
                return;
            }

            Assert.Multiple(() =>
            {
                VerifyMetadata(sourceSong, parsedSong);
                VerifySync(sourceSong, parsedSong);
                foreach (var difficulty in EnumX<Difficulty>.Values)
                {
                    VerifyTrack(sourceSong, parsedSong, MoonInstrument.Guitar, difficulty);
                    VerifyTrack(sourceSong, parsedSong, MoonInstrument.GHLiveGuitar, difficulty);
                    VerifyTrack(sourceSong, parsedSong, MoonInstrument.ProGuitar_22Fret, difficulty);
                    VerifyTrack(sourceSong, parsedSong, MoonInstrument.Drums, difficulty);
                }
            });
        }
    }
}