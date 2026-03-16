using NUnit.Framework;

namespace YARG.Core.UnitTests.Parsing
{
    internal class UltraStarLoaderTests_Duet : UltraStarLoaderTests
    {
        [Test]
        public void ParseDuetWithP1P2()
        {
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                "#PARTS:2",
                "P1",
                ": 0 4 0 Hello",
                ": 5 4 0 World",
                "P2",
                ": 0 4 2 Hi",
                ": 5 4 2 There"
            ));

            // When loading as Harmony, should get 2 parts
            var track = loader.LoadVocalsTrack(Instrument.Harmony);

            Assert.That(track.Parts, Has.Count.EqualTo(2));
        }

        [Test]
        public void ParseDuetWithHarmony()
        {
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                "#PARTS:2",
                "P1",
                ": 0 4 0 Hello",
                "P2",
                ": 0 4 2 Hello"
            ));

            var track = loader.LoadVocalsTrack(Instrument.Harmony);

            // Both parts should have lyrics at same position
            Assert.That(track.Parts[0].NotePhrases, Has.Count.EqualTo(1));
            Assert.That(track.Parts[1].NotePhrases, Has.Count.EqualTo(1));
        }

        [Test]
        public void ParseSoloVoiceWithoutDuetFlag()
        {
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                ": 0 4 0 Solo",
                ": 5 4 0 Voice"
            ));

            // Without #PARTS:2, should load as single vocals
            var track = loader.LoadVocalsTrack(Instrument.Vocals);

            Assert.That(track.Parts, Has.Count.EqualTo(1));
        }

        [Test]
        public void ParseDuetMetadata()
        {
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                "#PARTS:2",
                "P1",
                ": 0 4 0 Test"
            ));

            Assert.That(loader.GetMetadata("PARTS"), Is.EqualTo("2"));
        }

        [Test]
        public void ParseDuetWithDifferentLyrics()
        {
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                "#PARTS:2",
                "P1",
                ": 0 4 0 Part1",
                "- 5",
                ": 5 4 0 Song",
                "P2",
                ": 0 4 0 Part2",
                "- 5",
                ": 5 4 0 Here"
            ));

            var track = loader.LoadVocalsTrack(Instrument.Harmony);

            // Part 1 lyrics
            Assert.That(track.Parts[0].NotePhrases[0].Lyrics[0].Text, Is.EqualTo("Part1"));
            Assert.That(track.Parts[0].NotePhrases[1].Lyrics[0].Text, Is.EqualTo("Song"));

            // Part 2 lyrics
            Assert.That(track.Parts[1].NotePhrases[0].Lyrics[0].Text, Is.EqualTo("Part2"));
            Assert.That(track.Parts[1].NotePhrases[1].Lyrics[0].Text, Is.EqualTo("Here"));
        }

        [Test]
        public void ParseDuetWithDifferentPitches()
        {
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                "#PARTS:2",
                "P1",
                ": 0 4 0 Low",
                "P2",
                ": 0 4 12 High"
            ));

            var track = loader.LoadVocalsTrack(Instrument.Harmony);

            // Part 1 should have lower pitch (0 + 60 = 60)
            var part1Pitch = track.Parts[0].NotePhrases[0].PhraseParentNote.ChildNotes[0].Pitch;
            Assert.That(part1Pitch, Is.EqualTo(60f));

            // Part 2 should have higher pitch (12 + 60 = 72)
            var part2Pitch = track.Parts[1].NotePhrases[0].PhraseParentNote.ChildNotes[0].Pitch;
            Assert.That(part2Pitch, Is.EqualTo(72f));
        }

        [Test]
        public void ParseDuetStarPowerSeparate()
        {
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                "#PARTS:2",
                "P1",
                "* 0 4 0 SP1",
                "P2",
                "* 5 4 0 SP2"
            ));

            var track = loader.LoadVocalsTrack(Instrument.Harmony);

            // Part 1 has StarPower
            Assert.That(track.Parts[0].NotePhrases[0].IsStarPower, Is.True);
            Assert.That(track.Parts[0].OtherPhrases, Has.Count.EqualTo(1));

            // Part 2 has StarPower
            Assert.That(track.Parts[1].NotePhrases[0].IsStarPower, Is.True);
            Assert.That(track.Parts[1].OtherPhrases, Has.Count.EqualTo(1));
        }

        [Test]
        public void ParseDuetMixedPhrases()
        {
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                "#PARTS:2",
                "P1",
                ": 0 4 0 Both",
                "- 5",
                "* 5 4 0 SP",
                "- 10",
                ": 10 4 0 Both",
                "P2",
                ": 0 4 0 Both",
                "- 5",
                ": 5 4 0 Both",
                "- 10",
                ": 10 4 0 Both"
            ));

            var track = loader.LoadVocalsTrack(Instrument.Harmony);

            // Part 1: normal, StarPower, normal
            Assert.That(track.Parts[0].NotePhrases[0].IsStarPower, Is.False);
            Assert.That(track.Parts[0].NotePhrases[1].IsStarPower, Is.True);
            Assert.That(track.Parts[0].NotePhrases[2].IsStarPower, Is.False);

            // Part 2: all normal (no StarPower markers)
            Assert.That(track.Parts[1].NotePhrases[0].IsStarPower, Is.False);
            Assert.That(track.Parts[1].NotePhrases[1].IsStarPower, Is.False);
            Assert.That(track.Parts[1].NotePhrases[2].IsStarPower, Is.False);
        }
    }
}
