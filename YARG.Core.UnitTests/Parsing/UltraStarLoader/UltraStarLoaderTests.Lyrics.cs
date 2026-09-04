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
        public void LeadingMelismaOnFirstNoteHasNoPreviousNoteToJoin()
        {
            // A '~' normally hyphenates the previous note; on the very first note there
            // isn't one, which must not crash or lose the pitch-slide marker.
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                ": 0 4 0 ~la",
                ": 5 4 2 la"
            ));

            var track = loader.LoadVocalsTrack(Instrument.Vocals);
            var lyrics = track.Parts[0].NotePhrases[0].Lyrics;

            Assert.That(lyrics[0].Text, Is.EqualTo("la+"));
            Assert.That(lyrics[0].JoinWithNext, Is.False);
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

        [Test]
        public void BareTildeOnUnpitchedNoteHasNoPitchSlideMarker()
        {
            // A bare '~' on a Freestyle note has no pitch to slide into/from --
            // unpitched detection should take precedence over the pitch-slide ('+')
            // marker a bare '~' would otherwise produce.
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                ": 0 4 0 Scream",
                "F 5 4 3 ~"
            ));

            var track = loader.LoadVocalsTrack(Instrument.Vocals);
            var notes = track.Parts[0].NotePhrases[0].PhraseParentNote.ChildNotes;
            var lyrics = track.Parts[0].NotePhrases[0].Lyrics;

            Assert.That(notes[1].IsNonPitched, Is.True);
            Assert.That(notes[1].Pitch, Is.EqualTo(-1f));
            // The continuation note carries no syllable, but still needs a "#"-only
            // lyric event (no "+" pitch-slide) -- downstream conversion derives a
            // note's final pitched/unpitched status from this text, not from Pitch.
            Assert.That(lyrics, Has.Count.EqualTo(2));
            Assert.That(lyrics[0].Text, Is.EqualTo("Scream"));
            Assert.That(lyrics[1].Text, Is.EqualTo("#"));
            Assert.That(lyrics[1].NonPitched, Is.True);
        }

        [Test]
        public void TrailingTildeBlendsWithNextNote()
        {
            // "a~" followed by "round": the first note gets a decorative hyphen
            // (matching the leading-'~' convention), and the SECOND note's raw text
            // carries an embedded pitch-slide symbol ("+"). This loader's own
            // LyricEvent.Flags doesn't interpret embedded symbols (only downstream's
            // MoonSongLoader.Vocals.cs re-parses raw text for that -- see
            // TrailingTildeStructurallyMergesIntoOneNote for the full-pipeline check).
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                ": 0 4 0 a~",
                ": 5 4 2 round"
            ));

            var track = loader.LoadVocalsTrack(Instrument.Vocals);
            var lyrics = track.Parts[0].NotePhrases[0].Lyrics;

            Assert.That(lyrics, Has.Count.EqualTo(2));
            Assert.That(lyrics[0].Text, Is.EqualTo("a-"));
            Assert.That(lyrics[0].JoinWithNext, Is.True);
            Assert.That(lyrics[1].Text, Is.EqualTo("round+"));
        }

        [Test]
        public void TrailingTildeStructurallyMergesIntoOneNote()
        {
            // Regression test: a decorative hyphen alone (JoinWithNext) does NOT merge
            // two notes into one bar in the actual game -- only the pitch-slide flag
            // does (MoonSongLoader.Vocals.cs's GetVocalsPhrases only merges on
            // LyricSymbolFlags.PitchSlide). Verify through the full SongChart pipeline
            // that "n~"/"eed" ends up as one parent note with a child, not two notes.
            var songChart = LoadUltraStarChart(Us(
                "#BPM:120",
                ": 260 4 14  n~",
                ": 265 20 16 eed"
            ));

            var notes = songChart.Vocals.Parts[0].NotePhrases[0].PhraseParentNote.ChildNotes;
            Assert.That(notes, Has.Count.EqualTo(1));
            Assert.That(notes[0].ChildNotes, Has.Count.EqualTo(1));
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
