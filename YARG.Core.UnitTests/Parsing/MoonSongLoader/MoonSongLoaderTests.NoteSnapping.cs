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

        private static List<DrumNote> LoadProDrums(MoonSong song, long snapThreshold,
            Difficulty difficulty = Difficulty.Expert)
        {
            var settings = ParseSettings.Default;
            settings.NoteSnapThreshold = snapThreshold;
            settings.DrumsType = DrumsType.FourLane;

            return new MoonSongLoader(song, settings)
                .LoadDrumsTrack(Instrument.ProDrums, null)
                .GetDifficulty(difficulty).Notes;
        }

        // Adds the notes in the order given, as the .chart reader does, so two notes of one pad can share a tick
        private static MoonSong CreateSongWithDrumNotes(
            params (uint tick, MoonNote.DrumPad pad, uint length, MoonNote.Flags flags)[] notes)
        {
            var song = CreateSong();
            var chart = song.GetChart(MoonSong.MoonInstrument.Drums, MoonSong.Difficulty.Expert);
            foreach (var (tick, pad, length, flags) in notes)
            {
                MoonObjectHelper.PushNote(new MoonNote(tick, (int) pad, length, flags), chart.notes);
            }

            return song;
        }

        private static List<(uint tick, int pad, bool isDoubleKick)> GetDrumNotes(List<DrumNote> chords)
        {
            var notes = new List<(uint tick, int pad, bool isDoubleKick)>();
            foreach (var chord in chords)
            {
                foreach (var note in chord.AllNotes)
                {
                    notes.Add((note.Tick, note.Pad, note.IsDoubleKick));
                }
            }

            return notes;
        }

        [Test]
        public void SnappedDrumNoteOnAColorTheChordHas_TakesThatNote()
        {
            // A yellow cymbal, then an accented yellow tom 1 tick later
            var song = CreateSong();
            var chart = song.GetChart(MoonSong.MoonInstrument.Drums, MoonSong.Difficulty.Expert);
            chart.Insert(new MoonNote(100, (int) MoonNote.DrumPad.Yellow, 0, MoonNote.Flags.ProDrums_Cymbal));
            chart.Insert(new MoonNote(101, (int) MoonNote.DrumPad.Yellow, 0, MoonNote.Flags.ProDrums_Accent));

            var notes = LoadProDrums(song, 2);

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

            var notes = LoadProDrums(song, 2);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(notes, Has.Count.EqualTo(1));
                Assert.That(notes[0].Pad, Is.EqualTo((int) FourLaneDrumPad.YellowCymbal));
                Assert.That(notes[0].ChildNotes.Select(note => (note.Tick, note.Pad)),
                    Is.EqualTo(new[] { (100u, (int) FourLaneDrumPad.RedDrum) }));
            }
        }

        [TestCase(Difficulty.Expert)]
        [TestCase(Difficulty.ExpertPlus)]
        public void SnappedDoubleKick_TakesTheChordsKick(Difficulty difficulty)
        {
            var song = CreateSongWithDrumNotes(
                (100, MoonNote.DrumPad.Kick, 0, MoonNote.Flags.None),
                (101, MoonNote.DrumPad.Kick, 192, MoonNote.Flags.DoubleKick));

            var notes = GetDrumNotes(LoadProDrums(song, 2, difficulty));

            Assert.That(notes, Is.EqualTo(new[] { (100u, (int) FourLaneDrumPad.Kick, false) }));
        }

        [TestCase(Difficulty.Expert)]
        [TestCase(Difficulty.ExpertPlus)]
        public void SnappedKick_TakesTheChordsDoubleKick(Difficulty difficulty)
        {
            var song = CreateSongWithDrumNotes(
                (100, MoonNote.DrumPad.Kick, 0, MoonNote.Flags.DoubleKick),
                (101, MoonNote.DrumPad.Kick, 192, MoonNote.Flags.None));

            var notes = GetDrumNotes(LoadProDrums(song, 2, difficulty));

            // Below Expert+, the chord's kick is a double kick, so the chord has no kick
            var expected = difficulty == Difficulty.ExpertPlus
                ? new[] { (100u, (int) FourLaneDrumPad.Kick, true) }
                : Array.Empty<(uint, int, bool)>();
            Assert.That(notes, Is.EqualTo(expected));
        }

        [TestCase(Difficulty.Expert)]
        [TestCase(Difficulty.ExpertPlus)]
        public void SnappedDrumNote_JoinsAChordOfADoubleKickAlone(Difficulty difficulty)
        {
            var song = CreateSongWithDrumNotes(
                (100, MoonNote.DrumPad.Kick, 0, MoonNote.Flags.DoubleKick),
                (101, MoonNote.DrumPad.Red, 0, MoonNote.Flags.None));

            var notes = GetDrumNotes(LoadProDrums(song, 2, difficulty));

            var expected = difficulty == Difficulty.ExpertPlus
                ? new[] { (100u, (int) FourLaneDrumPad.Kick, true), (100u, (int) FourLaneDrumPad.RedDrum, false) }
                : new[] { (100u, (int) FourLaneDrumPad.RedDrum, false) };
            Assert.That(notes, Is.EqualTo(expected));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SnappedKick_JoinsTheKickOfAChordWithAKickAndADoubleKick(bool doubleKickFirst)
        {
            var kick = (100u, MoonNote.DrumPad.Kick, 0u, MoonNote.Flags.None);
            var doubleKick = (100u, MoonNote.DrumPad.Kick, 0u, MoonNote.Flags.DoubleKick);
            var song = CreateSongWithDrumNotes(
                doubleKickFirst ? doubleKick : kick,
                doubleKickFirst ? kick : doubleKick,
                (101, MoonNote.DrumPad.Kick, 192, MoonNote.Flags.None));

            var notes = GetDrumNotes(LoadProDrums(song, 2));

            Assert.That(notes, Is.EqualTo(new[] { (100u, (int) FourLaneDrumPad.Kick, false) }));
        }

        [TestCase(Difficulty.Expert)]
        [TestCase(Difficulty.ExpertPlus)]
        public void KickAndDoubleKickOnOneTick_ReadAsWithoutSnapping(Difficulty difficulty)
        {
            static MoonSong Create() => CreateSongWithDrumNotes(
                (100, MoonNote.DrumPad.Kick, 0, MoonNote.Flags.None),
                (100, MoonNote.DrumPad.Kick, 480, MoonNote.Flags.DoubleKick));

            var snapped = GetDrumNotes(LoadProDrums(Create(), 2, difficulty));
            var unsnapped = GetDrumNotes(LoadProDrums(Create(), 0, difficulty));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(unsnapped, Is.Not.Empty);
                Assert.That(snapped, Is.EqualTo(unsnapped));
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
