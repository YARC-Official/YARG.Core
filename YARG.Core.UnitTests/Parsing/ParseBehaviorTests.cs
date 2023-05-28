using MoonscraperChartEditor.Song;
using NUnit.Framework;

namespace YARG.Core.UnitTests.Parsing
{
    using static MoonSong;
    using static MoonNote;

    public class ParseBehaviorTests
    {
        public struct NoteData
        {
            public int number;
            public int length;
            public Flags flags;

            public NoteData(int number, int length = 0, Flags flags = Flags.None)
            {
                this.number = number;
                this.length = length;
                this.flags = flags;
            }

            public NoteData(GuitarFret fret, int length = 0, Flags flags = Flags.None) : this((int)fret, length, flags) { }
            public NoteData(GHLiveGuitarFret fret, int length = 0, Flags flags = Flags.None) : this((int)fret, length, flags) { }
            public NoteData(DrumPad pad, int length = 0, Flags flags = Flags.None) : this((int)pad, length, flags) { }
        }

        public const uint RESOLUTION = 192;
        public const double TEMPO = 120.0;
        public const int NUMERATOR = 4;
        public const int DENOMINATOR_POW2 = 2;

        public static readonly List<NoteData> GuitarNotes = new()
        {
            new NoteData(GuitarFret.Green),
            new NoteData(GuitarFret.Red),
            new NoteData(GuitarFret.Yellow),
            new NoteData(GuitarFret.Blue),
            new NoteData(GuitarFret.Orange),
            new NoteData(GuitarFret.Open),

            new NoteData(GuitarFret.Green, flags: Flags.Forced),
            new NoteData(GuitarFret.Red, flags: Flags.Forced),
            new NoteData(GuitarFret.Yellow, flags: Flags.Forced),
            new NoteData(GuitarFret.Blue, flags: Flags.Forced),
            new NoteData(GuitarFret.Orange, flags: Flags.Forced),
            new NoteData(GuitarFret.Open, flags: Flags.Forced),

            new NoteData(GuitarFret.Green, flags: Flags.Tap),
            new NoteData(GuitarFret.Red, flags: Flags.Tap),
            new NoteData(GuitarFret.Yellow, flags: Flags.Tap),
            new NoteData(GuitarFret.Blue, flags: Flags.Tap),
            new NoteData(GuitarFret.Orange, flags: Flags.Tap),
        };

        public static readonly List<NoteData> GhlGuitarNotes = new()
        {
            new NoteData(GHLiveGuitarFret.Black1),
            new NoteData(GHLiveGuitarFret.Black2),
            new NoteData(GHLiveGuitarFret.Black3),
            new NoteData(GHLiveGuitarFret.White1),
            new NoteData(GHLiveGuitarFret.White2),
            new NoteData(GHLiveGuitarFret.White3),
            new NoteData(GHLiveGuitarFret.Open),

            new NoteData(GHLiveGuitarFret.Black1, flags: Flags.Forced),
            new NoteData(GHLiveGuitarFret.Black2, flags: Flags.Forced),
            new NoteData(GHLiveGuitarFret.Black3, flags: Flags.Forced),
            new NoteData(GHLiveGuitarFret.White1, flags: Flags.Forced),
            new NoteData(GHLiveGuitarFret.White2, flags: Flags.Forced),
            new NoteData(GHLiveGuitarFret.White3, flags: Flags.Forced),
            new NoteData(GHLiveGuitarFret.Open, flags: Flags.Forced),

            new NoteData(GHLiveGuitarFret.Black1, flags: Flags.Tap),
            new NoteData(GHLiveGuitarFret.Black2, flags: Flags.Tap),
            new NoteData(GHLiveGuitarFret.Black3, flags: Flags.Tap),
            new NoteData(GHLiveGuitarFret.White1, flags: Flags.Tap),
            new NoteData(GHLiveGuitarFret.White2, flags: Flags.Tap),
            new NoteData(GHLiveGuitarFret.White3, flags: Flags.Tap),
        };

        public static readonly List<NoteData> DrumsNotes = new()
        {
            new NoteData(DrumPad.Kick),
            new NoteData(DrumPad.Kick, flags: Flags.DoubleKick),

            new NoteData(DrumPad.Red, length: 16),
            new NoteData(DrumPad.Yellow, length: 16),
            new NoteData(DrumPad.Blue, length: 16),
            new NoteData(DrumPad.Orange, length: 16),
            new NoteData(DrumPad.Green, length: 16),
            new NoteData(DrumPad.Yellow, flags: Flags.ProDrums_Cymbal),
            new NoteData(DrumPad.Blue, flags: Flags.ProDrums_Cymbal),
            new NoteData(DrumPad.Orange, flags: Flags.ProDrums_Cymbal),

            new NoteData(DrumPad.Red, flags: Flags.ProDrums_Accent),
            new NoteData(DrumPad.Yellow, flags: Flags.ProDrums_Accent),
            new NoteData(DrumPad.Blue, flags: Flags.ProDrums_Accent),
            new NoteData(DrumPad.Orange, flags: Flags.ProDrums_Accent),
            new NoteData(DrumPad.Green, flags: Flags.ProDrums_Accent),
            new NoteData(DrumPad.Yellow, flags: Flags.ProDrums_Cymbal | Flags.ProDrums_Accent),
            new NoteData(DrumPad.Blue, flags: Flags.ProDrums_Cymbal | Flags.ProDrums_Accent),
            new NoteData(DrumPad.Orange, flags: Flags.ProDrums_Cymbal | Flags.ProDrums_Accent),

            new NoteData(DrumPad.Red, flags: Flags.ProDrums_Ghost),
            new NoteData(DrumPad.Yellow, flags: Flags.ProDrums_Ghost),
            new NoteData(DrumPad.Blue, flags: Flags.ProDrums_Ghost),
            new NoteData(DrumPad.Orange, flags: Flags.ProDrums_Ghost),
            new NoteData(DrumPad.Green, flags: Flags.ProDrums_Ghost),
            new NoteData(DrumPad.Yellow, flags: Flags.ProDrums_Cymbal | Flags.ProDrums_Ghost),
            new NoteData(DrumPad.Blue, flags: Flags.ProDrums_Cymbal | Flags.ProDrums_Ghost),
            new NoteData(DrumPad.Orange, flags: Flags.ProDrums_Cymbal | Flags.ProDrums_Ghost),
        };

        public static void VerifyMetadata(MoonSong song)
        {
            Assert.Multiple(() =>
            {
                Assert.That(song.resolution, Is.EqualTo((float)RESOLUTION), $"Resolution was not parsed correctly!");
            });
        }

        public static void VerifySync(MoonSong song)
        {
            Assert.Multiple(() =>
            {
                Assert.That(song.bpms, Has.Count.EqualTo(1), $"Incorrect number of BPM events!");
                Assert.That(song.timeSignatures, Has.Count.EqualTo(1), $"Incorrect number of time signatures!");

                if (song.bpms.Count > 0)
                    Assert.That(song.bpms[0].displayValue, Is.InRange(TEMPO - 0.001, TEMPO + 0.001), "Parsed tempo is incorrect!");

                if (song.timeSignatures.Count > 0)
                {
                    Assert.That(song.timeSignatures[0].numerator, Is.EqualTo(NUMERATOR), "Parsed numerator is incorrect!");
                    uint denominator = song.timeSignatures[0].denominator;
                    uint denominator_pow2 = denominator;
                    for (int i = 1; i < DENOMINATOR_POW2; i++)
                        denominator_pow2 /= 2;
                    Assert.That(denominator_pow2, Is.EqualTo(DENOMINATOR_POW2), $"Parsed denominator is incorrect! (Original: {denominator})");
                }
            });
        }

        public static void VerifyTrack(MoonSong song, List<NoteData> data, MoonInstrument instrument, Difficulty difficulty)
        {
            Assert.Multiple(() =>
            {
                bool chartExists = song.DoesChartExist(instrument, difficulty);
                Assert.That(chartExists, Is.True, $"Chart for {difficulty} {instrument} was not parsed!");
                if (!chartExists)
                    return;

                var chart = song.GetChart(instrument, difficulty);
                for (int index = 0; index < data.Count; index++)
                {
                    uint tick = RESOLUTION * (uint)index;
                    var note = data[index];
                    SongObjectHelper.FindObjectsAtPosition(tick, chart.notes, out int start, out int length);
                    Assert.That(start, Is.Not.EqualTo(SongObjectHelper.NOTFOUND), $"Note at position {tick} was not parsed on {difficulty} {instrument}!");
                    Assert.That(length, Is.AtLeast(1), $"Note at position {tick} was not parsed on {difficulty} {instrument}!");
                    Assert.That(length, Is.AtMost(1), $"More than one note was found at position {tick} on {difficulty} {instrument}!");
                    if (start == SongObjectHelper.NOTFOUND || length != 1)
                        continue;

                    var moonNote = chart.notes[start];
                    Assert.That(moonNote.tick, Is.EqualTo(tick), $"Note position does not match! (Note {note.number} on {difficulty} {instrument})");
                    Assert.That(moonNote.rawNote, Is.EqualTo(note.number), $"Raw note does not match! (Tick {tick} on {difficulty} {instrument})");
                    Assert.That(moonNote.length, Is.EqualTo(note.length), $"Note length does not match! (Note {note.number} at {tick} on {difficulty} {instrument})");
                    Assert.That(moonNote.flags, Is.EqualTo(note.flags), $"Note flags do not match! (Note {note.number} at {tick} on {difficulty} {instrument})");
                }
            });
        }
    }
}