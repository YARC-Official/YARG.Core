using MoonscraperChartEditor.Song;
using MoonscraperChartEditor.Song.IO;
using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.Parsing;

using static MoonscraperChartEditor.Song.MoonNote;
using static YARG.Core.UnitTests.Parsing.MoonNoteAssertions;
using MoonDifficulty = MoonscraperChartEditor.Song.MoonSong.Difficulty;
using MoonInstrument = MoonscraperChartEditor.Song.MoonSong.MoonInstrument;
using YargDifficulty = YARG.Core.Difficulty;

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
        /// <summary>
        /// Helper to load chart from text with _inclusiveSoloBoundary set correctly for .chart format
        /// </summary>
        private static InstrumentTrack<GuitarNote> LoadFiveFretGuitarTrack(string chartText)
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
            return songChart.GetFiveFretTrack(Instrument.FiveFretGuitar);
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

        [Test]
        public void SoloBoundaries_NoteAtSoloEndTick_HasSoloFlag()
        {
            // Solo from tick 500 to 1000 (inclusive end)
            // Note at tick 1000 should have Solo flag
            // IMPORTANT: In .chart format, all entries must be in tick-ascending order
            var chartText = ChartText.Chart(
                ChartText.SongSection(),
                ChartText.SyncSection(),
                ChartText.Section("ExpertSingle",
                    "400 = N 0 0",
                    $"500 = E \"{TextEvents.SOLO_START}\"",
                    "600 = N 1 0",
                    "800 = N 2 0",
                    $"1000 = E \"{TextEvents.SOLO_END}\"",
                    "1000 = N 3 0",
                    "1200 = N 4 0"));

            var track = LoadFiveFretGuitarTrack(chartText);
            var difficulty = track.GetDifficulty(YargDifficulty.Expert);

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
            var chartText = ChartText.Chart(
                ChartText.SongSection(),
                ChartText.SyncSection(),
                ChartText.Section("ExpertSingle",
                    $"500 = E \"{TextEvents.SOLO_START}\"",
                    $"1000 = E \"{TextEvents.SOLO_END}\"",
                    "1000 = N 0 0",
                    "1001 = N 1 0",
                    "1100 = N 2 0"));
            var track = LoadFiveFretGuitarTrack(chartText);

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
            var chartText = ChartText.Chart(
                ChartText.SongSection(),
                ChartText.SyncSection(),
                ChartText.Section("ExpertSingle",
                    "480 = N 0 0",
                    $"500 = E \"{TextEvents.SOLO_START}\"",
                    "600 = N 1 0",
                    "600 = N 2 0",
                    "600 = N 3 0",
                    "800 = N 2 0",
                    $"1000 = E \"{TextEvents.SOLO_END}\"",
                    "1000 = N 0 0",
                    "1000 = N 1 0",
                    "1000 = N 2 0",
                    "1000 = N 3 0",
                    "1000 = N 4 0",
                    "1020 = N 0 0",
                    "1020 = N 1 0"));
            var track = LoadFiveFretGuitarTrack(chartText);

            // Note before solo - no solo flag
            var noteBefore = FindNoteAtTick(track, 480);
            AssertDoesNotHaveSolo(noteBefore, "Note before solo should not have Solo flag");

            // Chord inside solo - all notes have solo flag
            var chordInside = FindChordAtTick(track, 600);
            Assert.That(chordInside, Has.Count.EqualTo(3), "Chord inside solo should have 3 notes");
            foreach (var note in chordInside)
            {
                AssertHasSolo(note, "All notes in chord inside solo should have Solo flag");
            }

            // Another note inside solo
            var noteInside = FindNoteAtTick(track, 800);
            AssertHasSolo(noteInside, "Note at 800 (inside solo) should have Solo flag");

            // Chord at exact solo end tick - ALL notes should have solo flag (inclusive)
            var chordAtEnd = FindChordAtTick(track, 1000);
            Assert.That(chordAtEnd, Has.Count.EqualTo(5), "Chord at solo end should have 5 notes");
            foreach (var note in chordAtEnd)
            {
                AssertHasSolo(note, "All notes in chord at solo end tick should have Solo flag (inclusive boundary)");
            }

            // Chord after solo - no solo flags
            var chordAfter = FindChordAtTick(track, 1020);
            Assert.That(chordAfter, Has.Count.EqualTo(2), "Chord after solo should have 2 notes");
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
            var chartText = ChartText.Chart(
                ChartText.SongSection(),
                ChartText.SyncSection(),
                ChartText.Section("ExpertSingle",
                    $"500 = E \"{TextEvents.SOLO_START}\"",
                    "600 = N 0 0",
                    $"1000 = E \"{TextEvents.SOLO_END}\"",
                    $"1000 = E \"{TextEvents.SOLO_START}\"",
                    "1000 = N 1 0",
                    "1100 = N 2 0",
                    $"1500 = E \"{TextEvents.SOLO_END}\""));
            var track = LoadFiveFretGuitarTrack(chartText);

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
            var chartText = ChartText.Chart(
                ChartText.SongSection(),
                ChartText.SyncSection(),
                ChartText.Section("ExpertSingle",
                    "480 = E \"test\"",
                    $"500 = E \"{TextEvents.SOLO_START}\"",
                    $"500 = E \"{TextEvents.SOLO_END}\"",
                    "500 = N 0 0",
                    "520 = N 1 0"));
            var track = LoadFiveFretGuitarTrack(chartText);

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
            var difficulty = track.GetDifficulty(YargDifficulty.Expert);
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
            var difficulty = track.GetDifficulty(YargDifficulty.Expert);
            foreach (var note in difficulty.Notes)
            {
                if (note.Tick == tick)
                {
                    var chord = new List<GuitarNote>
                    {
                        note
                    };
                    chord.AddRange(note.ChildNotes);
                    return chord;
                }
            }

            return new List<GuitarNote>();
        }


        [Test]
        public void ReadFromText_ThrowsWhenSongSectionIsMissing()
        {
            var chartText = ChartText.Chart(
                ChartText.Section(ChartIOHelper.SECTION_SYNC_TRACK, "0 = B 120000"));

            Assert.Throws<InvalidDataException>(() => ChartReader.ReadFromText(chartText));
        }

        [Test]
        public void ReadFromText_ThrowsWhenRequiredSectionsAreOutOfOrder()
        {
            var chartText = ChartText.Chart(
                ChartText.Section(ChartIOHelper.SECTION_SYNC_TRACK, "0 = B 120000"),
                ChartText.Section(ChartIOHelper.SECTION_SONG, "Resolution = 192"));

            Assert.Throws<InvalidDataException>(() => ChartReader.ReadFromText(chartText));
        }

        [Test]
        public void ReadFromText_ParsesSongAndSyncSections()
        {
            var chartText = ChartText.Chart(
                ChartText.SongSection(resolution: 480),
                ChartText.Section(ChartIOHelper.SECTION_SYNC_TRACK,
                    "0 = B 150000",
                    "240 = TS 7 3"));

            var song = ChartReader.ReadFromText(chartText);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(song.resolution, Is.EqualTo(480));
                Assert.That(song.hopoThreshold, Is.EqualTo(162));
                Assert.That(song.syncTrack.Tempos, Has.Count.EqualTo(1));
                Assert.That(song.syncTrack.Tempos[0].BeatsPerMinute, Is.EqualTo(150));
                Assert.That(song.syncTrack.TimeSignatures, Has.Count.EqualTo(2));
                Assert.That(song.syncTrack.TimeSignatures[^1].Tick, Is.EqualTo(240));
                Assert.That(song.syncTrack.TimeSignatures[^1].Numerator, Is.EqualTo(7));
                Assert.That(song.syncTrack.TimeSignatures[^1].Denominator, Is.EqualTo(8));
            }
        }

        [Test]
        public void ReadFromText_ParsesGlobalSectionsAndEvents()
        {
            var chartText = ChartText.Chart(
                ChartText.SongSection(),
                ChartText.SyncSection(),
                ChartText.Section(ChartIOHelper.SECTION_EVENTS,
                    "0 = E \"section Intro\"",
                    "192 = E \"music_start\""));

            var song = ChartReader.ReadFromText(chartText);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(song.sections, Has.One.Matches<MoonText>(text =>
                    text.tick == 0 && text.text == "Intro"));
                Assert.That(song.events, Has.One.Matches<MoonText>(text =>
                    text.tick == 192 && text.text == "music_start"));
            }
        }

        [Test]
        public void GuitarProcessList_AppliesTapOverForcedAfterNotesAreParsed()
        {
            var chartText = ChartText.Chart(
                ChartText.SongSection(),
                ChartText.SyncSection(),
                ChartText.Section("ExpertSingle",
                    "0 = N 0 192",
                    "0 = N 5 0",
                    "0 = N 6 0"));

            var song = ChartReader.ReadFromText(chartText);
            var notes = song.GetChart(MoonInstrument.Guitar, MoonDifficulty.Expert).notes;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(notes, Has.Count.EqualTo(1));
                Assert.That(notes[0].guitarFret, Is.EqualTo(GuitarFret.Green));
                AssertHasFlag(notes[0], Flags.Tap);
                AssertDoesNotHaveFlag(notes[0], Flags.Forced);
            }
        }

        [Test]
        public void GuitarProcessList_ConvertsSoloTextEventsToInclusiveSoloPhrase()
        {
            var chartText = ChartText.Chart(
                ChartText.SongSection(),
                ChartText.SyncSection(),
                ChartText.Section("ExpertSingle",
                    $"100 = E \"{TextEvents.SOLO_START}\"",
                    $"199 = E \"{TextEvents.SOLO_END}\""));

            var song = ChartReader.ReadFromText(chartText);
            var chart = song.GetChart(MoonInstrument.Guitar, MoonDifficulty.Expert);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(chart.events, Is.Empty);
                Assert.That(chart.specialPhrases, Has.One.Matches<MoonPhrase>(phrase =>
                    phrase is { tick: 100, length: 99, type: MoonPhrase.Type.Solo }));
            }
        }

        [Test]
        public void GuitarProcessList_SplitsSameTickSoloEndAndStartWithoutOverlap()
        {
            var chartText = ChartText.Chart(
                ChartText.SongSection(),
                ChartText.SyncSection(),
                ChartText.Section("ExpertSingle",
                    $"100 = E \"{TextEvents.SOLO_START}\"",
                    $"200 = E \"{TextEvents.SOLO_START}\"",
                    $"200 = E \"{TextEvents.SOLO_END}\"",
                    $"299 = E \"{TextEvents.SOLO_END}\""));

            var song = ChartReader.ReadFromText(chartText);
            var phrases = song.GetChart(MoonInstrument.Guitar, MoonDifficulty.Expert).specialPhrases;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(phrases, Has.Count.EqualTo(2));
                Assert.That(phrases, Has.One.Matches<MoonPhrase>(phrase =>
                    phrase is { tick: 100, length: 100, type: MoonPhrase.Type.Solo }));
                Assert.That(phrases, Has.One.Matches<MoonPhrase>(phrase =>
                    phrase is { tick: 200, length: 99, type: MoonPhrase.Type.Solo }));
            }
        }

        [Test]
        public void GuitarProcessList_ConvertsZeroLengthSoloTextEventsToSoloPhrase()
        {
            var chartText = ChartText.Chart(
                ChartText.SongSection(),
                ChartText.SyncSection(),
                ChartText.Section("ExpertSingle",
                    "480 = E \"test\"",
                    $"500 = E \"{TextEvents.SOLO_START}\"",
                    $"500 = E \"{TextEvents.SOLO_END}\""));

            var song = ChartReader.ReadFromText(chartText);
            var chart = song.GetChart(MoonInstrument.Guitar, MoonDifficulty.Expert);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(chart.events, Has.One.Matches<MoonText>(text =>
                    text is { tick: 480, text: "test" }));
                Assert.That(chart.specialPhrases, Has.One.Matches<MoonPhrase>(phrase =>
                    phrase is { tick: 500, length: 0, type: MoonPhrase.Type.Solo }));
            }
        }

        [Test]
        public void DrumsProcessList_AppliesCymbalAndDynamicsAfterNotesAreParsed()
        {
            var chartText = ChartText.Chart(
                ChartText.SongSection(),
                ChartText.SyncSection(),
                ChartText.Section("ExpertDrums",
                    "0 = N 2 192",
                    $"0 = N {ChartIOHelper.NOTE_OFFSET_PRO_DRUMS + 2} 0",
                    $"0 = N {ChartIOHelper.NOTE_OFFSET_DRUMS_ACCENT + 2} 0",
                    $"0 = N {ChartIOHelper.NOTE_OFFSET_DRUMS_GHOST + 2} 0"));

            var song = ChartReader.ReadFromText(chartText);
            var notes = song.GetChart(MoonInstrument.Drums, MoonDifficulty.Expert).notes;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(notes, Has.Count.EqualTo(1));
                Assert.That(notes[0].drumPad, Is.EqualTo(DrumPad.Yellow));
                AssertHasFlag(notes[0], Flags.ProDrums_Cymbal);
                AssertHasFlag(notes[0], Flags.ProDrums_Accent);
                AssertDoesNotHaveFlag(notes[0], Flags.ProDrums_Ghost);
            }
        }

        [Test]
        public void DrumsProcessList_DisambiguatesUnknownDrumsTypeFromCymbalMarkers()
        {
            var settings = ParseSettings.Default_Chart;
            var chartText = ChartText.Chart(
                ChartText.SongSection(),
                ChartText.SyncSection(),
                ChartText.Section("ExpertDrums",
                    "0 = N 2 192",
                    $"0 = N {ChartIOHelper.NOTE_OFFSET_PRO_DRUMS + 2} 0"));

            ChartReader.ReadFromText(ref settings, chartText);

            Assert.That(settings.DrumsType, Is.EqualTo(DrumsType.FourLane));
        }

        [Test]
        public void DrumsProcessList_DisambiguatesUnknownDrumsTypeFromFiveLaneGreen()
        {
            var settings = ParseSettings.Default_Chart;
            var chartText = ChartText.Chart(
                ChartText.SongSection(),
                ChartText.SyncSection(),
                ChartText.Section("ExpertDrums", "0 = N 5 192"));

            ChartReader.ReadFromText(ref settings, chartText);

            Assert.That(settings.DrumsType, Is.EqualTo(DrumsType.FiveLane));
        }

        [Test]
        public void ReadFromText_AppliesSustainCutoffThreshold()
        {
            var settings = ParseSettings.Default_Chart;
            settings.SustainCutoffThreshold = 10;
            var chartText = ChartText.Chart(
                ChartText.SongSection(),
                ChartText.SyncSection(),
                ChartText.Section("ExpertSingle", "0 = N 0 9", "100 = N 1 10"));

            var song = ChartReader.ReadFromText(ref settings, chartText);
            var notes = song.GetChart(MoonInstrument.Guitar, MoonDifficulty.Expert).notes;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(notes, Has.Count.EqualTo(2));
                Assert.That(notes.Single(note => note.tick == 0).length, Is.EqualTo(0));
                Assert.That(notes.Single(note => note.tick == 100).length, Is.EqualTo(10));
            }
        }

        [Test]
        public void GhlProcessList_ParsesGhlSpecificNoteMapAndTapFlag()
        {
            var chartText = ChartText.Chart(
                ChartText.SongSection(),
                ChartText.SyncSection(),
                ChartText.Section("ExpertGHLGuitar",
                    "0 = N 8 192",
                    "0 = N 6 0"));

            var song = ChartReader.ReadFromText(chartText);
            var notes = song.GetChart(MoonInstrument.GHLiveGuitar, MoonDifficulty.Expert).notes;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(notes, Has.Count.EqualTo(1));
                Assert.That(notes[0].ghliveGuitarFret, Is.EqualTo(GHLiveGuitarFret.Black3));
                AssertHasFlag(notes[0], Flags.Tap);
            }
        }
    }
}
