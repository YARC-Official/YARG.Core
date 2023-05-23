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

        private static readonly Dictionary<int, byte> GuitarNoteLookup = new()
        {
            { (int)GuitarFret.Open,   95 },
            { (int)GuitarFret.Green,  96 },
            { (int)GuitarFret.Red,    97 },
            { (int)GuitarFret.Yellow, 98 },
            { (int)GuitarFret.Blue,   99 },
            { (int)GuitarFret.Orange, 100 },
        };

        private static readonly Dictionary<int, byte> GhlGuitarNoteLookup = new()
        {
            { (int)GHLiveGuitarFret.Open,   94 },
            { (int)GHLiveGuitarFret.Black1, 98 },
            { (int)GHLiveGuitarFret.Black2, 99 },
            { (int)GHLiveGuitarFret.Black3, 100 },
            { (int)GHLiveGuitarFret.White1, 95 },
            { (int)GHLiveGuitarFret.White2, 96 },
            { (int)GHLiveGuitarFret.White3, 97 },
        };

        private static readonly Dictionary<int, byte> DrumsNoteLookup = new()
        {
            { (int)DrumPad.Kick,   96 },
            { (int)DrumPad.Red,    97 },
            { (int)DrumPad.Yellow, 98 },
            { (int)DrumPad.Blue,   99 },
            { (int)DrumPad.Orange, 100 },
            { (int)DrumPad.Green,  101 },
        };

        private static readonly Dictionary<GameMode, Dictionary<int, byte>> InstrumentToNoteLookupLookup = new()
        {
            { GameMode.Guitar,    GuitarNoteLookup },
            { GameMode.Drums,     DrumsNoteLookup },
            { GameMode.GHLGuitar, GhlGuitarNoteLookup },
        };

        private static SevenBitNumber S(byte number) => (SevenBitNumber)number;

        private static TrackChunk GenerateTrackChunk(List<NoteData> data, MoonInstrument instrument)
        {
            // This code is hacky and makes a lot of assumptions, but it does the job

            string instrumentName = InstrumentToNameLookup[instrument];
            var gameMode = MoonSong.InstumentToChartGameMode(instrument);
            var chunk = new TrackChunk(new SequenceTrackNameEvent(instrumentName));

            if (gameMode == GameMode.Drums)
                chunk.Events.Add(new TextEvent(CHART_DYNAMICS_TEXT_BRACKET));
            else if (gameMode == GameMode.Guitar)
                chunk.Events.Add(new TextEvent(ENHANCED_OPENS_TEXT_BRACKET));

            long deltaTime = 0;
            var noteLookup = InstrumentToNoteLookupLookup[gameMode];
            for (int index = 0; index < data.Count; index++)
            {
                var note = data[index];
                var flags = note.flags;

                byte noteNumber = noteLookup[note.number];
                // hack: double-kick is one note below kick
                // when done properly, needs to only be done on kick
                if (gameMode == GameMode.Drums && (flags & Flags.DoubleKick) != 0)
                    noteNumber--;

                byte velocity = VELOCITY;
                if (gameMode == GameMode.Drums)
                {
                    if ((flags & Flags.ProDrums_Accent) != 0)
                        velocity = VELOCITY_ACCENT;
                    else if ((flags & Flags.ProDrums_Ghost) != 0)
                        velocity = VELOCITY_GHOST;
                }

                chunk.Events.Add(new NoteOnEvent(S(noteNumber), S(velocity)) { DeltaTime = deltaTime });
                if (gameMode != GameMode.Drums && (flags & Flags.Forced) != 0)
                    // hack: since we're spacing things out based on resolution, notes are always 1 beat apart and
                    // forcing will always turn things into HOPOs
                    // when done properly, need to check if previous note time is less than RESOLUTION / 3
                    chunk.Events.Add(new NoteOnEvent(S(101), S(VELOCITY)));
                if (gameMode != GameMode.Drums && (flags & Flags.Tap) != 0)
                    chunk.Events.Add(new NoteOnEvent(S(104), S(VELOCITY)));
                if (gameMode == GameMode.Drums && PAD_TO_CYMBAL_LOOKUP.TryGetValue((DrumPad)note.number, out int padNote) &&
                    (flags & Flags.ProDrums_Cymbal) == 0)
                    // hack: tom markers are exactly 1 octave above their corresponding Expert notes
                    // when done properly, only on yellow/blue/green
                    chunk.Events.Add(new NoteOnEvent(S((byte)padNote), S(VELOCITY)));

                if (note.length < (RESOLUTION / 3))
                {
                    note.length = 0;
                    data[index] = note;
                }

                long endDelta = Math.Max(note.length, 1);
                chunk.Events.Add(new NoteOffEvent(S(noteNumber), S(0)) { DeltaTime = endDelta });
                if (gameMode != GameMode.Drums && (flags & Flags.Forced) != 0)
                    chunk.Events.Add(new NoteOffEvent(S(101), S(0)));
                if (gameMode != GameMode.Drums && (flags & Flags.Tap) != 0)
                    chunk.Events.Add(new NoteOffEvent(S(104), S(0)));
                if (gameMode == GameMode.Drums && PAD_TO_CYMBAL_LOOKUP.TryGetValue((DrumPad)note.number, out padNote) &&
                    (flags & Flags.ProDrums_Cymbal) == 0)
                    chunk.Events.Add(new NoteOffEvent(S((byte)padNote), S(0)));

                deltaTime = RESOLUTION - endDelta;
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
                VerifyTrack(song, GuitarNotes, MoonInstrument.Guitar, Difficulty.Expert);
                VerifyTrack(song, GhlGuitarNotes, MoonInstrument.GHLiveGuitar, Difficulty.Expert);
                VerifyTrack(song, DrumsNotes, MoonInstrument.Drums, Difficulty.Expert);
            });
        }
    }
}