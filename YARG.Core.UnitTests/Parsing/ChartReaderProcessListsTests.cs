using MoonscraperChartEditor.Song;
using MoonscraperChartEditor.Song.IO;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using YARG.Core.Chart;
using YARG.Core.Song;

namespace YARG.Core.UnitTests.Parsing
{
    /// <summary>
    /// Tests for solo boundary edge cases in .chart parsing.
    /// These tests verify that solo phrases apply correctly with inclusive boundaries
    /// for the .chart format, and handle edge cases like chord snapping and back-to-back solos.
    /// </summary>
    [TestFixture]
    public class ChartReaderProcessListsTests
    {
        private const uint RESOLUTION = 192;
        private const float TEMPO = 120f;

        /// <summary>
        /// Helper to load chart from text with _inclusiveSoloBoundary set correctly for .chart format
        /// </summary>
        private static (SongChart songChart, InstrumentTrack<GuitarNote> track) LoadChartForSoloTest(string chartText)
        {
            var settings = new ParseSettings
            {
                DrumsType = DrumsType.Unknown,
                HopoThreshold = ParseSettings.SETTING_DEFAULT,
                SustainCutoffThreshold = 0,
                ChordHopoCancellation = false,
                StarPowerNote = ParseSettings.SETTING_DEFAULT,
                NoteSnapThreshold = 0,
                TuningOffsetCents = 0,
            };

            var songChart = SongChart.FromDotChart(settings, chartText.AsSpan());
            var track = songChart.GetFiveFretTrack(Instrument.FiveFretGuitar);
            return (songChart, track);
        }

        /// <summary>
        /// Asserts that a note has the Solo flag set
        /// </summary>
        private static void AssertHasSolo(GuitarNote note, string message)
        {
            Assert.That(note.IsSolo, Is.True, message);
        }

        /// <summary>
        /// Asserts that a note does NOT have the Solo flag set
        /// </summary>
        private static void AssertDoesNotHaveSolo(GuitarNote note, string message)
        {
            Assert.That(note.IsSolo, Is.False, message);
        }

        /// <summary>
        /// Standard chart header for test charts
        /// </summary>
        private static string StandardChartHeader()
        {
            return @"
[Song]
{
  Name = ""Solo Boundary Test""
  Artist = ""YARG Test Suite""
  Charter = ""Claude""
  Offset = 0
  Resolution = 192
  Player2 = bass
  Difficulty = 0
  PreviewStart = 0
  PreviewEnd = 0
  Genre = ""test""
  MediaType = ""test""
}

[SyncTrack]
{
  0 = TS 4
  0 = B 120000
}

[Events]
{
}
";
        }

        [Test]
        public void SoloBoundaries_NoteAtSoloEndTick_HasSoloFlag()
        {
            // Solo from tick 500 to 1000 (inclusive end)
            // Note at tick 1000 should have Solo flag
            // IMPORTANT: In .chart format, all entries must be in tick-ascending order
            var chartText = StandardChartHeader() + @"
[ExpertSingle]
{
  400 = N 0 0
  500 = E ""solo""
  600 = N 1 0
  800 = N 2 0
  1000 = E ""soloend""
  1000 = N 3 0
  1200 = N 4 0
}
";

            var (_, track) = LoadChartForSoloTest(chartText);
            var difficulty = track.GetDifficulty(Difficulty.Expert);

            Console.WriteLine($"Total notes: {difficulty.Notes.Count}");
            foreach (var note in difficulty.Notes)
            {
                Console.WriteLine($"  Note at tick {note.Tick}: fret={note.Fret}, solo={note.IsSolo}");
            }

            // Note before solo - no solo flag
            var noteBefore = FindNoteAtTick(track, 400);
            AssertDoesNotHaveSolo(noteBefore, "Note before solo should not have Solo flag");

            // Note inside solo - has solo flag
            var noteInside1 = FindNoteAtTick(track, 600);
            AssertHasSolo(noteInside1, "Note at 600 (inside solo) should have Solo flag");

            var noteInside2 = FindNoteAtTick(track, 800);
            AssertHasSolo(noteInside2, "Note at 800 (inside solo) should have Solo flag");

            // Note at exact solo end tick - SHOULD have solo flag (inclusive)
            var noteAtEnd = FindNoteAtTick(track, 1000);
            AssertHasSolo(noteAtEnd, "Note at exact solo end tick should have Solo flag (inclusive boundary)");

            // Note after solo - no solo flag
            var noteAfter = FindNoteAtTick(track, 1200);
            AssertDoesNotHaveSolo(noteAfter, "Note after solo should not have Solo flag");
        }

        [Test]
        public void SoloBoundaries_NoteJustAfterSoloEnd_HasNoSoloFlag()
        {
            // Solo from tick 500 to 1000 (inclusive end)
            // Note at tick 1001 should NOT have Solo flag
            // IMPORTANT: All entries must be in tick-ascending order
            var chartText = StandardChartHeader() + @"
[ExpertSingle]
{
  500 = E ""solo""
  1000 = E ""soloend""
  1000 = N 0 0
  1001 = N 1 0
  1100 = N 2 0
}
";
            var (_, track) = LoadChartForSoloTest(chartText);
            var difficulty = track.GetDifficulty(Difficulty.Expert);

            // Debug: check phrases
            Console.WriteLine($"Phrases: {difficulty.Phrases.Count}");
            foreach (var phrase in difficulty.Phrases)
            {
                Console.WriteLine($"  Phrase: {phrase.Type} at {phrase.Tick} length {phrase.TickLength}");
            }

            // Note at exact solo end tick - has solo flag
            var noteAtEnd = FindNoteAtTick(track, 1000);
            Console.WriteLine($"Note at 1000: IsSolo={noteAtEnd.IsSolo}");
            AssertHasSolo(noteAtEnd, "Note at exact solo end tick should have Solo flag");

            // Note one tick after solo end - should NOT have solo flag
            var noteAfter = FindNoteAtTick(track, 1001);
            AssertDoesNotHaveSolo(noteAfter, "Note one tick after solo end should NOT have Solo flag");

            // Note well after solo - no solo flag
            var noteWellAfter = FindNoteAtTick(track, 1100);
            AssertDoesNotHaveSolo(noteWellAfter, "Note well after solo should not have Solo flag");
        }

        [Test]
        public void SoloBoundaries_ChordAtSoloEnd_AllNotesInSolo()
        {
            // Chord at solo end tick - all notes should have Solo flag
            // IMPORTANT: All entries must be in tick-ascending order
            var chartText = StandardChartHeader() + @"
[ExpertSingle]
{
  480 = N 0 0
  500 = E ""solo""
  600 = N 1 0
  600 = N 2 0
  600 = N 3 0
  800 = N 2 0
  1000 = E ""soloend""
  1000 = N 0 0
  1000 = N 1 0
  1000 = N 2 0
  1000 = N 3 0
  1000 = N 4 0
  1020 = N 0 0
  1020 = N 1 0
}
";
            var (_, track) = LoadChartForSoloTest(chartText);

            // Note before solo - no solo flag
            var noteBefore = FindNoteAtTick(track, 480);
            AssertDoesNotHaveSolo(noteBefore, "Note before solo should not have Solo flag");

            // Chord inside solo - all notes have solo flag
            var chordInside = FindChordAtTick(track, 600);
            Assert.That(chordInside.Count, Is.EqualTo(3), "Chord inside solo should have 3 notes");
            foreach (var note in chordInside)
            {
                AssertHasSolo(note, "All notes in chord inside solo should have Solo flag");
            }

            // Another note inside solo
            var noteInside = FindNoteAtTick(track, 800);
            AssertHasSolo(noteInside, "Note at 800 (inside solo) should have Solo flag");

            // Chord at exact solo end tick - ALL notes should have solo flag (inclusive)
            var chordAtEnd = FindChordAtTick(track, 1000);
            Assert.That(chordAtEnd.Count, Is.EqualTo(5), "Chord at solo end should have 5 notes");
            foreach (var note in chordAtEnd)
            {
                AssertHasSolo(note, "All notes in chord at solo end tick should have Solo flag (inclusive boundary)");
            }

            // Chord after solo - no solo flags
            var chordAfter = FindChordAtTick(track, 1020);
            Assert.That(chordAfter.Count, Is.EqualTo(2), "Chord after solo should have 2 notes");
            foreach (var note in chordAfter)
            {
                AssertDoesNotHaveSolo(note, "All notes in chord after solo should NOT have Solo flag");
            }
        }

        [Test]
        public void SoloBoundaries_BackToBackSolos_NoteAtBoundary_InSecondSolo()
        {
            // Two consecutive solos sharing a boundary
            // Note at boundary should be in SECOND solo only
            // IMPORTANT: All entries must be in tick-ascending order
            var chartText = StandardChartHeader() + @"
[ExpertSingle]
{
  500 = E ""solo""
  600 = N 0 0
  1000 = E ""soloend""
  1000 = E ""solo""
  1000 = N 1 0
  1100 = N 2 0
  1500 = E ""soloend""
}
";
            var (_, track) = LoadChartForSoloTest(chartText);

            // Note in first solo - has solo flag
            var noteFirst = FindNoteAtTick(track, 600);
            AssertHasSolo(noteFirst, "Note in first solo should have Solo flag");

            // Note at shared boundary tick 1000
            // Should have solo flag (it's in the second solo)
            var noteAtBoundary = FindNoteAtTick(track, 1000);
            AssertHasSolo(noteAtBoundary, "Note at shared boundary tick should have Solo flag (in second solo)");

            // Note in second solo - has solo flag
            var noteSecond = FindNoteAtTick(track, 1100);
            AssertHasSolo(noteSecond, "Note in second solo should have Solo flag");
        }

        [Test]
        public void SoloBoundaries_ZeroLengthSolo_NoteAtTick_HasSoloFlag()
        {
            // Zero-length solo with note at same tick
            // IMPORTANT: All entries must be in tick-ascending order
            var chartText = StandardChartHeader() + @"
[ExpertSingle]
{
  480 = E ""test""
  500 = E ""solo""
  500 = E ""soloend""
  500 = N 0 0
  520 = N 1 0
}
";
            var (_, track) = LoadChartForSoloTest(chartText);

            // Note at same tick as zero-length solo - should have solo flag
            var noteAt = FindNoteAtTick(track, 500);
            AssertHasSolo(noteAt, "Note at same tick as zero-length solo should have Solo flag");

            // Note after solo - no solo flag
            var noteAfter = FindNoteAtTick(track, 520);
            AssertDoesNotHaveSolo(noteAfter, "Note after zero-length solo should not have Solo flag");
        }

        /// <summary>
        /// Helper method to find a note at a specific tick
        /// </summary>
        private static GuitarNote FindNoteAtTick(InstrumentTrack<GuitarNote> track, uint tick)
        {
            var difficulty = track.GetDifficulty(Difficulty.Expert);
            foreach (var note in difficulty.Notes)
            {
                if (note.Tick == tick)
                {
                    return note;
                }
            }
            Assert.Fail($"No note found at tick {tick}");
            return null!;
        }

        /// <summary>
        /// Helper method to find all notes in a chord at a specific tick
        /// </summary>
        private static List<GuitarNote> FindChordAtTick(InstrumentTrack<GuitarNote> track, uint tick)
        {
            var difficulty = track.GetDifficulty(Difficulty.Expert);
            foreach (var note in difficulty.Notes)
            {
                if (note.Tick == tick)
                {
                    var chord = new List<GuitarNote> { note };
                    chord.AddRange(note.ChildNotes);
                    return chord;
                }
            }
            return new List<GuitarNote>();
        }
    }
}
