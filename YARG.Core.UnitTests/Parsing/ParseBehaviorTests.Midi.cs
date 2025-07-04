using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using MoonscraperChartEditor.Song;
using MoonscraperChartEditor.Song.IO;
using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.Extensions;
using YARG.Core.Logging;
using YARG.Core.Parsing;

namespace YARG.Core.UnitTests.Parsing
{
    using static MoonSong;
    using static MoonChart;
    using static MoonNote;
    using static MidIOHelper;
    using static ParseBehaviorTests;

    using MidiEventList = List<(long absoluteTick, MidiEvent midiEvent)>;
    using MidiTextEvent = Melanchall.DryWetMidi.Core.TextEvent;

    public class MidiParseBehaviorTests
    {
        private const uint SUSTAIN_CUTOFF_THRESHOLD = RESOLUTION / 3;
        private const uint HOPO_THRESHOLD = (RESOLUTION / 3) + 1;

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

            { MoonInstrument.Vocals,   VOCALS_TRACK },
            { MoonInstrument.Harmony1, HARMONY_1_TRACK },
            { MoonInstrument.Harmony2, HARMONY_2_TRACK },
            { MoonInstrument.Harmony3, HARMONY_3_TRACK },
        };

#pragma warning disable IDE0230 // Use UTF-8 string literal

        #region Guitar
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

        private static readonly Dictionary<MoonPhrase.Type, byte[]> GuitarSpecialPhraseLookup = new()
        {
            { MoonPhrase.Type.Starpower,      new[] { STARPOWER_NOTE } },
            { MoonPhrase.Type.Solo,           new[] { SOLO_NOTE } },
            { MoonPhrase.Type.Versus_Player1, new[] { VERSUS_PHRASE_PLAYER_1 } },
            { MoonPhrase.Type.Versus_Player2, new[] { VERSUS_PHRASE_PLAYER_2 } },
            { MoonPhrase.Type.TremoloLane,    new[] { TREMOLO_LANE_NOTE } },
            { MoonPhrase.Type.TrillLane,      new[] { TRILL_LANE_NOTE } },
        };
        #endregion

        #region GhlGuitar
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

        private static readonly Dictionary<MoonPhrase.Type, byte[]> GhlGuitarSpecialPhraseLookup = new()
        {
            { MoonPhrase.Type.Starpower, new[] { STARPOWER_NOTE } },
            { MoonPhrase.Type.Solo,      new[] { SOLO_NOTE } },
        };
        #endregion

        #region ProGuitar
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

        private static readonly Dictionary<MoonPhrase.Type, byte[]> ProGuitarSpecialPhraseLookup = new()
        {
            { MoonPhrase.Type.Starpower,   new[] { STARPOWER_NOTE } },
            { MoonPhrase.Type.Solo,        new[] { SOLO_NOTE_PRO_GUITAR } },
            { MoonPhrase.Type.TremoloLane, new[] { TREMOLO_LANE_NOTE } },
            { MoonPhrase.Type.TrillLane,   new[] { TRILL_LANE_NOTE } },
        };
        #endregion

        #region Drums
        private static readonly Dictionary<int, int> DrumsNoteOffsetLookup = new()
        {
            { (int)DrumPad.Kick,   0 },
            { (int)DrumPad.Red,    1 },
            { (int)DrumPad.Yellow, 2 },
            { (int)DrumPad.Blue,   3 },
            { (int)DrumPad.Orange, 4 },
            { (int)DrumPad.Green,  5 },
        };

        private static readonly Dictionary<MoonPhrase.Type, byte[]> DrumsSpecialPhraseLookup = new()
        {
            { MoonPhrase.Type.Starpower,           new[] { STARPOWER_NOTE } },
            { MoonPhrase.Type.Solo,                new[] { SOLO_NOTE } },
            { MoonPhrase.Type.Versus_Player1,      new[] { VERSUS_PHRASE_PLAYER_1 } },
            { MoonPhrase.Type.Versus_Player2,      new[] { VERSUS_PHRASE_PLAYER_2 } },
            { MoonPhrase.Type.TremoloLane,         new[] { TREMOLO_LANE_NOTE } },
            { MoonPhrase.Type.TrillLane,           new[] { TRILL_LANE_NOTE } },
            { MoonPhrase.Type.ProDrums_Activation, new[] { DRUM_FILL_NOTE_0, DRUM_FILL_NOTE_1, DRUM_FILL_NOTE_2, DRUM_FILL_NOTE_3, DRUM_FILL_NOTE_4 } },
        };
        #endregion

        #region Vocals
        private static readonly Dictionary<int, int> VocalsNoteOffsetLookup = BuildVocalsNoteLookup();

        private static Dictionary<int, int> BuildVocalsNoteLookup()
        {
            var lookup = new Dictionary<int, int>()
            {
                { 0,   PERCUSSION_NOTE},
            };

            for (int i = VOCALS_RANGE_START; i <= VOCALS_RANGE_END; i++)
            {
                lookup.Add(i, i);
            }

            return lookup;
        }

        private static readonly Dictionary<Difficulty, int> VocalsDifficultyStartOffsetLookup = new()
        {
            { Difficulty.Expert, 0 },
            { Difficulty.Hard,   0 },
            { Difficulty.Medium, 0 },
            { Difficulty.Easy,   0 },
        };

        private static readonly Dictionary<MoonPhrase.Type, byte[]> VocalsSpecialPhraseLookup = new()
        {
            { MoonPhrase.Type.Starpower,      new[] { STARPOWER_NOTE } },
            { MoonPhrase.Type.Versus_Player1, new[] { LYRICS_PHRASE_1 } },
            { MoonPhrase.Type.Versus_Player2, new[] { LYRICS_PHRASE_2 } },
        };
        #endregion

        #region ProKeys
        private static readonly Dictionary<Difficulty, string> ProKeysTrackNameLookup = new()
        {
            { Difficulty.Expert, PRO_KEYS_EXPERT },
            { Difficulty.Hard,   PRO_KEYS_HARD },
            { Difficulty.Medium, PRO_KEYS_MEDIUM },
            { Difficulty.Easy,   PRO_KEYS_EASY },
        };

        private static readonly Dictionary<int, int> ProKeysNoteOffsetLookup
            = Enumerable.Range(0, 25).ToDictionary((i) => i, (i) => i);

        private static readonly Dictionary<Difficulty, int> ProKeysDifficultyStartOffsetLookup = new()
        {
            { Difficulty.Expert, 48 },
            { Difficulty.Hard,   48 },
            { Difficulty.Medium, 48 },
            { Difficulty.Easy,   48 },
        };

        private static readonly Dictionary<MoonPhrase.Type, byte[]> ProKeysSpecialPhraseLookup = new()
        {
            { MoonPhrase.Type.Starpower,     new[] { STARPOWER_NOTE } },
            { MoonPhrase.Type.Solo,          new[] { SOLO_NOTE_PRO_KEYS } },
            { MoonPhrase.Type.TrillLane,     new[] { TRILL_LANE_NOTE } },

            { MoonPhrase.Type.ProKeys_RangeShift0,   new[] { PRO_KEYS_SHIFT_0 } },
            { MoonPhrase.Type.ProKeys_RangeShift1,   new[] { PRO_KEYS_SHIFT_1 } },
            { MoonPhrase.Type.ProKeys_RangeShift2,   new[] { PRO_KEYS_SHIFT_2 } },
            { MoonPhrase.Type.ProKeys_RangeShift3,   new[] { PRO_KEYS_SHIFT_3 } },
            { MoonPhrase.Type.ProKeys_RangeShift4,   new[] { PRO_KEYS_SHIFT_4 } },
            { MoonPhrase.Type.ProKeys_RangeShift5,   new[] { PRO_KEYS_SHIFT_5 } },
            { MoonPhrase.Type.ProKeys_Glissando,     new[] { PRO_KEYS_GLISSANDO } },
        };
        #endregion

        #region Lookups
        private static readonly HashSet<MoonInstrument> SplitDifficultyTrackLookup = new()
        {
            MoonInstrument.ProKeys
        };

        private static readonly Dictionary<MoonInstrument, Dictionary<Difficulty, string>> InstrumentDifficultyToNameLookupLookup = new()
        {
            { MoonInstrument.ProKeys, ProKeysTrackNameLookup },
        };

        private static readonly Dictionary<GameMode, Dictionary<int, int>> InstrumentNoteOffsetLookup = new()
        {
            { GameMode.Guitar,    GuitarNoteOffsetLookup },
            { GameMode.Drums,     DrumsNoteOffsetLookup },
            { GameMode.GHLGuitar, GhlGuitarNoteOffsetLookup },
            { GameMode.ProGuitar, ProGuitarNoteOffsetLookup },
            { GameMode.Vocals,    VocalsNoteOffsetLookup },
            { GameMode.ProKeys,   ProKeysNoteOffsetLookup },
        };

        private static readonly Dictionary<GameMode, Dictionary<MoonNoteType, int>> InstrumentForceOffsetLookup = new()
        {
            { GameMode.Guitar,    GuitarForceOffsetLookup },
            { GameMode.Drums,     new() },
            { GameMode.GHLGuitar, GhlGuitarForceOffsetLookup },
            { GameMode.ProGuitar, ProGuitarForceOffsetLookup },
            { GameMode.Vocals,     new() },
            { GameMode.ProKeys,    new() },
        };

        private static readonly Dictionary<GameMode, Dictionary<Flags, byte>> InstrumentChannelFlagLookup = new()
        {
            { GameMode.Guitar,    new() },
            { GameMode.Drums,     new() },
            { GameMode.GHLGuitar, new() },
            { GameMode.ProGuitar, ProGuitarChannelFlagLookup },
            { GameMode.Vocals,     new() },
            { GameMode.ProKeys,    new() },
        };

        private static readonly Dictionary<GameMode, Dictionary<Difficulty, int>> InstrumentDifficultyStartLookup = new()
        {
            { GameMode.Guitar,    GUITAR_DIFF_START_LOOKUP },
            { GameMode.Drums,     DRUMS_DIFF_START_LOOKUP },
            { GameMode.GHLGuitar, GHL_GUITAR_DIFF_START_LOOKUP },
            { GameMode.ProGuitar, PRO_GUITAR_DIFF_START_LOOKUP },
            { GameMode.Vocals,    VocalsDifficultyStartOffsetLookup },
            { GameMode.ProKeys,   ProKeysDifficultyStartOffsetLookup },
        };

        private static readonly Dictionary<GameMode, Dictionary<MoonPhrase.Type, byte[]>> InstrumentSpecialPhraseLookup = new()
        {
            { GameMode.Guitar,    GuitarSpecialPhraseLookup },
            { GameMode.Drums,     DrumsSpecialPhraseLookup },
            { GameMode.GHLGuitar, GhlGuitarSpecialPhraseLookup },
            { GameMode.ProGuitar, ProGuitarSpecialPhraseLookup },
            { GameMode.Vocals,    VocalsSpecialPhraseLookup },
            { GameMode.ProKeys,   ProKeysSpecialPhraseLookup },
        };
        #endregion

#pragma warning restore IDE0230

        // Because SevenBitNumber andFourBitNumber have no implicit operators for taking in bytes
        private static SevenBitNumber S(byte number) => (SevenBitNumber) number;
        private static FourBitNumber F(byte number) => (FourBitNumber) number;

        private static TrackChunk GenerateSyncChunk(MoonSong sourceSong)
        {
            var timedEvents = new MidiEventList();

            foreach (var bpm in sourceSong.syncTrack.Tempos)
            {
                long microseconds = TempoChange.BpmToMicroSeconds(bpm.BeatsPerMinute);
                timedEvents.Add((bpm.Tick, new SetTempoEvent(microseconds)));
            }

            foreach (var ts in sourceSong.syncTrack.TimeSignatures)
            {
                timedEvents.Add((ts.Tick, new TimeSignatureEvent((byte) ts.Numerator, (byte) ts.Denominator)));
            }

            return FinalizeTrackChunk("TEMPO_TRACK", timedEvents);
        }

        private static TrackChunk GenerateEventsChunk(MoonSong sourceSong)
        {
            MidiEventList timedEvents = new();

            foreach (var section in sourceSong.sections)
            {
                timedEvents.Add((section.tick, new MidiTextEvent(section.text)));
            }

            foreach (var ev in sourceSong.events)
            {
                timedEvents.Add((ev.tick, new MidiTextEvent(ev.text)));
            }

            return FinalizeTrackChunk(EVENTS_TRACK, timedEvents);
        }

        private static TrackChunk GenerateTrackChunk(MoonSong sourceSong, MoonInstrument instrument)
        {
            var gameMode = MoonSong.InstrumentToChartGameMode(instrument);
            var timedEvents = new MidiEventList();

            bool singleDifficulty = gameMode is GameMode.Vocals;

            // Text event flags to enable extended features
            if (gameMode == GameMode.Drums)
                timedEvents.Add((0, new MidiTextEvent($"[{CHART_DYNAMICS_TEXT}]")));
            else if (gameMode == GameMode.Guitar)
                timedEvents.Add((0, new MidiTextEvent($"[{ENHANCED_OPENS_TEXT}]")));

            foreach (var difficulty in EnumExtensions<Difficulty>.Values)
            {
                if (singleDifficulty && difficulty != Difficulty.Expert)
                    continue;

                var notesChart = sourceSong.GetChart(instrument, difficulty);

                long lastNoteTick = 0;
                foreach (var note in notesChart.notes)
                {
                    GenerateNote(timedEvents, note, gameMode, difficulty, ref lastNoteTick);
                }

                // Fixup: except for Pro Keys, skip trills/tremolos below Expert
                if (instrument != MoonInstrument.ProKeys && difficulty != Difficulty.Expert)
                {
                    notesChart.specialPhrases.RemoveAll((phrase) =>
                        phrase.type is MoonPhrase.Type.TremoloLane or MoonPhrase.Type.TrillLane);
                }
            }

            // Phrases and text events apply to all difficulties
            // Grab the ones from the Expert chart
            var chart = sourceSong.GetChart(instrument, Difficulty.Expert);
            foreach (var phrase in chart.specialPhrases)
            {
                GenerateSpecialPhrase(timedEvents, phrase, gameMode);
            }

            foreach (var ev in chart.events)
            {
                timedEvents.Add((ev.tick, new MidiTextEvent(ev.text)));
            }

            // Write events to new track
            string instrumentName = InstrumentToNameLookup[instrument];
            return FinalizeTrackChunk(instrumentName, timedEvents);
        }

        private static TrackChunk GenerateTrackChunk(MoonSong sourceSong, MoonInstrument instrument, Difficulty difficulty)
        {
            var gameMode = MoonSong.InstrumentToChartGameMode(instrument);
            var timedEvents = new MidiEventList();

            var chart = sourceSong.GetChart(instrument, difficulty);

            long lastNoteTick = 0;
            foreach (var note in chart.notes)
            {
                GenerateNote(timedEvents, note, gameMode, difficulty, ref lastNoteTick);
            }

            foreach (var phrase in chart.specialPhrases)
            {
                GenerateSpecialPhrase(timedEvents, phrase, gameMode);
            }

            foreach (var ev in chart.events)
            {
                timedEvents.Add((ev.tick, new MidiTextEvent(ev.text)));
            }

            string instrumentName = InstrumentDifficultyToNameLookupLookup[instrument][difficulty];
            return FinalizeTrackChunk(instrumentName, timedEvents);
        }

        private static void GenerateNote(MidiEventList events, MoonNote note, GameMode gameMode, Difficulty difficulty,
            ref long lastNoteTick)
        {
            // Apply sustain cutoffs
            if (note.length < (SUSTAIN_CUTOFF_THRESHOLD) && gameMode != GameMode.Vocals)
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
            int rawNote = gameMode switch
            {
                GameMode.Guitar => (int) note.guitarFret,
                GameMode.GHLGuitar => (int) note.ghliveGuitarFret,
                GameMode.ProGuitar => (int) note.proGuitarString,
                GameMode.Drums => (int) note.drumPad,
                GameMode.Vocals => note.vocalsPitch,
                GameMode.ProKeys => note.proKeysKey,

                _ => throw new NotImplementedException($"Unimplemented game mode {gameMode}!")
            };

            // Note number
            byte noteNumber = (byte) (difficultyStart + noteOffsetLookup[rawNote]);
            if (canDoubleKick && rawNote == (int) DrumPad.Kick && (flags & Flags.DoubleKick) != 0)
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
                velocity = (byte) (100 + note.proGuitarFret);

            // Pro Guitar channel flags
            if (!channelFlagLookup.TryGetValue(flags, out byte channel))
                channel = 0;

            // Vocals percussion note
            if (gameMode is GameMode.Vocals && (flags & Flags.Vocals_Percussion) != 0)
                noteNumber = PERCUSSION_NOTE;

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
                if (canForceHopo && lastStartDelta >= HOPO_THRESHOLD)
                {
                    type = MoonNoteType.Hopo;
                    // Apply additional flag to match the parsed data
                    note.flags |= Flags.Forced_Hopo;
                }
                else
                {
                    type = MoonNoteType.Strum;
                    // Apply additional flag to match the parsed data
                    note.flags |= Flags.Forced_Strum;
                }

                byte forceNote = (byte) (difficultyStart + forceOffsetLookup[type]);
                midiNote = new TNoteEvent() { NoteNumber = S(forceNote), Velocity = S(velocity) };
                events.Add((noteTick, midiNote));
            }
            if (canTap && (flags & Flags.Tap) != 0)
            {
                midiNote = new TNoteEvent() { NoteNumber = S(TAP_NOTE_CH), Velocity = S(velocity) };
                events.Add((noteTick, midiNote));
            }
            if (canTom && PAD_TO_CYMBAL_LOOKUP.TryGetValue((DrumPad) rawNote, out int padNote) &&
                (flags & Flags.ProDrums_Cymbal) == 0)
            {
                midiNote = new TNoteEvent() { NoteNumber = S((byte) padNote), Velocity = S(velocity) };
                events.Add((noteTick, midiNote));
            }
        }

        private static void GenerateSpecialPhrase(MidiEventList events, MoonPhrase phrase, GameMode gameMode)
        {
            // Get note number (ignore if not supported by the game mode)
            if (!InstrumentSpecialPhraseLookup[gameMode].TryGetValue(phrase.type, out byte[]? notesToAdd))
                return;

            // Write notes
            long startTick = phrase.tick;
            long endTick = startTick + Math.Max(phrase.length, 1);
            foreach (byte note in notesToAdd)
            {
                events.Add((startTick, new NoteOnEvent() { NoteNumber = S(note), Velocity = S(VELOCITY) }));
                events.Add((endTick, new NoteOffEvent() { NoteNumber = S(note), Velocity = S(0) }));
            }
        }

        private static TrackChunk FinalizeTrackChunk(string trackName, MidiEventList events)
        {
            // Sort events by time
            events.Sort((left, right) =>
            {
                int compare = left.absoluteTick.CompareTo(right.absoluteTick);
                if (compare != 0)
                    return compare;

                // Determine priority for certain types of events
                return (left.midiEvent, right.midiEvent) switch
                {
                    // Same-type note events should be sorted by note number
                    // Not *entirely* necessary, but without this then
                    // sorting is inconsistent and will throw an exception
                    (NoteOnEvent on1, NoteOnEvent on2) => on1.NoteNumber.CompareTo(on2.NoteNumber),
                    (NoteOffEvent off1, NoteOffEvent off2) => off1.NoteNumber.CompareTo(off2.NoteNumber),
                    // Note on events should come last, and note offs first
                    (NoteOnEvent, _) => 1,
                    (NoteOffEvent, _) => -1,
                    // The ordering of other events doesn't matter
                    _ => 0
                };
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
                GenerateEventsChunk(sourceSong)
            )
            {
                TimeDivision = new TicksPerQuarterNoteTimeDivision((short) sourceSong.resolution)
            };

            foreach (var instrument in EnumExtensions<MoonInstrument>.Values)
            {
                if (SplitDifficultyTrackLookup.Contains(instrument))
                {
                    foreach (var difficulty in EnumExtensions<Difficulty>.Values)
                    {
                        var chunk = GenerateTrackChunk(sourceSong, instrument, difficulty);
                        midi.Chunks.Add(chunk);
                    }
                }
                else
                {
                    var chunk = GenerateTrackChunk(sourceSong, instrument);
                    midi.Chunks.Add(chunk);
                }
            }

            return midi;
        }

        [TestCase]
        public void GenerateAndParseMidiFile()
        {
            YargLogger.AddLogListener(new DebugYargLogListener());

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

            // Apply vocals phrases to source global events
            var vocalsChart = sourceSong.GetChart(MoonInstrument.Vocals, Difficulty.Expert);
            foreach (var phrase in vocalsChart.specialPhrases)
            {
                if (phrase.type == MoonPhrase.Type.Vocals_LyricPhrase)
                {
                    sourceSong.InsertText(new MoonText(TextEvents.LYRIC_PHRASE_START, phrase.tick));
                    sourceSong.InsertText(new MoonText(TextEvents.LYRIC_PHRASE_END, phrase.tick + phrase.length));
                }
            }

            VerifySong(sourceSong, parsedSong, InstrumentNoteOffsetLookup.Keys);
        }
    }
}