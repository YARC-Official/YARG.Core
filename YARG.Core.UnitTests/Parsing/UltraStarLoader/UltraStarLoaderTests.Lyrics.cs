using NUnit.Framework;

namespace YARG.Core.UnitTests.Parsing
{
    internal class UltraStarLoaderTests_Lyrics : UltraStarLoaderTests
    {
        [Test]
        public void ParseBasicLyrics()
        {
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                ": 0 4 0 Hello",
                ": 5 4 2 World",
                ": 10 4 4 Test"
            ));

            var track = loader.LoadVocalsTrack(Instrument.Vocals);
            var lyrics = track.Parts[0].NotePhrases[0].Lyrics;

            Assert.That(lyrics, Has.Count.EqualTo(3));
            Assert.That(lyrics[0].Text, Is.EqualTo("Hello"));
            Assert.That(lyrics[1].Text, Is.EqualTo("World"));
            Assert.That(lyrics[2].Text, Is.EqualTo("Test"));
        }

        [Test]
        public void ParseMultiWordLyric()
        {
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                ": 0 4 0 Hello World Test"
            ));

            var track = loader.LoadVocalsTrack(Instrument.Vocals);
            var lyric = track.Parts[0].NotePhrases[0].Lyrics[0];

            Assert.That(lyric.Text, Is.EqualTo("Hello World Test"));
        }

        [Test]
        public void ParseMelisma()
        {
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                ": 0 4 0 ~la",
                ": 5 4 2 la"
            ));

            var track = loader.LoadVocalsTrack(Instrument.Vocals);
            var notes = track.Parts[0].NotePhrases[0].PhraseParentNote.ChildNotes;

            // First note should have melisma
            Assert.That(notes[0].IsNonPitched, Is.False);
        }

        [Test]
        public void ParseHyphenJoinWithNext()
        {
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                ": 0 4 0 Hel-",
                ": 5 4 2 lo"
            ));

            var track = loader.LoadVocalsTrack(Instrument.Vocals);
            var lyrics = track.Parts[0].NotePhrases[0].Lyrics;

            // YARG trims hyphens, but the notes should be in the same phrase
            Assert.That(lyrics, Has.Count.EqualTo(2));
        }

        [Test]
        public void ParsePhraseWithMultipleLyrics()
        {
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                ": 0 4 0 Hel",
                ": 2 4 2 lo",
                ": 4 4 4 Wor",
                ": 6 4 5 ld"
            ));

            var track = loader.LoadVocalsTrack(Instrument.Vocals);
            var phrase = track.Parts[0].NotePhrases[0];

            // All lyrics in one phrase
            Assert.That(phrase.Lyrics, Has.Count.EqualTo(4));
            Assert.That(phrase.PhraseParentNote.ChildNotes, Has.Count.EqualTo(4));
        }

        [Test]
        public void ParseLyricsTrack()
        {
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                ": 0 4 0 Hello",
                ": 5 4 2 World"
            ));

            var lyricsTrack = loader.LoadLyrics();

            Assert.That(lyricsTrack.Phrases, Has.Count.EqualTo(1));
            Assert.That(lyricsTrack.Phrases[0].Lyrics, Has.Count.EqualTo(2));
        }

        [Test]
        public void EmptyLyricHandled()
        {
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                ": 0 4 0 ",
                ": 2 4 2 Test"
            ));

            var track = loader.LoadVocalsTrack(Instrument.Vocals);
            var lyrics = track.Parts[0].NotePhrases[0].Lyrics;

            // Empty lyrics should be filtered out
            Assert.That(lyrics, Has.Count.EqualTo(1));
            Assert.That(lyrics[0].Text, Is.EqualTo("Test"));
        }

        [Test]
        public void WhitespaceTrimmed()
        {
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                ": 0 4 0   Hello   ",
                ": 2 4 2   World  "
            ));

            var track = loader.LoadVocalsTrack(Instrument.Vocals);
            var lyrics = track.Parts[0].NotePhrases[0].Lyrics;

            Assert.That(lyrics[0].Text, Is.EqualTo("Hello"));
            Assert.That(lyrics[1].Text, Is.EqualTo("World"));
        }
    }
}
