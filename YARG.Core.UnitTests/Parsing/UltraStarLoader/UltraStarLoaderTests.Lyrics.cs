using NUnit.Framework;

namespace YARG.Core.UnitTests.Parsing
{
    internal class UltraStarLoaderTests_Lyrics : UltraStarLoaderTests
    {
        [Test]
        public void SongLyricsTrackMatchesTheVocals()
        {
            // SongChart.Lyrics also drives lipsync, and must be filled from the vocals.
            var songChart = LoadUltraStarChart(Us(
                "#BPM:120",
                ": 0 4 0  times",
                ": 5 4 0  of",
                ": 10 2 0  n~",
                ": 13 4 0 eed",
                "- 20",
                ": 24 4 0  again"
            ));

            Assert.That(DescribeLyrics(songChart.Lyrics.Phrases.Select(phrase => phrase.Lyrics)),
                Is.EqualTo("times | of | n* | eed / again"));
        }

        [Test]
        public void SongLyricsTrackUsesTheFirstVoiceOfADuet()
        {
            var songChart = LoadUltraStarChart(Us(
                "#BPM:120",
                "P1",
                ": 0 4 0  one",
                "P2",
                ": 0 4 2  two"
            ));

            Assert.That(DescribeLyrics(songChart.Lyrics.Phrases.Select(phrase => phrase.Lyrics)), Is.EqualTo("one"));
        }

        [Test]
        public void ParseBasicLyrics()
        {
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                ": 0 4 0 Hello",
                ": 5 4 2  World",
                ": 10 4 4  Test"
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
        public void MelismaJoinAppendsHyphenToLyric()
        {
            // ni ~ght. should display as "ni-ght." with JoinWithNext on "ni-"
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                ": 0 4 0 ni",
                ": 5 4 2 ~ght."
            ));

            var track = loader.LoadVocalsTrack(Instrument.Vocals);
            var lyrics = track.Parts[0].NotePhrases[0].Lyrics;

            Assert.That(lyrics, Has.Count.EqualTo(2));
            Assert.That(lyrics[0].Text, Is.EqualTo("ni-"));
            Assert.That(lyrics[0].JoinWithNext, Is.True);
            Assert.That(lyrics[1].Text, Does.Contain("ght."));
        }

        [Test]
        public void MelismaJoinInLyricsTrack()
        {
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                ": 0 4 0 ni",
                ": 5 4 2 ~ght."
            ));

            var lyricsTrack = loader.LoadLyrics();
            var events = lyricsTrack.Phrases[0].Lyrics;

            Assert.That(events, Has.Count.EqualTo(2));
            Assert.That(events[0].Text, Is.EqualTo("ni-"));
            Assert.That(events[0].JoinWithNext, Is.True);
            Assert.That(events[1].Text, Does.Contain("ght."));
        }

        // The loader's raw lyric events, before MoonSongLoader.Vocals interprets them: '+' is
        // a pitch slide onto the previous note. A bare '~' hold must still emit its "+" event
        // even though it has no syllable, since MoonSongLoader.Vocals merges notes on it (and
        // strips it from the display).
        [TestCase(": 0 4 0 ~la\n: 5 4 2  la", "la | la",
            TestName = "A leading ~ on the first note has nothing to join")]
        [TestCase(": 0 4 0 a~\n: 5 4 2 round", "a-* | round+",
            TestName = "A trailing ~ hyphenates and slides into the next note")]
        [TestCase(": 0 4 0 word~\n- 10\n: 20 4 0 next", "word-* / next",
            TestName = "A rest drops a pending trailing ~")]
        [TestCase(": 0 4 0 Hello\n- 10\n: 20 4 0 ~world", "Hello / world",
            TestName = "A leading ~ after a rest does not join across it")]
        [TestCase(": 0 4 0 out\n: 5 4 2 side", "out-* | side",
            TestName = "No extra space glues syllables into one word")]
        [TestCase(": 0 4 0 Mad\n: 5 4 2  world", "Mad | world",
            TestName = "An extra leading space separates words")]
        [TestCase(": 0 4 0 Hello \n: 5 4 2 World", "Hello | World",
            TestName = "An extra trailing space separates words")]
        [TestCase(": 0 4 0 out\n- 10\n: 20 4 2 side", "out / side",
            TestName = "A rest breaks whitespace glue")]
        [TestCase(": 0 4 0 la\n: 5 4 0 ~\n: 10 4 2 next", "la-* | + | next+",
            TestName = "A whitespace join walks past a bare hold")]
        [TestCase(": 0 4 0 la\n: 5 4 0 ~\n: 10 4 2 ~ght", "la-* | + | ght+",
            TestName = "A ~ join walks past a bare hold")]
        [TestCase(": 0 4 0 la\n: 5 4 0 ~", "la | +",
            TestName = "A bare hold emits a pitch-slide lyric event")]
        public void LoaderLyrics(string body, string expected)
        {
            var track = LoadUltraStar("#BPM:120\n" + body).LoadVocalsTrack(Instrument.Vocals);
            Assert.That(DescribeLyrics(track.Parts[0]), Is.EqualTo(expected));
        }

        [Test]
        public void BareTildeOnUnpitchedNoteHasNoPitchSlideMarker()
        {
            // An unpitched note has no pitch to slide, so a bare '~' on one gets no '+'. It
            // still needs its "#" lyric event: MoonSongLoader.Vocals decides whether a note
            // is pitched from that text, not from VocalNote.Pitch.
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                ": 0 4 0 Scream",
                "F 5 4 3 ~"
            ));

            var part = loader.LoadVocalsTrack(Instrument.Vocals).Parts[0];
            var notes = part.NotePhrases[0].PhraseParentNote.ChildNotes;

            Assert.That(notes[1].IsNonPitched, Is.True);
            Assert.That(notes[1].Pitch, Is.EqualTo(-1f));
            Assert.That(DescribeLyrics(part), Is.EqualTo("Scream | #"));
            Assert.That(part.NotePhrases[0].Lyrics[1].NonPitched, Is.True);
        }

        // Through the whole chart pipeline, where a pitch slide merges notes into one
        // note-bar group. A hyphen alone (JoinWithNext) merges nothing.
        [TestCase(": 260 4 14  n~\n: 265 20 16 eed", "n-* | eed", "1",
            TestName = "A trailing ~ merges two notes into one")]
        [TestCase(": 0 4 0 la\n: 5 4 0 ~\n: 10 4 2 next", "la-* | next", "2",
            TestName = "A glued hold merges the notes on both sides")]
        [TestCase(": 419 12 21  sta\n: 432 3 23 ~\n: 436 5 21 ~\n: 442 6 16 ~\n: 452 42 21 ars", "sta-* | ars", "4",
            TestName = "Three holds merge into one note without leaking a '+'")]
        [TestCase(": 0 4 0  la\n: 5 2 0 ~\n: 8 4 0  yeah", "la | yeah", "1,0",
            TestName = "A hold does not slide into a space-separated word")]
        [TestCase(": 0 4 0  Ri \n: 5 4 0 ~ght", "Ri | ght", "0,0",
            TestName = "A leading ~ does not blend across a word boundary")]
        [TestCase(": 0 4 0  I~\n: 5 4 0  feel", "I-* | feel", "0,0",
            TestName = "A trailing ~ does not blend across a word boundary")]
        [TestCase(": 0 4 67  feed-\n: 5 4 67 back", "feed-* | back", "0,0",
            TestName = "A source hyphen is not doubled by the join")]
        [TestCase("F 0 4 67  feed-\nF 5 4 67 back", "feed-* | back", "0,0",
            TestName = "A source hyphen is not doubled on an unpitched note")]
        [TestCase(": 0 4 2  goes\n: 5 1 2  ding-\n: 8 9 4 ~\n: 20 1 2 ding-\n: 24 4 0 a", "goes | ding-* | ding-* | a", "0,2,0",
            TestName = "A source hyphen is not doubled behind a pitch-slide marker")]
        [TestCase(": 0 4 67  out\n: 5 4 67 side", "out-* | side", "0,0",
            TestName = "A syllable without a source hyphen gets the join hyphen")]
        [TestCase(": 0 4 0  \"A\n: 5 2 0  +\n: 8 4 0  they", "A | plus | they", "0,0,0",
            TestName = "A literal + is spelled out and merges nothing")]
        public void ChartLyricsAndNoteGroups(string body, string expectedLyrics, string expectedGroups)
        {
            var part = LoadUltraStarChart("#BPM:120\n" + body).Vocals.Parts[0];
            using (Assert.EnterMultipleScope())
            {
                Assert.That(DescribeLyrics(part), Is.EqualTo(expectedLyrics));
                Assert.That(DescribeNoteGroups(part), Is.EqualTo(expectedGroups));
            }
        }

        // Static lyrics mode drops any lyric it can't match to a note, so a syllable riding a
        // slide-merged note must still be matched.
        [TestCase(": 0 4 0  times\n: 5 4 0  of\n: 10 2 0  n~\n: 13 4 0 eed", "times | of | n-* | eed",
            TestName = "Static lyrics keep both syllables of a slide-joined word")]
        [TestCase(": 419 12 21  sta\n: 432 3 23 ~\n: 436 5 21 ~\n: 442 6 16 ~\n: 452 42 21 ars", "sta-* | ars",
            TestName = "Static lyrics keep the syllable after a chain of holds")]
        public void StaticLyricsKeepEverySyllable(string body, string expected)
        {
            var part = LoadUltraStarChart("#BPM:120\n" + body).Vocals.Parts[0];
            Assert.That(DescribeLyrics(part.StaticLyricPhrases.Select(phrase => phrase.Lyrics)), Is.EqualTo(expected));
        }

        [Test]
        public void RealFileExcerptFromAuroraJoinsAndSeparatesCorrectly()
        {
            // From "AURORA - Under Stars": separate words, a whitespace-glued word
            // ("outside"), and a ~-joined one ("need").
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                ": 0 17 11 Mad",
                ": 29 15 11  world",
                ": 55 16 11  beats",
                "- 81",
                ": 90 6 13 out",
                ": 96 6 14 side",
                ": 104 2 23  our",
                ": 107 16 23  hearts",
                "- 149",
                ": 211 19 16 Times",
                ": 238 20 16  of",
                ": 260 4 14  n~",
                ": 265 20 16 eed"
            ));

            var part = loader.LoadVocalsTrack(Instrument.Vocals).Parts[0];

            Assert.That(DescribeLyrics(part), Is.EqualTo(
                "Mad | world | beats / out-* | side | our | hearts / Times | of | n-* | eed+"));
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
