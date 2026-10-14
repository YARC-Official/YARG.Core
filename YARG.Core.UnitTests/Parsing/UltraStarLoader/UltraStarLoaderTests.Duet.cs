using System.Linq;
using NUnit.Framework;
using YARG.Core.Chart;

namespace YARG.Core.UnitTests.Parsing
{
    internal class UltraStarLoaderTests_Duet : UltraStarLoaderTests
    {
        /// <summary>
        /// Across every pair of harmony parts, phrases that overlap in time must share the
        /// exact same (Tick, TickEnd) -- the invariant PartyVocalsCoordinatorEngine's phrase
        /// dedupe relies on. See UltraStarLoader.ComputeSharedPhraseCuts.
        /// </summary>
        private static void AssertNoMismatchedOverlappingPhrases(VocalsTrack harmony)
        {
            for (int i = 0; i < harmony.Parts.Count; i++)
            {
                for (int j = i + 1; j < harmony.Parts.Count; j++)
                {
                    foreach (var a in harmony.Parts[i].NotePhrases)
                    {
                        foreach (var b in harmony.Parts[j].NotePhrases)
                        {
                            bool overlaps = a.Tick < b.TickEnd && b.Tick < a.TickEnd;
                            if (!overlaps)
                            {
                                continue;
                            }

                            Assert.That(a.Tick, Is.EqualTo(b.Tick),
                                $"Overlapping phrases on parts {i}/{j} start at different ticks");
                            Assert.That(a.TickEnd, Is.EqualTo(b.TickEnd),
                                $"Overlapping phrases on parts {i}/{j} end at different ticks");
                        }
                    }
                }
            }
        }

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
            // VoiceCount reflects the voice markers actually used (P1, P2, ...), which is
            // independent of the raw #PARTS header tag.
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                "#PARTS:2",
                "P1",
                ": 0 4 0 Test",
                "P2",
                ": 0 4 0 Test"
            ));

            Assert.That(loader.VoiceCount, Is.EqualTo(2));
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
            AssertNoMismatchedOverlappingPhrases(track);

            // Part 1: normal, StarPower, normal
            Assert.That(track.Parts[0].NotePhrases[0].IsStarPower, Is.False);
            Assert.That(track.Parts[0].NotePhrases[1].IsStarPower, Is.True);
            Assert.That(track.Parts[0].NotePhrases[2].IsStarPower, Is.False);

            // Part 2: all normal (no StarPower markers)
            Assert.That(track.Parts[1].NotePhrases[0].IsStarPower, Is.False);
            Assert.That(track.Parts[1].NotePhrases[1].IsStarPower, Is.False);
            Assert.That(track.Parts[1].NotePhrases[2].IsStarPower, Is.False);
        }

        [Test]
        public void DuetSoloVocalsOnlyShowsFirstPart()
        {
            // Bug fix: when duet, solo Vocals chart should only contain P1
            var content = Us(
                "#BPM:120",
                "#PARTS:2",
                "P1",
                ": 0 4 0 Hello",
                ": 5 4 0  World",
                "P2",
                ": 0 4 2 Hi",
                ": 5 4 2  There"
            );
            var songChart = LoadUltraStarChart(content);

            // Solo Vocals should only have P1 notes (Hello, World)
            var vocalsTrack = songChart.Vocals;
            Assert.That(vocalsTrack.Parts, Has.Count.EqualTo(1));

            var lyrics = new List<string>();
            foreach (var phrase in vocalsTrack.Parts[0].NotePhrases)
            {
                foreach (var lyric in phrase.Lyrics)
                {
                    lyrics.Add(lyric.Text);
                }
            }

            Assert.That(lyrics, Contains.Item("Hello"));
            Assert.That(lyrics, Contains.Item("World"));
            Assert.That(lyrics, Does.Not.Contain("Hi"));
            Assert.That(lyrics, Does.Not.Contain("There"));
        }

        [Test]
        public void DuetHarmonyHasBothParts()
        {
            // Both parts should still be available in Harmony tracks
            var content = Us(
                "#BPM:120",
                "#PARTS:2",
                "P1",
                ": 0 4 0 Hello",
                "P2",
                ": 0 4 2 Hi"
            );
            var songChart = LoadUltraStarChart(content);

            // Harmony track loads 3 parts from MoonSong (HARM1, HARM2, HARM3)
            // For UltraStar duet, HARM3 is empty
            var harmonyTrack = songChart.Harmony;
            Assert.That(harmonyTrack.Parts[0].NotePhrases, Has.Count.EqualTo(1));
            Assert.That(harmonyTrack.Parts[1].NotePhrases, Has.Count.EqualTo(1));
        }

        [Test]
        public void ThirdVoiceViaP3GetsItsOwnVocalsPart()
        {
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                "P1",
                ": 0 4 0 One",
                "P2",
                ": 0 4 4 Two",
                "P3",
                ": 0 4 7 Three"
            ));

            var track = loader.LoadVocalsTrack(Instrument.Harmony);
            AssertNoMismatchedOverlappingPhrases(track);

            Assert.That(loader.VoiceCount, Is.EqualTo(3));
            Assert.That(track.Parts.Select(DescribeLyrics), Is.EqualTo(new[] { "One", "Two", "Three" }));
        }

        [Test]
        public void SpacedVoiceMarkersAreRecognized()
        {
            // "P 1" is at least as common as "P1"; missing it would merge every voice into one.
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                "#PARTS:2",
                "P 1",
                ": 0 4 0 Hello",
                "P 2",
                ": 0 4 2 Hi"
            ));

            var track = loader.LoadVocalsTrack(Instrument.Harmony);

            Assert.That(loader.VoiceCount, Is.EqualTo(2));
            Assert.That(track.Parts.Select(DescribeLyrics), Is.EqualTo(new[] { "Hello", "Hi" }));
        }

        [Test]
        public void FourthVoiceMarkerIsIgnoredNotCrashed()
        {
            // P4 has no harmony part, so it's ignored and its notes stay in the active voice.
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                "P1",
                ": 0 4 0 One",
                "P2",
                ": 0 4 4 Two",
                "P4",
                ": 5 4 7  StillTwo"
            ));

            var track = loader.LoadVocalsTrack(Instrument.Harmony);
            AssertNoMismatchedOverlappingPhrases(track);

            Assert.That(loader.VoiceCount, Is.EqualTo(2));
            Assert.That(track.Parts.Select(DescribeLyrics), Is.EqualTo(new[] { "One", "Two | StillTwo" }));
        }

        private static string DescribeStaticLyrics(List<VocalsPhrase> phrases)
            => DescribeLyrics(phrases.Select(phrase => phrase.Lyrics));

        [Test]
        public void DuetSecondVoiceHasStaticLyrics()
        {
            // The harmony static-lyrics lanes read HARM2's static and merged phrases, not its
            // scoring phrases, so P2 must produce those too.
            var harmony = LoadUltraStarChart(Us(
                "#BPM:120",
                "#PARTS:2",
                "P1",
                ": 0 2 0 Watch",
                ": 3 2 0  out",
                "- 6",
                ": 8 2 0  you",
                "P2",
                ": 4 2 2 Burn",
                ": 7 2 2 ing",
                "- 10",
                ": 12 2 2  down"
            )).Harmony;
            AssertNoMismatchedOverlappingPhrases(harmony);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(DescribeStaticLyrics(harmony.Parts[0].StaticLyricPhrases), Is.EqualTo("Watch | out / you"));
                Assert.That(DescribeStaticLyrics(harmony.Parts[1].StaticLyricPhrases), Is.EqualTo("Burn-* | ing / down"));
                Assert.That(DescribeStaticLyrics(harmony.Parts[1].MergedStaticLyricPhrases), Is.EqualTo("Burn-* | ing / down"));
                // P1's line gap falls cleanly at beat 7, inside P2's "Burn | ing" line, so the
                // shared window split there cuts P2's scoring into 3 phrases even though it
                // still displays as 2 lines above.
                Assert.That(harmony.Parts[1].NotePhrases, Has.Count.EqualTo(3));
            }
        }

        [Test]
        public void MergedHarmonyMatchesStandaloneDuetWhenTemposMatch()
        {
            // Equal BPM and GAP: the duet's own ticks already line up with the solo file's,
            // so merging must not shift anything.
            string solo = Us("#BPM:120", "#MP3:audio.mp3", ": 0 4 0 Lead");
            string duet = Us(
                "#BPM:120", "#MP3:audio.mp3",
                "P1",
                ": 5 4 0 Hi",
                "P2",
                ": 5 4 2 There");

            var merged = LoadMergedUltraStarChart(solo, duet);
            var standalone = LoadUltraStarChart(duet);

            uint mergedTick = merged.Harmony.Parts[0].NotePhrases[0].PhraseParentNote.Tick;
            uint standaloneTick = standalone.Harmony.Parts[0].NotePhrases[0].PhraseParentNote.Tick;
            Assert.That(mergedTick, Is.EqualTo(standaloneTick));
            // Beat 5 at a 120-tick resolution (15 ticks per UltraStar beat).
            Assert.That(mergedTick, Is.EqualTo(75u));
        }

        [Test]
        public void MergedHarmonyIsRetimedWhenDuetHasADifferentBpm()
        {
            // The duet is written at double the solo's BPM with its beats doubled to match,
            // a real USDB pattern -- both describe the same note at real time 1s, so after
            // retiming onto the solo's tempo (BPM 120) that note must land on tick 120.
            string solo = Us("#BPM:120", "#MP3:audio.mp3", ": 0 4 0 Lead");
            string duet = Us(
                "#BPM:240", "#MP3:audio.mp3",
                "P1",
                ": 16 4 0 Hi",
                "P2",
                ": 16 4 2 There");

            var merged = LoadMergedUltraStarChart(solo, duet);

            uint mergedTick = merged.Harmony.Parts[0].NotePhrases[0].PhraseParentNote.Tick;
            Assert.That(mergedTick, Is.EqualTo(120u));
        }

        [Test]
        public void MergedHarmonyAccountsForDifferingNegativeGap()
        {
            // A negative GAP only ever becomes SongOffset (see ScanUltraStar); it's clamped
            // to tick 0 in the loader itself, so a different negative GAP between the two
            // files has to be reapplied here as a time shift, even though their BPM matches.
            string solo = Us("#BPM:120", "#GAP:-500", "#MP3:audio.mp3", ": 0 4 0 Lead");
            string duet = Us(
                "#BPM:120", "#GAP:-1000", "#MP3:audio.mp3",
                "P1",
                ": 10 4 0 Hi",
                "P2",
                ": 10 4 2 There");

            var merged = LoadMergedUltraStarChart(solo, duet);

            uint mergedTick = merged.Harmony.Parts[0].NotePhrases[0].PhraseParentNote.Tick;
            // Duet's own tick for beat 10 is 150; the 500ms (half-beat-at-60bpm => 60 tick)
            // GAP gap between the two files shifts it back to 90.
            Assert.That(mergedTick, Is.EqualTo(90u));
        }

        [Test]
        public void ThirdVoiceStaticLyricsAreMergedIntoSecondLane()
        {
            var harmony = LoadUltraStarChart(Us(
                "#BPM:120",
                "P1",
                ": 0 4 0 One",
                "P2",
                ": 0 4 4 Two",
                "P3",
                ": 20 4 7 Three"
            )).Harmony;
            AssertNoMismatchedOverlappingPhrases(harmony);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(DescribeStaticLyrics(harmony.Parts[1].StaticLyricPhrases), Is.EqualTo("Two"));
                Assert.That(DescribeStaticLyrics(harmony.Parts[2].StaticLyricPhrases), Is.EqualTo("Three"));
                Assert.That(DescribeStaticLyrics(harmony.Parts[1].MergedStaticLyricPhrases), Is.EqualTo("Two / Three"));
            }
        }

        [Test]
        public void SharedWindowBoundsMatchAcrossVoicesEvenWhenLinesEndDifferently()
        {
            // P1's line ends at beat 4, P2's overlapping line ends at beat 7 -- without
            // shared bounds these would be two different (Tick, TickEnd) phrases.
            var harmony = LoadUltraStarChart(Us(
                "#BPM:120",
                "#PARTS:2",
                "P1",
                ": 0 4 0 Hello",
                "- 20",
                ": 20 4 0 World",
                "P2",
                ": 0 4 2 Hi",
                ": 5 2 2 There"
            )).Harmony;
            AssertNoMismatchedOverlappingPhrases(harmony);

            var p1First = harmony.Parts[0].NotePhrases[0];
            var p2First = harmony.Parts[1].NotePhrases[0];
            Assert.That(harmony.Parts[1].NotePhrases, Has.Count.EqualTo(1));
            Assert.That(p1First.Tick, Is.EqualTo(p2First.Tick));
            Assert.That(p1First.TickEnd, Is.EqualTo(p2First.TickEnd));
            // Beat 7 at a 120-tick resolution (15 ticks per UltraStar beat): both voices'
            // phrase ends on P2's later note, not P1's own earlier one.
            Assert.That(p1First.TickEnd, Is.EqualTo(105u));
        }

        [Test]
        public void SecondVoiceAnsweringDuringFirstVoicesRestGetsItsOwnWindow()
        {
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                "#PARTS:2",
                "P1",
                ": 0 4 0 Hello",
                "- 50",
                ": 50 4 0 World",
                "P2",
                ": 20 4 2 Alone"
            ));

            var track = loader.LoadVocalsTrack(Instrument.Harmony);
            AssertNoMismatchedOverlappingPhrases(track);

            // HARM1 keeps both its own lines; HARM2 gets a window of its own in the rest.
            Assert.That(track.Parts[0].NotePhrases, Has.Count.EqualTo(2));
            Assert.That(track.Parts[1].NotePhrases, Has.Count.EqualTo(1));

            // Every source note (Hello, World, Alone) is accounted for exactly once.
            int totalNotes = track.Parts.Sum(p => p.NotePhrases.Sum(phrase => phrase.PhraseParentNote.ChildNotes.Count));
            Assert.That(totalNotes, Is.EqualTo(3));
        }

        [Test]
        public void StraddlingNoteWithNoCleanCutHangsOverIntoTheNextPhrase()
        {
            // P2's note spans beats 3-7, straddling every candidate cut between P1's lines
            // (which end at 4 and start at 5) all the way back past P1's own line end -- so
            // there's no clean cut, and the cut falls at P1's next line start (5) anyway.
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                "#PARTS:2",
                "P1",
                ": 0 4 0 Hello",
                "- 5",
                ": 5 4 0 World",
                "P2",
                ": 3 4 2 Cross"
            ));

            var track = loader.LoadVocalsTrack(Instrument.Harmony);

            // P1's two lines stay separate phrases; they never merge because of the straddler.
            Assert.That(track.Parts[0].NotePhrases, Has.Count.EqualTo(2));

            // P2's note belongs only to the first phrase, with its full un-clamped length...
            Assert.That(track.Parts[1].NotePhrases, Has.Count.EqualTo(1));
            var crossPhrase = track.Parts[1].NotePhrases[0];
            var crossNote = crossPhrase.PhraseParentNote.ChildNotes[0];
            Assert.That(crossNote.TickLength, Is.EqualTo(60u)); // 4 beats * 15 ticks/beat

            // ...which runs past the phrase's own TickEnd, so it's a carried note downstream.
            Assert.That(crossNote.TickEnd, Is.GreaterThan(crossPhrase.TickEnd));
        }

        [Test]
        public void CleanCutIsPreferredOverFallbackWhenOneExists()
        {
            // P2's note (18-22) straddles P1's next line start (20), but walking back from 20
            // lands on 18 -- P2's own note start -- where nothing straddles, so that's used as
            // a clean cut instead of falling back to 20. No note hangs over as a result.
            var loader = LoadUltraStar(Us(
                "#BPM:120",
                "#PARTS:2",
                "P1",
                ": 0 4 0 Hello",
                "- 20",
                ": 20 4 0 World",
                "P2",
                ": 18 4 2 Echo"
            ));

            var track = loader.LoadVocalsTrack(Instrument.Harmony);
            AssertNoMismatchedOverlappingPhrases(track);

            Assert.That(track.Parts[0].NotePhrases, Has.Count.EqualTo(2));
            Assert.That(track.Parts[1].NotePhrases, Has.Count.EqualTo(1));

            // The shared window pulls P1's "World" phrase's start back to 18 (P2's note start),
            // matching P2's phrase exactly, and nothing hangs over past either phrase's end.
            var worldPhrase = track.Parts[0].NotePhrases[1];
            var echoPhrase = track.Parts[1].NotePhrases[0];
            Assert.That(worldPhrase.Tick, Is.EqualTo(echoPhrase.Tick));
            Assert.That(worldPhrase.TickEnd, Is.EqualTo(echoPhrase.TickEnd));

            var echoNote = echoPhrase.PhraseParentNote.ChildNotes[0];
            Assert.That(echoNote.TickEnd, Is.LessThanOrEqualTo(echoPhrase.TickEnd));
        }
    }
}
