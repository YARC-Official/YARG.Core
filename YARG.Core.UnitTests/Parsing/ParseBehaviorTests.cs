using MoonscraperChartEditor.Song;
using NUnit.Framework;

namespace YARG.Core.UnitTests.Parsing
{
    using static MoonSong;
    using static MoonNote;

    public class ParseBehaviorTests
    {
        public const uint RESOLUTION = 192;
        public const double TEMPO = 120.0;
        public const int NUMERATOR = 4;
        public const int DENOMINATOR_POW2 = 2;

        public const uint HOPO_THRESHOLD = (uint)(SongConfig.FORCED_NOTE_TICK_THRESHOLD * RESOLUTION / SongConfig.STANDARD_BEAT_RESOLUTION);

        private static MoonNote NewNote(GuitarFret fret, uint length = 0, Flags flags = Flags.None)
            => new(0, (int)fret, length, flags);
        private static MoonNote NewNote(GHLiveGuitarFret fret, uint length = 0, Flags flags = Flags.None)
            => new(0, (int)fret, length, flags);
        private static MoonNote NewNote(DrumPad pad, uint length = 0, Flags flags = Flags.None)
            => new(0, (int)pad, length, flags);
        private static MoonNote NewNote(ProGuitarString str, int fret, uint length = 0, Flags flags = Flags.None)
            => new(0, MoonNote.MakeProGuitarRawNote(str, fret), length, flags);

        public static readonly List<MoonNote> GuitarNotes = new()
        {
            NewNote(GuitarFret.Green),
            NewNote(GuitarFret.Red),
            NewNote(GuitarFret.Yellow),
            NewNote(GuitarFret.Blue),
            NewNote(GuitarFret.Orange),
            NewNote(GuitarFret.Open),

            NewNote(GuitarFret.Green, flags: Flags.Forced),
            NewNote(GuitarFret.Red, flags: Flags.Forced),
            NewNote(GuitarFret.Yellow, flags: Flags.Forced),
            NewNote(GuitarFret.Blue, flags: Flags.Forced),
            NewNote(GuitarFret.Orange, flags: Flags.Forced),
            NewNote(GuitarFret.Open, flags: Flags.Forced),

            NewNote(GuitarFret.Green, flags: Flags.Tap),
            NewNote(GuitarFret.Red, flags: Flags.Tap),
            NewNote(GuitarFret.Yellow, flags: Flags.Tap),
            NewNote(GuitarFret.Blue, flags: Flags.Tap),
            NewNote(GuitarFret.Orange, flags: Flags.Tap),
        };

        public static readonly List<MoonNote> GhlGuitarNotes = new()
        {
            NewNote(GHLiveGuitarFret.Black1),
            NewNote(GHLiveGuitarFret.Black2),
            NewNote(GHLiveGuitarFret.Black3),
            NewNote(GHLiveGuitarFret.White1),
            NewNote(GHLiveGuitarFret.White2),
            NewNote(GHLiveGuitarFret.White3),
            NewNote(GHLiveGuitarFret.Open),

            NewNote(GHLiveGuitarFret.Black1, flags: Flags.Forced),
            NewNote(GHLiveGuitarFret.Black2, flags: Flags.Forced),
            NewNote(GHLiveGuitarFret.Black3, flags: Flags.Forced),
            NewNote(GHLiveGuitarFret.White1, flags: Flags.Forced),
            NewNote(GHLiveGuitarFret.White2, flags: Flags.Forced),
            NewNote(GHLiveGuitarFret.White3, flags: Flags.Forced),
            NewNote(GHLiveGuitarFret.Open, flags: Flags.Forced),

            NewNote(GHLiveGuitarFret.Black1, flags: Flags.Tap),
            NewNote(GHLiveGuitarFret.Black2, flags: Flags.Tap),
            NewNote(GHLiveGuitarFret.Black3, flags: Flags.Tap),
            NewNote(GHLiveGuitarFret.White1, flags: Flags.Tap),
            NewNote(GHLiveGuitarFret.White2, flags: Flags.Tap),
            NewNote(GHLiveGuitarFret.White3, flags: Flags.Tap),
        };

        public static readonly List<MoonNote> ProGuitarNotes = new()
        {
            NewNote(ProGuitarString.Red, 0),
            NewNote(ProGuitarString.Green, 1),
            NewNote(ProGuitarString.Orange, 2),
            NewNote(ProGuitarString.Blue, 3),
            NewNote(ProGuitarString.Yellow, 4),
            NewNote(ProGuitarString.Purple, 5),

            NewNote(ProGuitarString.Red, 6, flags: Flags.Forced),
            NewNote(ProGuitarString.Green, 7, flags: Flags.Forced),
            NewNote(ProGuitarString.Orange, 8, flags: Flags.Forced),
            NewNote(ProGuitarString.Blue, 9, flags: Flags.Forced),
            NewNote(ProGuitarString.Yellow, 10, flags: Flags.Forced),
            NewNote(ProGuitarString.Purple, 11, flags: Flags.Forced),

            NewNote(ProGuitarString.Red, 12, flags: Flags.ProGuitar_Muted),
            NewNote(ProGuitarString.Green, 13, flags: Flags.ProGuitar_Muted),
            NewNote(ProGuitarString.Orange, 14, flags: Flags.ProGuitar_Muted),
            NewNote(ProGuitarString.Blue, 15, flags: Flags.ProGuitar_Muted),
            NewNote(ProGuitarString.Yellow, 16, flags: Flags.ProGuitar_Muted),
            NewNote(ProGuitarString.Purple, 17, flags: Flags.ProGuitar_Muted),
        };

        public static readonly List<MoonNote> DrumsNotes = new()
        {
            NewNote(DrumPad.Kick),
            NewNote(DrumPad.Kick, flags: Flags.DoubleKick),

            NewNote(DrumPad.Red, length: 16),
            NewNote(DrumPad.Yellow, length: 16),
            NewNote(DrumPad.Blue, length: 16),
            NewNote(DrumPad.Orange, length: 16),
            NewNote(DrumPad.Green, length: 16),
            NewNote(DrumPad.Yellow, flags: Flags.ProDrums_Cymbal),
            NewNote(DrumPad.Blue, flags: Flags.ProDrums_Cymbal),
            NewNote(DrumPad.Orange, flags: Flags.ProDrums_Cymbal),

            NewNote(DrumPad.Red, flags: Flags.ProDrums_Accent),
            NewNote(DrumPad.Yellow, flags: Flags.ProDrums_Accent),
            NewNote(DrumPad.Blue, flags: Flags.ProDrums_Accent),
            NewNote(DrumPad.Orange, flags: Flags.ProDrums_Accent),
            NewNote(DrumPad.Green, flags: Flags.ProDrums_Accent),
            NewNote(DrumPad.Yellow, flags: Flags.ProDrums_Cymbal | Flags.ProDrums_Accent),
            NewNote(DrumPad.Blue, flags: Flags.ProDrums_Cymbal | Flags.ProDrums_Accent),
            NewNote(DrumPad.Orange, flags: Flags.ProDrums_Cymbal | Flags.ProDrums_Accent),

            NewNote(DrumPad.Red, flags: Flags.ProDrums_Ghost),
            NewNote(DrumPad.Yellow, flags: Flags.ProDrums_Ghost),
            NewNote(DrumPad.Blue, flags: Flags.ProDrums_Ghost),
            NewNote(DrumPad.Orange, flags: Flags.ProDrums_Ghost),
            NewNote(DrumPad.Green, flags: Flags.ProDrums_Ghost),
            NewNote(DrumPad.Yellow, flags: Flags.ProDrums_Cymbal | Flags.ProDrums_Ghost),
            NewNote(DrumPad.Blue, flags: Flags.ProDrums_Cymbal | Flags.ProDrums_Ghost),
            NewNote(DrumPad.Orange, flags: Flags.ProDrums_Cymbal | Flags.ProDrums_Ghost),
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

        public static void VerifyTrack(MoonSong song, List<MoonNote> data, MoonInstrument instrument, Difficulty difficulty)
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
                    var originalNote = data[index];
                    SongObjectHelper.FindObjectsAtPosition(tick, chart.notes, out int start, out int length);
                    Assert.That(start, Is.Not.EqualTo(SongObjectHelper.NOTFOUND), $"Note at position {tick} was not parsed on {difficulty} {instrument}!");
                    Assert.That(length, Is.AtLeast(1), $"Note at position {tick} was not parsed on {difficulty} {instrument}!");
                    Assert.That(length, Is.AtMost(1), $"More than one note was found at position {tick} on {difficulty} {instrument}!");
                    if (start == SongObjectHelper.NOTFOUND || length != 1)
                        continue;

                    var parsedNote = chart.notes[start];
                    Assert.That(parsedNote.tick, Is.EqualTo(tick), $"Note position does not match! (Note {originalNote.rawNote} on {difficulty} {instrument})");
                    Assert.That(parsedNote.rawNote, Is.EqualTo(originalNote.rawNote), $"Raw note does not match! (Tick {tick} on {difficulty} {instrument})");
                    Assert.That(parsedNote.length, Is.EqualTo(originalNote.length), $"Note length does not match! (Note {originalNote.rawNote} at {tick} on {difficulty} {instrument})");
                    Assert.That(parsedNote.flags, Is.EqualTo(originalNote.flags), $"Note flags do not match! (Note {originalNote.rawNote} at {tick} on {difficulty} {instrument})");
                }
            });
        }
    }
}