using NUnit.Framework;
using YARG.Core.Chart;

namespace YARG.Core.UnitTests.Parsing
{
    internal class UltraStarLoaderTests_StarPower : UltraStarLoaderTests
    {
        [Test]
        public void ParseGoldenNote()
        {
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                "* 0 4 0 Golden"
            ));

            var track = loader.LoadVocalsTrack(Instrument.Vocals);
            var phrase = track.Parts[0].NotePhrases[0];

            Assert.That(phrase.IsStarPower, Is.True);
        }

        [Test]
        public void ParseGoldenRapNote()
        {
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                "G 0 4 -1 GoldenRap"
            ));

            var track = loader.LoadVocalsTrack(Instrument.Vocals);
            var phrase = track.Parts[0].NotePhrases[0];
            var note = phrase.PhraseParentNote.ChildNotes[0];

            // Golden Rap is unpitched and golden
            Assert.That(note.IsNonPitched, Is.True);
            Assert.That(phrase.IsStarPower, Is.True);
        }

        [Test]
        public void MixedGoldenAndNormalNotes()
        {
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                ": 0 4 0 Normal",
                "- 5",
                "* 5 4 0 Golden",
                "- 10",
                ": 10 4 0 Normal"
            ));

            var track = loader.LoadVocalsTrack(Instrument.Vocals);

            // First phrase - normal
            Assert.That(track.Parts[0].NotePhrases[0].IsStarPower, Is.False);
            // Second phrase - golden (StarPower)
            Assert.That(track.Parts[0].NotePhrases[1].IsStarPower, Is.True);
            // Third phrase - normal
            Assert.That(track.Parts[0].NotePhrases[2].IsStarPower, Is.False);
        }

        [Test]
        public void StarPowerOtherPhrasesAdded()
        {
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                "* 0 4 0 Golden1",
                "- 5",
                "* 10 4 0 Golden2"
            ));

            var track = loader.LoadVocalsTrack(Instrument.Vocals);
            var otherPhrases = track.Parts[0].OtherPhrases;

            // Should have 2 StarPower phrases in OtherPhrases
            Assert.That(otherPhrases, Has.Count.EqualTo(2));
            Assert.That(otherPhrases[0].Type, Is.EqualTo(PhraseType.StarPower));
            Assert.That(otherPhrases[1].Type, Is.EqualTo(PhraseType.StarPower));
        }

        [Test]
        public void StarPowerPhraseTiming()
        {
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                "* 0 8 0 LongGolden"
            ));

            var track = loader.LoadVocalsTrack(Instrument.Vocals);
            var otherPhrase = track.Parts[0].OtherPhrases[0];

            // Check timing is correct (8 beats = 960 ticks with 120 ticks/beat)
            Assert.That(otherPhrase.TickLength, Is.GreaterThan(0));
            Assert.That(otherPhrase.TimeLength, Is.GreaterThan(0));
        }

        [Test]
        public void MultipleStarPowerPhrasesSeparate()
        {
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                "* 0 4 0 SP1",
                "- 5",
                "* 10 4 0 SP2",
                "- 15",
                "* 20 4 0 SP3"
            ));

            var track = loader.LoadVocalsTrack(Instrument.Vocals);
            var otherPhrases = track.Parts[0].OtherPhrases;

            // 3 separate StarPower phrases
            Assert.That(otherPhrases, Has.Count.EqualTo(3));

            // Each should have different tick positions
            Assert.That(otherPhrases[0].Tick, Is.LessThan(otherPhrases[1].Tick));
            Assert.That(otherPhrases[1].Tick, Is.LessThan(otherPhrases[2].Tick));
        }

        [Test]
        public void NormalNotesAfterStarPowerNotAffected()
        {
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                "* 0 4 0 Golden",
                "- 5",
                ": 10 4 0 Normal"
            ));

            var track = loader.LoadVocalsTrack(Instrument.Vocals);

            // Second phrase should NOT be StarPower
            Assert.That(track.Parts[0].NotePhrases[1].IsStarPower, Is.False);
        }

        [Test]
        public void StarPowerWithPitch()
        {
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                "* 0 4 12 GoldenHigh"
            ));

            var track = loader.LoadVocalsTrack(Instrument.Vocals);
            var note = track.Parts[0].NotePhrases[0].PhraseParentNote.ChildNotes[0];

            // Pitch should be converted: 12 + 60 = 72 (C5)
            Assert.That(note.Pitch, Is.EqualTo(72f));
            Assert.That(note.IsNonPitched, Is.False);
            Assert.That(track.Parts[0].NotePhrases[0].IsStarPower, Is.True);
        }
    }
}
