using MoonscraperChartEditor.Song;
using NUnit.Framework;
using YARG.Core;
using YARG.Core.Chart;

namespace YARG.Core.UnitTests.Parsing
{
    using static MoonSongLoaderTests;

    public class MoonSongLoaderTests_NoteSnapping
    {
        private static List<GuitarNote> LoadExpertGuitar(MoonSong song, long snapThreshold, bool rockBandNoteSnap = false)
        {
            var settings = ParseSettings.Default;
            settings.NoteSnapThreshold = snapThreshold;
            settings.RockBandNoteSnap = rockBandNoteSnap;

            return new MoonSongLoader(song, settings)
                .LoadGuitarTrack(Instrument.FiveFretGuitar)
                .GetDifficulty(Difficulty.Expert).Notes;
        }

        private static MoonSong CreateSongWithNotes(params (uint tick, MoonNote.GuitarFret fret, uint length)[] notes)
        {
            var song = CreateSong();
            song.hopoThreshold = 65;

            var chart = song.GetChart(MoonSong.MoonInstrument.Guitar, MoonSong.Difficulty.Expert);
            foreach (var (tick, fret, length) in notes)
            {
                chart.Insert(new MoonNote(tick, (int) fret, length));
            }

            return song;
        }

        [Test]
        public void NotesLessThanThresholdAfterChord_SnapIntoIt()
        {
            var song = CreateSongWithNotes(
                (100, MoonNote.GuitarFret.Green, 0),
                (105, MoonNote.GuitarFret.Red, 0),
                (109, MoonNote.GuitarFret.Yellow, 0),
                (110, MoonNote.GuitarFret.Blue, 0));

            var notes = LoadExpertGuitar(song, 10);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(notes, Has.Count.EqualTo(2));
                Assert.That(notes[0].Tick, Is.EqualTo(100));
                Assert.That(notes[0].NoteMask, Is.EqualTo(0b0111), "Green, red and yellow should form a chord");
                Assert.That(notes[0].ChildNotes.Select(note => note.Tick), Is.All.EqualTo(100));
                Assert.That(notes[1].Tick, Is.EqualTo(110), "A note at the threshold should not snap");
            }
        }

        [Test]
        public void SnappedNote_TakesTheChordsNoteTypeAndKeepsItsOwnLength()
        {
            // The green at 100 is a HOPO, 50 ticks after the yellow
            var song = CreateSongWithNotes(
                (50, MoonNote.GuitarFret.Yellow, 0),
                (100, MoonNote.GuitarFret.Green, 0),
                (101, MoonNote.GuitarFret.Red, 192));

            var notes = LoadExpertGuitar(song, 2);
            var red = notes[1].ChildNotes.Single();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(notes, Has.Count.EqualTo(2));
                Assert.That(red.Tick, Is.EqualTo(100));
                Assert.That(red.Type, Is.EqualTo(GuitarNoteType.Hopo));
                Assert.That(notes[1].TickLength, Is.EqualTo(0));
                Assert.That(red.TickLength, Is.EqualTo(192));
                Assert.That(red.TimeLength, Is.EqualTo(SECONDS(1)).Within(0.0001));
                Assert.That(notes[1].IsDisjoint, Is.True, "A chord whose sustains end apart should be disjoint");
                Assert.That(red.IsDisjoint, Is.True);
            }
        }

        [Test]
        public void SnappedNoteOnAFretOfTheChord_KeepsTheLongerNote()
        {
            var song = CreateSongWithNotes(
                (0, MoonNote.GuitarFret.Green, 0),
                (5, MoonNote.GuitarFret.Green, 192));

            var notes = LoadExpertGuitar(song, 10);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(notes, Has.Count.EqualTo(1));
                Assert.That(notes[0].ChildNotes, Is.Empty);
                Assert.That(notes[0].TickLength, Is.EqualTo(192));
                Assert.That(notes[0].TimeLength, Is.EqualTo(SECONDS(1)).Within(0.0001));
            }
        }

        private static List<DrumNote> LoadExpertProDrums(MoonSong song, long snapThreshold)
        {
            var settings = ParseSettings.Default;
            settings.NoteSnapThreshold = snapThreshold;
            settings.DrumsType = DrumsType.FourLane;

            return new MoonSongLoader(song, settings)
                .LoadDrumsTrack(Instrument.ProDrums, null)
                .GetDifficulty(Difficulty.Expert).Notes;
        }

        [Test]
        public void SnappedDrumNoteOnAColorTheChordHas_TakesThatNote()
        {
            // A yellow cymbal, then an accented yellow tom 1 tick later
            var song = CreateSong();
            var chart = song.GetChart(MoonSong.MoonInstrument.Drums, MoonSong.Difficulty.Expert);
            chart.Insert(new MoonNote(100, (int) MoonNote.DrumPad.Yellow, 0, MoonNote.Flags.ProDrums_Cymbal));
            chart.Insert(new MoonNote(101, (int) MoonNote.DrumPad.Yellow, 0, MoonNote.Flags.ProDrums_Accent));

            var notes = LoadExpertProDrums(song, 2);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(notes, Has.Count.EqualTo(1));
                Assert.That(notes[0].ChildNotes, Is.Empty);
                Assert.That(notes[0].Pad, Is.EqualTo((int) FourLaneDrumPad.YellowCymbal));
                Assert.That(notes[0].Type, Is.EqualTo(DrumNoteType.Neutral));
            }
        }

        [Test]
        public void SnappedRedDrumNote_TakesTheDiscoFlipOfTheChordsYellowNote()
        {
            // A disco flip starts on the red's written tick, after the yellow cymbal it snaps to
            var song = CreateSong();
            var chart = song.GetChart(MoonSong.MoonInstrument.Drums, MoonSong.Difficulty.Expert);
            chart.Insert(new MoonNote(100, (int) MoonNote.DrumPad.Yellow, 0, MoonNote.Flags.ProDrums_Cymbal));
            chart.Insert(new MoonNote(101, (int) MoonNote.DrumPad.Red));
            chart.Add(new MoonText("mix 3 drums0d", 101));

            var notes = LoadExpertProDrums(song, 2);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(notes, Has.Count.EqualTo(1));
                Assert.That(notes[0].Pad, Is.EqualTo((int) FourLaneDrumPad.YellowCymbal));
                Assert.That(notes[0].ChildNotes.Select(note => (note.Tick, note.Pad)),
                    Is.EqualTo(new[] { (100u, (int) FourLaneDrumPad.RedDrum) }));
            }
        }

        [Test]
        public void RockBandNoteSnap_SnapsNotesUpToThresholdWithTheChordsLength()
        {
            var song = CreateSongWithNotes(
                (100, MoonNote.GuitarFret.Green, 0),
                (110, MoonNote.GuitarFret.Red, 192));

            var notes = LoadExpertGuitar(song, 10, rockBandNoteSnap: true);
            var red = notes[0].ChildNotes.Single();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(notes, Has.Count.EqualTo(1));
                Assert.That(red.Tick, Is.EqualTo(100));
                Assert.That(red.TickLength, Is.EqualTo(0));
            }
        }
    }
}
