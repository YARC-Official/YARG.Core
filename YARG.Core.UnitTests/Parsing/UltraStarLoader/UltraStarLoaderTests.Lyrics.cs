using NUnit.Framework;

namespace YARG.Core.UnitTests.Parsing
{
    internal class UltraStarLoaderTests_Lyrics : UltraStarLoaderTests
    {
        [Test]
        public void SongLyricsTrackMatchesTheVocals()
        {
            // SongChart.Lyrics (also what lipsync is generated from) was always empty for US,
            // since only the vocals charts were filled.
            var songChart = LoadUltraStarChart(Us(
                "#BPM:120",
                ": 0 4 0  times",
                ": 5 4 0  of",
                ": 10 2 0  n~",
                ": 13 4 0 eed",
                "- 20",
                ": 24 4 0  again"
            ));

            var phrases = songChart.Lyrics.Phrases;
            Assert.That(phrases, Has.Count.EqualTo(2));
            Assert.That(phrases[0].Lyrics.Select(l => l.Text), Is.EqualTo(new[] { "times", "of", "n", "eed" }));
            Assert.That(phrases[0].Lyrics[2].JoinWithNext, Is.True);
            Assert.That(phrases[1].Lyrics.Select(l => l.Text), Is.EqualTo(new[] { "again" }));
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

            var lyrics = songChart.Lyrics.Phrases.SelectMany(p => p.Lyrics).Select(l => l.Text);
            Assert.That(lyrics, Is.EqualTo(new[] { "one" }));
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
        public void LeadingMelismaOnFirstNoteHasNoPreviousNoteToJoin()
        {
            // A leading '~' blends back into the previous note; on the very first note
            // there isn't one, so there is nothing to blend into and no pitch-slide
            // marker is emitted. Must not crash or join backwards into nothing.
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                ": 0 4 0 ~la",
                ": 5 4 2  la"
            ));

            var track = loader.LoadVocalsTrack(Instrument.Vocals);
            var lyrics = track.Parts[0].NotePhrases[0].Lyrics;

            Assert.That(lyrics[0].Text, Is.EqualTo("la"));
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
        public void RestBreaksPendingPitchSlideAcrossPhrases()
        {
            // Regression test: a trailing '~' sets a pending pitch-slide for whatever note
            // comes next, but a rest between them ends the phrase -- the flag must not
            // survive past the rest into the note that starts the next phrase (same rule
            // ParseVoiceMarker already applies across voices).
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                ": 0 4 0 word~",
                "- 10",
                ": 20 4 0 next"
            ));

            var track = loader.LoadVocalsTrack(Instrument.Vocals);
            var secondPhraseLyrics = track.Parts[0].NotePhrases[1].Lyrics;

            Assert.That(secondPhraseLyrics[0].Text, Is.EqualTo("next"));
        }

        [Test]
        public void LeadingMelismaAfterRestDoesNotJoinAcrossPhrases()
        {
            // Regression test: MarkPreviousNoteMelismaJoin must only look at the
            // immediately preceding note, not walk back across a rest to the nearest real
            // note -- a rest breaks the phrase, so a leading '~' right after one must not
            // hyphenate onto whatever real note happened to precede that rest.
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                ": 0 4 0 Hello",
                "- 10",
                ": 20 4 0 ~world"
            ));

            var track = loader.LoadVocalsTrack(Instrument.Vocals);
            var firstPhraseLyrics = track.Parts[0].NotePhrases[0].Lyrics;

            Assert.That(firstPhraseLyrics[0].Text, Is.EqualTo("Hello"));
            Assert.That(firstPhraseLyrics[0].JoinWithNext, Is.False);
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
        public void NoExtraSpaceGluesSyllablesTogether()
        {
            // Per spec (§4.1), the note-text field's only mandatory separator is the single
            // space before it -- no additional space on either side means the syllable
            // glues directly onto its neighbor, exactly like an explicit '~' join.
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                ": 0 4 0 out",
                ": 5 4 2 side"
            ));

            var track = loader.LoadVocalsTrack(Instrument.Vocals);
            var lyrics = track.Parts[0].NotePhrases[0].Lyrics;

            Assert.That(lyrics, Has.Count.EqualTo(2));
            Assert.That(lyrics[0].Text, Is.EqualTo("out-"));
            Assert.That(lyrics[0].JoinWithNext, Is.True);
            Assert.That(lyrics[1].Text, Is.EqualTo("side"));
        }

        [Test]
        public void ExtraLeadingSpaceKeepsSeparateWord()
        {
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                ": 0 4 0 Mad",
                ": 5 4 2  world"
            ));

            var track = loader.LoadVocalsTrack(Instrument.Vocals);
            var lyrics = track.Parts[0].NotePhrases[0].Lyrics;

            Assert.That(lyrics[0].Text, Is.EqualTo("Mad"));
            Assert.That(lyrics[0].JoinWithNext, Is.False);
        }

        [Test]
        public void ExtraTrailingSpaceKeepsSeparateWord()
        {
            // The spec calls a trailing space on this note and a leading space on the next
            // "semantically equivalent" -- either alone must prevent the glue.
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                ": 0 4 0 Hello ",
                ": 5 4 2 World"
            ));

            var track = loader.LoadVocalsTrack(Instrument.Vocals);
            var lyrics = track.Parts[0].NotePhrases[0].Lyrics;

            Assert.That(lyrics[0].Text, Is.EqualTo("Hello"));
            Assert.That(lyrics[0].JoinWithNext, Is.False);
        }

        [Test]
        public void RestBreaksWhitespaceGlueAcrossPhrases()
        {
            // Mirrors RestBreaksPendingPitchSlideAcrossPhrases, but for the no-space case:
            // a rest between two notes that would otherwise glue must still force a gap.
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                ": 0 4 0 out",
                "- 10",
                ": 20 4 2 side"
            ));

            var track = loader.LoadVocalsTrack(Instrument.Vocals);
            var firstPhraseLyrics = track.Parts[0].NotePhrases[0].Lyrics;

            Assert.That(firstPhraseLyrics[0].Text, Is.EqualTo("out"));
            Assert.That(firstPhraseLyrics[0].JoinWithNext, Is.False);
        }

        [Test]
        public void JoinWalksPastSilentHoldToRealSyllable()
        {
            // A bare '~' hold emits its own pitch-slide-only LyricEvent ("+") but has no
            // real syllable to attach a join to -- the join must walk past it to "la". The
            // "+" event must still be emitted here (see
            // BareHoldMustEmitPitchSlideLyricEventForDownstreamMerge): MoonSongLoader.
            // Vocals.ProcessLyric needs to see it to derive the PitchSlide flag that
            // structurally merges the hold's note-bar into the previous note in the full
            // chart pipeline; it strips the text back out before final display on its own.
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                ": 0 4 0 la",
                ": 5 4 0 ~",
                ": 10 4 2 next"
            ));

            var track = loader.LoadVocalsTrack(Instrument.Vocals);
            var lyrics = track.Parts[0].NotePhrases[0].Lyrics;

            Assert.That(lyrics, Has.Count.EqualTo(3));
            Assert.That(lyrics[0].Text, Is.EqualTo("la-"));
            Assert.That(lyrics[0].JoinWithNext, Is.True);
            Assert.That(lyrics[1].Text, Is.EqualTo("+"));
            Assert.That(lyrics[1].JoinWithNext, Is.False);
            // The hold's slide also blends forward onto "next" (see
            // ThreeConsecutiveHoldsStructurallyMergeAndGlueTextWithNoGap for the
            // structural-merge consequence of this through the full chart pipeline).
            Assert.That(lyrics[2].Text, Is.EqualTo("next+"));
        }

        [Test]
        public void BareHoldMustEmitPitchSlideLyricEventForDownstreamMerge()
        {
            // Regression test: it's tempting to suppress a bare '~' hold's "+"-only
            // LyricEvent to avoid a stray '+' in the displayed lyric -- don't. The full
            // chart pipeline (MoonSongLoader.Vocals.cs's GetVocalsPhrases) re-derives which
            // notes structurally merge into one note-bar by re-scanning these intermediate
            // "lyric +" events at each note's tick (ProcessLyric); removing this event here
            // makes GetVocalsPhrases treat the hold as an unconnected, independent note
            // instead of a pitch-slide continuation of the previous one -- exactly the "no
            // connected notes, each separate" regression this test guards against.
            // ProcessLyric itself already strips the "+" back out before the note reaches
            // final display (LyricSymbols.StripForVocals), so nothing needs to change here.
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                ": 0 4 0 la",
                ": 5 4 0 ~"
            ));

            var track = loader.LoadVocalsTrack(Instrument.Vocals);
            var phrase = track.Parts[0].NotePhrases[0];

            Assert.That(phrase.Lyrics, Has.Count.EqualTo(2));
            Assert.That(phrase.Lyrics[1].Text, Is.EqualTo("+"));
        }

        [Test]
        public void ThreeConsecutiveHoldsStructurallyMergeAndGlueTextWithNoGap()
        {
            // Regression test built directly from the reported file (AURORA - Under
            // Stars), through the FULL chart pipeline (not the standalone loader) since
            // that's what actually distinguishes this from the "no connected notes"
            // regression: "sta" / '~' / '~' / '~' / "ars" must (a) fold the three bare
            // holds AND "ars" into one connected note-bar group (a hold's slide blends
            // both backward into the previous note and forward into the next one) and
            // (b) still render as two lyric entries -- "sta-" (joined) then "ars" -- with
            // none of the holds leaking a stray '+' into the final display text.
            var songChart = LoadUltraStarChart(Us(
                "#BPM:120",
                ": 419 12 21  sta",
                ": 432 3 23 ~",
                ": 436 5 21 ~",
                ": 442 6 16 ~",
                ": 452 42 21 ars"
            ));

            var phrase = songChart.Vocals.Parts[0].NotePhrases[0];

            // One top-level note ("sta") with all four remaining notes (three holds plus
            // "ars") folded in as children -- one connected group end to end.
            var topLevelNotes = phrase.PhraseParentNote.ChildNotes;
            Assert.That(topLevelNotes, Has.Count.EqualTo(1));
            Assert.That(topLevelNotes[0].ChildNotes, Has.Count.EqualTo(4));

            Assert.That(phrase.Lyrics, Has.Count.EqualTo(2));
            Assert.That(phrase.Lyrics[0].Text, Is.EqualTo("sta-"));
            Assert.That(phrase.Lyrics[0].JoinWithNext, Is.True);
            Assert.That(phrase.Lyrics[1].Text, Is.EqualTo("ars"));
        }

        [Test]
        public void StaticLyricsModeKeepsBothSyllablesOfAPitchSlideJoinedWord()
        {
            // Regression test: "n~" / "eed" ("need") must keep BOTH syllables in static
            // lyrics mode, not just "n". FixLyricLengths matches lyric events to notes by
            // walking phrase.PhraseParentNote.ChildNotes; a pitch-slide-joined note like
            // "eed" is folded in one level deeper (as a child of "n", not a phrase-level
            // sibling -- same structural merge as the bare-hold case above), so it must
            // still be visited or its lyric is never marked matched and static lyrics mode
            // (unlike gameplay's NotePhrases) deletes anything unmatched.
            var songChart = LoadUltraStarChart(Us(
                "#BPM:120",
                ": 0 4 0  times",
                ": 5 4 0  of",
                ": 10 2 0  n~",
                ": 13 4 0 eed"
            ));

            var phrase = songChart.Vocals.Parts[0].StaticLyricPhrases[0];

            Assert.That(phrase.Lyrics.Select(l => l.Text), Is.EqualTo(new[] { "times", "of", "n-", "eed" }));
        }

        [Test]
        public void StaticLyricsModeKeepsFullTextOfAThreeHoldChain()
        {
            // Same AURORA case as ThreeConsecutiveHoldsStructurallyMergeAndGlueTextWithNoGap
            // above, but checked against StaticLyricPhrases instead of NotePhrases -- here
            // "ars" is the only real syllable riding a slide-merged note (the three holds
            // in between carry no lyric text of their own), so this pins that "ars"
            // specifically survives, not just any matched note.
            var songChart = LoadUltraStarChart(Us(
                "#BPM:120",
                ": 419 12 21  sta",
                ": 432 3 23 ~",
                ": 436 5 21 ~",
                ": 442 6 16 ~",
                ": 452 42 21 ars"
            ));

            var phrase = songChart.Vocals.Parts[0].StaticLyricPhrases[0];

            Assert.That(phrase.Lyrics.Select(l => l.Text), Is.EqualTo(new[] { "sta-", "ars" }));
        }

        [Test]
        public void SingleHoldMergesNoteBeforeAndAfterWhenGlued()
        {
            // General case (not just the 3-hold chain above): a lone bare '~' hold blends
            // both ways, so "la" and "next" end up in one connected note-bar group with
            // the hold folded in between them, through the full chart pipeline. The
            // forward half of that requires "next" to be glued to the hold -- see
            // HoldDoesNotSlideIntoASpaceSeparatedNextWord for the separated case.
            var songChart = LoadUltraStarChart(Us(
                "#BPM:120",
                ": 0 4 0 la",
                ": 5 4 0 ~",
                ": 10 4 2 next"
            ));

            var phrase = songChart.Vocals.Parts[0].NotePhrases[0];
            var topLevelNotes = phrase.PhraseParentNote.ChildNotes;

            Assert.That(topLevelNotes, Has.Count.EqualTo(1));
            Assert.That(topLevelNotes[0].ChildNotes, Has.Count.EqualTo(2));
        }

        [Test]
        public void ExistingSourceHyphenIsNotDoubledByTheJoinSymbol()
        {
            // Roughly a tenth of real files already end mid-word syllables with an
            // FoF-style hyphen, which is the same character as LYRIC_JOIN_SYMBOL. Adding
            // the join symbol on top of it renders "feed--".
            var songChart = LoadUltraStarChart(Us(
                "#BPM:120",
                ": 0 4 67  feed-",
                ": 5 4 67 back"
            ));

            var phrase = songChart.Vocals.Parts[0].NotePhrases[0];

            Assert.That(phrase.Lyrics.Select(l => l.Text), Is.EqualTo(new[] { "feed-", "back" }));
            Assert.That(phrase.Lyrics[0].JoinWithNext, Is.True);
        }

        [Test]
        public void ExistingSourceHyphenIsNotDoubledOnAnUnpitchedNote()
        {
            // The '#' non-pitched suffix is appended before the join symbol, so it must not
            // hide the syllable's existing hyphen from the de-duplication.
            var songChart = LoadUltraStarChart(Us(
                "#BPM:120",
                "F 0 4 67  feed-",
                "F 5 4 67 back"
            ));

            var phrase = songChart.Vocals.Parts[0].NotePhrases[0];

            Assert.That(phrase.Lyrics.Select(l => l.Text), Is.EqualTo(new[] { "feed-", "back" }));
            Assert.That(phrase.Lyrics[0].JoinWithNext, Is.True);
        }

        [Test]
        public void ExistingSourceHyphenIsNotDoubledBehindAPitchSlideMarker()
        {
            // "Teach-in - Ding-a-Dong" shape: a hyphenated syllable, a bare hold, then a
            // glued syllable. The hold's slide appends '+' to the syllable, which would
            // hide its hyphen from the de-duplication and render "ding--" once the '+' is
            // stripped for display.
            var songChart = LoadUltraStarChart(Us(
                "#BPM:120",
                ": 0 4 2  goes",
                ": 5 1 2  ding-",
                ": 8 9 4 ~",
                ": 20 1 2 ding-",
                ": 24 4 0 a"
            ));

            var phrase = songChart.Vocals.Parts[0].NotePhrases[0];

            Assert.That(phrase.Lyrics.Select(l => l.Text),
                Is.EqualTo(new[] { "goes", "ding-", "ding-", "a" }));
        }

        [Test]
        public void SyllableWithoutASourceHyphenStillGetsTheJoinSymbol()
        {
            // Guards the de-duplication against over-reaching: the ordinary case must still
            // pick up the hyphen that signals a mid-word split.
            var songChart = LoadUltraStarChart(Us(
                "#BPM:120",
                ": 0 4 67  out",
                ": 5 4 67 side"
            ));

            var phrase = songChart.Vocals.Parts[0].NotePhrases[0];

            Assert.That(phrase.Lyrics.Select(l => l.Text), Is.EqualTo(new[] { "out-", "side" }));
            Assert.That(phrase.Lyrics[0].JoinWithNext, Is.True);
        }

        [Test]
        public void LiteralPlusInSourceTextIsSpelledOutAndDoesNotMergeNotes()
        {
            // '+' is YARG's pitch-slide marker but is ordinary text in UltraStar (this
            // shape is from "Weird Al" Yankovic - eBay, whose lyric is «"A+!"»). A literal
            // one must neither be mistaken for a request to merge into the previous note
            // nor vanish from the display (StripForVocals deletes '+' unconditionally).
            var songChart = LoadUltraStarChart(Us(
                "#BPM:120",
                ": 0 4 0  \"A",
                ": 5 2 0  +",
                ": 8 4 0  they"
            ));

            var phrase = songChart.Vocals.Parts[0].NotePhrases[0];
            var topLevelNotes = phrase.PhraseParentNote.ChildNotes;

            Assert.That(topLevelNotes, Has.Count.EqualTo(3));
            foreach (var note in topLevelNotes)
            {
                Assert.That(note.ChildNotes, Is.Empty);
            }

            Assert.That(phrase.Lyrics.Select(l => l.Text),
                Is.EqualTo(new[] { "A", "plus", "they" }));
        }

        [Test]
        public void HoldDoesNotSlideIntoASpaceSeparatedNextWord()
        {
            // A hold sustains the syllable before it, so it must not drag its slide into
            // a note that starts a new word -- the extra space before "yeah" marks that
            // boundary. This is the overwhelmingly common shape in real files (a bare
            // '~' followed by a separate word), so an ungated forward blend would wrongly
            // connect most of the library.
            var songChart = LoadUltraStarChart(Us(
                "#BPM:120",
                ": 0 4 0  la",
                ": 5 2 0 ~",
                ": 8 4 0  yeah"
            ));

            var phrase = songChart.Vocals.Parts[0].NotePhrases[0];
            var topLevelNotes = phrase.PhraseParentNote.ChildNotes;

            // "la" + the hold form one group; "yeah" stands alone.
            Assert.That(topLevelNotes, Has.Count.EqualTo(2));
            Assert.That(topLevelNotes[0].ChildNotes, Has.Count.EqualTo(1));
            Assert.That(topLevelNotes[1].ChildNotes, Is.Empty);
        }

        [Test]
        public void LeadingMelismaDoesNotBlendAcrossAWordBoundary()
        {
            // A leading '~' blends backward, but not past the previous note's trailing
            // word-boundary space.
            var songChart = LoadUltraStarChart(Us(
                "#BPM:120",
                ": 0 4 0  Ri ",
                ": 5 4 0 ~ght"
            ));

            var phrase = songChart.Vocals.Parts[0].NotePhrases[0];

            Assert.That(phrase.PhraseParentNote.ChildNotes, Has.Count.EqualTo(2));
        }

        [Test]
        public void TrailingMelismaDoesNotBlendAcrossAWordBoundary()
        {
            // A trailing '~' blends forward, but not past the next note's leading
            // word-boundary space.
            var songChart = LoadUltraStarChart(Us(
                "#BPM:120",
                ": 0 4 0  I~",
                ": 5 4 0  feel"
            ));

            var phrase = songChart.Vocals.Parts[0].NotePhrases[0];

            Assert.That(phrase.PhraseParentNote.ChildNotes, Has.Count.EqualTo(2));
        }

        [Test]
        public void ExplicitTildeJoinWalksPastSilentHoldToRealSyllable()
        {
            // Same fix, exercised through the pre-existing leading-'~'-with-text trigger
            // instead of the whitespace trigger above.
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                ": 0 4 0 la",
                ": 5 4 0 ~",
                ": 10 4 2 ~ght"
            ));

            var track = loader.LoadVocalsTrack(Instrument.Vocals);
            var lyrics = track.Parts[0].NotePhrases[0].Lyrics;

            Assert.That(lyrics[0].Text, Is.EqualTo("la-"));
            Assert.That(lyrics[0].JoinWithNext, Is.True);
        }

        [Test]
        public void RealFileExcerptFromAuroraJoinsAndSeparatesCorrectly()
        {
            // Regression test built directly from the user's reported file (AURORA -
            // Under Stars): "Mad world beats" stays three separate words, "outside"
            // glues with no space, "our hearts" stays separate, and "need" (via the
            // pre-existing trailing-'~' mechanism) is unaffected by this change.
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

            var track = loader.LoadVocalsTrack(Instrument.Vocals);
            var phrases = track.Parts[0].NotePhrases;

            Assert.That(phrases, Has.Count.EqualTo(3));

            var phrase0 = phrases[0].Lyrics;
            Assert.That(phrase0[0].Text, Is.EqualTo("Mad"));
            Assert.That(phrase0[0].JoinWithNext, Is.False);
            Assert.That(phrase0[1].Text, Is.EqualTo("world"));
            Assert.That(phrase0[1].JoinWithNext, Is.False);

            var phrase1 = phrases[1].Lyrics;
            Assert.That(phrase1[0].Text, Is.EqualTo("out-"));
            Assert.That(phrase1[0].JoinWithNext, Is.True);
            Assert.That(phrase1[1].Text, Is.EqualTo("side"));
            Assert.That(phrase1[1].JoinWithNext, Is.False);
            Assert.That(phrase1[2].Text, Is.EqualTo("our"));
            Assert.That(phrase1[2].JoinWithNext, Is.False);
            Assert.That(phrase1[3].Text, Is.EqualTo("hearts"));

            var phrase2 = phrases[2].Lyrics;
            Assert.That(phrase2[0].Text, Is.EqualTo("Times"));
            Assert.That(phrase2[0].JoinWithNext, Is.False);
            Assert.That(phrase2[1].Text, Is.EqualTo("of"));
            Assert.That(phrase2[1].JoinWithNext, Is.False);
            Assert.That(phrase2[2].Text, Is.EqualTo("n-"));
            Assert.That(phrase2[2].JoinWithNext, Is.True);
            Assert.That(phrase2[3].Text, Is.EqualTo("eed+"));
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
