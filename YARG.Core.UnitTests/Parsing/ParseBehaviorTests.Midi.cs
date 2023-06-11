using System.Text;
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

        private static TrackChunk GenerateTrackChunk(List<MoonNote> data, MoonInstrument instrument)
        {
            string instrumentName = InstrumentToNameLookup[instrument];
            var gameMode = MoonSong.InstumentToChartGameMode(instrument);
            var chunk = new TrackChunk(new SequenceTrackNameEvent(instrumentName));

            if (gameMode == GameMode.Drums)
                chunk.Events.Add(new TextEvent(CHART_DYNAMICS_TEXT_BRACKET));
            else if (gameMode == GameMode.Guitar)
                chunk.Events.Add(new TextEvent(ENHANCED_OPENS_TEXT_BRACKET));

            long deltaTime = 0;
            long lastNoteStartDelta = 0;
            foreach (var note in data)
            {
                // Apply sustain cutoffs
                if (note.length < (SUSTAIN_CUTOFF_THRESHOLD))
                    note.length = 0;

                // Note ons
                // *MUST* be generated on all difficulties before note offs! Otherwise notes will be placed incorrectly
                long currentDelta = deltaTime;
                foreach (var difficulty in EnumX<Difficulty>.Values)
                {
                    GenerateNotesForDifficulty<NoteOnEvent>(chunk, gameMode, difficulty, note, currentDelta, VELOCITY, lastNoteStartDelta);
                    currentDelta = 0;
                }

                // Note offs
                long endDelta = Math.Max(note.length, 1);
                currentDelta = endDelta;
                foreach (var difficulty in EnumX<Difficulty>.Values)
                {
                    GenerateNotesForDifficulty<NoteOffEvent>(chunk, gameMode, difficulty, note, currentDelta, 0, lastNoteStartDelta);
                    currentDelta = 0;
                }

                deltaTime = RESOLUTION - endDelta;
                lastNoteStartDelta = RESOLUTION;
            }

            return chunk;
        }

        private static void GenerateNotesForDifficulty<TNoteEvent>(TrackChunk chunk, GameMode gameMode, Difficulty difficulty,
            MoonNote note, long noteDelta, byte velocity, long lastStartDelta)
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
            chunk.Events.Add(new TNoteEvent() { NoteNumber = S(noteNumber), Velocity = S(velocity), DeltaTime = noteDelta, Channel = F(channel) });

            // Note flags
            if ((canForceStrum || canForceHopo) && (flags & Flags.Forced) != 0)
            {
                byte forceNote;
                if (canForceHopo && lastStartDelta is >= HOPO_THRESHOLD and > 0) 
                    forceNote = (byte)(difficultyStart + forceOffsetLookup[MoonNoteType.Hopo]);
                else
                    forceNote = (byte)(difficultyStart + forceOffsetLookup[MoonNoteType.Strum]);
                chunk.Events.Add(new TNoteEvent() { NoteNumber = S(forceNote), Velocity = S(velocity) });
            }
            if (canTap && (flags & Flags.Tap) != 0)
                chunk.Events.Add(new TNoteEvent() { NoteNumber = S(TAP_NOTE_CH), Velocity = S(velocity) });
            if (canTom && PAD_TO_CYMBAL_LOOKUP.TryGetValue((DrumPad)rawNote, out int padNote) &&
                (flags & Flags.ProDrums_Cymbal) == 0)
                chunk.Events.Add(new TNoteEvent() { NoteNumber = S((byte)padNote), Velocity = S(velocity) });
        }

        private static MidiFile GenerateMidi()
        {
            byte denominator = 1;
            for (int i = 0; i < DENOMINATOR_POW2; i++)
                denominator *= 2;

            var sync = new TrackChunk(
                new SequenceTrackNameEvent("TEMPO_TRACK"),
                new SetTempoEvent((long)((60 / TEMPO) * 1000000)),
                new TimeSignatureEvent(NUMERATOR, denominator)
            );
            var midi = new MidiFile(
                sync,
                GenerateTrackChunk(GuitarNotes, MoonInstrument.Guitar),
                GenerateTrackChunk(GhlGuitarNotes, MoonInstrument.GHLiveGuitar),
                GenerateTrackChunk(ProGuitarNotes, MoonInstrument.ProGuitar_22Fret),
                GenerateTrackChunk(DrumsNotes, MoonInstrument.Drums))
            {
                TimeDivision = new TicksPerQuarterNoteTimeDivision((short)RESOLUTION)
            };

            return midi;
        }

        [TestCase]
        public void GenerateAndParseMidiFile()
        {
            var midi = GenerateMidi();
            MoonSong song;
            try
            {
                song = MidReader.ReadMidi(midi);
            }
            catch (Exception ex)
            {
                Assert.Fail($"Chart parsing threw an exception!\n{ex}");
                return;
            }

            Assert.Multiple(() =>
            {
                VerifyMetadata(song);
                VerifySync(song);
                foreach (var difficulty in EnumX<Difficulty>.Values)
                {
                    VerifyTrack(song, GuitarNotes, MoonInstrument.Guitar, difficulty);
                    VerifyTrack(song, GhlGuitarNotes, MoonInstrument.GHLiveGuitar, difficulty);
                    VerifyTrack(song, ProGuitarNotes, MoonInstrument.ProGuitar_22Fret, difficulty);
                    VerifyTrack(song, DrumsNotes, MoonInstrument.Drums, difficulty);
                }
            });
        }
    }
}