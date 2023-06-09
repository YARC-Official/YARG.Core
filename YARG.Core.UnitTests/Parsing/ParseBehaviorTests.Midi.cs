using System.Text;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using MoonscraperChartEditor.Song;
using MoonscraperChartEditor.Song.IO;
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
        };

        private static readonly Dictionary<GameMode, Dictionary<MoonNoteType, int>> InstrumentForceOffsetLookup = new()
        {
            { GameMode.Guitar,    GuitarForceOffsetLookup },
            { GameMode.Drums,     new() },
            { GameMode.GHLGuitar, GhlGuitarForceOffsetLookup },
        };

        private static readonly Dictionary<GameMode, Dictionary<Difficulty, int>> InstrumentDifficultyStartLookup = new()
        {
            { GameMode.Guitar,    GUITAR_DIFF_START_LOOKUP },
            { GameMode.Drums,     DRUMS_DIFF_START_LOOKUP },
            { GameMode.GHLGuitar, GHL_GUITAR_DIFF_START_LOOKUP },
        };

        private static SevenBitNumber S(byte number) => (SevenBitNumber)number;

        private static TrackChunk GenerateTrackChunk(List<MoonNote> data, MoonInstrument instrument)
        {
            // This code is hacky and makes a lot of assumptions, but it does the job

            string instrumentName = InstrumentToNameLookup[instrument];
            var gameMode = MoonSong.InstumentToChartGameMode(instrument);
            var chunk = new TrackChunk(new SequenceTrackNameEvent(instrumentName));

            bool canForce = gameMode is GameMode.Guitar or GameMode.GHLGuitar;
            bool canTap = gameMode is GameMode.Guitar or GameMode.GHLGuitar;
            bool canCymbal = gameMode is GameMode.Drums;
            bool canDoubleKick = gameMode is GameMode.Drums;
            bool canDynamics = gameMode is GameMode.Drums;

            if (gameMode == GameMode.Drums)
                chunk.Events.Add(new TextEvent(CHART_DYNAMICS_TEXT_BRACKET));
            else if (gameMode == GameMode.Guitar)
                chunk.Events.Add(new TextEvent(ENHANCED_OPENS_TEXT_BRACKET));

            int difficultyStart = InstrumentDifficultyStartLookup[gameMode][Difficulty.Expert];
            var noteOffsetLookup = InstrumentNoteOffsetLookup[gameMode];
            var forceOffsetLookup = InstrumentForceOffsetLookup[gameMode];

            long deltaTime = 0;
            long lastNoteStartDelta = 0;
            for (int index = 0; index < data.Count; index++)
            {
                var note = data[index];
                var flags = note.flags;

                // Apply sustain cutoffs
                if (note.length < (SUSTAIN_CUTOFF_THRESHOLD))
                    note.length = 0;

                int rawNote = gameMode switch {
                    GameMode.Guitar => (int)note.guitarFret,
                    GameMode.GHLGuitar => (int)note.ghliveGuitarFret,
                    GameMode.Drums => (int)note.drumPad,
                    _ => note.rawNote
                };

                byte noteNumber = (byte)(difficultyStart + noteOffsetLookup[rawNote]);
                if (canDoubleKick && rawNote == (int)DrumPad.Kick && (flags & Flags.DoubleKick) != 0)
                    noteNumber--;

                byte velocity = VELOCITY;
                if (canDynamics)
                {
                    if ((flags & Flags.ProDrums_Accent) != 0)
                        velocity = VELOCITY_ACCENT;
                    else if ((flags & Flags.ProDrums_Ghost) != 0)
                        velocity = VELOCITY_GHOST;
                }

                chunk.Events.Add(new NoteOnEvent(S(noteNumber), S(velocity)) { DeltaTime = deltaTime });
                if (canForce && (flags & Flags.Forced) != 0)
                {
                    byte forceNote;
                    if (lastNoteStartDelta is >= HOPO_THRESHOLD and > 0) 
                        forceNote = (byte)(difficultyStart + forceOffsetLookup[MoonNoteType.Hopo]);
                    else
                        forceNote = (byte)(difficultyStart + forceOffsetLookup[MoonNoteType.Strum]);
                    chunk.Events.Add(new NoteOnEvent(S(forceNote), S(VELOCITY)));
                }
                if (canTap && (flags & Flags.Tap) != 0)
                    chunk.Events.Add(new NoteOnEvent(S(104), S(VELOCITY)));
                if (canCymbal && PAD_TO_CYMBAL_LOOKUP.TryGetValue((DrumPad)rawNote, out int padNote) &&
                    (flags & Flags.ProDrums_Cymbal) == 0)
                    chunk.Events.Add(new NoteOnEvent(S((byte)padNote), S(VELOCITY)));

                long endDelta = Math.Max(note.length, 1);
                chunk.Events.Add(new NoteOffEvent(S(noteNumber), S(0)) { DeltaTime = endDelta });
                if (canForce && (flags & Flags.Forced) != 0)
                {
                    byte forceNote;
                    if (lastNoteStartDelta is >= HOPO_THRESHOLD and > 0) 
                        forceNote = (byte)(difficultyStart + forceOffsetLookup[MoonNoteType.Hopo]);
                    else
                        forceNote = (byte)(difficultyStart + forceOffsetLookup[MoonNoteType.Strum]);
                    chunk.Events.Add(new NoteOffEvent(S(forceNote), S(0)));
                }
                if (canTap && (flags & Flags.Tap) != 0)
                    chunk.Events.Add(new NoteOffEvent(S(104), S(0)));
                if (canCymbal && PAD_TO_CYMBAL_LOOKUP.TryGetValue((DrumPad)rawNote, out padNote) &&
                    (flags & Flags.ProDrums_Cymbal) == 0)
                    chunk.Events.Add(new NoteOffEvent(S((byte)padNote), S(0)));

                deltaTime = RESOLUTION - endDelta;
                lastNoteStartDelta = RESOLUTION;
            }

            return chunk;
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
                VerifyTrack(song, GuitarNotes, MoonInstrument.Guitar, Difficulty.Expert);
                VerifyTrack(song, GhlGuitarNotes, MoonInstrument.GHLiveGuitar, Difficulty.Expert);
                VerifyTrack(song, DrumsNotes, MoonInstrument.Drums, Difficulty.Expert);
            });
        }
    }
}